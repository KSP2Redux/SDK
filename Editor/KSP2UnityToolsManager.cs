using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ksp2UnityTools.Editor.Modding;
using ThunderKit.Core.Data;
using ThunderKit.Core.Pipelines;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace Ksp2UnityTools.Editor
{
    /// <summary>
    /// Sets the SDK up in a project when the editor loads, and builds, tests and deploys mods through their pipelines.
    /// </summary>
    /// <remarks>
    /// On load it creates or loads the SDK's settings asset and, outside Redux itself, copies in the boot scene and the
    /// game import asset a mod project needs.
    /// </remarks>
    [InitializeOnLoad]
    public static class KSP2UnityToolsManager
    {
        private const string BootKspSceneFileName = "boot-ksp.unity";
        private const string PreferredBootKspScenePath = "Assets/boot-ksp.unity";
        private const string SettingsAssetPath = "Assets/ReduxSDKSettings.asset";
        private const string LegacySettingsAssetPath = "Assets/KSP2UTSettings.asset";

        /// <summary>
        /// The SDK's settings for this project.
        /// </summary>
        public static readonly KSP2UnityToolsSettings Settings;

        static KSP2UnityToolsManager()
        {
            MoveLegacyAsset(LegacySettingsAssetPath, SettingsAssetPath);

            if (!File.Exists(SettingsAssetPath))
            {
                Settings = ScriptableObject.CreateInstance<KSP2UnityToolsSettings>();
                AssetDatabase.CreateAsset(Settings, SettingsAssetPath);
                AssetDatabase.SaveAssets();
            }
            else
            {
                Settings = AssetDatabase.LoadAssetAtPath<KSP2UnityToolsSettings>(SettingsAssetPath);
            }

            EditorApplication.delayCall += RelinkAddressablesSettings;

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

        private static void MoveLegacyAsset(string legacyPath, string currentPath)
        {
            if (!File.Exists(legacyPath) && !Directory.Exists(legacyPath))
                return;

            if (File.Exists(currentPath) || Directory.Exists(currentPath))
                return;

            string error = AssetDatabase.MoveAsset(legacyPath, currentPath);
            if (!string.IsNullOrEmpty(error))
            {
                UnityEngine.Debug.LogError($"Redux SDK could not rename '{legacyPath}' to '{currentPath}': {error}");
            }
        }

        // Addressables finds the project's settings only through an EditorBuildSettings entry naming its DefaultObject.
        // Re-applying the template replaces that file and drops the entry, so restore it from the project's own assets.
        private static void RelinkAddressablesSettings()
        {
            if (AddressableAssetSettingsDefaultObject.Settings != null)
                return;

            var defaultObject = FindProjectAsset<AddressableAssetSettingsDefaultObject>(
                $"{AddressableAssetSettingsDefaultObject.kDefaultConfigFolder}/DefaultObject.asset"
            );
            if (defaultObject != null)
            {
                EditorBuildSettings.AddConfigObject(
                    AddressableAssetSettingsDefaultObject.kDefaultConfigObjectName,
                    defaultObject,
                    true
                );
                if (AddressableAssetSettingsDefaultObject.Settings != null)
                {
                    UnityEngine.Debug.Log($"Redux SDK relinked the Addressables settings through {AssetDatabase.GetAssetPath(defaultObject)}.");
                    return;
                }
            }

            var settings = FindProjectAsset<AddressableAssetSettings>(
                $"{AddressableAssetSettingsDefaultObject.kDefaultConfigFolder}/{AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName}.asset"
            );
            if (settings == null)
                return;

            // The setter points the registered DefaultObject at these settings, or creates one when none is registered
            AddressableAssetSettingsDefaultObject.Settings = settings;
            UnityEngine.Debug.Log($"Redux SDK relinked the Addressables settings to {AssetDatabase.GetAssetPath(settings)}.");
        }

        private static T FindProjectAsset<T>(string preferredPath) where T : UnityEngine.Object
        {
            var preferred = AssetDatabase.LoadAssetAtPath<T>(preferredPath);
            if (preferred != null)
                return preferred;

            string guid = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets" }).FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
        }

        /// <summary>
        /// Builds mods into the project's testing folder and enters play mode from the boot scene.
        /// </summary>
        /// <param name="mods">The mods to test.</param>
        /// <returns>The build and launch.</returns>
        /// <exception cref="InvalidOperationException">Thrown when a mod's build fails, before play mode starts.</exception>
        /// <exception cref="FileNotFoundException">Thrown when a mod has no pipeline, or the project has no boot scene.</exception>
        public static async Task TestModsInPlayMode(params Mod[] mods)
        {
            if (Directory.Exists("Assets/Mods/__Testing"))
            {
                Directory.Delete("Assets/Mods/__Testing", true);
            }

            foreach (Mod mod in mods)
            {
                string testPipeline = mod.Folder + "/Pipelines/Build for Editor.asset";
                await ExecuteOrThrow(AssetDatabase.LoadAssetAtPath<Pipeline>(testPipeline), testPipeline);
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

        // ThunderKit logs a failed job and returns as if it ran, leaving JobIndex on that job. A finished run resets it
        // to -1, so anything that needs the build stops here instead of launching a partly staged mod.
        private static async Task ExecuteOrThrow(Pipeline pipeline, string path)
        {
            if (pipeline == null)
                throw new FileNotFoundException($"Could not find the pipeline {path}. Refresh ThunderKit on the mod.");

            await pipeline.Execute();
            if (pipeline.JobIndex != -1)
                throw new InvalidOperationException($"{path} failed. See the pipeline log for why.");
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

        /// <summary>
        /// Builds mods into the installed game's testing folder and launches the game.
        /// </summary>
        /// <param name="mods">The mods to test.</param>
        /// <returns>The build and launch.</returns>
        /// <exception cref="InvalidOperationException">Thrown when a mod's build fails, before the game launches.</exception>
        /// <exception cref="FileNotFoundException">Thrown when a mod has no pipeline.</exception>
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
                await ExecuteOrThrow(AssetDatabase.LoadAssetAtPath<Pipeline>(buildPipeline), buildPipeline);
            }

            var info = new ProcessStartInfo
            {
                WorkingDirectory = settings.GamePath,
                FileName = Path.Combine(settings.GamePath, settings.GameExecutable),
                Arguments = string.Join(' ', Settings.gameLaunchArguments)
            };

            Process.Start(info);
        }

        /// <summary>
        /// Builds each mod into a zip file to publish, writing its version check swinfo first.
        /// </summary>
        /// <param name="mods">The mods to deploy.</param>
        /// <returns>The builds.</returns>
        /// <exception cref="InvalidOperationException">Thrown when a mod's build fails, before the next mod builds.</exception>
        /// <exception cref="FileNotFoundException">Thrown when a mod has no pipeline.</exception>
        public static async Task DeployMods(params Mod[] mods)
        {
            foreach (Mod mod in mods)
            {
                mod.CreateVersionCheckSwinfo();
                string deployPipeline = mod.Folder + "/Pipelines/Deploy To Zip File.asset";
                await ExecuteOrThrow(AssetDatabase.LoadAssetAtPath<Pipeline>(deployPipeline), deployPipeline);
            }
        }
    }
}
