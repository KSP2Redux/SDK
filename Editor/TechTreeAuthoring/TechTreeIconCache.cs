using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// Loads tech node icons by their Addressables address for the tech tree editor, once per address.
    /// </summary>
    /// <remarks>
    /// Stock icons resolve when the editor has the game's catalog loaded. An address no catalog knows is remembered as
    /// missing and the node is drawn without an icon, as the editor never draws a placeholder.
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

            Sprite icon = null;
            if (Addressables.ResourceLocators.Any(locator => locator.Locate(address, typeof(Sprite), out _)))
            {
                var handle = Addressables.LoadAssetAsync<Sprite>(address);
                icon = handle.WaitForCompletion();
            }

            Icons[address] = icon;
            return icon;
        }
    }
}
