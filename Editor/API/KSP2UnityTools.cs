using System;
using System.IO;
using System.Reflection;
using JetBrains.Annotations;
using KSP.IO;
using Ksp2UnityTools.Editor.Modding;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ksp2UnityTools.Editor.API
{
    /// <summary>
    /// This is a global API for interfacing with Redux SDK
    /// </summary>
    [PublicAPI]
    public static class KSP2UnityTools
    {
        private static bool _initialized;

        private static void Initialize()
        {
            typeof(IOProvider).GetMethod(
                    "Init",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                )
                ?.Invoke(null, new object[] { });
            _initialized = true;
        }

        /// <summary>
        /// Ignore any files that have a certain name when copying files over to the built stuff
        /// </summary>
        /// <param name="filenames">The filenames to ignore</param>
        public static void IgnoreFilesInCopy(params string[] filenames)
        {
            foreach (string filename in filenames)
            {
                KSP2UnityToolsManager.Settings.AddIgnoredFile(filename);
            }
        }

        /// <summary>
        /// Remove any previously ignored filenames from the ignore list
        /// </summary>
        /// <param name="filenames">The filenames to remove from the ignore list</param>
        public static void DontIgnoreFilesInCopy(params string[] filenames)
        {
            foreach (string filename in filenames)
            {
                KSP2UnityToolsManager.Settings.RemoveIgnoredFile(filename);
            }
        }

        [CanBeNull]
        public static Mod FindParentMod(Object thisAsset)
        {
            string path = AssetDatabase.GetAssetPath(thisAsset);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            path = path.Replace('\\', '/');
            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return null;
            }

            int trueStart = path.IndexOf('/', "Assets/".Length);
            if (trueStart < 0)
            {
                return null;
            }

            string truePath = path[..trueStart];
            var legacyMod = AssetDatabase.LoadAssetAtPath<Mod>(truePath + "/swinfo.asset");
            if (legacyMod != null)
            {
                return legacyMod;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Mod", new[] { truePath }))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (!string.Equals(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'), truePath, StringComparison.Ordinal))
                {
                    continue;
                }

                var mod = AssetDatabase.LoadAssetAtPath<Mod>(assetPath);
                if (mod != null)
                {
                    return mod;
                }
            }

            return null;
        }

        public static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
            }

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            if (!Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                if (file.Extension == ".meta" || KSP2UnityToolsManager.Settings.ignoredFiles.Contains(file.Name))
                {
                    continue;
                }

                string targetFilePath = Path.Combine(destinationDir, file.Name);

                if (File.Exists(targetFilePath))
                {
                    File.Delete(targetFilePath);
                }

                file.CopyTo(targetFilePath);
            }

            if (!recursive)
            {
                return;
            }

            // If recursive and copying subdirectories, recursively call this method
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, true);
            }
        }

        public static T CreateKsp2UnityToolsAssetAtSelectedPath<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            path += $"/{name}.asset";
            ProjectWindowUtil.CreateAsset(asset, path);
            return asset;
        }

        public const int MenuPriority = 19;

        public static string ToJson<T>(T value)
        {
            if (!_initialized)
            {
                Initialize();
            }

            return IOProvider.ToJson(value, IOProvider.GetDontDeserializeKspStateSerializerSettings());
        }
    }
}
