using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.PlanetAuthoring;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// Loads tech node icons by their Addressables address for the tech tree editor, once per address.
    /// </summary>
    /// <remarks>
    /// Stock icons resolve through the game's catalog, which the cache registers in the editor first, and the
    /// project's own through its Addressables settings. An address neither knows is remembered as missing and the node
    /// is drawn without an icon, as the editor never draws a placeholder.
    /// </remarks>
    public static class TechTreeIconCache
    {
        private static readonly Dictionary<string, Sprite> Icons = new();

        /// <summary>
        /// Gets the icon at an address.
        /// </summary>
        /// <param name="address">The node's icon address.</param>
        /// <returns>The icon, or null when no loaded catalog has it.</returns>
        public static Sprite Get(string address)
        {
            if (string.IsNullOrEmpty(address))
                return null;

            if (Icons.TryGetValue(address, out Sprite cached))
                return cached;

            // Stock icons are in the game's catalog, which the editor only has once it is registered
            bool hasStockCatalog = EditorPqsBootstrap.EnsureStockCatalog();
            Sprite icon = null;
            if (Addressables.ResourceLocators.Any(locator => locator.Locate(address, typeof(Sprite), out _)))
            {
                var handle = Addressables.LoadAssetAsync<Sprite>(address);
                icon = handle.WaitForCompletion();
            }
            else
            {
                icon = LoadProjectIcon(address);
            }

            // A miss is only remembered once the game's catalog could be asked, so a later registration finds it
            if (icon != null || hasStockCatalog)
            {
                Icons[address] = icon;
            }

            return icon;
        }

        // The project's own icons are not in any runtime catalog until its Addressables are built
        private static Sprite LoadProjectIcon(string address)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetEntry entry = settings != null
                ? settings.groups
                    .Where(group => group != null)
                    .SelectMany(group => group.entries)
                    .FirstOrDefault(candidate => candidate.address == address)
                : null;
            return entry != null ? AssetDatabase.LoadAssetAtPath<Sprite>(entry.AssetPath) : null;
        }
    }
}
