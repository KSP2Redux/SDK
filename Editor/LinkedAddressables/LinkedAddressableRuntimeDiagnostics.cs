using Ksp2UnityTools.LinkedAddressables;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.LinkedAddressables
{
    public static class LinkedAddressableRuntimeDiagnostics
    {
        [MenuItem("Modding/Linked Addressables/Diagnostics/Validate Runtime State", false, 300)]
        private static void Validate()
        {
            var manifest = AssetDatabase.LoadAssetAtPath<LinkedAddressableRuntimeManifest>(
                LinkedAddressableRuntimeManifestBuilder.ManifestAssetPath
            );
            if (manifest == null)
            {
                Debug.LogError(
                    "[ReduxSDK.LinkedAddressables] The generated runtime manifest is missing."
                );
                return;
            }

            foreach (var entry in manifest.Entries)
            {
                var descriptor = entry?.Descriptor;
                LinkedAddressableRuntime.TryGetFailure(
                    descriptor?.StableId,
                    out var failure
                );
                LinkedAddressableRuntime.TryGetLoadedRoot(
                    descriptor?.StableId,
                    out var runtimeRoot
                );
                Debug.Log(
                    $"[ReduxSDK.LinkedAddressables] Runtime state for "
                        + $"'{descriptor?.Address}': "
                        + $"mode={(EditorApplication.isPlaying ? "play" : "edit")}; "
                        + $"mounted={runtimeRoot != null}; "
                        + $"runtimeType={runtimeRoot?.GetType().FullName ?? "<none>"}; "
                        + $"failure={failure ?? "<none>"}; "
                        + $"loadedBundles={AssetBundle.GetAllLoadedAssetBundles().Count()}."
                );
            }

            Debug.Log(
                $"[ReduxSDK.LinkedAddressables] Runtime ready="
                    + $"{LinkedAddressableRuntime.IsReady}; translatedScene="
                    + $"{LinkedAddressableRuntime.TranslatedSceneLoaded}; failure="
                    + $"{LinkedAddressableRuntime.RuntimeFailure ?? "<none>"}; source="
                    + $"{LinkedAddressableRuntime.ResolvedSourceRoot ?? "<none>"}; "
                    + $"sceneTexture={DescribeSceneTexture()}."
            );
        }

        [MenuItem("Modding/Linked Addressables/Diagnostics/Unload Bundles and Validate Assets", false, 310)]
        private static void UnloadAndValidate()
        {
            Debug.Log(
                "[ReduxSDK.LinkedAddressables] Runtime state immediately before forced unload:"
            );
            Validate();
            AssetBundle.UnloadAllAssetBundles(true);
            Debug.Log(
                "[ReduxSDK.LinkedAddressables] Runtime state immediately after forced unload:"
            );
            Validate();
        }

        private static string DescribeSceneTexture()
        {
            var rawImage = Resources
                .FindObjectsOfTypeAll<Component>()
                .FirstOrDefault(
                    component =>
                        component != null
                        && component.GetType().FullName == "UnityEngine.UI.RawImage"
                );
            if (rawImage == null)
                return "<RawImage not found>";

            var serializedRawImage = new SerializedObject(rawImage);
            var textureProperty = serializedRawImage.FindProperty("m_Texture");
            var sceneTexture = textureProperty?.objectReferenceValue as Texture2D;
            if (sceneTexture == null)
                return "<null>";

            return $"{sceneTexture.name} ({sceneTexture.width}x{sceneTexture.height}); "
                + $"assetPath={AssetDatabase.GetAssetPath(sceneTexture)}; "
                + $"sample={DescribePixel(sceneTexture)}";
        }

        private static string DescribePixel(Texture2D texture)
        {
            if (texture == null)
                return "<null>";
            if (!texture.isReadable)
                return "<not-readable>";

            try
            {
                return texture.GetPixel(texture.width / 2, texture.height / 2).ToString();
            }
            catch (Exception exception)
            {
                return $"<error: {exception.Message}>";
            }
        }
    }
}
