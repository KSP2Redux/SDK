using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Surface
{
    /// <summary>
    /// Errors per missing layer heightmap slot on <c>PQSData.heightMapInfo</c>.
    /// </summary>
    /// <remarks>
    /// Each of the eight large/medium R/G/B/A layer slots feeds both the GPU mesh-builder and the Burst-mode CPU
    /// scatter path. A null slot reads garbage on the GPU and NREs on the CPU. One issue is emitted per missing
    /// slot so the artist sees exactly which channels are gaps.
    /// </remarks>
    public sealed class MissingLayerHeightmapValidator : IPlanetValidator
    {
        /// <summary>
        /// Stable code identifying issues emitted by this validator.
        /// </summary>
        public const string Code = "MISSING_LAYER_HEIGHTMAP";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null) yield break;
            PQS pqs = BodyResolver.FindPqsIncludingAsset(body);
            var info = pqs?.data?.heightMapInfo;
            if (info == null) yield break;

            if (info.largeR?.heightMap == null) yield return Build("largeR");
            if (info.largeG?.heightMap == null) yield return Build("largeG");
            if (info.largeB?.heightMap == null) yield return Build("largeB");
            if (info.largeA?.heightMap == null) yield return Build("largeA");
            if (info.mediumR?.heightMap == null) yield return Build("mediumR");
            if (info.mediumG?.heightMap == null) yield return Build("mediumG");
            if (info.mediumB?.heightMap == null) yield return Build("mediumB");
            if (info.mediumA?.heightMap == null) yield return Build("mediumA");
        }

        private static ValidationIssue Build(string slot) => new(
            Code,
            ValidationSeverity.Error,
            $"PQSData heightmap slot '{slot}' is empty. Assign a heightmap texture or the renderer reads garbage at runtime.");
    }
}
