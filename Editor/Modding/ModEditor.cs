using System;
using Ksp2UnityTools.Editor.PartAuthoring;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections;
using Ksp2UnityTools.Editor.Widgets;
using SpaceWarp2.API.Features;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.Modding
{
    [CustomEditor(typeof(Mod))]
    public class ModEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var gui = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                SDKConfiguration.BasePath + "/Assets/Windows/ModEditor.uxml"
            );
            TemplateContainer root = gui.CloneTree();
            Ksp2UnityToolsStyles.Apply(root);
            var addCode = root.Q<Button>("AddCodeButton");
            addCode.clicked += () =>
            {
                (target as Mod)?.CreateAssembly();
                (target as Mod)?.RefreshPipelines();
            };
            var integratedModId = root.Q<TextField>("IntegratedModIdField");
            var addIntegration = root.Q<Button>("AddIntegrationButton");
            addIntegration.clicked += () =>
            {
                (target as Mod)?.CreateIntegrationAssembly(integratedModId.value);
                (target as Mod)?.RefreshPipelines();
            };
            var refreshAddressables = root.Q<Button>("RefreshAddressablesButton");
            refreshAddressables.clicked += () => { (target as Mod)?.CreateAddressablesGroups(); };
            var refreshThunderkit = root.Q<Button>("RefreshThunderkitButton");
            refreshThunderkit.clicked += () => { (target as Mod)?.RefreshPipelines(); };
            var convertKsp1Parts = root.Q<Button>("ConvertKsp1PartsButton");
            convertKsp1Parts.clicked += () => { Ksp1ModConverterWindow.Open((Mod)target); };
            var testInEditor = root.Q<Button>("TestInEditorButton");
            testInEditor.clicked += async () => { await KSP2UnityToolsManager.TestModsInPlayMode((Mod)target); };
            var testInPlayer = root.Q<Button>("TestInPlayerButton");
            testInPlayer.clicked += async () => { await KSP2UnityToolsManager.TestModsInBuiltGame((Mod)target); };
            var deployToZipFile = root.Q<Button>("DeployToZipFileButton");
            deployToZipFile.clicked += async () => { await KSP2UnityToolsManager.DeployMods((Mod)target); };
            var openAssetFeatures = root.Q<Button>("OpenAssetFeaturesButton");
            openAssetFeatures.clicked += () => { AssetFeaturesWindow.Open((Mod)target); };
            BuildFeatures(root.Q("FeaturesSlot"), (Mod)target);
            return root;
        }

        // The mod's own feature is pinned above the rest, since it always exists and cannot be renamed or removed
        private void BuildFeatures(VisualElement slot, Mod mod)
        {
            VisualElement ownCard = CardShell.Build(out CardShell.Slots ownSlots);
            var ownName = new Label(mod.id);
            ownName.AddToClassList("sdk-card__name-field");
            ownSlots.Header.Add(ownName);
            var ownId = new Label(FeatureRegistry.DefaultFor(mod.id));
            ownId.AddToClassList("sdk-card__chip");
            ownSlots.Header.Add(ownId);
            BuildFeatureBody(serializedObject.FindProperty(nameof(Mod.ownFeature)), ownSlots.Body, mod, false);
            slot.Add(ownCard);

            slot.Add(CardListSection.Build(serializedObject.FindProperty(nameof(Mod.features)), new CardListSection.Config
            {
                Title = "Features",
                AddButtonText = "+ Add Feature",
                IdentityFieldName = nameof(ModFeature.id),
                ChipFieldName = nameof(ModFeature.id),
                ChipFormatter = idProp =>
                    string.IsNullOrWhiteSpace(idProp.stringValue)
                        ? null
                        : FeatureRegistry.Qualify(mod.id, idProp.stringValue.Trim()),
                BuildBody = (entry, body) => BuildFeatureBody(entry, body, mod, true),
                ApplyDefaultsToNew = (entry, newIndex) =>
                {
                    // A new element copies the last one, so its relations are cleared
                    entry.FindPropertyRelative(nameof(ModFeature.id)).stringValue = $"Feature{newIndex}";
                    entry.FindPropertyRelative(nameof(ModFeature.recommended)).boolValue = false;
                    entry.FindPropertyRelative(nameof(ModFeature.requires)).ClearArray();
                    entry.FindPropertyRelative(nameof(ModFeature.conflicts)).ClearArray();
                    entry.FindPropertyRelative(nameof(ModFeature.provides)).ClearArray();
                }
            }));
        }

        private static void BuildFeatureBody(SerializedProperty feature, VisualElement body, Mod mod, bool canRecommend)
        {
            if (canRecommend)
            {
                body.Add(new PropertyField(feature.FindPropertyRelative(nameof(ModFeature.recommended)), "Recommended"));
            }

            body.Add(InlineStringListBlock.Build(
                feature.FindPropertyRelative(nameof(ModFeature.requires)),
                "Requires",
                entry => new FeatureField(entry, null, mod)
            ));
            body.Add(InlineStringListBlock.Build(
                feature.FindPropertyRelative(nameof(ModFeature.conflicts)),
                "Conflicts",
                entry => new FeatureField(entry, null, mod)
            ));
            body.Add(InlineStringListBlock.Build(
                feature.FindPropertyRelative(nameof(ModFeature.provides)),
                "Provides",
                entry => new FeatureField(entry, null, mod)
            ));
        }
    }
}
