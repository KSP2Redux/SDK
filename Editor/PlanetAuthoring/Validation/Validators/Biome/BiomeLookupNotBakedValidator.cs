using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Biomes;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Biome
{
    /// <summary>
    /// Flags solid bodies that have a biome mask and at least one channel mapping but no baked
    /// <see cref="BiomeLookupHashTable" /> assigned.
    /// </summary>
    /// <remarks>
    /// Without a baked hash table, the runtime falls through to NONE for every surface query, so
    /// physics-material selection, footstep audio, and the science context biome read all degrade.
    /// One-click fix invokes the baker.
    /// </remarks>
    public sealed class BiomeLookupNotBakedValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "BIOME_LOOKUP_NOT_BAKED";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null) yield break;
            var pqs = BodyResolver.FindPqsIncludingAsset(body);
            PQSData pqsData = pqs?.data;
            if (pqsData?.heightMapInfo?.mask == null) yield break;
            if (pqsData.PlanetBiomeHashTable != null) yield break;

            PQSDataAuthoring sidecar = AuthoringSidecars.GetOrCreate(pqsData);
            if (sidecar?.biomeChannelMapping == null) yield break;
            bool anyMapped = false;
            for (int i = 0; i < sidecar.biomeChannelMapping.Length; i++)
                if (sidecar.biomeChannelMapping[i] != PQSData.KSP2BiomeType.NONE) { anyMapped = true; break; }
            if (!anyMapped) yield break;

            PQSData captured = pqsData;
            var fixes = new[]
            {
                new ValidationFix("Bake biome lookup", () => BiomeLookupBaker.Bake(captured)),
            };
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                $"Body '{body.Data?.bodyName ?? "(unnamed)"}' has biome channel mappings but no baked BiomeLookupHashTable. " +
                $"Click Bake biome lookup to produce one.",
                fixes);
        }
    }
}
