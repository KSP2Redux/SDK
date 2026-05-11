using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.Modding;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring
{
    /// <summary>
    /// Shared helpers for resolving the addressables group every planet-authoring artifact registers
    /// itself into (celestial body JSON, baked science regions, baked decals, etc.).
    /// </summary>
    /// <remarks>
    /// Priority: a parent <see cref="Mod" />'s <c>celestialBodiesGroup</c> when one exists, otherwise
    /// a project-level addressables group named <see cref="CelestialBodiesGroupName" /> (the same
    /// group the Create Celestial Body wizard offers to create for non-mod projects). Centralized
    /// here so the wizard, science region baker, and body JSON exporter share one source of truth.
    /// Per-asset Mod lookups and the project-level group lookup are cached to avoid repeated
    /// AssetDatabase / settings.groups scans on every bake or registration.
    /// </remarks>
    internal static class PlanetAuthoringAddressables
    {
        /// <summary>
        /// Name of the project-level addressables group the wizard creates and every artifact-export
        /// flow falls back to when there is no parent mod.
        /// </summary>
        public const string CelestialBodiesGroupName = "Celestial Bodies";

        // Parent-mod resolution is an AssetDatabase round-trip per call, so cache by asset path so
        // bake / register cycles don't pay it repeatedly. AuthoringSidecarBootstrap invalidates
        // on any postprocess pass.
        private static readonly Dictionary<string, Mod> ModCache = new();

        // The project-level group is also stable across calls. Refetched when null or Unity-null.
        private static AddressableAssetGroup _celestialBodiesGroup;

        // Once-per-session flag so the "no settings configured" warning doesn't spam every bake.
        private static bool _warnedMissingSettings;

        /// <summary>
        /// Drops every cached lookup.
        /// </summary>
        /// <remarks>
        /// Called by the AssetPostprocessor on any asset movement.
        /// </remarks>
        public static void InvalidateCaches()
        {
            ModCache.Clear();
            _celestialBodiesGroup = null;
            _warnedMissingSettings = false;
        }

        /// <summary>
        /// Resolves the addressables group to register celestial-body artifacts into.
        /// </summary>
        /// <remarks>
        /// Tries the parent mod's <c>celestialBodiesGroup</c> first, then falls back to the
        /// project-level group named <see cref="CelestialBodiesGroupName" />.
        /// </remarks>
        /// <param name="asset">The asset whose parent mod (if any) is consulted before the project-level fallback.</param>
        /// <returns>The resolved addressables group, or null when no parent mod group and no project-level group exists.</returns>
        public static AddressableAssetGroup ResolveCelestialBodiesGroup(Object asset)
        {
            var mod = FindCachedParentMod(asset);
            if (mod != null && mod.celestialBodiesGroup != null) return mod.celestialBodiesGroup;

            if (_celestialBodiesGroup != null) return _celestialBodiesGroup;

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                if (!_warnedMissingSettings)
                {
                    Debug.LogWarning("[PlanetAuthoringAddressables] No AddressableAssetSettings configured. Bake outputs will not register as addressables until you initialize addressables via Window > Asset Management > Addressables > Groups.");
                    _warnedMissingSettings = true;
                }
                return null;
            }

            _celestialBodiesGroup = settings.groups.FirstOrDefault(g => g != null && g.Name == CelestialBodiesGroupName);
            return _celestialBodiesGroup;
        }

        private static Mod FindCachedParentMod(Object asset)
        {
            if (asset == null) return null;
            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path)) return null;
            if (ModCache.TryGetValue(path, out var cached)) return cached;
            var mod = KSP2UnityTools.FindParentMod(asset);
            ModCache[path] = mod;
            return mod;
        }
    }
}
