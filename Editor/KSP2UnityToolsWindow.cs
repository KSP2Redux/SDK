using System.Collections.Generic;
using Ksp2UnityTools.Editor.Modding;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor
{
    public class KSP2UnityToolsWindow : EditorWindow
    {
        [MenuItem("Modding/KSP2 Unity Tools")]
        public static void ShowWindow()
        {
            EditorWindow window = GetWindow<KSP2UnityToolsWindow>();
            window.titleContent = new GUIContent("KSP2 Unity Tools");
        }

        private List<Mod> _allMods = new();
        private List<Mod> _selectedMods = new();
        private VisualElement _scrollContainer;

        private void CreateGUI()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/ksp2community.ksp2unitytools/Assets/Windows/KSP2UnityTools.uxml"
            );
            rootVisualElement.Add(asset.Instantiate());
            foreach (StyleSheet stylesheet in asset.stylesheets)
            {
                rootVisualElement.styleSheets.Add(stylesheet);
            }

            var refreshButton = rootVisualElement.Q<Button>("RefreshModsButton");
            refreshButton.clicked += RefreshModList;
            var createButton = rootVisualElement.Q<Button>("CreateModButton");
            createButton.clicked += () =>
            {
                CreateNewModWindow.ShowWindow();
                RefreshModList();
            };
            var testInEditorButton = rootVisualElement.Q<Button>("TestInEditorButton");
            testInEditorButton.clicked += async () =>
                await KSP2UnityToolsManager.TestModsInPlayMode(_selectedMods.ToArray());
            var testInPlayerButton = rootVisualElement.Q<Button>("TestInPlayerButton");
            testInPlayerButton.clicked += async () =>
                await KSP2UnityToolsManager.TestModsInBuiltGame(_selectedMods.ToArray());
            var deployToZipFileButton = rootVisualElement.Q<Button>("DeployToZipFileButton");
            deployToZipFileButton.clicked += async () =>
                await KSP2UnityToolsManager.DeployMods(_selectedMods.ToArray());
            _scrollContainer = rootVisualElement.Q("ModsContainer");
            RefreshModList();
        }


        private void RefreshModList()
        {
            _allMods.Clear();
            _scrollContainer.Clear();
            foreach (string mod in AssetDatabase.FindAssets("t:Mod"))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(mod);
                var modObject = AssetDatabase.LoadAssetAtPath<Mod>(assetPath);
                _allMods.Add(modObject);
                var toggle = new Toggle
                {
                    label = modObject.name,
                    value = _selectedMods.Contains(modObject)
                };
                toggle.RegisterValueChangedCallback(evt =>
                    {
                        if (evt.newValue)
                        {
                            if (!_selectedMods.Contains(modObject))
                            {
                                _selectedMods.Add(modObject);
                            }
                        }
                        else
                        {
                            if (_selectedMods.Contains(modObject))
                            {
                                _selectedMods.Remove(modObject);
                            }
                        }
                    }
                );
                _scrollContainer.Add(toggle);
            }

            _selectedMods.RemoveAll(x => !_allMods.Contains(x));
        }
    }
}