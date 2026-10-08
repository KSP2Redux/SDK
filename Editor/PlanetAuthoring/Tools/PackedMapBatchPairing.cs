using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// One tile's source files, resolved from a folder scan by shared file-name stem.
    /// </summary>
    public sealed class PackedMapTileSources
    {
        /// <summary>
        /// Gets the file-name stem the sources share, with the role suffix removed.
        /// </summary>
        /// <remarks>
        /// Taken from the normal map's file name, so its original casing is preserved for the
        /// output file name even though stems are matched case-insensitively.
        /// </remarks>
        public string Stem { get; set; }

        /// <summary>
        /// Gets the project-relative path of the normal map.
        /// </summary>
        public string NormalPath { get; set; }

        /// <summary>
        /// Gets the project-relative path of the smoothness map, or null when the stem has none.
        /// </summary>
        public string SmoothnessPath { get; set; }

        /// <summary>
        /// Gets the project-relative path of the roughness map, or null when the stem has none.
        /// </summary>
        /// <remarks>
        /// Only consulted when <see cref="SmoothnessPath" /> is null. A stem carrying both is
        /// reported through <see cref="PackedMapBatchPairing.ScanResult.Notes" /> rather than
        /// silently picking one.
        /// </remarks>
        public string RoughnessPath { get; set; }

        /// <summary>
        /// Gets the project-relative path of the ambient-occlusion map, or null when the stem has none.
        /// </summary>
        public string AmbientOcclusionPath { get; set; }

        /// <summary>
        /// Gets the file name the bake writes for this tile, without a directory or extension.
        /// </summary>
        public string OutputName => Stem + PackedMapSuffixes.OUTPUT_SUFFIX;
    }

    /// <summary>
    /// Resolves a folder of source maps into per-tile source sets by shared file-name stem.
    /// </summary>
    /// <remarks>
    /// Pure string work with no asset-database access, so the pairing rules are testable headlessly.
    /// The bake operation turns the resolved paths into textures.
    /// </remarks>
    public static class PackedMapBatchPairing
    {
        /// <summary>
        /// The outcome of scanning a set of file paths.
        /// </summary>
        public sealed class ScanResult
        {
            /// <summary>
            /// Gets the tiles that can be baked, ordered by stem.
            /// </summary>
            public IReadOnlyList<PackedMapTileSources> Tiles { get; set; } = Array.Empty<PackedMapTileSources>();

            /// <summary>
            /// Gets the stems that matched a source role but have no normal map, so no tile can be built for them.
            /// </summary>
            public IReadOnlyList<string> StemsWithoutNormal { get; set; } = Array.Empty<string>();

            /// <summary>
            /// Gets the human-readable notes about ambiguous input the scan resolved by rule rather than by guessing.
            /// </summary>
            public IReadOnlyList<string> Notes { get; set; } = Array.Empty<string>();
        }

        // The four roles a file name suffix can name. Files matching none are ignored outright,
        // which is what keeps the height and diffuse maps sitting alongside the sources out of
        // the scan instead of turning into bogus one-file stems.
        private enum SourceRole
        {
            Normal,
            Smoothness,
            Roughness,
            AmbientOcclusion,
        }

        private sealed class Candidate
        {
            public string Stem { get; set; }
            public SourceRole Role { get; set; }
            // Position of the matched suffix within its role's list. Lower wins when one stem has
            // several files in the same role, which is how "_n_fixed" beats the "_n" it was
            // derived from without any special-casing of that pair.
            public int SuffixRank { get; set; }
            public string Path { get; set; }
        }

        /// <summary>
        /// Groups the given file paths into per-tile source sets.
        /// </summary>
        /// <remarks>
        /// A file name is matched against every role's suffixes at once and the longest match wins,
        /// so a stem that itself ends in a role word is not truncated: <c>rock_flat_rough_01_g</c>
        /// resolves to stem <c>rock_flat_rough_01</c> in the smoothness role.
        /// </remarks>
        /// <param name="filePaths">Project-relative paths to consider. Extensions are ignored.</param>
        /// <param name="suffixes">The suffix lists identifying each role.</param>
        /// <returns>The resolved tiles, the stems that could not produce one, and any ambiguity notes.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="suffixes" /> is null.</exception>
        public static ScanResult Scan(IEnumerable<string> filePaths, PackedMapSuffixes suffixes)
        {
            if (suffixes == null)
            {
                throw new ArgumentNullException(nameof(suffixes));
            }
            if (filePaths == null)
            {
                return new ScanResult();
            }

            var candidates = new List<Candidate>();
            foreach (string path in filePaths)
            {
                Candidate candidate = Classify(path, suffixes);
                if (candidate == null)
                {
                    continue;
                }
                candidates.Add(candidate);
            }

            var tiles = new List<PackedMapTileSources>();
            var stemsWithoutNormal = new List<string>();
            var notes = new List<string>();

            // Stems are identifier keys, so they group under invariant lowering rather than the
            // current culture. See the Turkish-locale casing trap in csharp-authoring.md.
            IEnumerable<IGrouping<string, Candidate>> byStem = candidates
                .GroupBy(c => c.Stem.ToLowerInvariant())
                .OrderBy(g => g.Key, StringComparer.Ordinal);

            foreach (IGrouping<string, Candidate> group in byStem)
            {
                Candidate normal = Best(group, SourceRole.Normal);
                Candidate smoothness = Best(group, SourceRole.Smoothness);
                Candidate roughness = Best(group, SourceRole.Roughness);
                Candidate ao = Best(group, SourceRole.AmbientOcclusion);

                if (normal == null)
                {
                    stemsWithoutNormal.Add(group.Key);
                    continue;
                }

                if (smoothness != null && roughness != null)
                {
                    notes.Add(
                        $"'{normal.Stem}' has both a smoothness and a roughness map. " +
                        $"Using '{Path.GetFileName(smoothness.Path)}' and ignoring " +
                        $"'{Path.GetFileName(roughness.Path)}'."
                    );
                }

                tiles.Add(new PackedMapTileSources
                {
                    Stem = normal.Stem,
                    NormalPath = normal.Path,
                    SmoothnessPath = smoothness?.Path,
                    RoughnessPath = smoothness == null ? roughness?.Path : null,
                    AmbientOcclusionPath = ao?.Path,
                });
            }

            return new ScanResult
            {
                Tiles = tiles,
                StemsWithoutNormal = stemsWithoutNormal,
                Notes = notes,
            };
        }

        // Picks the best-ranked candidate for one role within a stem group, or null when the
        // stem has no file in that role.
        private static Candidate Best(IEnumerable<Candidate> group, SourceRole role) =>
            group
                .Where(c => c.Role == role)
                .OrderBy(c => c.SuffixRank)
                .ThenBy(c => c.Path, StringComparer.Ordinal)
                .FirstOrDefault();

        // Matches one file name against every role's suffixes and returns the longest match, so a
        // suffix that is a tail of another one cannot shadow it regardless of declaration order.
        private static Candidate Classify(string path, PackedMapSuffixes suffixes)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            string fileName = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            Candidate best = null;
            int bestLength = 0;

            void Consider(IReadOnlyList<string> roleSuffixes, SourceRole role)
            {
                if (roleSuffixes == null)
                {
                    return;
                }

                for (var rank = 0; rank < roleSuffixes.Count; rank++)
                {
                    string suffix = roleSuffixes[rank];
                    if (string.IsNullOrEmpty(suffix) || suffix.Length >= fileName.Length)
                    {
                        continue;
                    }
                    if (!fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) || suffix.Length <= bestLength)
                    {
                        continue;
                    }

                    bestLength = suffix.Length;
                    best = new Candidate
                    {
                        Stem = fileName[..^suffix.Length],
                        Role = role,
                        SuffixRank = rank,
                        Path = path,
                    };
                }
            }

            Consider(suffixes.Normal, SourceRole.Normal);
            Consider(suffixes.Smoothness, SourceRole.Smoothness);
            Consider(suffixes.Roughness, SourceRole.Roughness);
            Consider(suffixes.AmbientOcclusion, SourceRole.AmbientOcclusion);

            return best;
        }
    }
}
