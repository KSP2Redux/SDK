using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Surface
{
    /// <summary>
    /// Flags a body whose body-surface bake outputs are out of date with respect to the bake inputs.
    /// </summary>
    /// <remarks>
    /// <see cref="BodySurfaceBakerOperation" /> stamps a fingerprint of the per-biome raw heightmaps,
    /// height scales, global heightmap, and body radius onto the <see cref="PQSDataAuthoring" /> sidecar
    /// after a successful bake. This validator recomputes the fingerprint from the current state and
    /// compares. Mismatch (or a missing fingerprint with populated inputs) means the artist edited an
    /// input since the last bake, so the gradience and per-biome normal textures bound on the surface
    /// material are stale: the runtime's slope window and biome normals are evaluated against the
    /// old inputs.
    /// </remarks>
    public sealed class SurfaceBakeDriftValidator : IPlanetValidator
    {
        /// <summary>
        /// Stable code identifying issues emitted by this validator.
        /// </summary>
        public const string Code = "SURFACE_BAKE_DRIFT";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null) yield break;
            PQS pqs = BodyResolver.FindPqsIncludingAsset(body);
            PQSData pqsData = pqs?.data;
            if (pqsData == null) yield break;

            PQSDataAuthoring authoring = AuthoringSidecars.Find(pqsData);
            if (authoring == null) yield break;
            if (!HasAnyBakeInput(pqsData)) yield break;

            float radius = (float)(body.Data?.radius ?? 0.0);
            if (radius <= 0f) yield break;

            string current = BodySurfaceBakerOperation.ComputeSurfaceBakeFingerprint(pqsData, radius);
            if (current == authoring.LastSurfaceBakeFingerprint) yield break;

            string bodyName = body.Data?.bodyName ?? body.gameObject.name;
            CoreCelestialBodyData capturedBody = body;
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                string.IsNullOrEmpty(authoring.LastSurfaceBakeFingerprint)
                    ? $"Body '{bodyName}' has heightmap inputs but no recorded surface bake fingerprint. The gradience and per-biome normal textures bound on the surface material are likely stale."
                    : $"Body '{bodyName}' surface bake inputs have changed since the last bake. The gradience and per-biome normal textures bound on the surface material are stale.",
                new[]
                {
                    new ValidationFix(
                        "Re-bake Body Surface",
                        () => BodySurfaceBakeSection.BakeWithPersistedSettings(capturedBody)),
                });
        }

        private static bool HasAnyBakeInput(PQSData pqsData)
        {
            var hmi = pqsData?.heightMapInfo;
            if (hmi == null) return false;
            if (hmi.globalHeightMap != null) return true;
            return HasRawHeightmap(hmi.largeR) || HasRawHeightmap(hmi.largeG)
                || HasRawHeightmap(hmi.largeB) || HasRawHeightmap(hmi.largeA)
                || HasRawHeightmap(hmi.mediumR) || HasRawHeightmap(hmi.mediumG)
                || HasRawHeightmap(hmi.mediumB) || HasRawHeightmap(hmi.mediumA);
        }

        private static bool HasRawHeightmap(PQSData.HeightRegion region) => region?.heightMap != null;
    }
}
