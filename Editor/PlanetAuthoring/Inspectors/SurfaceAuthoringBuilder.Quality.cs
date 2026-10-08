using System;
using Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Fields;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    public static partial class SurfaceAuthoringBuilder
    {
        // ===================== Quality keywords =====================

        /// <summary>
        /// Builds the Quality foldout exposing surface keyword toggles and the reserved-fields preference.
        /// </summary>
        /// <param name="material">The surface material whose shader keywords are toggled.</param>
        /// <param name="pqsDataSO">SerializedObject wrapping the bound PQSData for mirrored-keyword fields.</param>
        /// <param name="refresh">Callback invoked when a keyword toggles so the inspector rebuilds gated sections.</param>
        /// <returns>The populated Quality foldout.</returns>
        public static Foldout BuildQualitySection(
            Material material,
            SerializedObject pqsDataSO,
            Action refresh
        )
        {
            var foldout = new Foldout { text = "Quality", value = true };
            foldout.AddToClassList("pqs-inspector-section");

            foldout.Add(MaterialPropertyFields.MirroredKeyword(
                pqsDataSO, "heightMapInfo.subZonesEnabled",
                material, "SUB_ZONES_ENABLED",
                "Sub-zones enabled",
                "Enables the subzone overlay pipeline (4 extra 0..1 channels via the Subzone " +
                "mask that re-tint and re-weight per-biome layers). Off disables the entire " +
                "_SmallSubzone, _Subzone3, and _Subzone4 family. Mirrored to " +
                "PQSData.heightMapInfo.subZonesEnabled.",
                refresh
            ));

            foldout.Add(MaterialPropertyFields.MirroredKeyword(
                pqsDataSO, "materialSettings.antiTileOn",
                material, "ANTI_TILE_QUALITY_ON",
                "Anti-tile",
                "Enables stochastic hex-grid anti-tiling on the small-biome detail samples. " +
                "Heavier on the GPU but eliminates obvious tile repetition on uniform fields. " +
                "Mirrored to PQSData.materialSettings.antiTileOn."
            ));

            foldout.Add(MaterialPropertyFields.MirroredKeyword(
                pqsDataSO, "materialSettings.reduxGradienceEnabled",
                material, "REDUX_GRADIENCE",
                "Redux gradience format",
                "When on, gradience textures store true (dh/du, dh/dv) gradients that sum as " +
                "vectors, and the global gradience texture contributes to slope evaluation. " +
                "Slope windows read true degrees. Re-bake required after toggling.",
                refresh
            ));

            foldout.Add(MaterialPropertyFields.KeywordReadOnly(
                material, "DECALS_ENABLED", "Decals enabled (auto)",
                "Auto-managed by PQSDecalController based on decal instance count. Read-only. " +
                "Toggling here would just be undone next frame."
            ));

            foldout.Add(MaterialPropertyFields.KeywordReadOnly(
                material, "LOW_QUALITY", "Low quality (auto)",
                "Runtime-driven by view distance. Read-only. Do not toggle manually."
            ));

            var showReservedToggle = new Toggle("Show reserved fields")
            {
                tooltip = "When on, the inspector shows shader fields declared but not yet " +
                    "consumed by V3 (Peak/cavity windows, curvature maps). Authoring-only " +
                    "preference. Persisted in EditorPrefs.",
                value = ShowReservedPref,
            };
            showReservedToggle.AddToClassList("unity-base-field__aligned");
            showReservedToggle.RegisterValueChangedCallback(evt =>
            {
                ShowReservedPref = evt.newValue;
                refresh?.Invoke();
            });
            foldout.Add(showReservedToggle);

            return foldout;
        }
    }
}
