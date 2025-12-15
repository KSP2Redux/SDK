using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.Modding
{
    [CustomEditor(typeof(Mod))]
    public class ModEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var gui = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/ksp2community.ksp2unitytools/Assets/Windows/ModEditor.uxml"
            );
            TemplateContainer root = gui.CloneTree();
            var addCode = root.Q<Button>("AddCodeButton");
            addCode.clicked += () =>
            {
                (target as Mod)?.CreateAssembly();
                (target as Mod)?.RefreshPipelines();
            };
            var refreshAddressables = root.Q<Button>("RefreshAddressablesButton");
            refreshAddressables.clicked += () => { (target as Mod)?.CreateAddressablesGroups(); };
            var refreshThunderkit = root.Q<Button>("RefreshThunderkitButton");
            refreshThunderkit.clicked += () => { (target as Mod)?.RefreshPipelines(); };
            var testInEditor = root.Q<Button>("TestInEditorButton");
            testInEditor.clicked += async () => { await KSP2UnityToolsManager.TestModsInPlayMode((Mod)target); };
            var testInPlayer = root.Q<Button>("TestInPlayerButton");
            testInPlayer.clicked += async () => { await KSP2UnityToolsManager.TestModsInBuiltGame((Mod)target); };
            var deployToZipFile = root.Q<Button>("DeployToZipFileButton");
            deployToZipFile.clicked += async () => { await KSP2UnityToolsManager.DeployMods((Mod)target); };
            return root;
        }
    }
}