using System.Collections.Generic;
using System.Linq;
using AwesomeTechnologies.VegetationSystem;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Scatter
{
    /// <summary>
    /// Keeps scatter texture rules pointing at the same ground across a small-tile repack.
    /// </summary>
    /// <remarks>
    /// A rule stores a packed slice index, and <c>Texture2DArrayPacker</c> assigns slices as ranks
    /// among the filled cells. Filling a cell that was previously empty therefore shifts every later
    /// cell's slice by one, and every rule holding one of those numbers silently starts naming a
    /// different stratum. The only visible symptom is scatter appearing on the wrong ground.
    ///
    /// The cell index is the stable identity across a repack, so each rule is routed through it: old
    /// slice to cell, cell to new slice. Nothing else about the rule changes.
    ///
    /// Discovery is best effort. A package that lives outside the body's folder and is not in a
    /// loaded scene cannot be found and its rules will be wrong with no indication, so the pass
    /// reports what it touched.
    /// </remarks>
    public static class ScatterRuleRemap
    {
        /// <summary>
        /// Returns the slice a rule should hold after a repack.
        /// </summary>
        /// <remarks>
        /// A slice that matched no cell before the repack was already dead and stays dead.
        /// </remarks>
        /// <param name="oldSlice">The slice the rule holds now.</param>
        /// <param name="oldChannels">Per-biome slice indices from before the repack.</param>
        /// <param name="newChannels">Per-biome slice indices from after it.</param>
        /// <returns>The remapped slice, or -1 when the rule's cell no longer has one.</returns>
        public static int RemapSlice(int oldSlice, Vector4[] oldChannels, Vector4[] newChannels)
        {
            if (oldSlice < 0 || oldChannels == null || newChannels == null)
                return oldSlice;

            int channelCount = PlanetAuthoringNaming.BiomeChannels.Length;
            if (oldChannels.Length < channelCount || newChannels.Length < channelCount)
                return oldSlice;

            for (var channel = 0; channel < channelCount; channel++)
            {
                for (var layer = 0; layer < 4; layer++)
                {
                    if (Mathf.RoundToInt(oldChannels[channel][layer]) != oldSlice)
                        continue;

                    int updated = Mathf.RoundToInt(newChannels[channel][layer]);
                    return updated < 0 ? -1 : updated;
                }
            }

            return -1;
        }

        /// <summary>
        /// Rewrites every reachable scatter rule for a body after its slices were renumbered.
        /// </summary>
        /// <param name="data">The body whose tiles were repacked.</param>
        /// <param name="oldChannels">Per-biome slice indices captured before the repack.</param>
        /// <param name="newChannels">Per-biome slice indices written by the repack.</param>
        /// <returns>How many rules changed.</returns>
        public static int Apply(PQSData data, Vector4[] oldChannels, Vector4[] newChannels)
        {
            if (data == null || oldChannels == null || newChannels == null)
                return 0;

            List<VegetationPackagePro> packages = FindPackagesForBody(data);
            if (packages.Count == 0)
                return 0;

            var changed = 0;
            foreach (VegetationPackagePro package in packages)
            {
                int packageChanges = RemapPackage(package, oldChannels, newChannels);
                if (packageChanges > 0)
                {
                    EditorUtility.SetDirty(package);
                    changed += packageChanges;
                }
            }

            if (changed > 0)
            {
                // Loud on purpose. The pack button silently rewriting other assets is exactly the
                // kind of thing that should leave a trace an author can go back and read.
                UnityEngine.Debug.Log(
                    $"[Scatter] Repack renumbered surface strata, so {changed} scatter rule(s) across "
                    + $"{packages.Count} package(s) were remapped to keep pointing at the same ground.");
            }

            return changed;
        }

        /// <summary>
        /// Finds the packages that could belong to a body.
        /// </summary>
        /// <remarks>
        /// Two sources, unioned rather than preferred, because a repack has to reach every package
        /// that could be wrong afterwards rather than the single best guess a picker wants. The
        /// folder search is recursive, so packages filed under a body in any arrangement of
        /// subfolders are covered.
        /// </remarks>
        /// <param name="data">The body's PQS data.</param>
        /// <returns>The distinct packages found.</returns>
        private static List<VegetationPackagePro> FindPackagesForBody(PQSData data)
        {
            var found = new List<VegetationPackagePro>();

            foreach (VegetationSystemPro system in UnityEngine.Object.FindObjectsByType<VegetationSystemPro>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (system == null || system.VegetationPackageProList == null)
                    continue;

                PQS pqs = system.GetComponentInParent<PQS>(true);
                if (pqs == null || pqs.data != data)
                    continue;

                foreach (VegetationPackagePro package in system.VegetationPackageProList)
                {
                    if (package != null && !found.Contains(package))
                    {
                        found.Add(package);
                    }
                }
            }

            string dataPath = AssetDatabase.GetAssetPath(data);
            int slash = string.IsNullOrEmpty(dataPath) ? -1 : dataPath.LastIndexOf('/');
            if (slash < 0)
                return found;

            foreach (string guid in AssetDatabase.FindAssets(
                         "t:VegetationPackagePro", new[] { dataPath.Substring(0, slash) }))
            {
                var package = AssetDatabase.LoadAssetAtPath<VegetationPackagePro>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (package != null && !found.Contains(package))
                {
                    found.Add(package);
                }
            }

            return found;
        }

        private static int RemapPackage(VegetationPackagePro package, Vector4[] oldChannels, Vector4[] newChannels)
        {
            if (package == null || package.VegetationInfoList == null)
                return 0;

            var pending = new List<(TerrainTextureRule rule, int updated)>();
            foreach (VegetationItemInfoPro item in package.VegetationInfoList)
            {
                if (item == null)
                    continue;

                CollectChanges(item.ProceduralTextureIncludeRuleList, oldChannels, newChannels, pending);
                CollectChanges(item.ProceduralTextureExcludeRuleList, oldChannels, newChannels, pending);
            }

            if (pending.Count == 0)
                return 0;

            // Recorded only once something will actually change, so a repack that moved nothing does
            // not leave an empty entry on the undo stack.
            Undo.RecordObject(package, "Remap Scatter Texture Rules");
            foreach ((TerrainTextureRule rule, int updated) in pending)
            {
                rule.TextureIndex = updated;
            }

            return pending.Count;
        }

        private static void CollectChanges(
            List<TerrainTextureRule> rules,
            Vector4[] oldChannels,
            Vector4[] newChannels,
            List<(TerrainTextureRule, int)> pending)
        {
            if (rules == null)
                return;

            foreach (TerrainTextureRule rule in rules.Where(r => r != null))
            {
                int updated = RemapSlice(rule.TextureIndex, oldChannels, newChannels);
                if (updated != rule.TextureIndex)
                {
                    pending.Add((rule, updated));
                }
            }
        }
    }
}
