using Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Fields;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    public static partial class SurfaceAuthoringBuilder
    {
        // ===================== PARAMS 3.1: Scaled space =====================

        /// <summary>
        /// Builds the Scaled space foldout exposing the orbital albedo, normal, packed, and emission maps.
        /// </summary>
        /// <param name="material">The surface material whose scaled-space properties are edited.</param>
        /// <returns>The populated Scaled space foldout.</returns>
        public static Foldout BuildScaledSpaceSection(Material material)
        {
            var foldout = new Foldout { text = "Scaled space", value = true };
            foldout.AddToClassList("pqs-inspector-section");

            foldout.Add(MaterialPropertyFields.Texture(
                material, "_AlbedoScaledTex", "Albedo",
                "Whole-planet base color visible from orbit. Drives the planet's identity at distance."
            ));
            foldout.Add(MaterialPropertyFields.FadeCurve(
                material, "_AlbedoScaledFadeParams", "Albedo fade",
                "(start, range, near, far) distance fade controlling when local detail takes over from the orbital albedo."
            ));
            foldout.Add(MaterialPropertyFields.Texture(
                material, "_NormalScaledTex", "Normal",
                "Whole-planet normal map. Drives lighting from orbit."
            ));
            foldout.Add(MaterialPropertyFields.FadeCurve(
                material, "_NormalScaledFadeParams", "Normal fade",
                "(start, range, near, far) distance fade. Controls how the orbital normal gives way to per-biome layers."
            ));
            foldout.Add(MaterialPropertyFields.Texture(
                material, "_PackedScaledTex", "Packed (MOES)",
                "Channel-packed: R=Metallic, G=Occlusion, B=Emission strength, A=Smoothness. PBR + emission for the orbital pass."
            ));
            foldout.Add(MaterialPropertyFields.FadeCurve(
                material, "_PackedScaledFadeParams", "Packed fade",
                "(start, range, near, far) distance fade for the packed map."
            ));
            foldout.Add(MaterialPropertyFields.Texture(
                material, "_EmissionScaledTex", "Emission",
                "Whole-planet emission color. Multiplied by packed.B (emission strength) and Emission Scale."
            ));
            foldout.Add(MaterialPropertyFields.FadeCurve(
                material, "_EmissionScaledFadeParams", "Emission fade",
                "(start, range, near, far) distance fade for emission."
            ));
            foldout.Add(MaterialPropertyFields.Range(
                material, "_EmissionScale", "Emission scale", 0f, 20f,
                "Global emission intensity multiplier (0..20). 0 disables emission."
            ));

            return foldout;
        }

        // ===================== PARAMS 3.2: Biome control =====================

        /// <summary>
        /// Builds the Biome control foldout exposing the biome mask, biome cutoffs, and subzone mask.
        /// </summary>
        /// <param name="material">The surface material whose biome-control properties are edited.</param>
        /// <param name="pqsDataSO">SerializedObject wrapping the bound PQSData for mirrored mask fields.</param>
        /// <returns>The populated Biome control foldout.</returns>
        public static Foldout BuildBiomeControlSection(Material material, SerializedObject pqsDataSO)
        {
            var foldout = new Foldout { text = "Biome control", value = true };
            foldout.AddToClassList("pqs-inspector-section");

            foldout.Add(MaterialPropertyFields.MirroredTexture(
                pqsDataSO, "heightMapInfo.mask",
                material, "_BiomeMaskTex",
                "Biome mask",
                "Mask defining which biome covers which region of the planet. R/G/B/A are 0..1 " +
                "weights for biomes 1..4. The shader normalizes channels per-pixel, so they " +
                "need not sum to 1. The runtime pushes this to the surface material as " +
                "_BiomeMaskTex. The inspector also writes the material side immediately for " +
                "edit-mode preview."
            ));

            foldout.Add(MaterialPropertyFields.Vector4Channels(
                material, "_BiomeCutoffs", "Biome cutoffs",
                "Per-channel cutoff thresholds (R, G, B, A) used by the runtime to bucket " +
                "quads into biomes during PQSQuadConstructor culling. Lower thresholds let " +
                "more pixels register for that biome's pipeline.",
                channelTooltips: new[]
                {
                    "Cutoff threshold for biome R (channel 1).",
                    "Cutoff threshold for biome G (channel 2).",
                    "Cutoff threshold for biome B (channel 3).",
                    "Cutoff threshold for biome A (channel 4).",
                }
            ));

            var subzoneMaskField = MaterialPropertyFields.MirroredTexture(
                pqsDataSO, "heightMapInfo.subZoneMask",
                material, "_SubzoneMaskTex",
                "Subzone mask",
                "Subzone weight map. Active only when SUB_ZONES_ENABLED. Re-biases the " +
                "per-biome layers by 4 additional 0..1 channels. Pushed to the surface " +
                "material as _SubzoneMaskTex."
            );
            if (!material.IsKeywordEnabled("SUB_ZONES_ENABLED"))
                subzoneMaskField.SetEnabled(false);
            foldout.Add(subzoneMaskField);

            return foldout;
        }

        // ===================== PARAMS 3.3: Triplanar / projection =====================

        /// <summary>
        /// Builds the Triplanar / projection foldout exposing triplanar contrast, UV transform, stochastic scale, and the read-only planet radius.
        /// </summary>
        /// <param name="material">The surface material whose triplanar properties are edited.</param>
        /// <returns>The populated Triplanar / projection foldout.</returns>
        public static Foldout BuildTriplanarSection(Material material)
        {
            var foldout = new Foldout { text = "Triplanar / projection", value = true };
            foldout.AddToClassList("pqs-inspector-section");

            foldout.Add(MaterialPropertyFields.Range(
                material, "_TriplanarContrast", "Contrast", 1f, 8f,
                "How sharply the three triplanar projections blend at edges. Low values " +
                "produce soft transitions across rock edges, high values produce crisp " +
                "boundaries with little blending. 4 is the typical default."
            ));

            foldout.Add(MaterialPropertyFields.UVScaleOffset(
                material, "_TriplanarUVScaleOffset", "UV scale/offset",
                "(Sx, Sy, Ox, Oy) shared by all triplanar samples. Adjusts the world size of " +
                "the triplanar projection grid. Scaling down here makes every Small biome " +
                "tile bigger."
            ));

            var stochasticScale = MaterialPropertyFields.Range(
                material, "_StochasticScale", "Stochastic scale", 0.25f, 2f,
                "Hex-grid period for the anti-tiling system. Smaller values shuffle more " +
                "frequently (better tile breakup, more sample blur). Larger values make " +
                "bigger uniform patches. Active only when Anti-tile is on."
            );
            if (!material.IsKeywordEnabled("ANTI_TILE_QUALITY_ON"))
                stochasticScale.SetEnabled(false);
            foldout.Add(stochasticScale);

            foldout.Add(MaterialPropertyFields.FloatReadOnly(
                material, "_Radius", "Planet radius (auto)",
                "Planet radius in PQS units. Auto-set from CoreCelestialBodyData. Read-only."
            ));

            return foldout;
        }

        // ===================== PARAMS 3.8: Decals =====================

        /// <summary>
        /// Builds the Decals foldout exposing the auto-managed decals keyword and the global decal fade curve.
        /// </summary>
        /// <param name="material">The surface material whose decal properties are edited.</param>
        /// <returns>The populated Decals foldout.</returns>
        public static Foldout BuildDecalsSection(Material material)
        {
            var foldout = new Foldout { text = "Decals", value = true };
            foldout.AddToClassList("pqs-inspector-section");

            foldout.Add(MaterialPropertyFields.KeywordReadOnly(
                material, "DECALS_ENABLED", "Decals active (auto)",
                "Auto-managed by PQSDecalController based on the planet's decal instance count. " +
                "Read-only here. To place decals, use the Decal Manager."
            ));

            foldout.Add(MaterialPropertyFields.FadeCurve(
                material, "_DecalFadeParams", "Fade",
                "(start, end, nearOpacity, farOpacity) global decal distance fade. Decals " +
                "appear fully within start..end meters and lerp toward farOpacity past that range. " +
                "Use this to keep decals from drawing on far-LOD terrain where their projection " +
                "accuracy degrades."
            ));

            if (ShowReservedPref)
            {
                foldout.Add(MaterialPropertyFields.Texture(
                    material, "_DecalControl", "Decal control (res)",
                    "Reserved. Declared but not yet consumed by V3."
                ));
                foldout.Add(MaterialPropertyFields.Texture(
                    material, "_DecalStaticData", "Decal static data (res)",
                    "Reserved. Declared but not yet consumed by V3."
                ));
            }

            return foldout;
        }

        // ===================== PARAMS 3.9: Distance-cascade resample =====================

        /// <summary>
        /// Builds the Distance cascade foldout exposing the four resample band distances, UV scales, and per-band albedo and normal opacities.
        /// </summary>
        /// <param name="material">The surface material whose distance-cascade properties are edited.</param>
        /// <returns>The populated Distance cascade foldout.</returns>
        public static Foldout BuildDistanceCascadeSection(Material material)
        {
            var foldout = new Foldout { text = "Distance cascade", value = true };
            foldout.AddToClassList("pqs-inspector-section");

            foldout.Add(MaterialPropertyFields.Vector4Channels(
                material, "_DistanceResampleDistances", "Band distances",
                "The four distance band centers (in meters) at which the small-biome detail " +
                "retiles. Per-layer aggressiveness is dialled in via the Resample tier on each cell.",
                channelLabels: new[] { "b0", "b1", "b2", "b3" },
                channelTooltips: new[]
                {
                    "Distance (m) of band 0 (closest band).",
                    "Distance (m) of band 1.",
                    "Distance (m) of band 2.",
                    "Distance (m) of band 3 (farthest band).",
                }
            ));

            foldout.Add(MaterialPropertyFields.Vector4Channels(
                material, "_DistanceResampleUVScales", "UV scales",
                "UV scale at each band. Typically a power-of-2 cascade like (1, 2, 4, 8) so each " +
                "band tiles 2x larger than the previous and the visible repeat shrinks at range.",
                channelLabels: new[] { "b0", "b1", "b2", "b3" },
                channelTooltips: new[]
                {
                    "UV scale at band 0 (closest).",
                    "UV scale at band 1.",
                    "UV scale at band 2.",
                    "UV scale at band 3 (farthest).",
                }
            ));

            foldout.Add(MaterialPropertyFields.Vector4Channels(
                material, "_DistanceResampleAlbedoOpacity", "Albedo opacity",
                "Per-band albedo opacity. Use to soften retiling at far range so the resampled " +
                "tiles don't pop visually.",
                channelLabels: new[] { "b0", "b1", "b2", "b3" },
                channelTooltips: new[]
                {
                    "Albedo opacity at band 0 (closest).",
                    "Albedo opacity at band 1.",
                    "Albedo opacity at band 2.",
                    "Albedo opacity at band 3 (farthest).",
                }
            ));

            foldout.Add(MaterialPropertyFields.Vector4Channels(
                material, "_DistanceResampleNormalOpacity", "Normal opacity",
                "Per-band normal opacity. Same idea as albedo opacity but for the per-layer normal contribution.",
                channelLabels: new[] { "b0", "b1", "b2", "b3" },
                channelTooltips: new[]
                {
                    "Normal opacity at band 0 (closest).",
                    "Normal opacity at band 1.",
                    "Normal opacity at band 2.",
                    "Normal opacity at band 3 (farthest).",
                }
            ));

            return foldout;
        }

        // ===================== PARAMS 3.10: Cross-biome blend controls =====================

        /// <summary>
        /// Builds the Cross-biome blend foldout exposing heightblend bias, alpha-to-height fade, and per-biome global blend strength.
        /// </summary>
        /// <param name="material">The surface material whose cross-biome blend properties are edited.</param>
        /// <returns>The populated Cross-biome blend foldout.</returns>
        public static Foldout BuildCrossBiomeBlendSection(Material material)
        {
            var foldout = new Foldout { text = "Cross-biome blend", value = true };
            foldout.AddToClassList("pqs-inspector-section");

            foldout.Add(MaterialPropertyFields.Vector4Channels(
                material, "_HeightblendFactor", "Heightblend factor",
                "Per-biome bias controlling how harshly the highest-weight layer takes over " +
                "within each biome. PQSRendererHelper indexes this Vector4 by biome (R, G, B, A) " +
                "so each biome can have its own value. The deferred surface shader currently " +
                "reads only the R component, so today all four entries should usually match. " +
                "Lower = softer all-layers blend, higher = dominant layer wins more aggressively.",
                channelLabels: new[] { "R", "G", "B", "A" },
                channelTooltips: new[]
                {
                    "Heightblend bias for biome R.",
                    "Heightblend bias for biome G (currently sampled only via runtime per-biome path).",
                    "Heightblend bias for biome B (currently sampled only via runtime per-biome path).",
                    "Heightblend bias for biome A (currently sampled only via runtime per-biome path).",
                }
            ));

            foldout.Add(MaterialPropertyFields.FadeCurve(
                material, "_AlphaToHeightFadeParams", "Alpha-to-height fade",
                "(start, range, nearMix, farMix) distance fade for the alpha-vs-heightmap blend " +
                "mode. Up close the blend uses the full height-map signal. At distance it falls " +
                "back to a weight-normalised average."
            ));

            foldout.Add(MaterialPropertyFields.Vector4Channels(
                material, "_GlobalBlend", "Global blend",
                "Per-biome master blend strength. Multiplies the entire per-biome contribution. " +
                "Useful for fading a biome out globally without zeroing every layer.",
                channelLabels: new[] { "R", "G", "B", "A" },
                channelTooltips: new[]
                {
                    "Master blend strength for biome R.",
                    "Master blend strength for biome G.",
                    "Master blend strength for biome B.",
                    "Master blend strength for biome A.",
                }
            ));

            if (ShowReservedPref)
            {
                foldout.Add(MaterialPropertyFields.Texture(
                    material, "_GlobalGradienceTex", "Global gradience (res)",
                    "Reserved. Declared but not yet consumed by V3."
                ));
                foldout.Add(MaterialPropertyFields.Texture(
                    material, "_GlobalCurvatureTex", "Global curvature (res)",
                    "Reserved. Declared but not yet consumed by V3."
                ));
            }

            return foldout;
        }

        // ===================== PARAMS 3.11: Misc =====================

        /// <summary>
        /// Builds the Misc foldout exposing the auto-managed Transition value and any reserved fields.
        /// </summary>
        /// <param name="material">The surface material whose miscellaneous properties are edited.</param>
        /// <returns>The populated Misc foldout.</returns>
        public static Foldout BuildMiscSection(Material material)
        {
            var foldout = new Foldout { text = "Misc", value = false };
            foldout.AddToClassList("pqs-inspector-section");

            foldout.Add(MaterialPropertyFields.FloatReadOnly(
                material, "_Transition", "Transition (auto)",
                "Dither-fade alpha test value. 0 = fully visible, 1 = fully discarded. " +
                "PQSRenderer manages this during quad LOD transitions to crossfade tiles. Read-only."
            ));

            if (ShowReservedPref)
            {
                foldout.Add(MaterialPropertyFields.Texture(
                    material, "_ShorelineTex", "Shoreline (res)",
                    "Reserved. Declared but not yet consumed by V3. When ported, will drive " +
                    "per-pixel shoreline tinting near sea level."
                ));
                foldout.Add(MaterialPropertyFields.Float(
                    material, "_HighQualityEnabled", "High-quality (res)",
                    "Reserved quality-tier toggle. Leave at 1."
                ));
            }

            return foldout;
        }
    }
}
