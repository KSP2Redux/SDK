using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    public static partial class SurfaceAuthoringBuilder
    {
        // ===================== Heightmap stack (PQSData) =====================

        /// <summary>
        /// Builds the Heightmap stack foldout exposing global heightmap, transition, and filtering fields.
        /// </summary>
        /// <param name="pqsDataSO">SerializedObject wrapping the bound PQSData whose heightmap settings are edited.</param>
        /// <returns>The populated Heightmap stack foldout.</returns>
        public static Foldout BuildHeightmapStackSection(SerializedObject pqsDataSO)
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
                "Filtering mode for the per-biome large gradience maps."
            ));
            foldout.Add(BindPropertyField(
                pqsDataSO, "heightMapInfo.mediumTextureFilteringMode",
                "Mid",
                "Filtering mode for the per-biome mid gradience maps."
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
