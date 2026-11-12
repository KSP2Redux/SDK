using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Biomes;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Biome
{
    /// <summary>
    /// Flags solid bodies with biome authoring inputs and no baked <see cref="BiomeLookupHashTable" /> assigned.
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
            PQSData pqsData = PlanetAuthoringSession.AuthoredData(pqs);
            if (pqsData == null || pqsData.PlanetBiomeHashTable != null)
                yield break;

            PQSDataAuthoring sidecar = AuthoringSidecars.Find(pqsData);
            if (sidecar == null)
                yield break;
            if (sidecar.BiomeLookupSource == null && (pqsData.heightMapInfo?.mask == null
                || sidecar.biomeChannelMapping == null
                || !System.Array.Exists(sidecar.biomeChannelMapping, biome => biome != PQSData.KSP2BiomeType.NONE)))
                yield break;

            PQSData captured = pqsData;
            var fixes = new[]
            {
                new ValidationFix("Bake biome lookup", () => BiomeLookupBaker.Bake(captured)),
            };
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                $"Body '{body.Data?.bodyName ?? "(unnamed)"}' has biome authoring inputs but no baked BiomeLookupHashTable. " +
                $"Click Bake biome lookup to produce one.",
                fixes);
        }
    }
}
