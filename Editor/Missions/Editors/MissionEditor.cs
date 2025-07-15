using System.IO;
using KSP.IO;
using ksp2community.ksp2unitytools.editor.API;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ksp2community.ksp2unitytools.editor.Missions.Editors
{
    [CustomEditor(typeof(Mission))]
    public class MissionEditor : UnityEditor.Editor
    {
        private Toggle _usePatchesToggle;
        private Button _bakeIntoAddressable;

        public override VisualElement CreateInspectorGUI()
        {
            var gui = new VisualElement();
            foreach (var property in typeof(Mission).GetFields())
            {
                if (property.GetCustomAttributes(typeof(HideInInspector), false).Length > 0) continue;
                var propertyAsSerializedProperty = serializedObject.FindProperty(property.Name);
                gui.Add(new PropertyField(propertyAsSerializedProperty));
            }
            _usePatchesToggle = new Toggle("Generate As Patch")
            {
                bindingPath = "usePatches",
            };
            _usePatchesToggle.RegisterValueChangedCallback(OnToggleChanged);
            gui.Add(_usePatchesToggle);
            _bakeIntoAddressable = new Button(Bake)
            {
                text = "Bake Into Addressable",
            };
            gui.Add(_bakeIntoAddressable);
            return gui;
        }

        private void OnToggleChanged(ChangeEvent<bool> change)
        {
            UpdateBasedOnToggle();
        }

        private void UpdateBasedOnToggle()
        {
            _bakeIntoAddressable.style.display = _usePatchesToggle.value ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void Bake()
        {
            var targ = target as Mission;
            var mission = targ!.GenerateMissionData();
            var json = KSP2UnityTools.ToJson(mission);
            var targPath = AssetDatabase.GetAssetPath(targ);
            var newPath = Path.GetDirectoryName(targPath) + $"/{targ.missionId}.json";
            File.WriteAllText(newPath, json);
            AssetDatabase.Refresh();
            var giveCouldntMakeAddressableWarning = true;
            if (KSP2UnityTools.FindParentMod(targ) is { } mod)
            {
                giveCouldntMakeAddressableWarning = false;
                AddressablesTools.MakeAddressable(mod.missionsGroup, newPath, $"{targ.missionId}.json", "missions");
            }
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Created",
                giveCouldntMakeAddressableWarning
                    ? $"Created asset at '{newPath}', could not automatically make addressable, you will have to do that manually"
                    : $"Created asset at '{newPath}', could automatically make addressable",
                "Ok");
        }
    }
}