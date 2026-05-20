using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    public static partial class SurfaceAuthoringBuilder
    {
        // ===================== Heightmap stack (PQSData) =====================

        /// <summary>
        /// Builds the Heightmap stack foldout exposing global heightmap, per-biome raw heightmaps, transition, and filtering fields.
        /// </summary>
        /// <param name="pqsDataSO">SerializedObject wrapping the bound PQSData whose heightmap settings are edited.</param>
        /// <param name="material">Surface material that hosts the <c>_LargeHeightMapUVScales</c> and <c>_MediumHeightMapUVScales</c> Vector4 slots mirrored from the per-biome UV scales.</param>
        /// <returns>The populated Heightmap stack foldout.</returns>
        public static Foldout BuildHeightmapStackSection(SerializedObject pqsDataSO, Material material)
        {
            var foldout = new Foldout { text = "Heightmap stack", value = true };
            foldout.AddToClassList("pqs-inspector-section");

            foldout.Add(GroupLabel("Global heightmap"));
            foldout.Add(BindPropertyField(
                pqsDataSO, "heightMapInfo.globalHeightMap",
                "Heightmap",
                "Whole-planet 16-bit heightmap. Drives mesh displacement at all distances."
            ));
            foldout.Add(BindPropertyField(
                pqsDataSO, "heightMapInfo.heightMapScale",
                "Height scale",
                "Vertical scale applied to the global heightmap (in meters)."
            ));
            foldout.Add(BindPropertyField(
                pqsDataSO, "heightMapInfo.heightMapContrast",
                "Contrast",
                "Contrast multiplier applied during heightmap sampling. Sharpens or softens elevation transitions."
            ));
            foldout.Add(BindPropertyField(
                pqsDataSO, "heightMapInfo.DitheringScale",
                "Dither scale",
                "Per-quad displacement dithering scale. Prevents banding on flat regions."
            ));

            // Global gradience texture only contributes when REDUX_GRADIENCE is enabled.
            if (material != null && material.IsKeywordEnabled("REDUX_GRADIENCE"))
            {
                foldout.Add(MaterialPropertyFields.MaterialOnlyTexture(
                    material, "_GlobalGradienceTex",
                    "Global gradience",
                    "Baked whole-planet gradience texture. Auto-populated by the body surface bake. Re-bake to refresh."
                ));
            }

            foldout.Add(GroupLabel("Large heightmaps"));
            AddBiomeHeightmapRows(foldout, pqsDataSO, "large", "large-scale", material);

            foldout.Add(GroupLabel("Medium heightmaps"));
            AddBiomeHeightmapRows(foldout, pqsDataSO, "medium", "mid-scale", material);

            foldout.Add(GroupLabel("Scaled-to-local transition"));
            foldout.Add(BindPropertyField(
                pqsDataSO, "heightMapInfo.scaledToLocalTransition",
                "Transition distance",
                "Distance (m) at which rendering crosses from scaled-space to local-space."
            ));
            foldout.Add(BindPropertyField(
                pqsDataSO, "heightMapInfo.scaledToLocalBlend",
                "Blend distance",
                "Width (m) of the blend region around the transition where both regimes contribute."
            ));

            foldout.Add(GroupLabel("Filtering modes"));
            foldout.Add(BindPropertyField(
                pqsDataSO, "heightMapInfo.globalTextureFilteringMode",
                "Global",
                "Filtering mode for the global heightmap during sampling."
            ));
            foldout.Add(BindPropertyField(
                pqsDataSO, "heightMapInfo.largeTextureFilteringMode",
                "Large",
                "Filtering mode for the per-biome large raw heightmaps during CPU mesh-displacement sampling."
            ));
            foldout.Add(BindPropertyField(
                pqsDataSO, "heightMapInfo.mediumTextureFilteringMode",
                "Mid",
                "Filtering mode for the per-biome mid raw heightmaps during CPU mesh-displacement sampling."
            ));
            foldout.Add(BindPropertyField(
                pqsDataSO, "heightMapInfo.decalHeightFilteringMode",
                "Decal height",
                "Filtering mode for decal height contributions."
            ));
            foldout.Add(BindPropertyField(
                pqsDataSO, "heightMapInfo.decalAlphaFilteringMode",
                "Decal alpha",
                "Filtering mode for decal alpha contributions."
            ));

            return foldout;
        }

        // ===================== Pole settings (PQSData) =====================

        /// <summary>
        /// Builds the Pole settings foldout exposing distortion-fix offsets and per-pole height decals.
        /// </summary>
        /// <param name="pqsDataSO">SerializedObject wrapping the bound PQSData whose pole settings are edited.</param>
        /// <returns>The populated Pole settings foldout.</returns>
        public static Foldout BuildPoleSettingsSection(SerializedObject pqsDataSO)
        {
            var foldout = new Foldout { text = "Pole settings", value = true };
            foldout.AddToClassList("pqs-inspector-section");

            foldout.Add(GroupLabel("Distortion fix"));
            foldout.Add(BindPropertyField(
                pqsDataSO, "heightMapInfo.PoleDirectionOffset",
                "Direction offset",
                "XZ offsets applied to the sampling direction near the poles. Avoids " +
                "texture-sampling artifacts at the polar singularity."
            ));
            foldout.Add(BindPropertyField(
                pqsDataSO, "heightMapInfo.UVOffsetV",
                "V offset",
                "V-coordinate offset applied near the poles to fix UV distortion."
            ));

            foldout.Add(GroupLabel("North pole decal"));
            AddPoleDecalFields(foldout, pqsDataSO, "heightMapInfo.PoleHeightDecalSettings.North", "north");

            foldout.Add(GroupLabel("South pole decal"));
            AddPoleDecalFields(foldout, pqsDataSO, "heightMapInfo.PoleHeightDecalSettings.South", "south");

            return foldout;
        }

        // Renders four (Texture, Scale, UV Scale) triples (R/G/B/A) per biome bound directly
        // to the per-biome HeightRegion fields on PQSData. The Texture and Height Scale feed
        // CPU mesh displacement and the bake's gradience output. The UV Scale is also mirrored
        // into the material's _LargeHeightMapUVScales / _MediumHeightMapUVScales Vector4 so the
        // runtime shader samples the gradience at the same tile rate the raw heightmap uses.
        // Adds a spacer between biome groupings for visual separation, and applies
        // unity-base-field__aligned so labels share a column width across the section.
        private static void AddBiomeHeightmapRows(
            VisualElement parent, SerializedObject pqsDataSO, string regionPrefix, string scaleDescription, Material material)
        {
            string uvScaleMaterialProp = regionPrefix == "large"
                ? "_LargeHeightMapUVScales"
                : "_MediumHeightMapUVScales";

            for (int idx = 0; idx < PlanetAuthoringNaming.BiomeChannels.Length; idx++)
            {
                string c = PlanetAuthoringNaming.BiomeChannels[idx];

                if (idx > 0)
                    parent.Add(BiomeGroupSpacer());

                var textureField = BindPropertyField(
                    pqsDataSO, $"heightMapInfo.{regionPrefix}{c}.heightMap",
                    $"{c} Texture",
                    $"Raw heightmap for biome {c}'s {scaleDescription} contribution. " +
                    "Consumed by CPU mesh displacement and as the source for the bake's gradience output."
                );
                textureField.AddToClassList("unity-base-field__aligned");
                parent.Add(textureField);

                var scaleField = BindPropertyField(
                    pqsDataSO, $"heightMapInfo.{regionPrefix}{c}.heightScale",
                    $"{c} Scale",
                    $"Vertical scale (meters) applied to biome {c}'s {scaleDescription} heightmap."
                );
                scaleField.AddToClassList("unity-base-field__aligned");
                parent.Add(scaleField);

                var uvScaleField = MaterialPropertyFields.MirroredIntChannel(
                    pqsDataSO, $"heightMapInfo.{regionPrefix}{c}.uvScale",
                    material, uvScaleMaterialProp, idx,
                    $"{c} UV Scale",
                    $"Tile rate for biome {c}'s {scaleDescription} heightmap across the planet. " +
                    $"Higher values tile more frequently. Packed into {uvScaleMaterialProp} channel {idx} for runtime sampling."
                );
                uvScaleField.AddToClassList("unity-base-field__aligned");
                parent.Add(uvScaleField);
            }
        }

        private static VisualElement BiomeGroupSpacer()
        {
            var spacer = new VisualElement();
            spacer.style.height = 6;
            return spacer;
        }

        private static void AddPoleDecalFields(VisualElement parent, SerializedObject pqsDataSO, string pathPrefix, string poleName)
        {
            parent.Add(BindPropertyField(
                pqsDataSO, $"{pathPrefix}.Radius",
                "Radius",
                $"Radial extent (0..3) over which the {poleName} polar smoothing decal applies."
            ));
            parent.Add(BindPropertyField(
                pqsDataSO, $"{pathPrefix}.BlendFalloff",
                "Blend falloff",
                "Falloff softness for the decal's blend with surrounding terrain."
            ));
            parent.Add(BindPropertyField(
                pqsDataSO, $"{pathPrefix}.HeightOffset",
                "Height offset",
                "Vertical offset added to elevation inside the decal radius."
            ));
            parent.Add(BindPropertyField(
                pqsDataSO, $"{pathPrefix}.NoiseScale",
                "Noise scale",
                "Amplitude of noise added to the decal to break up uniform smoothing."
            ));
            parent.Add(BindPropertyField(
                pqsDataSO, $"{pathPrefix}.NoiseFrequency",
                "Noise frequency",
                "Spatial frequency of the noise added to the decal."
            ));
        }
    }
}
