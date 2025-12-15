using System.IO;
using System.Reflection;
using KSP.Game.Missions.Definitions;
using KSP.IO;
using Ksp2UnityTools.Editor.API;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.Missions.Editors
{
    [CustomEditor(typeof(Mission))]
    public class MissionEditor : UnityEditor.Editor
    {
        private Toggle _usePatchesToggle;
        private Button _bakeIntoAddressable;

        public override VisualElement CreateInspectorGUI()
        {
            var gui = new VisualElement();
            foreach (FieldInfo property in typeof(Mission).GetFields())
            {
                if (property.GetCustomAttributes(typeof(HideInInspector), false).Length > 0)
                {
                    continue;
                }

                SerializedProperty propertyAsSerializedProperty = serializedObject.FindProperty(property.Name);
                gui.Add(new PropertyField(propertyAsSerializedProperty));
            }

            _usePatchesToggle = new Toggle("Generate As Patch")
            {
                bindingPath = "usePatches"
            };
            _usePatchesToggle.RegisterValueChangedCallback(OnToggleChanged);
            gui.Add(_usePatchesToggle);
            _bakeIntoAddressable = new Button(Bake)
            {
                text = "Bake Into Addressable"
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
            MissionData mission = targ!.GenerateMissionData();
            string json = KSP2UnityTools.ToJson(mission);
            string targPath = AssetDatabase.GetAssetPath(targ);
            string newPath = Path.GetDirectoryName(targPath) + $"/{targ.missionId}.json";
            File.WriteAllText(newPath, json);
            AssetDatabase.Refresh();
            bool giveCouldntMakeAddressableWarning = true;
            if (KSP2UnityTools.FindParentMod(targ) is { } mod)
            {
                giveCouldntMakeAddressableWarning = false;
                AddressablesTools.MakeAddressable(mod.missionsGroup, newPath, $"{targ.missionId}.json", "missions");
            }

            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "Created",
                giveCouldntMakeAddressableWarning
                    ? $"Created asset at '{newPath}', could not automatically make addressable, you will have to do that manually"
                    : $"Created asset at '{newPath}', could automatically make addressable",
                "Ok"
            );
        }
    }
}