using System.Collections.Generic;
using KSP.VolumeCloud;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Clouds;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Builds the authoring UI for a <see cref="VolumeCloudConfiguration" />.
    /// </summary>
    /// <remarks>
    /// Layout lives in <c>Assets/Windows/PlanetAuthoring/Inspectors/VolumeCloudConfigurationInspector.uxml</c>. Nothing
    /// the game ships raises the configuration's change events, so every edit raises them here, which is what makes a
    /// running cloud preview pick up layer changes. Each edit also re-derives the layers and re-syncs the scaled clouds.
    /// </remarks>
    public static class VolumeCloudConfigurationInspector
    {
        private const string UXML_PATH = "/Assets/Windows/PlanetAuthoring/Inspectors/VolumeCloudConfigurationInspector.uxml";

        private static readonly (string Path, string Label, string Tooltip)[] LAYER_FIELDS =
        {
            ("isEnable", "Enabled", "Draw this layer."),
            ("layerName", "Name", "The layer's name, which its scaled layer shares."),
            ("cloudHeightRange", "Height Range (m)", "Bottom and top of the layer, in meters above the planet radius. The scaled layer sits at the bottom."),
            ("castShadow", "Cast Shadow", "Let this layer cast shadows when the configuration's shadows are on."),
            ("distributionMap", "Distribution Map", "Cubemap of where clouds form over the body, its coverage mask. Imported, not painted."),
            ("cloudColorMap", "Color Map", "Optional cubemap tinting the layer, used when Use Color Maps is on."),
            ("coverageScale", "Coverage", "How much of the distribution map turns into cloud."),
            ("cloudsDensity", "Density", "How thick the clouds are."),
            ("cloudsMaskBias", "Mask Bias", "Shifts the distribution map up or down before coverage is applied."),
            ("evanish", "Vanish", "How strongly clouds thin out at their edges."),
            ("upperFalloff", "Upper Falloff", "How sharply density falls toward the layer's top."),
            ("lowerFalloff", "Lower Falloff", "How sharply density falls toward the layer's bottom."),
            ("topOffset", "Top Offset", "Shears the layer's tops sideways along the wind."),
            ("baseTexture", "Base Noise", "3D noise that shapes the clouds. Use Stock Noise links stock's."),
            ("baseTexureTile", "Base Noise Tiling", "How many times the base noise repeats."),
            ("enableDetailTexture", "Use Detail Noise", "Erode the cloud edges with the detail noise."),
            ("detailTexture", "Detail Noise", "3D noise that erodes the cloud edges. Use Stock Noise links stock's."),
            ("detailTextureTile", "Detail Noise Tiling", "How many times the detail noise repeats."),
            ("detailStrength", "Detail Strength", "How hard the detail noise erodes."),
            ("detailAmount", "Detail Amount", "How much of each cloud the detail noise reaches."),
            ("detailAltitudeShift", "Detail Altitude Shift", "Moves the detail noise's effect up or down through the layer."),
            ("normalScale", "Normal Scale", "Strength of the scaled clouds' normal map."),
            ("scaleCloudColor", "Scaled Color", "Tint of this layer's scaled clouds."),
            ("cloudsLayerRotate", "Rotation", "This layer's rotation, in degrees."),
            ("enableWind", "Wind", "Move and evolve the layer over time."),
            ("windDirection", "Wind Direction", "The axis the layer drifts around."),
            ("movementSpeed", "Movement Speed", "How fast the layer drifts."),
            ("evolveSpeed", "Evolve Speed", "How fast the clouds change shape."),
            ("bakedScaledTexture", "Baked Scaled Clouds", "The scaled clouds' color cubemap. Written by Bake Scaled Clouds."),
            ("cloudNormalMap", "Baked Scaled Normals", "The scaled clouds' normal cubemap. Written by Bake Scaled Clouds."),
            ("bakedBottomScaledTexture", "Baked Scaled Underside", "The scaled clouds' underside cubemap. Written by Bake Scaled Clouds."),
        };

        // Written by the scaled cloud bake, so not edited by hand.
        private static readonly HashSet<string> BAKED_FIELDS = new() { "bakedScaledTexture", "cloudNormalMap", "bakedBottomScaledTexture" };

        /// <summary>
        /// Builds the configuration's authoring UI bound to <paramref name="serializedConfiguration" />.
        /// </summary>
        /// <param name="configuration">The configuration being edited.</param>
        /// <param name="serializedConfiguration">A serialized view of <paramref name="configuration" /> to bind the fields to.</param>
        /// <returns>The inspector root.</returns>
        public static VisualElement Build(VolumeCloudConfiguration configuration, SerializedObject serializedConfiguration)
        {
            var root = new VisualElement();
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UXML_PATH);
            if (tree == null)
            {
                root.Add(new Label("Failed to load VolumeCloudConfigurationInspector.uxml"));
                return root;
            }

            tree.CloneTree(root);
            Ksp2UnityToolsStyles.Apply(root);
            root.Q("clouds-planet-radius")?.SetEnabled(false);

            var status = root.Q<Label>("clouds-status");
            SetStatus(status, string.Empty);
            var layers = root.Q<ListView>("clouds-layer-list");
            var detail = root.Q("clouds-layer-detail");
            WireLayerList(root, layers, detail, configuration, serializedConfiguration);
            WirePresets(root, status, layers, configuration, serializedConfiguration);
            WireStockNoise(root, status, configuration, serializedConfiguration);

            root.Bind(serializedConfiguration);
            root.TrackSerializedObjectValue(serializedConfiguration, _ => NotifyChanged(configuration, serializedConfiguration, layers));
            return root;
        }

        // Re-derives the layers, raises the change events a running preview listens to, and keeps the scaled clouds in
        // step. Undo restores the serialized values, which lands here as well.
        private static void NotifyChanged(VolumeCloudConfiguration configuration, SerializedObject serializedConfiguration, ListView layers)
        {
            CloudSetup.DeriveLayers(configuration);
            serializedConfiguration.Update();
            CloudSetup.SyncScaled(configuration);
            configuration.OnCloudLayerChanged?.Invoke(configuration);
            configuration.OnConfigChanged?.Invoke(configuration);
            layers?.RefreshItems();
            SceneView.RepaintAll();
        }

        private static void WireLayerList(
            VisualElement root,
            ListView layers,
            VisualElement detail,
            VolumeCloudConfiguration configuration,
            SerializedObject serializedConfiguration
        )
        {
            if (layers == null || detail == null)
                return;

            configuration.cumulusList ??= new List<VolumeCloudConfiguration.CumulusData>();
            layers.itemsSource = configuration.cumulusList;
            layers.makeItem = () => new Label();
            layers.bindItem = (element, index) => ((Label)element).text = DescribeLayer(configuration.cumulusList[index]);
            layers.selectionChanged += _ => ShowLayer(detail, configuration, serializedConfiguration, layers.selectedIndex);
            if (configuration.cumulusList.Count > 0)
            {
                layers.SetSelection(0);
            }

            ShowLayer(detail, configuration, serializedConfiguration, layers.selectedIndex);

            root.Q<Button>("clouds-add-layer")?.RegisterCallback<ClickEvent>(_ =>
            {
                Undo.RecordObject(configuration, "Add Cloud Layer");
                int selected = layers.selectedIndex;
                VolumeCloudConfiguration.CumulusData layer = selected >= 0 && selected < configuration.cumulusList.Count
                    ? CopyLayer(configuration.cumulusList[selected])
                    : CloudSetup.CreateDefaultLayer();
                layer.layerName = $"{layer.layerName} {configuration.cumulusList.Count + 1}";
                configuration.cumulusList.Add(layer);
                ApplyStructuralChange(configuration, serializedConfiguration, layers);
                layers.SetSelection(configuration.cumulusList.Count - 1);
            });

            root.Q<Button>("clouds-bake-layer")?.RegisterCallback<ClickEvent>(_ =>
            {
                ScaledCloudBaker.TryBake(configuration, layers.selectedIndex, out string message);
                serializedConfiguration.Update();
                ShowLayer(detail, configuration, serializedConfiguration, layers.selectedIndex);
                SetStatus(root.Q<Label>("clouds-status"), message);
            });

            root.Q<Button>("clouds-remove-layer")?.RegisterCallback<ClickEvent>(_ =>
            {
                int selected = layers.selectedIndex;
                if (selected < 0 || selected >= configuration.cumulusList.Count)
                    return;

                Undo.RecordObject(configuration, "Remove Cloud Layer");
                configuration.cumulusList.RemoveAt(selected);
                ApplyStructuralChange(configuration, serializedConfiguration, layers);
                layers.SetSelection(Mathf.Min(selected, configuration.cumulusList.Count - 1));
            });
        }

        private static void ApplyStructuralChange(VolumeCloudConfiguration configuration, SerializedObject serializedConfiguration, ListView layers)
        {
            EditorUtility.SetDirty(configuration);
            serializedConfiguration.Update();
            layers.Rebuild();
            NotifyChanged(configuration, serializedConfiguration, layers);
        }

        private static void ShowLayer(VisualElement detail, VolumeCloudConfiguration configuration, SerializedObject serializedConfiguration, int index)
        {
            detail.Unbind();
            detail.Clear();
            SerializedProperty layers = serializedConfiguration.FindProperty("cumulusList");
            if (layers == null || index < 0 || index >= layers.arraySize)
            {
                detail.Add(new Label("Select a layer to edit it.") { tooltip = string.Empty });
                return;
            }

            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            foreach ((string path, string label, string tooltip) in LAYER_FIELDS)
            {
                SerializedProperty property = layer.FindPropertyRelative(path);
                if (property == null)
                    continue;

                var field = new PropertyField(property, label) { tooltip = tooltip };
                field.BindProperty(property);
                field.SetEnabled(!BAKED_FIELDS.Contains(path));
                detail.Add(field);
                if (path == "distributionMap")
                {
                    detail.Add(BuildDistributionGenerator(configuration, serializedConfiguration, index));
                }
            }
        }

        private static readonly (string Path, string Label)[] DISTRIBUTION_FIELDS =
        {
            ("Seed", "Seed"),
            ("FeatureSizeKm", "Feature Size (km)"),
            ("Octaves", "Octaves"),
            ("Roughness", "Roughness"),
            ("WarpStrength", "Warp"),
            ("Coverage", "Coverage"),
            ("Softness", "Edge Softness"),
            ("LatitudeProfile", "Latitude Profile"),
            ("Resolution", "Face Size"),
            ("Compress", "Compress (BC4)"),
        };

        // The settings live in the configuration's sidecar, so a map can be tweaked and regenerated later.
        private static VisualElement BuildDistributionGenerator(
            VolumeCloudConfiguration configuration,
            SerializedObject serializedConfiguration,
            int index
        )
        {
            var foldout = new Foldout { text = "Generate Distribution", value = false };
            foldout.tooltip = "Generate this layer's distribution map from warped noise evaluated on the sphere, so it is seamless across faces and at the poles. Latitude Profile scales coverage from the equator, at 0, to the poles, at 1.";
            VolumeCloudConfigurationAuthoring sidecar = AuthoringSidecars.GetOrCreate(configuration);
            if (sidecar == null)
            {
                foldout.Add(new HelpBox("Save the configuration as an asset first.", HelpBoxMessageType.Info));
                return foldout;
            }

            string layerName = configuration.cumulusList[index].layerName;
            int knownLayers = sidecar.Distributions.Count;
            CloudDistributionSettings settings = sidecar.GetDistribution(layerName);
            int settingsIndex = sidecar.Distributions.IndexOf(settings);
            if (sidecar.Distributions.Count != knownLayers)
            {
                EditorUtility.SetDirty(sidecar);
            }
            var serializedSidecar = new SerializedObject(sidecar);
            SerializedProperty settingsProperty = serializedSidecar.FindProperty($"Distributions.Array.data[{settingsIndex}]");
            foreach ((string path, string label) in DISTRIBUTION_FIELDS)
            {
                SerializedProperty property = settingsProperty.FindPropertyRelative(path);
                if (property == null)
                    continue;

                var field = new PropertyField(property, label);
                field.BindProperty(property);
                foldout.Add(field);
            }

            var status = new Label();
            status.AddToClassList("sdk-hint");
            var generate = new Button { text = "Generate" };
            generate.AddToClassList("sdk-action-button");
            generate.clicked += () =>
            {
                serializedSidecar.ApplyModifiedProperties();
                CloudDistributionBaker.TryBake(configuration, index, out string message);
                serializedConfiguration.Update();
                SetStatus(status, message);
                SceneView.RepaintAll();
            };
            foldout.Add(generate);
            foldout.Add(status);
            SetStatus(status, string.Empty);
            return foldout;
        }

        private static void WirePresets(
            VisualElement root,
            Label status,
            ListView layers,
            VolumeCloudConfiguration configuration,
            SerializedObject serializedConfiguration
        )
        {
            var dropdown = root.Q<DropdownField>("clouds-preset");
            var apply = root.Q<Button>("clouds-apply-preset");
            if (dropdown == null || apply == null)
                return;

            dropdown.choices = new List<string>(CloudPresets.STOCK_BODIES);
            dropdown.index = 0;
            apply.clicked += () =>
            {
                bool applied = CloudPresets.TryApply(configuration, dropdown.value, out string message);
                if (applied && StockCloudNoise.TryLink(out Texture3D baseNoise, out Texture3D detailNoise, out _))
                {
                    StockCloudNoise.AssignWhereMissing(configuration, baseNoise, detailNoise);
                }

                SetStatus(status, message);
                if (applied)
                {
                    ApplyStructuralChange(configuration, serializedConfiguration, layers);
                }
            };
        }

        private static void WireStockNoise(VisualElement root, Label status, VolumeCloudConfiguration configuration, SerializedObject serializedConfiguration)
        {
            root.Q<Button>("clouds-link-noise")?.RegisterCallback<ClickEvent>(_ =>
            {
                if (!StockCloudNoise.TryLink(out Texture3D baseNoise, out Texture3D detailNoise, out string problem))
                {
                    SetStatus(status, $"Stock noise could not be linked: {problem}.");
                    return;
                }

                Undo.RecordObject(configuration, "Use Stock Cloud Noise");
                int assigned = StockCloudNoise.AssignWhereMissing(configuration, baseNoise, detailNoise);
                EditorUtility.SetDirty(configuration);
                serializedConfiguration.Update();
                SetStatus(status, assigned > 0 ? $"Gave {assigned} layer(s) stock noise." : "Every layer already has noise.");
            });

            root.Q<Button>("clouds-generate-noise")?.RegisterCallback<ClickEvent>(_ =>
            {
                CloudNoiseBaker.TryBake(configuration, out string message);
                serializedConfiguration.Update();
                SetStatus(status, message);
            });
        }

        private static string DescribeLayer(VolumeCloudConfiguration.CumulusData layer)
        {
            string name = string.IsNullOrEmpty(layer.layerName) ? "Unnamed" : layer.layerName;
            string state = layer.isEnable ? string.Empty : " (off)";
            return $"{name}   {layer.cloudHeightRange.x:0} to {layer.cloudHeightRange.y:0} m{state}";
        }

        private static VolumeCloudConfiguration.CumulusData CopyLayer(VolumeCloudConfiguration.CumulusData source) =>
            JsonUtility.FromJson<VolumeCloudConfiguration.CumulusData>(JsonUtility.ToJson(source));

        private static void SetStatus(Label status, string message)
        {
            if (status == null)
                return;

            status.text = message;
            status.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
