using System;
using System.IO;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Builds the per-(biome, layer) inline detail editor that sits below the <see cref="SmallLayerMatrix" />.
    /// </summary>
    /// <remarks>
    /// One instance per matrix selection. The parent clears its detail slot and inserts a new
    /// editor when the selection changes. Fields are grouped into named sub-sections via
    /// <c>pqs-inspector-group-label</c> USS labels. Per-body fields (Master Mix, Height/Slope
    /// windows, Distance resample, Subzone overrides) write directly to the surface Material as
    /// before. SO-overridable fields (textures, UV, color grading, PBR strengths, emission) are
    /// authored through override rows on the slot and pushed to the Material via
    /// <see cref="SmallLayerMaterialCompiler.Compile" /> after each edit.
    /// </remarks>
    public static class SmallBiomeDetailEditor
    {
        /// <summary>
        /// Builds the detail editor visual element for the given biome row and layer column.
        /// </summary>
        /// <param name="material">The surface material that owns per-layer shader properties.</param>
        /// <param name="pqsDataSO">The serialized <see cref="PQSData" />, for legacy fields still wired through it.</param>
        /// <param name="pqsDataAuthoringSO">The serialized <see cref="PQSDataAuthoring" /> sidecar that hosts the small-layer slots.</param>
        /// <param name="pqsData">The backing <see cref="PQSData" /> instance, used to derive planet altitude limits and as the texture-pack target.</param>
        /// <param name="biome">The biome row index (0-3), or a negative value for no selection.</param>
        /// <param name="layer">The layer column index (0-3), or a negative value for no selection.</param>
        /// <param name="repackTiles">Callback invoked when a tile texture override changes so the parent can rerun the packer.</param>
        /// <returns>The constructed detail editor element.</returns>
        public static VisualElement Build(
            Material material,
            SerializedObject pqsDataSO,
            SerializedObject pqsDataAuthoringSO,
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

            var c = PlanetAuthoringNaming.BiomeChannels[biome];
            var i = layer + 1;
            var slot = biome * 4 + layer;
            var authoring = pqsDataAuthoringSO?.targetObject as PQSDataAuthoring;

            void OnSlotChanged()
            {
                if (authoring != null)
                    SmallLayerMaterialCompiler.RequestCompile(authoring, material);
            }

            void OnTextureSlotChanged()
            {
                OnSlotChanged();
                repackTiles?.Invoke();
            }

            root.Add(SectionLabel($"Layer {i} of Biome {c}"));
            root.Add(BuildMaterialSlot(material, pqsDataAuthoringSO, authoring, pqsData, slot, c, i, repackTiles));
            root.Add(BuildTileAssets(material, pqsDataAuthoringSO, authoring, slot, c, layer, OnTextureSlotChanged));
            root.Add(BuildMasterMix(material, c, layer));
            root.Add(BuildHeightWindow(material, pqsData, c, layer, i));
            root.Add(BuildSlopeWindow(material, c, layer, i));
            root.Add(BuildPeakCavity(material, c, layer, i));
            root.Add(BuildDistanceResample(material, c, layer));
            root.Add(BuildUVs(pqsDataAuthoringSO, authoring, slot, OnSlotChanged));
            root.Add(BuildColorGrading(pqsDataAuthoringSO, authoring, slot, OnSlotChanged));
            root.Add(BuildPBR(pqsDataAuthoringSO, authoring, slot, OnSlotChanged));
            root.Add(BuildEmission(pqsDataAuthoringSO, authoring, slot, OnSlotChanged));
            if (material.IsKeywordEnabled("SUB_ZONES_ENABLED"))
                root.Add(BuildSubzoneOverrides(material, c, i));

            if (pqsDataAuthoringSO != null)
                root.Bind(pqsDataAuthoringSO);

            return root;
        }

        private static VisualElement BuildMaterialSlot(
            Material material,
            SerializedObject pqsDataAuthoringSO,
            PQSDataAuthoring authoring,
            PQSData pqsData,
            int slot,
            string biomeChannel,
            int layerOneBased,
            Action repackTiles)
        {
            var section = new VisualElement();
            section.Add(GroupLabel("Material"));

            var materialProp = pqsDataAuthoringSO?.FindProperty(PlanetAuthoringNaming.SmallLayerSlotFieldPath(slot, "Material"));
            var materialField = new ObjectField("Shared material")
            {
                objectType = typeof(SmallLayerMaterial),
                allowSceneObjects = false,
                tooltip = "Optional SmallLayerMaterial asset carrying shared textures, UV, color, PBR, and emission defaults. Override toggles below let you replace any of the SO's values on this body.",
            };
            materialField.AddToClassList("unity-base-field__aligned");
            if (materialProp != null)
            {
                materialField.BindProperty(materialProp);
                TrackChangedOnly(materialField, materialProp, () =>
                {
                    if (authoring != null)
                        SmallLayerMaterialCompiler.RequestCompile(authoring, material);
                    repackTiles?.Invoke();
                });
                ResetSlotOnMaterialCleared(materialField, pqsDataAuthoringSO, slot);
            }
            section.Add(materialField);

            var newButton = new Button(() => CreateNewMaterialFor(pqsDataAuthoringSO, authoring, pqsData, material, slot, biomeChannel, layerOneBased, repackTiles))
            {
                text = "New...",
                tooltip = "Create a new SmallLayerMaterial seeded with this cell's current effective values, assign it to this slot, and clear all override flags. Saved in <body folder>/Materials/.",
            };
            section.Add(newButton);

            var hint = new Label("Drop a SmallLayerMaterial asset above to share defaults across bodies. Toggle the boxes on each row below to override individual fields for this body.");
            hint.AddToClassList("pqs-inspector-empty-selection");
            section.Add(hint);

            return section;
        }

        private static void CreateNewMaterialFor(
            SerializedObject pqsDataAuthoringSO,
            PQSDataAuthoring authoring,
            PQSData pqsData,
            Material material,
            int slot,
            string biomeChannel,
            int layerOneBased,
            Action repackTiles)
        {
            if (authoring == null || pqsData == null)
            {
                EditorUtility.DisplayDialog("New Small Layer Material", "Cannot resolve the authoring sidecar or body asset.", "OK");
                return;
            }
            if (authoring.smallLayerSlots == null || slot < 0 || slot >= authoring.smallLayerSlots.Length)
                return;

            var bodyAssetPath = AssetDatabase.GetAssetPath(pqsData);
            if (string.IsNullOrEmpty(bodyAssetPath))
            {
                EditorUtility.DisplayDialog("New Small Layer Material", "The PQSData asset is not on disk yet.", "OK");
                return;
            }
            var bodyFolder = Path.GetDirectoryName(bodyAssetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(bodyFolder)) return;

            var materialsFolder = bodyFolder + "/Materials";
            if (!AssetDatabase.IsValidFolder(materialsFolder))
                AssetDatabase.CreateFolder(bodyFolder, "Materials");

            var defaultName = $"SmallLayer_{biomeChannel}{layerOneBased}";
            var path = EditorUtility.SaveFilePanelInProject(
                "New Small Layer Material",
                defaultName,
                "asset",
                "Choose a name and location for the new Small Layer Material.",
                materialsFolder
            );
            if (string.IsNullOrEmpty(path)) return;

            var slotObj = authoring.smallLayerSlots[slot];
            var smallMat = ScriptableObject.CreateInstance<SmallLayerMaterial>();
            smallMat.AlbedoTexture = slotObj.EffectiveAlbedoTexture;
            smallMat.NormalTexture = slotObj.EffectiveNormalTexture;
            smallMat.MetallicTexture = slotObj.EffectiveMetallicTexture;
            smallMat.UVScale = slotObj.EffectiveUVScale;
            smallMat.UVOffset = slotObj.EffectiveUVOffset;
            smallMat.Tint = slotObj.EffectiveTint;
            smallMat.Brightness = slotObj.EffectiveBrightness;
            smallMat.Contrast = slotObj.EffectiveContrast;
            smallMat.Saturation = slotObj.EffectiveSaturation;
            smallMat.NormalStrength = slotObj.EffectiveNormalStrength;
            smallMat.GlossStrength = slotObj.EffectiveGlossStrength;
            smallMat.MetallicStrength = slotObj.EffectiveMetallicStrength;
            smallMat.AOStrength = slotObj.EffectiveAOStrength;
            smallMat.EmissionStrength = slotObj.EffectiveEmissionStrength;
            smallMat.EmissionColor = slotObj.EffectiveEmissionColor;

            AssetDatabase.CreateAsset(smallMat, path);
            AssetDatabase.SaveAssetIfDirty(smallMat);

            Undo.RecordObject(authoring, "Assign new Small Layer Material");
            slotObj.Material = smallMat;
            slotObj.OverrideAlbedoTexture = false;
            slotObj.OverrideNormalTexture = false;
            slotObj.OverrideMetallicTexture = false;
            slotObj.OverrideUVScale = false;
            slotObj.OverrideUVOffset = false;
            slotObj.OverrideTint = false;
            slotObj.OverrideBrightness = false;
            slotObj.OverrideContrast = false;
            slotObj.OverrideSaturation = false;
            slotObj.OverrideNormalStrength = false;
            slotObj.OverrideGlossStrength = false;
            slotObj.OverrideMetallicStrength = false;
            slotObj.OverrideAOStrength = false;
            slotObj.OverrideEmissionStrength = false;
            slotObj.OverrideEmissionColor = false;

            EditorUtility.SetDirty(authoring);
            pqsDataAuthoringSO?.Update();

            SmallLayerMaterialCompiler.Compile(authoring, material);
            repackTiles?.Invoke();
        }

        private static VisualElement BuildTileAssets(
            Material material,
            SerializedObject pqsDataAuthoringSO,
            PQSDataAuthoring authoring,
            int slot,
            string c,
            int layer,
            Action onChanged)
        {
            var section = new VisualElement();
            section.Add(GroupLabel("Tile assets"));

            section.Add(BuildOverrideRow(
                pqsDataAuthoringSO, authoring, slot,
                "OverrideAlbedoTexture", "AlbedoTexture",
                "Albedo",
                "Albedo (color) tile for this layer. Override the SO's default by ticking the box and dragging a Texture2D in.",
                seedFromSO: s => SerializedAssign(pqsDataAuthoringSO, PlanetAuthoringNaming.SmallLayerSlotFieldPath(slot, "AlbedoTexture"), s?.Material?.AlbedoTexture),
                onChanged: onChanged
            ));
            section.Add(BuildOverrideRow(
                pqsDataAuthoringSO, authoring, slot,
                "OverrideNormalTexture", "NormalTexture",
                "Normal+SAO",
                "Normal+packed tile. RGBA encodes (metallic-influence, normalY, AO, normalX) DXT5nm-style.",
                seedFromSO: s => SerializedAssign(pqsDataAuthoringSO, PlanetAuthoringNaming.SmallLayerSlotFieldPath(slot, "NormalTexture"), s?.Material?.NormalTexture),
                onChanged: onChanged
            ));
            section.Add(BuildOverrideRow(
                pqsDataAuthoringSO, authoring, slot,
                "OverrideMetallicTexture", "MetallicTexture",
                "Metallic",
                "Metallic mask tile. Sampled R channel is the per-tile metallic value.",
                seedFromSO: s => SerializedAssign(pqsDataAuthoringSO, PlanetAuthoringNaming.SmallLayerSlotFieldPath(slot, "MetallicTexture"), s?.Material?.MetallicTexture),
                onChanged: onChanged
            ));
            return section;
        }

        private static VisualElement BuildMasterMix(Material material, string c, int layer)
        {
            var section = new VisualElement();
            section.Add(GroupLabel("Master mix"));
            section.Add(MaterialPropertyFields.Vector4ChannelToggle(
                material, $"_SmallEnable{c}", layer,
                "Enabled",
                "Per-layer master toggle. Mutes this layer without changing its slice assignment. Useful for A/B comparing layers."
            ));
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

        private static VisualElement BuildUVs(SerializedObject so, PQSDataAuthoring authoring, int slot, Action onChanged)
        {
            var section = new VisualElement();
            section.Add(GroupLabel("UVs"));
            section.Add(BuildOverrideRow(
                so, authoring, slot,
                "OverrideUVScale", "UVScale",
                "Scale",
                "Per-layer UV scale. Larger value prints the tile smaller on the surface.",
                seedFromSO: s => SerializedAssignFloat(so, PlanetAuthoringNaming.SmallLayerSlotFieldPath(slot, "UVScale"), s?.Material != null ? s.Material.UVScale : 1f),
                onChanged: onChanged
            ));
            section.Add(BuildOverrideRow(
                so, authoring, slot,
                "OverrideUVOffset", "UVOffset",
                "Offset",
                "Per-layer UV offset. Use to break alignment between layers sharing the same tile.",
                seedFromSO: s => SerializedAssignFloat(so, PlanetAuthoringNaming.SmallLayerSlotFieldPath(slot, "UVOffset"), s?.Material != null ? s.Material.UVOffset : 0f),
                onChanged: onChanged
            ));
            return section;
        }

        private static VisualElement BuildColorGrading(SerializedObject so, PQSDataAuthoring authoring, int slot, Action onChanged)
        {
            var section = new VisualElement();
            section.Add(GroupLabel("Color grading"));
            section.Add(BuildOverrideRow(
                so, authoring, slot,
                "OverrideTint", "Tint",
                "Tint",
                "Per-layer tint color (RGBA). RGB multiplies the layer's albedo. Alpha multiplies the height-blend alpha.",
                seedFromSO: s => SerializedAssignColor(so, PlanetAuthoringNaming.SmallLayerSlotFieldPath(slot, "Tint"), s?.Material != null ? s.Material.Tint : Color.white),
                onChanged: onChanged
            ));
            section.Add(BuildOverrideRow(
                so, authoring, slot,
                "OverrideBrightness", "Brightness",
                "Brightness",
                "Additive brightness applied before contrast and saturation. Positive pushes brighter, negative darker.",
                seedFromSO: s => SerializedAssignFloat(so, PlanetAuthoringNaming.SmallLayerSlotFieldPath(slot, "Brightness"), s?.Material != null ? s.Material.Brightness : 0f),
                onChanged: onChanged
            ));
            section.Add(BuildOverrideRow(
                so, authoring, slot,
                "OverrideContrast", "Contrast",
                "Contrast",
                "Contrast multiplier around mid-gray. 1 = neutral, >1 = punchier, <1 = washed out.",
                seedFromSO: s => SerializedAssignFloat(so, PlanetAuthoringNaming.SmallLayerSlotFieldPath(slot, "Contrast"), s?.Material != null ? s.Material.Contrast : 1f),
                onChanged: onChanged
            ));
            section.Add(BuildOverrideRow(
                so, authoring, slot,
                "OverrideSaturation", "Saturation",
                "Saturation",
                "Saturation multiplier. 0 = grayscale, 1 = neutral, >1 = oversaturated.",
                seedFromSO: s => SerializedAssignFloat(so, PlanetAuthoringNaming.SmallLayerSlotFieldPath(slot, "Saturation"), s?.Material != null ? s.Material.Saturation : 1f),
                onChanged: onChanged
            ));
            return section;
        }

        private static VisualElement BuildPBR(SerializedObject so, PQSDataAuthoring authoring, int slot, Action onChanged)
        {
            var section = new VisualElement();
            section.Add(GroupLabel("PBR"));
            section.Add(BuildOverrideRow(
                so, authoring, slot,
                "OverrideNormalStrength", "NormalStrength",
                "Normal",
                "Multiplier on the layer's normal map. 0 = flat, 1 = neutral, >1 = exaggerated.",
                seedFromSO: s => SerializedAssignFloat(so, PlanetAuthoringNaming.SmallLayerSlotFieldPath(slot, "NormalStrength"), s?.Material != null ? s.Material.NormalStrength : 1f),
                onChanged: onChanged
            ));
            section.Add(BuildOverrideRow(
                so, authoring, slot,
                "OverrideGlossStrength", "GlossStrength",
                "Gloss",
                "Smoothness multiplier. Set ≥ 15 to switch the shader to override mode.",
                seedFromSO: s => SerializedAssignFloat(so, PlanetAuthoringNaming.SmallLayerSlotFieldPath(slot, "GlossStrength"), s?.Material != null ? s.Material.GlossStrength : 1f),
                onChanged: onChanged
            ));
            section.Add(BuildOverrideRow(
                so, authoring, slot,
                "OverrideMetallicStrength", "MetallicStrength",
                "Metallic",
                "Metallic multiplier. Same ≥ 15 override convention as gloss.",
                seedFromSO: s => SerializedAssignFloat(so, PlanetAuthoringNaming.SmallLayerSlotFieldPath(slot, "MetallicStrength"), s?.Material != null ? s.Material.MetallicStrength : 1f),
                onChanged: onChanged
            ));
            section.Add(BuildOverrideRow(
                so, authoring, slot,
                "OverrideAOStrength", "AOStrength",
                "AO",
                "Ambient-occlusion power. Higher values give a more aggressive AO contribution from this layer.",
                seedFromSO: s => SerializedAssignFloat(so, PlanetAuthoringNaming.SmallLayerSlotFieldPath(slot, "AOStrength"), s?.Material != null ? s.Material.AOStrength : 1f),
                onChanged: onChanged
            ));
            return section;
        }

        private static VisualElement BuildEmission(SerializedObject so, PQSDataAuthoring authoring, int slot, Action onChanged)
        {
            var section = new VisualElement();
            section.Add(GroupLabel("Emission"));
            section.Add(BuildOverrideRow(
                so, authoring, slot,
                "OverrideEmissionStrength", "EmissionStrength",
                "Strength",
                "Self-illumination multiplier. 0 disables emission.",
                seedFromSO: s => SerializedAssignFloat(so, PlanetAuthoringNaming.SmallLayerSlotFieldPath(slot, "EmissionStrength"), s?.Material != null ? s.Material.EmissionStrength : 0f),
                onChanged: onChanged
            ));
            section.Add(BuildOverrideRow(
                so, authoring, slot,
                "OverrideEmissionColor", "EmissionColor",
                "Color",
                "Per-layer emission color (HDR).",
                seedFromSO: s => SerializedAssignColor(so, PlanetAuthoringNaming.SmallLayerSlotFieldPath(slot, "EmissionColor"), s?.Material != null ? s.Material.EmissionColor : Color.black),
                onChanged: onChanged
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

        // Override row: bool override toggle + PropertyField for the value, gated by the toggle.
        // Mirrors PQSDecalInstanceEditor.BuildOverrideRow. When the artist toggles override on, the
        // optional seedFromSO action copies the assigned SO's current value into the slot's local
        // override property so the artist starts editing from the SO's value, not a stale local.
        private static VisualElement BuildOverrideRow(
            SerializedObject so,
            PQSDataAuthoring authoring,
            int slot,
            string overrideFieldName,
            string valueFieldName,
            string label,
            string tooltip,
            Action<SmallLayerSlot> seedFromSO,
            Action onChanged)
        {
            var row = new VisualElement();
            row.AddToClassList("decal-inspector-override-row");

            var overrideProp = so?.FindProperty(PlanetAuthoringNaming.SmallLayerSlotFieldPath(slot, overrideFieldName));
            var valueProp = so?.FindProperty(PlanetAuthoringNaming.SmallLayerSlotFieldPath(slot, valueFieldName));

            var toggle = new Toggle { tooltip = "Override the shared material's default for this field on this body." };
            toggle.AddToClassList("decal-inspector-override-toggle");
            if (overrideProp != null) toggle.BindProperty(overrideProp);
            row.Add(toggle);

            var field = new PropertyField(valueProp, label) { tooltip = tooltip };
            field.AddToClassList("decal-inspector-override-field");
            field.AddToClassList("unity-base-field__aligned");
            field.SetEnabled(overrideProp?.boolValue ?? false);

            // Hash-guard so post-Bind spurious sync doesn't trigger onChanged (Repack).
            var lastOverrideHash = overrideProp?.contentHash ?? 0L;
            toggle.RegisterValueChangedCallback(evt =>
            {
                field.SetEnabled(evt.newValue);
                var currentHash = overrideProp?.contentHash ?? 0L;
                if (currentHash == lastOverrideHash) return;
                lastOverrideHash = currentHash;
                if (evt.newValue && !evt.previousValue && seedFromSO != null && authoring != null && slot >= 0 && slot < authoring.smallLayerSlots.Length)
                    seedFromSO(authoring.smallLayerSlots[slot]);
                onChanged?.Invoke();
            });

            if (valueProp != null)
                TrackChangedOnly(field, valueProp, onChanged);

            row.Add(field);
            return row;
        }

        // contentHash-guarded so post-Bind spurious fires don't trigger heavy callbacks (Repack).
        internal static void TrackChangedOnly(VisualElement element, SerializedProperty prop, Action onChanged)
        {
            if (element == null || prop == null) return;
            var lastHash = prop.contentHash;
            element.TrackPropertyValue(prop, p =>
            {
                if (p.contentHash == lastHash) return;
                lastHash = p.contentHash;
                onChanged?.Invoke();
            });
        }

        /// <summary>
        /// Wires the given small-layer Material ObjectField so that clearing it (non-null to null) wipes the slot's per-field overrides back to defaults, keeping the rest of the slot from carrying stale data once the shared SO is gone.
        /// </summary>
        internal static void ResetSlotOnMaterialCleared(ObjectField materialField, SerializedObject authoringSO, int slot)
        {
            if (materialField == null || authoringSO == null) return;
            materialField.RegisterValueChangedCallback(evt =>
            {
                if (evt.previousValue == null || evt.newValue != null) return;
                if (authoringSO.targetObject is not PQSDataAuthoring authoring) return;
                if (authoring.smallLayerSlots == null) return;
                if (slot < 0 || slot >= authoring.smallLayerSlots.Length) return;
                var slotObj = authoring.smallLayerSlots[slot];
                if (slotObj == null) return;
                Undo.RecordObject(authoring, "Reset Small Layer Slot");
                slotObj.ResetLocals();
                EditorUtility.SetDirty(authoring);
                authoringSO.Update();
            });
        }

        private static void SerializedAssign(SerializedObject so, string propertyPath, UnityEngine.Object value)
        {
            var prop = so?.FindProperty(propertyPath);
            if (prop == null) return;
            prop.objectReferenceValue = value;
            prop.serializedObject.ApplyModifiedProperties();
        }

        private static void SerializedAssignFloat(SerializedObject so, string propertyPath, float value)
        {
            var prop = so?.FindProperty(propertyPath);
            if (prop == null) return;
            prop.floatValue = value;
            prop.serializedObject.ApplyModifiedProperties();
        }

        private static void SerializedAssignColor(SerializedObject so, string propertyPath, Color value)
        {
            var prop = so?.FindProperty(propertyPath);
            if (prop == null) return;
            prop.colorValue = value;
            prop.serializedObject.ApplyModifiedProperties();
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
