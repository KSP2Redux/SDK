using Redux.VFX.Plume;
using Redux.VFX.Plume.Services;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Redux.VFX.Plumes.Editor.Services
{
    public class UnityAssetManager : BaseAssetManager
    {
        private static IPlumeLogger Logger => ServiceProvider.GetService<IPlumeLogger>();
        private static readonly string[] AssetFolders = { "Assets", "Packages/ksp2community.ksp2unitytools/Assets" };

        private string[] FindGuids(string name)
        {
            string[] foundGuids = AssetDatabase.FindAssets(name, AssetFolders);

            if (foundGuids.Length == 0 && GetRenamedAssetName(name) is { } renamedAssetName)
            {
                return FindGuids(renamedAssetName);
            }

            return foundGuids;
        }

        public override string GetAssetPath<T>(string name)
        {
            string[] foundGuids = FindGuids(name);

            if (foundGuids.Length == 0)
            {
                Logger.LogError($"No asset named {name} was found.");
                return null;
            }

            string foundPath = null;

            foreach (string guid in foundGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UnityObject asset = AssetDatabase.LoadAssetAtPath(path, typeof(T));
                if (asset == null || !asset.name.ToLowerInvariant().Equals(name.ToLowerInvariant()))
                {
                    continue;
                }

                if (foundPath != null)
                {
                    Logger.LogError($"Multiple assets found with name {name}.");
                    return null;
                }

                foundPath = path;
            }

            return foundPath;
        }

        public override T GetAsset<T>(string name)
        {
            T foundAsset = null;

            string[] foundGuids = FindGuids(name);

            if (foundGuids.Length == 0)
            {
                Logger.LogError($"No asset named {name} was found.");
                return null;
            }

            foreach (string guid in FindGuids(name))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null || !asset.name.ToLowerInvariant().Equals(name.ToLowerInvariant()))
                {
                    continue;
                }

                if (foundAsset != null)
                {
                    Logger.LogError($"Multiple assets found with name {name}.");
                    return null;
                }

                foundAsset = asset;
            }

            if (foundAsset == null)
            {
                Logger.LogError($"No asset with type {typeof(T).Name} found for the name {name}.");
                return null;
            }

            return foundAsset;
        }

        public override bool TryGetAsset<T>(string name, out T asset)
        {
            asset = null;

            string[] foundGuids = FindGuids(name);
            if (foundGuids.Length == 0)
            {
                return false;
            }

            T foundAsset = null;
            foreach (string guid in foundGuids)
            {
                var loadedAsset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (loadedAsset == null || !loadedAsset.name.ToLowerInvariant().Equals(name.ToLowerInvariant()))
                {
                    continue;
                }

                if (foundAsset != null)
                {
                    Logger.LogWarning($"Multiple assets found with name {name}, using first one.");
                    return foundAsset;
                }

                foundAsset = loadedAsset;
            }

            if (foundAsset == null)
            {
                return false;
            }

            asset = foundAsset;
            return true;
        }

        public override Shader GetShader(string shaderOrMaterialName)
        {
            if (Shader.Find(shaderOrMaterialName) is { } shader)
            {
                return shader;
            }

            if (Shader.Find(GetRenamedAssetName(shaderOrMaterialName)) is { } renamedShader)
            {
                return renamedShader;
            }

            return base.GetShader(shaderOrMaterialName);
        }
    }
}