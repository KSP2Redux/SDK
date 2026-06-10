using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private const string BootKspSceneFileName = "boot-ksp.unity";
        private const string PreferredBootKspScenePath = "Assets/boot-ksp.unity";
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

            if (ResolveBootKspScenePath() == null)
            {
                File.Copy(
                    SDKConfiguration.BasePath + "/Assets/Scenes/boot-ksp.unity",
                    PreferredBootKspScenePath
                );
            }

            if (!File.Exists("Assets/ImportKsp2ToEditor.asset"))
            {
                File.Copy(
                    SDKConfiguration.BasePath + "/ImportKsp2ToEditor.asset",
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
            string bootScenePath = ResolveBootKspScenePath();
            if (bootScenePath == null)
            {
                throw new FileNotFoundException($"Could not find {BootKspSceneFileName} in the project.");
            }

            EditorSceneManager.OpenScene(bootScenePath);
            EditorApplication.EnterPlaymode();
        }

        private static string ResolveBootKspScenePath()
        {
            if (File.Exists(PreferredBootKspScenePath))
            {
                return PreferredBootKspScenePath;
            }

            return AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(BootKspSceneFileName), new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileName(path) == BootKspSceneFileName)
                .OrderBy(path => path.Length)
                .ThenBy(path => path)
                .FirstOrDefault();
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
                FileName = Path.Combine(settings.GamePath, settings.GameExecutable),
                Arguments = string.Join(' ', Settings.gameLaunchArguments)
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