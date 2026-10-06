using System;
using System.Collections.Generic;
using KSP;
using KSP.VolumeCloud;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Clouds
{
    /// <summary>
    /// Copies a stock body's cloud settings onto a cloud configuration.
    /// </summary>
    /// <remarks>
    /// The source is the stock body's High quality configuration, read out of the game's bundles. Every setting is
    /// copied except textures, which are bundle assets a project asset cannot reference, and the planet radius, which
    /// belongs to the target body. Stock measures layer heights from its own planet radius, which sits a few
    /// kilometers under the body's sea level, so heights are converted to heights above sea level on the way.
    /// </remarks>
    public static class CloudPresets
    {
        /// <summary>
        /// The stock bodies that ship cloud configurations.
        /// </summary>
        public static readonly string[] STOCK_BODIES = { "Kerbin", "Laythe", "Eve", "Duna", "Jool" };

        // Fields the preset leaves alone: the asset's identity, what belongs to the target body, the layer order that
        // is rebuilt afterwards, and the box and lenticular clouds the tools do not author.
        private static readonly HashSet<string> SKIPPED_FIELDS = new()
        {
            "m_Script",
            "m_Name",
            "m_ObjectHideFlags",
            "bodyName",
            "planetRadius",
            "cumulusIndex",
            "lenticularList",
            "boxCloudsGroupList",
        };

        /// <summary>
        /// Converts a stock layer height to a height above the body's sea level.
        /// </summary>
        /// <param name="height">The height above the configuration's planet radius, in meters.</param>
        /// <param name="configurationRadius">The configuration's planet radius, in meters.</param>
        /// <param name="bodyRadius">The body's sea-level radius, in meters.</param>
        /// <returns>The height above sea level, in meters.</returns>
        public static float ToSeaLevelHeight(float height, float configurationRadius, double bodyRadius) =>
            (float)(height + configurationRadius - bodyRadius);

        /// <summary>
        /// Copies a stock body's cloud settings onto a configuration.
        /// </summary>
        /// <param name="target">The configuration to overwrite.</param>
        /// <param name="stockBody">The stock body to copy, one of <see cref="STOCK_BODIES" />.</param>
        /// <param name="message">A status line describing the outcome or the reason for failure.</param>
        /// <returns>True if the settings were copied, false otherwise.</returns>
        public static bool TryApply(VolumeCloudConfiguration target, string stockBody, out string message)
        {
            if (target == null)
            {
                message = "No cloud configuration to apply the preset to.";
                return false;
            }

            if (!EditorPqsBootstrap.EnsureStockCatalog())
            {
                message = "The base-game catalog is not registered. Run 'ThunderKit > Import Ksp2 To Editor'.";
                return false;
            }

            var handles = new List<AsyncOperationHandle>();
            try
            {
                VolumeCloudConfiguration stock = LoadStockConfiguration(stockBody, handles);
                double bodyRadius = LoadStockRadius(stockBody, handles);
                if (stock == null || bodyRadius <= 0.0)
                {
                    message = $"{stockBody}'s stock clouds could not be loaded.";
                    return false;
                }

                Undo.RecordObject(target, $"Apply {stockBody} Clouds");
                CopySettings(stock, target);
                foreach (VolumeCloudConfiguration.CumulusData layer in target.cumulusList)
                {
                    layer.cloudHeightRange = new Vector2(
                        ToSeaLevelHeight(layer.cloudHeightRange.x, stock.planetRadius, bodyRadius),
                        ToSeaLevelHeight(layer.cloudHeightRange.y, stock.planetRadius, bodyRadius)
                    );
                }

                CloudSetup.DeriveLayers(target);
                EditorUtility.SetDirty(target);
                message = $"Applied {stockBody}'s clouds. Its textures are not copied, so each layer keeps its own distribution map and noise.";
                return true;
            }
            finally
            {
                foreach (AsyncOperationHandle handle in handles)
                {
                    Addressables.Release(handle);
                }
            }
        }

        private static VolumeCloudConfiguration LoadStockConfiguration(string stockBody, List<AsyncOperationHandle> handles)
        {
            GameObject local = Load<GameObject>($"Celestial.{stockBody}.Simulation.prefab", handles);
            CloudRenderHelper helper = local != null ? local.GetComponentInChildren<CloudRenderHelper>(true) : null;
            if (helper == null || helper.HighQualityCloudConfiguration == null || !helper.HighQualityCloudConfiguration.RuntimeKeyIsValid())
                return null;

            return Load<VolumeCloudConfiguration>(helper.HighQualityCloudConfiguration.RuntimeKey, handles);
        }

        private static double LoadStockRadius(string stockBody, List<AsyncOperationHandle> handles)
        {
            GameObject scaled = Load<GameObject>($"Celestial.{stockBody}.Scaled.prefab", handles);
            CoreCelestialBodyData data = scaled != null ? scaled.GetComponentInChildren<CoreCelestialBodyData>(true) : null;
            return data != null && data.Data != null ? data.Data.radius : 0.0;
        }

        private static T Load<T>(object key, List<AsyncOperationHandle> handles) where T : UnityEngine.Object
        {
            // Asking for locations first keeps a missing key from logging an InvalidKeyException.
            var locations = Addressables.LoadResourceLocationsAsync(key, typeof(T));
            locations.WaitForCompletion();
            bool found = locations.Result is { Count: > 0 };
            Addressables.Release(locations);
            if (!found)
                return null;

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
            handles.Add(handle);
            return handle.WaitForCompletion();
        }

        private static void CopySettings(VolumeCloudConfiguration source, VolumeCloudConfiguration target)
        {
            var from = new SerializedObject(source);
            var to = new SerializedObject(target);
            SerializedProperty property = from.GetIterator();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = true;
                if (SKIPPED_FIELDS.Contains(RootField(property.propertyPath)))
                {
                    enterChildren = false;
                    continue;
                }

                if (property.propertyType == SerializedPropertyType.ObjectReference)
                    continue;

                if (property.isArray && property.propertyType == SerializedPropertyType.Generic)
                {
                    SerializedProperty array = to.FindProperty(property.propertyPath);
                    if (array != null)
                    {
                        array.arraySize = property.arraySize;
                    }

                    continue;
                }

                if (property.propertyType is SerializedPropertyType.Generic or SerializedPropertyType.ArraySize)
                    continue;

                SerializedProperty destination = to.FindProperty(property.propertyPath);
                if (destination == null || destination.propertyType != property.propertyType)
                    continue;

                try
                {
                    destination.boxedValue = property.boxedValue;
                }
                catch (Exception)
                {
                    // A property type boxedValue cannot carry is left at the target's value.
                }

                enterChildren = property.propertyType != SerializedPropertyType.String;
            }

            to.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string RootField(string propertyPath)
        {
            int dot = propertyPath.IndexOf('.');
            return dot < 0 ? propertyPath : propertyPath.Substring(0, dot);
        }
    }
}
