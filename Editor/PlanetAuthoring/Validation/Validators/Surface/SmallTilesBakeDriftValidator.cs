using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEngine;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Surface
{
    /// <summary>
    /// Flags a body whose small-tile texture arrays are out of sync with the authoring slot inputs.
    /// </summary>
    /// <remarks>
    /// The packer stamps a fingerprint of the slot inputs onto the PQSData authoring sidecar after a
    /// successful repack. This validator recomputes it from the current state and compares. Mismatch
    /// (or a missing fingerprint with populated slots) means the artist edited a slot, swapped a
    /// SmallLayerMaterial, or replaced a source texture without re-running the packer, so the array
    /// subassets sampled by the surface and analytic-bake shaders are stale. Symptom: the affected
    /// biome bakes as flat gray.
    /// </remarks>
    public sealed class SmallTilesBakeDriftValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "SMALL_TILES_PACK_DRIFT";

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

            Material surfaceMaterial = pqsData.materialSettings?.surfaceMaterial;
            if (surfaceMaterial == null) yield break;

            PQSDataAuthoring authoring = AuthoringSidecars.Find(pqsData);
            if (authoring?.smallLayerSlots == null) yield break;
            if (!HasAnyPopulatedSlot(authoring)) yield break;

            string current = Texture2DArrayPacker.ComputeSmallTilesFingerprint(authoring);
            if (current == authoring.LastSmallTilesPackFingerprint) yield break;

            string bodyName = body.Data?.bodyName ?? body.gameObject.name;
            PQSData capturedData = pqsData;
            Material capturedMaterial = surfaceMaterial;
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                string.IsNullOrEmpty(authoring.LastSmallTilesPackFingerprint)
                    ? $"Body '{bodyName}' has populated small-tile slots but no recorded pack fingerprint. The Texture2DArray subassets are likely stale or empty, which bakes affected biomes as flat gray."
                    : $"Body '{bodyName}' small-tile slot inputs have changed since the last repack. The Texture2DArray subassets are stale, which bakes affected biomes as flat gray.",
                new[]
                {
                    new ValidationFix(
                        "Force Repack",
                        () => Texture2DArrayPacker.RepackSmallTiles(capturedData, capturedMaterial)),
                });
        }

        private static bool HasAnyPopulatedSlot(PQSDataAuthoring authoring)
        {
            for (var i = 0; i < authoring.smallLayerSlots.Length; i++)
            {
                var slot = authoring.smallLayerSlots[i];
                if (slot?.EffectiveAlbedoTexture != null
                    || slot?.EffectiveNormalTexture != null
                    || slot?.EffectiveMetallicTexture != null)
                    return true;
            }
            return false;
        }
    }
}
