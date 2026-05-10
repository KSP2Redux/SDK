using System;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Builds the per-(biome, layer) inline detail editor that sits below the <see cref="SmallLayerMatrix" />.
    /// </summary>
    /// <remarks>
    /// One instance per matrix selection. The parent clears its detail slot and inserts a new
    /// editor when the selection changes. All fields are grouped into named sub-sections via
    /// <c>pqs-inspector-group-label</c> USS labels rather than nested foldouts to keep the
    /// layout flat. Tile asset fields trigger the small-tile packer on change. The packer's
    /// <see cref="Texture2DArrayPacker.PackResult" /> is surfaced through an optional
    /// caller-supplied error HelpBox at the section root.
    /// </remarks>
    public static class SmallBiomeDetailEditor
    {
        /// <summary>
        /// Builds the detail editor visual element for the given biome row and layer column.
        /// </summary>
        /// <remarks>
        /// When <paramref name="biome" /> or <paramref name="layer" /> is negative, returns an
        /// empty-selection hint instead of the full editor.
        /// </remarks>
        /// <param name="material">The surface material that owns the per-layer shader properties.</param>
        /// <param name="pqsDataSO">The serialized <see cref="PQSData" /> hosting tile asset references.</param>
        /// <param name="pqsData">The backing <see cref="PQSData" /> instance, used to derive planet altitude limits.</param>
        /// <param name="biome">The biome row index (0-3), or a negative value for no selection.</param>
        /// <param name="layer">The layer column index (0-3), or a negative value for no selection.</param>
        /// <param name="repackTiles">Callback invoked when a tile asset changes so the parent can rerun the packer.</param>
        /// <returns>The constructed detail editor element.</returns>
        public static VisualElement Build(
            Material material,
            SerializedObject pqsDataSO,
            PQSData pqsData,
            int biome,
            int layer,
            Action repackTiles
        )
        {
            var root = new VisualElement();
            if (biome < 0 || layer < 0)
            {
                var hint = new Label("Select a cell in the matrix above to edit its layer parameters.");
                hint.AddToClassList("pqs-inspector-empty-selection");
                root.Add(hint);
                return root;
            }

            var c = PqsAuthoringNaming.BiomeChannels[biome];
            var i = layer + 1;
            var slot = biome * 4 + layer;

            root.Add(SectionLabel($"Layer {i} of Biome {c}"));
            root.Add(BuildTileAssets(material, pqsDataSO, slot, c, layer, repackTiles));
            root.Add(BuildMasterMix(material, c, layer));
            root.Add(BuildHeightWindow(material, pqsData, c, layer, i));
            root.Add(BuildSlopeWindow(material, c, layer, i));
            root.Add(BuildPeakCavity(material, c, layer, i));
            root.Add(BuildDistanceResample(material, c, layer));
            root.Add(BuildUVs(material, c, layer));
            root.Add(BuildColorGrading(material, c, layer, i));
            root.Add(BuildPBR(material, c, layer));
            root.Add(BuildEmission(material, c, layer, i));
            if (material.IsKeywordEnabled("SUB_ZONES_ENABLED"))
                root.Add(BuildSubzoneOverrides(material, c, i));

            return root;
        }

        private static VisualElement BuildTileAssets(
            Material material, SerializedObject pqsDataSO, int slot, string c, int layer, Action repackTiles)
        {
            var section = new VisualElement();
            section.Add(GroupLabel("Tile assets"));
            section.Add(MaterialPropertyFields.PqsDataTexture(
                pqsDataSO, PqsAuthoringNaming.SmallAlbedoTilePath(slot),
                "Albedo",
                "Albedo (color) tile for this layer. Drag a Texture2D in. The tool packs it " +
                "into _SmallAlbedoArray automatically. Cell is disabled (slice index -1) if albedo is empty.",
                onChanged: repackTiles
            ));
            section.Add(MaterialPropertyFields.PqsDataTexture(
                pqsDataSO, PqsAuthoringNaming.SmallNormalTilePath(slot),
                "Normal+SAO",
                "Normal+packed tile. RGBA encodes (metallic-influence, normalY, AO, normalX) DXT5nm-style. " +
                "Required when albedo is set - leave empty only when the cell has no albedo either.",
                onChanged: repackTiles
            ));
            section.Add(MaterialPropertyFields.PqsDataTexture(
                pqsDataSO, PqsAuthoringNaming.SmallMetalTilePath(slot),
                "Metallic",
                "Metallic mask tile. Sampled R channel is the per-tile metallic value. Required when albedo is set.",
                onChanged: repackTiles
            ));
            section.Add(MaterialPropertyFields.Vector4ChannelToggle(
                material, $"_SmallEnable{c}", layer,
                "Enabled",
                "Per-layer master toggle. Mutes this layer without changing its slice assignment. Useful for A/B comparing layers."
            ));
            return section;
        }

        private static VisualElement BuildMasterMix(Material material, string c, int layer)
        {
            var section = new VisualElement();
            section.Add(GroupLabel("Master mix"));
            section.Add(MaterialPropertyFields.Vector4ChannelFloat(
                material, $"_SmallHeightWeight{c}", layer,
                "Weight",
                "Master strength for this layer applied before height/slope gating. Treat as the layer's global intensity. " +
                "0 hides it, 1 makes it eligible everywhere this biome is active."
            ));
            section.Add(MaterialPropertyFields.Vector4ChannelFloat(
                material, $"_SmallWeightSoftness{c}", layer,
                "Softness",
                "Height-blend softness for this layer. Lower values give sharp transitions (crisp snow line). " +
                "Higher values give softer feathering."
            ));
            return section;
        }

        private static VisualElement BuildHeightWindow(Material material, PQSData pqsData, string c, int layer, int i)
        {
            var section = new VisualElement();
            section.Add(GroupLabel("Height window"));
            section.Add(MaterialPropertyFields.Vector4ChannelToggle(
                material, "_SmallBiomeHeightEnable" + c, layer,
                "Enabled",
                "Toggles the altitude window. Off means the layer ignores altitude entirely."
            ));
            section.Add(MaterialPropertyFields.TrapezoidWindow(
                material, $"_SmallBiome{c}HeightParams{i}",
                "Window",
                "Altitude trapezoidal window (center, +up, -down, fadeOut). The layer is at full strength " +
                "between center-down and center+up, fading out over fadeOut meters at each end.",
                TrapezoidWindowField.AxisMode.Height,
                xMaxOverride: GetPlanetMaxAltitude(pqsData)
            ));
            return section;
        }

        private static VisualElement BuildSlopeWindow(Material material, string c, int layer, int i)
        {
            var section = new VisualElement();
            section.Add(GroupLabel("Slope window"));
            section.Add(MaterialPropertyFields.Vector4ChannelToggle(
                material, "_SmallBiomeSlopeEnable" + c, layer,
                "Enabled",
                "Toggles the slope window. Off means the layer ignores slope entirely."
            ));
            section.Add(MaterialPropertyFields.TrapezoidWindow(
                material, $"_SmallBiome{c}SlopeParams{i}",
                "Window",
                "Slope trapezoidal window in degrees. Use a window centered near 0° (narrow) for flat-ground layers " +
                "and near 90° for cliffs. Fade-out smooths the transition between window edges and outside.",
                TrapezoidWindowField.AxisMode.Slope
            ));
            section.Add(MaterialPropertyFields.Vector4Channels(
                material, $"_SmallBiome{c}GradMapWeights{i}",
                "Grad weights",
                "Mixes per-pixel height-map gradient into the slope test. Higher values make the layer respond " +
                "more to height-map bumps even on geometric flats."
            ));
            return section;
        }

        private static VisualElement BuildPeakCavity(Material material, string c, int layer, int i)
        {
            var section = new VisualElement();
            section.Add(GroupLabel("Peak / cavity (reserved)"));
            section.Add(MaterialPropertyFields.Vector4ChannelToggle(
                material, "_SmallBiomePeakCavEnable" + c, layer,
                "Enabled",
                "Reserved. Declared but not yet consumed by V3."
            ));
            section.Add(MaterialPropertyFields.Vector4Channels(
                material, $"_SmallBiome{c}PeakCavParams{i}",
                "Window",
                "Reserved. Declared but not yet consumed by V3.",
                channelLabels: new[] { "c", "+", "-", "fade" }
            ));
            section.Add(MaterialPropertyFields.Vector4Channels(
                material, $"_SmallBiome{c}CurvMapWeights{i}",
                "Curv weights",
                "Reserved curvature-map mix. Declared but not yet consumed by V3."
            ));
            section.style.display = SurfaceAuthoringBuilder.ShowReservedPref
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            return section;
        }

        private static VisualElement BuildDistanceResample(Material material, string c, int layer)
        {
            var section = new VisualElement();
            section.Add(GroupLabel("Distance resample"));
            section.Add(MaterialPropertyFields.Vector4ChannelInt(
                material, $"_SmallDistanceResampleMax{c}", layer,
                "Tier",
                "Distance-resample tier 0..4. 0 = no resample, 4 = aggressive resample at all 4 distance bands. " +
                "Higher tiers cost more samples but reduce visible tile repetition at range."
            ));
            return section;
        }

        private static VisualElement BuildUVs(Material material, string c, int layer)
        {
            var section = new VisualElement();
            section.Add(GroupLabel("UVs"));
            section.Add(MaterialPropertyFields.Vector4ChannelFloat(
                material, $"_SmallUVScale{c}", layer,
                "Scale",
                "Per-layer UV scale. Larger value = the tile prints smaller on the surface."
            ));
            section.Add(MaterialPropertyFields.Vector4ChannelFloat(
                material, $"_SmallUVOffset{c}", layer,
                "Offset",
                "Per-layer UV offset. Use to break up alignment between two layers that share the same tile."
            ));
            return section;
        }

        private static VisualElement BuildColorGrading(Material material, string c, int layer, int i)
        {
            var section = new VisualElement();
            section.Add(GroupLabel("Color grading"));
            section.Add(MaterialPropertyFields.Color(
                material, $"_SmallTint{c}{i}",
                "Tint",
                "Per-layer tint color (RGBA). RGB multiplies the layer's albedo. Alpha multiplies the layer's height-blend alpha " +
                "(fades coverage without changing color)."
            ));
            section.Add(MaterialPropertyFields.Vector4ChannelFloat(
                material, $"_SmallBrightness{c}", layer,
                "Brightness",
                "Additive brightness applied before contrast/saturation. Positive pushes brighter, negative darker."
            ));
            section.Add(MaterialPropertyFields.Vector4ChannelFloat(
                material, $"_SmallContrast{c}", layer,
                "Contrast",
                "Contrast multiplier around mid-gray. 1 = neutral, >1 = punchier, <1 = washed out."
            ));
            section.Add(MaterialPropertyFields.Vector4ChannelFloat(
                material, $"_SmallSaturation{c}", layer,
                "Saturation",
                "Saturation multiplier. 0 = grayscale, 1 = neutral, >1 = oversaturated."
            ));
            return section;
        }

        private static VisualElement BuildPBR(Material material, string c, int layer)
        {
            var section = new VisualElement();
            section.Add(GroupLabel("PBR"));
            section.Add(MaterialPropertyFields.Vector4ChannelFloat(
                material, $"_SmallNormalStrength{c}", layer,
                "Normal",
                "Multiplier on the layer's normal map. 0 = flat, 1 = neutral, >1 = exaggerated."
            ));
            section.Add(MaterialPropertyFields.Vector4ChannelFloat(
                material, $"_SmallGlossStrength{c}", layer,
                "Gloss",
                "Smoothness multiplier. Set ≥ 15 to switch to override mode (forces a fixed smoothness instead of multiplying the source map)."
            ));
            section.Add(MaterialPropertyFields.Vector4ChannelFloat(
                material, $"_SmallMetallicStrength{c}", layer,
                "Metallic",
                "Metallic multiplier. Same ≥ 15 override convention as gloss."
            ));
            section.Add(MaterialPropertyFields.Vector4ChannelFloat(
                material, $"_SmallAOStrength{c}", layer,
                "AO",
                "Ambient-occlusion power. Higher values give a more aggressive AO contribution from this layer."
            ));
            return section;
        }

        private static VisualElement BuildEmission(Material material, string c, int layer, int i)
        {
            var section = new VisualElement();
            section.Add(GroupLabel("Emission"));
            section.Add(MaterialPropertyFields.Vector4ChannelFloat(
                material, $"_SmallEmissionStrength{c}", layer,
                "Strength",
                "Self-illumination multiplier. 0 disables this layer's emission."
            ));
            section.Add(MaterialPropertyFields.Color(
                material, $"_SmallEmissionColor{c}{i}",
                "Color",
                "Per-layer emission color (HDR). Useful for crystal veins, glowing fungi, lava cracks, bioluminescent plankton, etc."
            ));
            return section;
        }

        // Naming asymmetry: the underlying tint material properties end in _R/_G/_B/_A
        // (matching the biome channel they ride on), but the inspector labels them sz0..sz3
        // to align with the four subzone channels exposed elsewhere in this section.
        private static VisualElement BuildSubzoneOverrides(Material material, string c, int i)
        {
            var section = new VisualElement();
            section.Add(GroupLabel("Subzone overrides"));
            section.Add(MaterialPropertyFields.Vector4Channels(
                material, $"_SmallSubzoneWeight{c}{i}",
                "Weight",
                "Per-subzone weight applied to this layer. Replaces the constant Master mix " +
                "weight while SUB_ZONES_ENABLED. Lets you say 'this layer is twice as strong " +
                "in subzone 0, off in subzone 2'.",
                channelLabels: new[] { "sz0", "sz1", "sz2", "sz3" },
                channelTooltips: new[]
                {
                    $"Weight applied to layer {i} of biome {c} inside subzone channel 0.",
                    $"Weight applied to layer {i} of biome {c} inside subzone channel 1.",
                    $"Weight applied to layer {i} of biome {c} inside subzone channel 2.",
                    $"Weight applied to layer {i} of biome {c} inside subzone channel 3.",
                }
            ));
            section.Add(MaterialPropertyFields.Vector4Channels(
                material, $"_SmallSubzoneBrightness{c}{i}",
                "Brightness",
                "Per-subzone additive brightness applied to this layer.",
                channelLabels: new[] { "sz0", "sz1", "sz2", "sz3" },
                channelTooltips: new[]
                {
                    $"Additive brightness for layer {i} of biome {c} inside subzone channel 0.",
                    $"Additive brightness for layer {i} of biome {c} inside subzone channel 1.",
                    $"Additive brightness for layer {i} of biome {c} inside subzone channel 2.",
                    $"Additive brightness for layer {i} of biome {c} inside subzone channel 3.",
                }
            ));
            section.Add(MaterialPropertyFields.Color(
                material, $"_SmallSubzoneTint{c}{i}_R",
                "Tint sz0",
                "Tint applied to this layer inside subzone channel 0 (R). The tints across the " +
                "four subzone channels are weighted-blended by _SubzoneMaskTex per pixel. " +
                "Alpha doubles as a strength scalar for the Subzone3 height map's contribution " +
                "to this layer's slope gradient."
            ));
            section.Add(MaterialPropertyFields.Color(
                material, $"_SmallSubzoneTint{c}{i}_G",
                "Tint sz1",
                "Tint applied to this layer inside subzone channel 1 (G). Alpha doubles as a " +
                "strength scalar for the Subzone4 height map's contribution to this layer's " +
                "slope gradient."
            ));
            section.Add(MaterialPropertyFields.Color(
                material, $"_SmallSubzoneTint{c}{i}_B",
                "Tint sz2",
                "Tint applied to this layer inside subzone channel 2 (B)."
            ));
            section.Add(MaterialPropertyFields.Color(
                material, $"_SmallSubzoneTint{c}{i}_A",
                "Tint sz3",
                "Tint applied to this layer inside subzone channel 3 (A)."
            ));
            return section;
        }

        private static Label SectionLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("pqs-inspector-section-label");
            return label;
        }

        private static Label GroupLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("pqs-inspector-group-label");
            return label;
        }

        private static float GetPlanetMaxAltitude(PQSData pqsData)
        {
            if (pqsData == null || pqsData.heightMapInfo == null)
                return 0f;
            return pqsData.heightMapInfo.heightMapScale;
        }
    }
}
