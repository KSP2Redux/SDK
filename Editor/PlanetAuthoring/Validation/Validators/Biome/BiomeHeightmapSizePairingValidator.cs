using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEngine;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Biome
{
    /// <summary>
    /// Warns when the biome mask and global heightmap have different resolutions.
    /// </summary>
    /// <remarks>
    /// PQS samples the two textures at parallel UVs. Mismatched resolutions cause biome boundaries to drift relative to the terrain features the artist painted them on.
    /// </remarks>
    public sealed class BiomeHeightmapSizePairingValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "BIOME_HEIGHTMAP_SIZE_MISMATCH";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null || !body.Core.data.hasSolidSurface)
                yield break;
            var pqs = BodyResolver.FindPqsIncludingAsset(body);
            if (pqs == null || pqs.data == null || pqs.data.heightMapInfo == null)
                yield break;
            Texture2D height = pqs.data.heightMapInfo.globalHeightMap;
            Texture2D mask = pqs.data.heightMapInfo.mask;
            if (height == null || mask == null)
                yield break;
            if (height.width == mask.width && height.height == mask.height)
                yield break;

            string message = $"Biome mask resolution ({mask.width}x{mask.height}) does not match global heightmap resolution ({height.width}x{height.height}). Biome boundaries will drift relative to terrain features.";
            yield return new ValidationIssue(Code, ValidationSeverity.Warning, message);
        }
    }
}
