using System.Collections.Generic;
using Redux.VFX.Plume;
using Redux.VFX.Plume.Components;
using Redux.VFX.Plume.Configs;
using Redux.VFX.Plume.Services;
using UnityEngine;

namespace Redux.VFX.Plumes.Editor.Utility
{
    public static class PlumeUtility
    {
        private static IPlumeLogger Logger => ServiceProvider.GetService<IPlumeLogger>();
        private static IAssetManager AssetManager => ServiceProvider.GetService<IAssetManager>();

        public static PlumeConfig GetConfigFromPlume(PlumeThrottleDataMasterGroup group, string partName)
        {
            var config = new PlumeConfig();
            if (partName != null)
            {
                config.PartName = partName;
            }

            config.PlumeComponentConfigs = new Dictionary<string, List<PlumeComponentConfig>>();

            foreach (PlumeThrottleData throttleData in group.GetComponentsInChildren<PlumeThrottleData>())
            {
                var plumeConfig = new PlumeComponentConfig();
                Material material = throttleData.GetComponent<Renderer>().sharedMaterial;
                Transform transform = throttleData.transform;

                plumeConfig.ShaderSettings = ShaderConfig.GenerateConfig(material);
                plumeConfig.Position = transform.localPosition;
                plumeConfig.Scale = transform.localScale;
                plumeConfig.Rotation = transform.localRotation.eulerAngles;
                plumeConfig.FloatParams = throttleData.FloatParams;
                plumeConfig.MeshPath = throttleData.TryGetComponent(out SkinnedMeshRenderer skinnedRenderer)
                    ? skinnedRenderer.sharedMesh.name
                    : throttleData.GetComponent<MeshFilter>().sharedMesh.name;
                plumeConfig.TargetGameObject = throttleData.name;

                if (!config.PlumeComponentConfigs.ContainsKey(throttleData.transform.parent.name))
                {
                    config.PlumeComponentConfigs.Add(throttleData.transform.parent.name, new List<PlumeComponentConfig>());
                }

                config.PlumeComponentConfigs[throttleData.transform.parent.name].Add(plumeConfig);

                throttleData.Config = plumeConfig;
            }

            return config;
        }

        public static void CreatePlumeFromConfig(PlumeConfig plumeConfig, GameObject parent)
        {
            var createdObjects = new Dictionary<string, GameObject>();

            foreach ((string parentName, List<PlumeComponentConfig> plumeComponentConfigs) in plumeConfig.PlumeComponentConfigs)
            {
                foreach (PlumeComponentConfig plumeComponentConfig in plumeComponentConfigs)
                {
                    bool isNew = FindOrCreateObject(
                        plumeComponentConfig.TargetGameObject,
                        parentName,
                        ref createdObjects,
                        out GameObject gameObject
                    );

                    if (isNew)
                    {
                        gameObject.transform.localPosition = plumeComponentConfig.Position;
                        gameObject.transform.localScale = plumeComponentConfig.Scale;
                        gameObject.transform.localRotation = Quaternion.Euler(plumeComponentConfig.Rotation);

                        var renderer = gameObject.AddComponent<MeshRenderer>();
                        var meshFilter = gameObject.AddComponent<MeshFilter>();

                        if (AssetManager.GetMesh(plumeComponentConfig.MeshPath) is { } mesh)
                        {
                            meshFilter.sharedMesh = mesh;
                        }
                        else
                        {
                            Logger.LogError(
                                $"Couldn't find mesh {plumeComponentConfig.MeshPath} for object {plumeComponentConfig.TargetGameObject}"
                            );
                        }

                        var throttleData = gameObject.AddComponent<PlumeThrottleData>();
                        throttleData.Config = plumeComponentConfig;

                        renderer.sharedMaterial = throttleData.Config.GetEditorMaterial();
                        renderer.sharedMaterial.shader = AssetManager.GetShader(plumeComponentConfig.ShaderSettings.ShaderName);

                        if (plumeComponentConfig.ShaderSettings.ShaderName.ToLowerInvariant().Contains("volumetric"))
                        {
                            gameObject.AddComponent<PlumeVolume>();
                        }
                    }
                }
            }

            var rootObjects = new List<GameObject>();
            foreach (GameObject obj in createdObjects.Values)
            {
                obj.transform.SetParent(obj.transform.parent);

                if (obj.transform.parent == null)
                {
                    rootObjects.Add(obj);
                }
            }

            foreach (GameObject rootObject in rootObjects)
            {
                rootObject.transform.localRotation *= Quaternion.Euler(270, 0, 0);

                var masterGroup = rootObject!.AddComponent<PlumeThrottleDataMasterGroup>();
                masterGroup.GroupThrottle = 100;
                masterGroup.GroupAtmo = 1;
                rootObject!.transform.SetParent(parent != null ? parent.transform : null);
            }
        }

        /// <summary>
        /// Finds or creates a game object with the given name and parent.
        /// </summary>
        /// <param name="targetName">Name of the object to find or create.</param>
        /// <param name="parentName">Name of the parent object.</param>
        /// <param name="createdObjects">Dictionary of all created objects.</param>
        /// <param name="foundOrCreatedObject">Found or created object.</param>
        /// <returns>True if the object was created, false if it was found.</returns>
        private static bool FindOrCreateObject(
            string targetName,
            string parentName,
            ref Dictionary<string, GameObject> createdObjects,
            out GameObject foundOrCreatedObject
        )
        {
            if (createdObjects.TryGetValue(targetName, out GameObject existingObject))
            {
                foundOrCreatedObject = existingObject;

                if (parentName == null || (
                        existingObject.transform.parent != null
                        && existingObject.transform.parent.name == parentName
                    ))
                {
                    return false;
                }

                if (createdObjects.TryGetValue(parentName, out GameObject newParent))
                {
                    existingObject.transform.SetParent(newParent.transform);
                }

                return false;
            }

            var targetObject = new GameObject(targetName);

            if (parentName != null)
            {
                FindOrCreateObject(parentName, null, ref createdObjects, out GameObject parentObject);
                targetObject.transform.SetParent(parentObject.transform);
            }

            createdObjects.Add(targetName, targetObject);

            foundOrCreatedObject = targetObject;
            return true;
        }
    }
}