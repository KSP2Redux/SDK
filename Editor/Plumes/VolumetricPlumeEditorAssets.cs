using Redux.VFX.Plume.Volumetric;
using UnityEditor;
using UnityEngine;

namespace KSP.Editor
{
    /// <summary>
    /// Supplies the volumetric exhaust assets to the renderer in the editor, where the plume preview runs without
    /// Addressables.
    /// </summary>
    [InitializeOnLoad]
    internal static class VolumetricPlumeEditorAssets
    {
        private const string SHADER_NAME = "Redux/Plumes/Volumetric Exhaust";
        private const string SMOKE_SHADER_NAME = "Redux/Plumes/Volumetric Smoke";
        private const string SHADER_PATH = "Assets/ReduxAssets/Shaders/Plumes/VolumetricExhaust.shader";
        private const string NOISE_PATH = "Assets/ReduxAssets/Plumes/Textures/EngineFlowNoise.asset";
        private const string FLOW_PATH = "Assets/ReduxAssets/Shaders/Plumes/ExhaustFlow.compute";
        private const string SMOKE_SHADER_PATH = "Assets/ReduxAssets/Shaders/Plumes/VolumetricSmoke.shader";

        static VolumetricPlumeEditorAssets() => EditorApplication.delayCall += Load;

        private static void Load()
        {
            VolumetricPlumeRenderer.SetEditorAssets(LoadShader(SHADER_PATH, SHADER_NAME),
                LoadAsset<Texture2D>(NOISE_PATH, "EngineFlowNoise"),
                SystemInfo.supportsComputeShaders ? LoadAsset<ComputeShader>(FLOW_PATH, "ExhaustFlow") : null,
                LoadShader(SMOKE_SHADER_PATH, SMOKE_SHADER_NAME));
        }

        private static Shader LoadShader(string path, string name)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            return shader != null ? shader : Shader.Find(name);
        }

        // Projects that import Redux assets elsewhere, such as mod templates, find them by name.
        private static T LoadAsset<T>(string path, string name) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            foreach (string guid in AssetDatabase.FindAssets($"{name} t:{typeof(T).Name}"))
            {
                asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                    return asset;
            }

            return null;
        }
    }
}
