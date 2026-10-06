using System;
using System.Collections.Generic;
using System.IO;
using KSP;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.Modding;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.ResourceMaps;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Planet
{
    /// <summary>
    /// Where a body's place in the galaxy stands.
    /// </summary>
    public enum GalaxyEntryState
    {
        /// <summary>The body has no orbit, or no parent to orbit.</summary>
        NoOrbit,

        /// <summary>The body is in a mod, whose build writes the patch.</summary>
        BuiltWithMod,

        /// <summary>The project's checked-in patch has not been written.</summary>
        Missing,

        /// <summary>The project's checked-in patch differs from the orbit.</summary>
        OutOfDate,

        /// <summary>The project's checked-in patch matches the orbit.</summary>
        Written,
    }

    /// <summary>
    /// Puts a body in the default galaxy: the patch built into a mod, or the checked-in patch a project without one
    /// keeps beside the body.
    /// </summary>
    public static class GalaxyEntry
    {
        /// <summary>
        /// The addressables label Redux's own Lua patches load from.
        /// </summary>
        /// <remarks>
        /// Set by ReduxInternalModRegister as Redux's AddressableScriptLabel. A body outside any mod is Redux's own.
        /// </remarks>
        public const string PROJECT_PATCH_LABEL = "redux_patches";

        // Stock bodies, for suggesting a parent in a project that has no localization listing its bodies.
        private static readonly string[] STOCK_BODIES =
        {
            "Kerbol", "Moho", "Eve", "Gilly", "Kerbin", "Mun", "Minmus", "Duna", "Ike", "Dres",
            "Jool", "Laythe", "Vall", "Tylo", "Bop", "Pol", "Eeloo",
        };

        /// <summary>
        /// Gets the body's mod, or null for a body that belongs to the project itself.
        /// </summary>
        /// <param name="orbit">The body's orbit.</param>
        /// <returns>The mod, or null.</returns>
        public static Mod FindMod(CelestialBodyOrbitAuthoring orbit) => orbit != null ? KSP2UnityTools.FindParentMod(orbit) : null;

        /// <summary>
        /// Gets the path of the checked-in patch for a body outside any mod.
        /// </summary>
        /// <param name="orbit">The body's orbit.</param>
        /// <returns>The asset path, beside the body's Scaled prefab.</returns>
        public static string ProjectPatchPath(CelestialBodyOrbitAuthoring orbit)
        {
            string prefabPath = AssetDatabase.GetAssetPath(orbit.ScaledPrefab);
            return $"{Path.GetDirectoryName(prefabPath)?.Replace('\\', '/')}/{orbit.PatchFileName}";
        }

        /// <summary>
        /// Works out where the body's galaxy entry stands.
        /// </summary>
        /// <param name="orbit">The body's orbit, or null when it has none.</param>
        /// <returns>The entry's state.</returns>
        public static GalaxyEntryState GetState(CelestialBodyOrbitAuthoring orbit)
        {
            if (orbit == null || !orbit.ShouldGenerate)
                return GalaxyEntryState.NoOrbit;
            if (FindMod(orbit) != null)
                return GalaxyEntryState.BuiltWithMod;

            string path = ProjectPatchPath(orbit);
            if (!File.Exists(path))
                return GalaxyEntryState.Missing;

            return File.ReadAllText(path) == orbit.Generate() ? GalaxyEntryState.Written : GalaxyEntryState.OutOfDate;
        }

        /// <summary>
        /// Writes the checked-in patch for a body outside any mod, and registers it with the project's patch label.
        /// </summary>
        /// <param name="orbit">The body's orbit.</param>
        /// <returns>The message to show.</returns>
        public static string WriteProjectPatch(CelestialBodyOrbitAuthoring orbit)
        {
            if (orbit == null || !orbit.ShouldGenerate)
                return "Set a parent body before writing the galaxy patch.";

            Mod mod = FindMod(orbit);
            if (mod != null)
                return $"{orbit.BodyName} is in the mod {mod.id}, whose build writes the patch as {orbit.PathInMod}.";

            string path = ProjectPatchPath(orbit);
            File.WriteAllText(path, orbit.Generate());
            AssetDatabase.ImportAsset(path);

            // A patch already registered stays in its group, such as Redux's own patches in the Redux group.
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetEntry existing = settings != null ? settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path)) : null;
            AddressableAssetGroup group = existing != null
                ? existing.parentGroup
                : PlanetAuthoringAddressables.ResolveCelestialBodiesGroup(orbit.ScaledPrefab);
            if (group == null)
                return $"Wrote {path}. No Celestial Bodies addressables group was found, so give it the {PROJECT_PATCH_LABEL} label by hand.";

            AddressablesTools.MakeAddressable(group, path, path, PROJECT_PATCH_LABEL);
            AssetDatabase.SaveAssets();
            return $"Wrote {path}.";
        }

        /// <summary>
        /// Gets the names of the bodies a body could orbit: stock's, those the project's localization lists, and the
        /// project's own.
        /// </summary>
        /// <param name="exclude">A body name to leave out, usually the body being edited.</param>
        /// <returns>The names, sorted.</returns>
        public static List<string> KnownBodyNames(string exclude)
        {
            var names = new SortedSet<string>(STOCK_BODIES, StringComparer.OrdinalIgnoreCase);
            names.UnionWith(ResourceMapCatalog.GetCelestialBodyNames());
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab Celestial"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(PlanetAuthoringNaming.ScaledPrefabSuffix))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                string bodyName = prefab != null && prefab.TryGetComponent(out CoreCelestialBodyData body) ? body.Core?.data?.bodyName : null;
                if (!string.IsNullOrEmpty(bodyName))
                {
                    names.Add(bodyName);
                }
            }

            names.Remove(exclude ?? string.Empty);
            return new List<string>(names);
        }
    }
}
