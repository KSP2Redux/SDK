using KSP;
using Redux.VFX.Plume.Components;
using Redux.VFX.Plume.Configs;
using System.Collections;
using System.IO;
using System.Linq;
using ksp2community.ksp2unitytools.editor.API;
using Redux.VFX.Plume;
using Redux.VFX.Plume.Services;
using Redux.VFX.Plumes.Editor.Utility;
using UnityEditor;
using UnityEngine;

namespace Redux.VFX.Plumes.Editor.CustomEditors
{
    [CustomEditor(typeof(PlumeThrottleDataMasterGroup))]
    public class PlumeThrottleDataMasterGroupEditor : UnityEditor.Editor
    {
        private const string JsonConfigFolder = "Assets/plugin_template/assets/plumes/";
        private const string AddressablesConfigFolder = "Assets/Plumes/";

        private static IPlumeLogger Logger => ServiceProvider.GetService<IPlumeLogger>();
        private static IAssetManager AssetManager => ServiceProvider.GetService<IAssetManager>();

        private bool _useNewShader;

        public override void OnInspectorGUI()
        {
            var group = (PlumeThrottleDataMasterGroup)target;

            _useNewShader = EditorGUILayout.Toggle("Use New Shader", _useNewShader);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("Group Throttle");
            group.GroupThrottle = EditorGUILayout.Slider(
                group.GroupThrottle,
                0,
                PlumeThrottleDataMasterGroup.ThrottleMax
            );
            EditorGUILayout.LabelField("Group Atmospheric Pressure");
            group.GroupAtmo = EditorGUILayout.Slider(
                group.GroupAtmo,
                0,
                PlumeThrottleDataMasterGroup.AtmoMax
            );

            EditorGUI.BeginDisabledGroup(group.GroupAtmo > 0.0092f);
            EditorGUILayout.LabelField("UpperAtmo Fine tune");
            float atmo = EditorGUILayout.Slider(group.GroupAtmo, 0, 0.0092f);
            if (group.GroupAtmo <= 0.0092f)
            {
                group.GroupAtmo = atmo;
            }

            EditorGUI.EndDisabledGroup();
            if (EditorGUI.EndChangeCheck())
            {
                var allMasters = FindObjectsOfType<PlumeThrottleDataMasterGroup>();

                foreach (var master in allMasters)
                {
                    UpdateVisuals(master);
                }
            }

            EditorGUI.BeginChangeCheck();
            group.Active = EditorGUILayout.Toggle("Active?", group.Active);
            if (EditorGUI.EndChangeCheck())
            {
                group.ToggleVisibility(group.Active);
                EditorUtility.SetDirty(group);
            }

            GUILayout.Label($"{group.ThrottleDatas.Count} children");
            if (GUILayout.Button("Collect children"))
            {
                group.ThrottleDatas = group.GetComponentsInChildren<PlumeThrottleData>(true).ToList();
            }

            var partData = group.GetComponentInParent<CorePartData>();
            string filename = partData != null ? $"{partData.Data.partName}.json" : $"{group.name}.json";

            EditorGUILayout.Space(5);

            group.UseAddressables = !EditorGUILayout.Toggle("Don't use addressables", !group.UseAddressables);

            if (GUILayout.Button("Save plume"))
            {
                HandleSaveConfig(group, partData?.Data.partName, filename);
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Reload materials from JSON"))
            {
                HandleReloadConfig(group, filename);
            }

            if (GUILayout.Button("Load from JSON"))
            {
                if (EditorUtility.DisplayDialog(
                        "Warning",
                        "This will remove all child objects of this group and recreate them from JSON. Continue?",
                        "Load",
                        "Cancel"
                    ))
                {
                    HandleLoadConfig(group, filename);
                }
            }
        }

        private void HandleLoadConfig(PlumeThrottleDataMasterGroup group, string filename)
        {
            PlumeConfig plumeConfig = LoadFromJson(
                group.UseAddressables
                    ? AddressablesConfigFolder
                    : JsonConfigFolder,
                filename
            );
            PlumeUtility.CreatePlumeFromConfig(plumeConfig, group.gameObject.transform.parent.gameObject);
            group.gameObject.DestroyGameObjectImmediate();
        }

        private void HandleReloadConfig(PlumeThrottleDataMasterGroup group, string filename)
        {
            PlumeConfig plumeConfig = LoadFromJson(
                group.UseAddressables
                    ? AddressablesConfigFolder
                    : JsonConfigFolder,
                filename
            );

            foreach (var throttleData in group.GetComponentsInChildren<PlumeThrottleData>())
            {
                int index = plumeConfig.PlumeComponentConfigs[throttleData.transform.parent.name]
                    .FindIndex(a => a.TargetGameObject == throttleData.name);
                if (index < 0)
                {
                    continue;
                }

                throttleData.Config = plumeConfig.PlumeComponentConfigs[throttleData.transform.parent.name][index];

                throttleData.GetComponent<Renderer>().sharedMaterial = throttleData.Config.GetEditorMaterial();
                if (_useNewShader)
                {
                    throttleData.GetComponent<Renderer>().sharedMaterial.shader =
                        AssetManager.GetShader("Redux/Plumes/Additive");
                }

                throttleData.GetComponent<Renderer>().sharedMaterial.name =
                    throttleData.name + " Plume Material";
            }
        }

        private static void HandleSaveConfig(
            PlumeThrottleDataMasterGroup group,
            string partName,
            string filename
        )
        {
            PlumeConfig config = PlumeUtility.GetConfigFromPlume(group, partName);

            group.StartCoroutine(
                group.UseAddressables
                    ? SaveToAddressables(config, filename)
                    : SaveToJson(config, JsonConfigFolder, filename)
            );
        }

        private static void UpdateVisuals(PlumeThrottleDataMasterGroup throttleGroup)
        {
            if (!throttleGroup.Active)
            {
                return;
            }

            throttleGroup.ThrottleDatas.ForEach(throttleData =>
            {
                if (!throttleData.IsVisible())
                {
                    return;
                }

                if (throttleData.Config == null)
                {
                    Logger.LogWarning($"Config for {throttleData.name} is null");
                    return;
                }

                throttleData.TriggerUpdateVisuals(
                    throttleGroup.GroupThrottle / 100f,
                    throttleGroup.GroupAtmo,
                    0,
                    Vector3.zero
                );
            });
        }

        private static PlumeConfig LoadFromJson(string path, string filename)
        {
            if (File.Exists(Path.Combine(path, filename)))
            {
                string rawJsonExisting = File.OpenText(Path.Combine(path, filename)).ReadToEnd();
                return PlumeConfig.Deserialize(rawJsonExisting);
            }

            if (!Directory.Exists(path))
            {
                path = "Assets";
            }

            string rawJson = File.OpenText(EditorUtility.OpenFilePanel(
                "LFO Config File",
                path,
                "json"
            )).ReadToEnd();

            return PlumeConfig.Deserialize(rawJson);
        }

        private static IEnumerator SaveToJson(PlumeConfig config, string path, string filename)
        {
            Directory.CreateDirectory(path);

            string json = PlumeConfig.Serialize(config);

            using (StreamWriter sw = File.CreateText(Path.Combine(path, filename)))
            {
                sw.Write(json);
            }

            yield return null;

            AssetDatabase.Refresh();
        }

        private static IEnumerator SaveToAddressables(PlumeConfig config, string filename)
        {
            yield return SaveToJson(config, AddressablesConfigFolder, filename);

            AddressablesTools.MakeAddressable(
                Path.Combine(AddressablesConfigFolder, filename),
                $"{Constants.AddressablesPrefix}{filename}",
                Constants.ConfigLabel
            );

            foreach (PlumeComponentConfig plumeConfig in config.PlumeComponentConfigs.Values.SelectMany(item => item))
            {
                var meshPath = AssetManager.GetAssetPath<Mesh>(plumeConfig.MeshPath)
                               ?? AssetManager.GetAssetPath<GameObject>(plumeConfig.MeshPath);

                MakeAssetAddressable(plumeConfig.MeshPath, meshPath);

                foreach ((string _, object value) in plumeConfig.ShaderSettings.ShaderParams)
                {
                    if (value is string texture)
                    {
                        MakeAssetAddressable(texture, AssetManager.GetAssetPath<Texture>(texture));
                    }
                }
            }
        }

        private static void MakeAssetAddressable(string name, string path)
        {
            if (path.StartsWith("Packages/lfo.editor"))
            {
                return;
            }

            AddressablesTools.MakeAddressable(
                path,
                $"{Constants.AddressablesPrefix}{name}",
                Constants.AssetLabel
            );
        }
    }
}