using System.IO;
using Redux.VFX.Plume;
using Redux.VFX.Plume.Components;
using Redux.VFX.Plume.Configs;
using Redux.VFX.Plume.Services;
using UnityEngine;
using UnityEditor;

namespace Redux.VFX.Plumes.Editor.Utility
{
    internal static class PlumesContextMenu
    {
        private static IAssetManager AssetManager => ServiceProvider.GetService<IAssetManager>();

        [MenuItem("GameObject/Redux/Plumes/New Mesh Plume")]
        private static void CreateMeshPlume(MenuCommand command)
        {
            var parent = (GameObject)command.context;
            var go = new GameObject();
            go.AddComponent<SkinnedMeshRenderer>().sharedMesh = AssetManager.GetMesh("Flames");
            Object.DestroyImmediate(go.GetComponent<Collider>());

            go.AddComponent<PlumeThrottleData>();
            go.GetComponent<Renderer>().sharedMaterial = new Material(AssetManager.GetShader("Redux/Plumes/Additive"));

            if (parent != null)
            {
                go.transform.SetParent(parent.transform);
                go.transform.localPosition = Vector3.zero;
            }

            go.transform.rotation *= Quaternion.Euler(-90, 0, 0);
        }

        [MenuItem("GameObject/Redux/Plumes/New Volumetric Plume")]
        private static void CreateVolumetricPlume(MenuCommand command)
        {
            var parent = (GameObject)command.context;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Plume";
            Object.DestroyImmediate(go.GetComponent<Collider>());

            go.AddComponent<PlumeVolume>();
            go.AddComponent<PlumeThrottleData>();
            go.GetComponent<Renderer>().sharedMaterial = new Material(
                AssetManager.GetShader("Redux/Plumes/Volumetric (Additive)")
            );

            if (parent != null)
            {
                go.transform.SetParent(parent.transform);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
            }

            go.transform.localScale = Vector3.one * 5;
        }

        [MenuItem("GameObject/Redux/Plumes/New Volumetric Profiled Plume")]
        private static void CreateVolumetricProfiledPlume(MenuCommand command)
        {
            var parent = (GameObject)command.context;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Plume";
            Object.DestroyImmediate(go.GetComponent<Collider>());

            go.AddComponent<PlumeVolume>();
            go.AddComponent<PlumeThrottleData>();
            go.GetComponent<Renderer>().sharedMaterial = new Material(
                AssetManager.GetShader("Redux/Plumes/Volumetric (Profiled)")
            );

            if (parent != null)
            {
                go.transform.SetParent(parent.transform);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
            }

            go.transform.localScale = Vector3.one * 5;
        }

        [MenuItem("GameObject/Redux/Plumes/Create Plume from JSON")]
        private static void CreatePlumeFromJson(MenuCommand command)
        {
            string rawJson = File.OpenText(EditorUtility.OpenFilePanel(
                "Plume Config File",
                "Assets",
                "json"
            )).ReadToEnd();

            PlumeConfig config = PlumeConfig.Deserialize(rawJson);
            PlumeUtility.CreatePlumeFromConfig(config, (GameObject)command.context);
        }
    }
}