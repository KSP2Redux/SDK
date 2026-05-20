using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    public static partial class SurfaceAuthoringBuilder
    {
        // ===================== PARAMS 3.4.1: Large layer (per biome, top-level) =====================

        /// <summary>
        /// Builds a Large biome foldout exposing the per-biome large-scale gradience and normal layer.
        /// </summary>
        /// <param name="material">The surface material whose large-layer properties are edited.</param>
        /// <param name="pqsDataSO">SerializedObject wrapping the bound PQSData for mirrored fields.</param>
        /// <param name="c">The biome channel letter (R, G, B, or A) selecting which biome slot to build.</param>
        /// <param name="idx">The biome channel index (0..3) used to address packed material vector channels.</param>
        /// <param name="subzonesOn">True when SUB_ZONES_ENABLED is active, false otherwise. Disables the subzone filter row when false.</param>
        /// <returns>The populated Large biome foldout.</returns>
        public static Foldout BuildLargeBiomeSection(
            Material material,
            SerializedObject pqsDataSO,
            string c,
            int idx,
            bool subzonesOn
        )
        {
            var foldout = new Foldout { text = $"Large Biome {c}", value = false };
            foldout.AddToClassList("pqs-inspector-section");

            foldout.Add(MaterialPropertyFields.MaterialOnlyTexture(
                material, $"_LargeGradience{c}",
                "Gradience map",
                $"Baked gradience texture for biome {c}. Auto-populated by the body " +
                "surface bake from the raw heightmap in the Heightmap stack section. " +
                "Re-bake to refresh."
            ));

            foldout.Add(MaterialPropertyFields.Texture(
                material, $"_LargeNormal{c}", "Normal",
                "Per-biome large-scale normal map. Adds long, sweeping normal variation " +
                "across the biome (wind-shaped sand, eroded rock striations, glacial flow lines)."
            ));

            foldout.Add(MaterialPropertyFields.UVScaleOffset(
                material, $"_LargeNormal{c}UVParams", "Normal UV",
                $"(Sx, Sy, Ox, Oy) UV transform applied to biome {c}'s large normal map."
            ));

            foldout.Add(MaterialPropertyFields.FadeCurve(
                material, $"_LargeNormal{c}FadeParams", "Normal fade",
                "Distance fade for the large normal layer so it does not double up with the " +
                "scaled normal map at extreme range."
            ));

            if (ShowReservedPref)
            {
                foldout.Add(MaterialPropertyFields.Texture(
                    material, $"_LargeCurvature{c}", "Curvature (res)",
                    "Per-biome large-scale curvature map. Reserved. Declared but not yet consumed by V3."
                ));
            }

            var subzoneFilter = MaterialPropertyFields.SubzoneFilter(
                pqsDataSO, $"heightMapInfo.large{c}.subZoneFilter",
                material, $"_Large{c}SubzoneFilter",
                c, "large normal/gradience"
            );
            if (!subzonesOn)
                subzoneFilter.SetEnabled(false);
            foldout.Add(subzoneFilter);

            return foldout;
        }

        // ===================== PARAMS 3.4.2: Mid layer (per biome, top-level) =====================

        /// <summary>
        /// Builds a Mid biome foldout exposing the per-biome mid-scale gradience and normal layer.
        /// </summary>
        /// <param name="material">The surface material whose mid-layer properties are edited.</param>
        /// <param name="pqsDataSO">SerializedObject wrapping the bound PQSData for mirrored fields.</param>
        /// <param name="c">The biome channel letter (R, G, B, or A) selecting which biome slot to build.</param>
        /// <param name="idx">The biome channel index (0..3) used to address packed material vector channels.</param>
        /// <param name="subzonesOn">True when SUB_ZONES_ENABLED is active, false otherwise. Disables the subzone filter row when false.</param>
        /// <returns>The populated Mid biome foldout.</returns>
        public static Foldout BuildMidBiomeSection(
            Material material,
            SerializedObject pqsDataSO,
            string c,
            int idx,
            bool subzonesOn
        )
        {
            var foldout = new Foldout { text = $"Mid Biome {c}", value = false };
            foldout.AddToClassList("pqs-inspector-section");

            foldout.Add(MaterialPropertyFields.MaterialOnlyTexture(
                material, $"_MidGradience{c}",
                "Gradience map",
                $"Baked gradience texture for biome {c}. Auto-populated by the body " +
                "surface bake from the raw heightmap in the Heightmap stack section. " +
                "Re-bake to refresh."
            ));

            foldout.Add(MaterialPropertyFields.Texture(
                material, $"_MidNormal{c}", "Normal",
                "Per-biome mid-scale normal map. Adds ridge-sized normal variation across " +
                "the biome. Triplanar-sampled."
            ));

            foldout.Add(MaterialPropertyFields.UVScaleOffset(
                material, $"_MidNormal{c}UVParams", "Normal UV",
                $"(Sx, Sy, Ox, Oy) UV transform applied to biome {c}'s mid normal map."
            ));

            foldout.Add(MaterialPropertyFields.FadeCurve(
                material, $"_MidNormal{c}FadeParams", "Normal fade",
                "Distance fade for the mid normal layer so it gives way to the small-biome " +
                "detail tier closer to the camera and the large layer farther out."
            ));

            if (ShowReservedPref)
            {
                foldout.Add(MaterialPropertyFields.Texture(
                    material, $"_MidCurvature{c}", "Curvature (res)",
                    "Per-biome mid-scale curvature map. Reserved. Declared but not yet consumed by V3."
                ));
            }

            var subzoneFilter = MaterialPropertyFields.SubzoneFilter(
                pqsDataSO, $"heightMapInfo.medium{c}.subZoneFilter",
                material, $"_Mid{c}SubzoneFilter",
                c, "mid normal/gradience"
            );
            if (!subzonesOn)
                subzoneFilter.SetEnabled(false);
            foldout.Add(subzoneFilter);

            return foldout;
        }

        // ===================== PARAMS 3.5: Subzone tier 3/4 (per biome, top-level, gated) =====================

        /// <summary>
        /// Builds a Subzone-tier biome foldout for the given tier and biome channel.
        /// </summary>
        /// <remarks>
        /// Active only when SUB_ZONES_ENABLED. Per-biome normal textures across all 8 tier-3/4
        /// slots are repacked into the shared _SubZonesNormalTextureArray subasset whenever a
        /// slot is reassigned.
        /// </remarks>
        /// <param name="material">The surface material whose subzone-tier properties are edited.</param>
        /// <param name="pqsDataSO">SerializedObject wrapping the bound PQSData for mirrored fields.</param>
        /// <param name="pqsDataAuthoringSO">SerializedObject wrapping the PQSDataAuthoring sidecar that carries the subzone normal slice references.</param>
        /// <param name="pqsData">The bound PQSData. Used to drive the texture-array repack.</param>
        /// <param name="tier">The subzone tier number (3 or 4).</param>
        /// <param name="c">The biome channel letter (R, G, B, or A) selecting which biome slot to build.</param>
        /// <param name="idx">The biome channel index (0..3) used to address packed material vector channels and the normals array slice.</param>
        /// <returns>The populated Subzone-tier biome foldout.</returns>
        public static Foldout BuildSubzoneTierBiomeSection(
            Material material,
            SerializedObject pqsDataSO,
            SerializedObject pqsDataAuthoringSO,
            PQSData pqsData,
            int tier,
            string c,
            int idx
        )
        {
            var prefix = $"_Subzone{tier}";
            var dataPrefix = $"subzone{tier}{c}";
            // The normal slice source lives on the PQSDataAuthoring sidecar, not on PQSData itself.
            var normalsArrayPath = PlanetAuthoringNaming.SubzoneNormalPath(tier, idx);

            var foldout = new Foldout { text = $"Subzone{tier} Biome {c}", value = false };
            foldout.AddToClassList("pqs-inspector-section");

            var helpBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            helpBox.style.display = DisplayStyle.None;
            foldout.Add(helpBox);

            void Repack()
            {
                var result = Texture2DArrayPacker.RepackSubzoneNormals(pqsData, material);
                if (result.Success)
                {
                    helpBox.style.display = DisplayStyle.None;
                }
                else
                {
                    helpBox.text = result.ErrorMessage;
                    helpBox.style.display = DisplayStyle.Flex;
                }
            }

            foldout.Add(MaterialPropertyFields.MirroredTexture(
                pqsDataSO, $"heightMapInfo.{dataPrefix}.heightMap",
                material, $"{prefix}Gradience{c}",
                "Gradience map",
                $"Per-biome tier-{tier} height field. Active only when SUB_ZONES_ENABLED. " +
                "Feeds layer/slope derivation in the surface shader prepass. Does not " +
                "contribute to CPU collider/scatter displacement (only the global, large, " +
                "and mid gradience maps do)."
            ));

            foldout.Add(MaterialPropertyFields.MirroredIntChannel(
                pqsDataSO, $"heightMapInfo.{dataPrefix}.uvScale",
                material, $"{prefix}HeightMapUVScales", idx,
                "UV scale",
                $"Tile rate for this biome's tier-{tier} gradience map. Packed into " +
                $"{prefix}HeightMapUVScales channel {idx} for biome {c} at runtime."
            ));

            foldout.Add(MaterialPropertyFields.PqsDataFloat(
                pqsDataSO, $"heightMapInfo.{dataPrefix}.heightScale",
                "Height scale",
                $"Vertical scale factor for the biome's tier-{tier} gradience contribution. " +
                "Forwarded to the GPU mesh-construction compute shader and the surface " +
                "shader. Not used by CPU collider/scatter sampling."
            ));

            foldout.Add(MaterialPropertyFields.PqsDataTexture(
                pqsDataAuthoringSO, normalsArrayPath,
                "Normal",
                $"Per-biome tier-{tier} normal map for biome {c}. Assigned textures across all 8 " +
                "tier-3/4 slots are packed into the shared _SubZonesNormalTextureArray subasset " +
                "automatically. All assigned textures must share width, height, format, and mip count.",
                onChanged: Repack
            ));

            foldout.Add(MaterialPropertyFields.UVScaleOffset(
                material, $"{prefix}Normal{c}UVParams", "Normal UV",
                $"(Sx, Sy, Ox, Oy) UV transform applied to biome {c}'s tier-{tier} normal slice."
            ));

            foldout.Add(MaterialPropertyFields.FadeCurve(
                material, $"{prefix}Normal{c}FadeParams", "Normal fade",
                $"Distance fade for the tier-{tier} normal contribution."
            ));

            if (ShowReservedPref)
            {
                foldout.Add(MaterialPropertyFields.Texture(
                    material, $"{prefix}Curvature{c}", "Curvature (res)",
                    $"Per-biome tier-{tier} curvature map. Reserved. Declared but not yet consumed by V3."
                ));
            }

            foldout.Add(MaterialPropertyFields.SubzoneFilter(
                pqsDataSO, $"heightMapInfo.{dataPrefix}.subZoneFilter",
                material, $"{prefix}{c}SubzoneFilter",
                c, $"tier-{tier}"
            ));

            return foldout;
        }
    }
}
