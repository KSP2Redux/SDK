using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Ksp2UnityTools.Editor.Modding;
using ThunderKit.Core.Data;
using ThunderKit.Core.Pipelines;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace Ksp2UnityTools.Editor
{
    [InitializeOnLoad]
    public static class KSP2UnityToolsManager
    {
        public static readonly KSP2UnityToolsSettings Settings;

        static KSP2UnityToolsManager()
        {
            if (!File.Exists("Assets/KSP2UTSettings.asset"))
            {
                Settings = ScriptableObject.CreateInstance<KSP2UnityToolsSettings>();
                AssetDatabase.CreateAsset(Settings, "Assets/KSP2UTSettings.asset");
                AssetDatabase.SaveAssets();
            }
            else
            {
                Settings = AssetDatabase.LoadAssetAtPath<KSP2UnityToolsSettings>("Assets/KSP2UTSettings.asset");
            }

#if !REDUX

            if (!File.Exists("Assets/boot-ksp.unity"))
            {
                File.Copy(
                    "Packages/ksp2community.ksp2unitytools/Assets/Scenes/boot-ksp.unity",
                    "Assets/boot-ksp.unity"
                );
            }

            if (!File.Exists("Assets/ImportKsp2ToEditor.asset"))
            {
                File.Copy(
                    "Packages/ksp2community.ksp2unitytools/ImportKsp2ToEditor.asset",
                    "Assets/ImportKsp2ToEditor.asset"
                );
            }

#endif
        }


        private static Dictionary<string, PersistentDictionary> StoredDictionaries = new();

        public static PersistentDictionary GetDictionary(string dictionaryName)
        {
            if (StoredDictionaries.TryGetValue(dictionaryName, out PersistentDictionary result))
            {
                return result;
            }

            if (!Directory.Exists("Assets/KSP2UTData"))
            {
                Directory.CreateDirectory("Assets/KSP2UTData");
            }

            if (!File.Exists($"Assets/KSP2UTData/{dictionaryName}.asset"))
            {
                PersistentDictionary dict = StoredDictionaries[dictionaryName] =
                    ScriptableObject.CreateInstance<PersistentDictionary>();
                AssetDatabase.CreateAsset(dict, $"Assets/KSP2UTData/{dictionaryName}.asset");
                AssetDatabase.SaveAssets();
                return dict;
            }
            else
            {
                return StoredDictionaries[dictionaryName] =
                    AssetDatabase.LoadAssetAtPath<PersistentDictionary>($"Assets/KSP2UTData/{dictionaryName}.asset");
            }
        }


        public static async Task TestModsInPlayMode(params Mod[] mods)
        {
            if (Directory.Exists("Assets/Mods/__Testing"))
            {
                Directory.Delete("Assets/Mods/__Testing", true);
            }

            foreach (Mod mod in mods)
            {
                string testPipeline = mod.Folder + "/Pipelines/Build for Editor.asset";
                var pipeline = AssetDatabase.LoadAssetAtPath<Pipeline>(testPipeline);
                await pipeline.Execute();
            }

            // ReSharper disable once AccessToStaticMemberViaDerivedType
            EditorSceneManager.OpenScene("Assets/boot-ksp.unity");
            EditorApplication.EnterPlaymode();
        }

        public static async Task TestModsInBuiltGame(params Mod[] mods)
        {
            var settings = ThunderKitSetting.GetOrCreateSettings<ThunderKitSettings>();
            string testingFolder = Path.Combine(settings.GamePath, "mods/__Testing");
            if (Directory.Exists(testingFolder))
            {
                Directory.Delete(testingFolder, true);
            }

            foreach (Mod mod in mods)
            {
                string buildPipeline = mod.Folder + "/Pipelines/Build for Player.asset";
                var pipeline = AssetDatabase.LoadAssetAtPath<Pipeline>(buildPipeline);
                await pipeline.Execute();
            }

            var info = new ProcessStartInfo
            {
                WorkingDirectory = settings.GamePath,
                FileName = Path.Combine(settings.GamePath, settings.GameExecutable)
            };

            Process.Start(info);
        }

        public static async Task DeployMods(params Mod[] mods)
        {
            foreach (Mod mod in mods)
            {
                mod.CreateVersionCheckSwinfo();
                string deployPipeline = mod.Folder + "/Pipelines/Deploy To Zip File.asset";
                var pipeline = AssetDatabase.LoadAssetAtPath<Pipeline>(deployPipeline);
                await pipeline.Execute();
            }
        }
    }
}