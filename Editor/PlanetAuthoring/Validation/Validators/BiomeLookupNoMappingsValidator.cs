using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators
{
    /// <summary>
    /// Flags solid bodies whose PQSData has a biome mask but no channel mappings set in the
    /// authoring sidecar.
    /// </summary>
    /// <remarks>
    /// The biome lookup baker refuses to run when every channel is NONE because the resulting hash
    /// table would be uniformly NONE. Surfacing it here means the artist doesn't have to click
    /// Bake to find out the mappings are missing.
    /// </remarks>
    public sealed class BiomeLookupNoMappingsValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "BIOME_LOOKUP_NO_MAPPINGS";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null) yield break;
            var pqs = body.GetComponentInChildren<PQS>(true);
            PQSData pqsData = pqs?.data;
            if (pqsData?.heightMapInfo?.mask == null) yield break;

            PQSDataAuthoring sidecar = PlanetAuthoringRegistry.Instance.GetOrCreatePQSData(pqsData);
            if (sidecar?.biomeChannelMapping == null) yield break;

            bool anyMapped = false;
            for (int i = 0; i < sidecar.biomeChannelMapping.Length; i++)
                if (sidecar.biomeChannelMapping[i] != PQSData.KSP2BiomeType.NONE)
                {
                    anyMapped = true;
                    break;
                }
            if (anyMapped) yield break;

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                $"Body '{body.Data?.bodyName ?? "(unnamed)"}' has a biome mask but no biome channel is mapped to a KSP2BiomeType. " +
                $"Open the PQS inspector's Biome Lookup section and assign a type to each populated channel.");
        }
    }
}
