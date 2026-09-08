using System;
using System.Collections.Generic;
using System.Linq;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// The file-name suffixes that identify each source map role when the packed-map baker scans a folder.
    /// </summary>
    /// <remarks>
    /// Defaults follow the naming already in the repo's tile corpus, which is a mix of Poly Haven
    /// exports (<c>_nor</c>, <c>_rough</c>, <c>_ao</c>) and their single-letter abbreviations
    /// (<c>_n</c>, <c>_g</c>, <c>_h</c>, <c>_d</c>). Artists exporting from a different tool can
    /// replace any list from the baker window, so the batch mode is not bound to one exporter.
    /// </remarks>
    public sealed class PackedMapSuffixes
    {
        /// <summary>
        /// Suffixes identifying a tangent-space normal map.
        /// </summary>
        public IReadOnlyList<string> Normal { get; set; } = new[] { "_n_fixed", "_nor_gl", "_nor_dx", "_normal", "_nor", "_nrm", "_n" };

        /// <summary>
        /// Suffixes identifying a smoothness or gloss map.
        /// </summary>
        public IReadOnlyList<string> Smoothness { get; set; } = new[] { "_smoothness", "_smooth", "_gloss", "_g", "_s" };

        /// <summary>
        /// Suffixes identifying a roughness map, which the bake inverts into smoothness.
        /// </summary>
        public IReadOnlyList<string> Roughness { get; set; } = new[] { "_roughness", "_rough", "_r" };

        /// <summary>
        /// Suffixes identifying an ambient-occlusion map.
        /// </summary>
        public IReadOnlyList<string> AmbientOcclusion { get; set; } = new[] { "_occlusion", "_ao", "_occ" };

        /// <summary>
        /// Suffix appended to the shared stem when naming a baked packed map.
        /// </summary>
        public const string OUTPUT_SUFFIX = "_Packed";

        /// <summary>
        /// Parses a comma-separated suffix list into the trimmed, non-empty entries it names.
        /// </summary>
        /// <remarks>
        /// Backs the editable suffix fields in the baker window. Entries missing a leading
        /// underscore get one, so typing <c>ao, rough</c> behaves the same as <c>_ao, _rough</c>.
        /// </remarks>
        /// <param name="commaSeparated">The raw field text.</param>
        /// <returns>The parsed suffixes, in the order given.</returns>
        public static IReadOnlyList<string> Parse(string commaSeparated)
        {
            if (string.IsNullOrWhiteSpace(commaSeparated))
            {
                return Array.Empty<string>();
            }

            return commaSeparated
                .Split(',')
                .Select(entry => entry.Trim())
                .Where(entry => entry.Length > 0)
                .Select(entry => entry.StartsWith("_", StringComparison.Ordinal) ? entry : "_" + entry)
                .ToArray();
        }

        /// <summary>
        /// Renders a suffix list back into the comma-separated form the baker window edits.
        /// </summary>
        /// <param name="suffixes">The suffixes to render.</param>
        /// <returns>The comma-separated text, or an empty string when <paramref name="suffixes" /> is null or empty.</returns>
        public static string Format(IReadOnlyList<string> suffixes) =>
            suffixes == null ? "" : string.Join(", ", suffixes);
    }
}
