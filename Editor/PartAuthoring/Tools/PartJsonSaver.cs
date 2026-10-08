using System;
using System.IO;
using System.Reflection;
using KSP;
using KSP.IO;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Tools
{
    /// <summary>
    /// Serialises a <see cref="CorePartData" /> and its module data blocks to the part's JSON sidecar.
    /// </summary>
    /// <remarks>
    /// Walks every <see cref="PartBehaviourModule" /> on the GameObject, invokes the non-public
    /// <c>AddDataModules</c> and <c>RebuildDataContext</c> reflection hooks so each module rebuilds
    /// its serialised state, then pretty-prints the result through Newtonsoft. Registers the file
    /// with the parent mod's addressables group when one is reachable.
    /// </remarks>
    public static class PartJsonSaver
    {
        private static bool _initialized;

        /// <summary>
        /// Saves <paramref name="target" />'s JSON sidecar next to its prefab and shows a
        /// confirmation dialog with the resulting path.
        /// </summary>
        /// <remarks>
        /// No-op when <paramref name="target" /> is null, has no <see cref="PartCore" />, or its
        /// owning prefab path cannot be resolved.
        /// </remarks>
        /// <param name="target">The part to serialise.</param>
        public static void Save(CorePartData target, bool showDialog = true)
        {
            if (target == null || target.Core == null) return;

            var prefabPath = PathUtils.GetPrefabOrAssetPath(target, target.gameObject);
            if (string.IsNullOrEmpty(prefabPath)) return;

            // Use the canonical partName from the data so scene-instance renames don't fragment exports
            // into new files. Matches the addressable key constructed below.
            var partName = !string.IsNullOrEmpty(target.Core.data.partName) ? target.Core.data.partName : target.name;
            var path = Path.GetDirectoryName(prefabPath) + $"/{partName}.json";

            if (!_initialized)
            {
                IOProvider.Init();
                _initialized = true;
            }

            target.Core.data.serializedPartModules.Clear();
            foreach (var child in target.gameObject.GetComponents<Component>())
            {
                if (child is not PartBehaviourModule partBehaviourModule) continue;

                EditorModuleDataHydrator.Hydrate(child);

                foreach (var data in partBehaviourModule.DataModules.Values)
                {
                    // Reflection on private RebuildDataContext is fragile against stock-game API changes.
                    var rebuildMethod =
                        data.GetType().GetMethod("RebuildDataContext", BindingFlags.Instance | BindingFlags.NonPublic) ??
                        data.GetType().GetMethod("RebuildDataContext", BindingFlags.Instance | BindingFlags.Public);
                    rebuildMethod?.Invoke(data, Array.Empty<object>());
                }

                target.Core.data.serializedPartModules.Add(new SerializedPartModule(partBehaviourModule, false));
            }

            var json = IOProvider.ToJson(target.Core);
            var jObject = JObject.Parse(json);
            json = jObject.ToString(Formatting.Indented);
            var directoryName = new FileInfo(path).DirectoryName;
            Directory.CreateDirectory(directoryName);
            File.WriteAllText(path, json);
            AssetDatabase.ImportAsset(path);

            var madeAddressable = false;
            var group = PartAuthoringAddressables.ResolveGroup(target);
            if (group != null)
            {
                madeAddressable = true;
                AddressablesTools.MakeAddressable(
                    group,
                    path,
                    $"{target.Core.data.partName}.json",
                    "parts_data"
                );
            }

            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Part Exported",
                    !madeAddressable
                        ? $"Json is at: {path}, you need to manually make it addressable"
                        : $"Json is at: {path}",
                    "OK"
                );
            }
        }
    }
}
