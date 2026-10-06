using System.Collections.Generic;
using System.Linq;
using KSP.VolumeCloud;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Clouds;
using Ksp2UnityTools.Editor.Widgets;
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
    /// Layout lives in <c>Assets/Windows/PlanetAuthoring/Inspectors/VolumeCloudConfigurationInspector.uxml</c>, and each
    /// layer card's body in <c>Shared/CloudLayerSection.uxml</c>. Nothing the game ships raises the configuration's change
    /// events, so every edit raises them here, which is what makes a running cloud preview pick up layer changes. Each
    /// edit also re-derives the layers and re-syncs the scaled clouds.
    /// </remarks>
    public static class VolumeCloudConfigurationInspector
    {
        private const string UXML_PATH = "/Assets/Windows/PlanetAuthoring/Inspectors/VolumeCloudConfigurationInspector.uxml";
        private const string LAYER_UXML_PATH = "/Assets/Windows/PlanetAuthoring/Inspectors/Shared/CloudLayerSection.uxml";

        // Written by the scaled cloud bake for one layer, so a new or duplicated layer starts without them.
        private static readonly string[] BAKED_FIELDS = { "bakedScaledTexture", "cloudNormalMap", "bakedBottomScaledTexture" };

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
            VisualElement layers = root.Q("clouds-layers-host");
            RebuildLayers(layers, configuration, serializedConfiguration);
            WirePresets(root, status, layers, configuration, serializedConfiguration);
            WireStockNoise(root, status, configuration, serializedConfiguration);

            root.Bind(serializedConfiguration);
            root.TrackSerializedObjectValue(serializedConfiguration, _ => NotifyChanged(configuration, serializedConfiguration, layers));
            return root;
        }

        // Re-derives the layers, raises the change events a running preview listens to, and keeps the scaled clouds in
        // step. Undo restores the serialized values, which lands here as well, so a layer count the cards no longer
        // match means an add or remove was undone and the cards are rebuilt.
        private static void NotifyChanged(VolumeCloudConfiguration configuration, SerializedObject serializedConfiguration, VisualElement layers)
        {
            CloudSetup.DeriveLayers(configuration);
            serializedConfiguration.Update();
            CloudSetup.SyncScaled(configuration);
            configuration.OnCloudLayerChanged?.Invoke(configuration);
            configuration.OnConfigChanged?.Invoke(configuration);
            VisualElement cards = layers?.Q(className: "sdk-card-list__items");
            if (cards != null && cards.childCount != (configuration.cumulusList?.Count ?? 0))
            {
                RebuildLayers(layers, configuration, serializedConfiguration);
            }

            SceneView.RepaintAll();
        }

        private static void RebuildLayers(VisualElement host, VolumeCloudConfiguration configuration, SerializedObject serializedConfiguration)
        {
            if (host == null)
                return;

            host.Clear();
            serializedConfiguration.Update();
            SerializedProperty layers = serializedConfiguration.FindProperty("cumulusList");
            if (layers == null)
                return;

            host.Add(CardListSection.Build(layers, new CardListSection.Config
            {
                Title = "Layers",
                AddButtonText = "+ Add Layer",
                IdentityFieldName = "layerName",
                ChipFormatter = FormatChip,
                BuildBody = (entry, body) => BuildLayerBody(entry, body, configuration, serializedConfiguration),
                ApplyDefaultsToNew = (entry, index) => ApplyLayerDefaults(entry, index, configuration),
                OnDuplicate = index => DuplicateLayer(host, configuration, serializedConfiguration, index),
            }));
        }

        private static void BuildLayerBody(
            SerializedProperty entry,
            VisualElement body,
            VolumeCloudConfiguration configuration,
            SerializedObject serializedConfiguration
        )
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + LAYER_UXML_PATH);
            if (tree == null)
            {
                body.Add(new Label("Failed to load CloudLayerSection.uxml"));
                return;
            }

            // A BindableElement wrapper, so the markup's binding paths resolve against the layer without naming its index.
            var bindable = new BindableElement();
            tree.CloneTree(bindable);
            bindable.BindProperty(entry);
            body.Add(bindable);

            int index = LayerIndex(entry);
            bindable.Q("cloud-layer-distribution-generator")?.Add(BuildDistributionGenerator(configuration, serializedConfiguration, index));

            var status = bindable.Q<Label>("cloud-layer-status");
            SetStatus(status, string.Empty);
            bindable.Q<Button>("cloud-layer-bake-scaled")?.RegisterCallback<ClickEvent>(_ =>
            {
                ScaledCloudBaker.TryBake(configuration, LayerIndex(entry), out string message);
                serializedConfiguration.Update();
                SetStatus(status, message);
            });
        }

        // A new element is a copy of the last one, so it keeps that layer's look but needs its own name and loses the
        // other layer's baked scaled clouds. The first layer starts from the defaults with stock noise.
        private static void ApplyLayerDefaults(SerializedProperty entry, int index, VolumeCloudConfiguration configuration)
        {
            if (index == 0)
            {
                entry.boxedValue = CloudSetup.CreateDefaultLayer();
                if (StockCloudNoise.TryLink(out Texture3D baseNoise, out Texture3D detailNoise, out _))
                {
                    entry.FindPropertyRelative("baseTexture").objectReferenceValue = baseNoise;
                    entry.FindPropertyRelative("detailTexture").objectReferenceValue = detailNoise;
                }
            }

            foreach (string field in BAKED_FIELDS)
            {
                entry.FindPropertyRelative(field).objectReferenceValue = null;
            }

            SerializedProperty name = entry.FindPropertyRelative("layerName");
            name.stringValue = UniqueLayerName(configuration, name.stringValue, index);
        }

        private static void DuplicateLayer(VisualElement host, VolumeCloudConfiguration configuration, SerializedObject serializedConfiguration, int index)
        {
            if (index < 0 || index >= configuration.cumulusList.Count)
                return;

            Undo.RecordObject(configuration, "Duplicate Cloud Layer");
            VolumeCloudConfiguration.CumulusData copy = JsonUtility.FromJson<VolumeCloudConfiguration.CumulusData>(
                JsonUtility.ToJson(configuration.cumulusList[index]));
            copy.bakedScaledTexture = null;
            copy.cloudNormalMap = null;
            copy.bakedBottomScaledTexture = null;
            configuration.cumulusList.Insert(index + 1, copy);
            copy.layerName = UniqueLayerName(configuration, copy.layerName, index + 1);
            ApplyStructuralChange(host, configuration, serializedConfiguration);
        }

        private static void ApplyStructuralChange(VisualElement host, VolumeCloudConfiguration configuration, SerializedObject serializedConfiguration)
        {
            EditorUtility.SetDirty(configuration);
            RebuildLayers(host, configuration, serializedConfiguration);
            NotifyChanged(configuration, serializedConfiguration, host);
        }

        // Scaled layers and distribution settings are matched to a layer by name, so two layers cannot share one.
        private static string UniqueLayerName(VolumeCloudConfiguration configuration, string name, int index)
        {
            string stem = string.IsNullOrEmpty(name) ? "Clouds" : name;
            HashSet<string> taken = configuration.cumulusList
                .Where((_, i) => i != index)
                .Select(layer => layer.layerName)
                .ToHashSet();
            if (!taken.Contains(stem))
                return stem;

            int suffix = 2;
            while (taken.Contains($"{stem} {suffix}"))
            {
                suffix++;
            }

            return $"{stem} {suffix}";
        }

        // The card's element path ends in the layer's index, which stays current because removing a card rebinds the
        // cards after it.
        private static int LayerIndex(SerializedProperty entry)
        {
            string path = entry.propertyPath;
            int open = path.LastIndexOf('[');
            return open >= 0 && int.TryParse(path.Substring(open + 1, path.Length - open - 2), out int index) ? index : -1;
        }

        private static string FormatChip(SerializedProperty entry)
        {
            Vector2 range = entry.FindPropertyRelative("cloudHeightRange").vector2Value;
            string state = entry.FindPropertyRelative("isEnable").boolValue ? string.Empty : "  off";
            return $"{range.x:#,0}–{range.y:#,0} m{state}";
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
            if (sidecar == null || index < 0 || index >= configuration.cumulusList.Count)
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
            VisualElement layers,
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
                    ApplyStructuralChange(layers, configuration, serializedConfiguration);
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

        private static void SetStatus(Label status, string message)
        {
            if (status == null)
                return;

            status.text = message;
            status.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
