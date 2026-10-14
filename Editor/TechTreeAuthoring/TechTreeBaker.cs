using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.Modding;
using UnityEditor;
using Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Planet;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// Writes a tech tree's baked files into the project and registers them with Addressables.
    /// </summary>
    /// <remarks>
    /// Also the mod build's baker for tech trees, so every tree in a mod bakes before the mod is staged.
    /// </remarks>
    public class TechTreeBaker : IModAssetBaker
    {
        // The group Redux's own Lua patches are in
        private const string PATCHES_GROUP_NAME = "PM Patches";

        /// <inheritdoc />
        public Type AssetType => typeof(TechTreeAsset);

        /// <inheritdoc />
        List<string> IModAssetBaker.Bake(Object asset) => BakeQuietly((TechTreeAsset)asset);

        /// <summary>
        /// Gets what baking a tree is called: a patch bakes to a patch, with JSON only for the nodes it adds.
        /// </summary>
        /// <param name="tree">The tree.</param>
        /// <returns>The name of the bake action.</returns>
        public static string GetBakeLabel(TechTreeAsset tree) => tree is TechTreePatchAsset ? "Bake Patch" : "Bake JSON";

        /// <summary>
        /// Bakes a tree beside its asset, registers every file it writes, and removes the files of the last bake that
        /// this one no longer writes. A problem that stops the bake is shown in a dialog.
        /// </summary>
        /// <param name="tree">The tree to bake.</param>
        /// <returns>True if the tree baked, false if a problem stopped it.</returns>
        public static bool Bake(TechTreeAsset tree)
        {
            List<string> problems = BakeQuietly(tree);
            if (problems.Count == 0)
                return true;

            EditorUtility.DisplayDialog(GetBakeLabel(tree), $"Fix these first:\n\n{string.Join("\n", problems)}", "OK");
            return false;
        }

        /// <summary>
        /// Bakes a tree as <see cref="Bake(TechTreeAsset)" /> does, but returns what stopped it rather than showing it.
        /// </summary>
        /// <param name="tree">The tree to bake.</param>
        /// <returns>One message per problem that stopped the bake, empty when the tree baked.</returns>
        internal static List<string> BakeQuietly(TechTreeAsset tree)
        {
            string assetPath = AssetDatabase.GetAssetPath(tree);
            if (string.IsNullOrEmpty(assetPath))
                return new List<string> { "Save the tech tree as an asset before baking it." };

            List<string> problems = TechTreeBake.FindBlockingProblems(tree);
            if (problems.Count > 0)
                return problems;

            string folder = TechTreeBake.GetOutputFolder(assetPath);
            Directory.CreateDirectory(folder);
            AddressableAssetGroup group = TechTreeAuthoringAddressables.ResolveGroup(tree);
            if (group == null)
            {
                Debug.LogWarning(
                    $"[TechTreeBaker] No tech node Addressables group found for {assetPath}, so the baked files are not registered."
                );
            }

            // A tree's features live on its baked files, so files new to this bake take them from the last one. A
            // patch keeps its own, since its Lua has no labels to carry them.
            string ownerId = TechTreeBake.GetOwnerId(tree);
            var patch = tree as TechTreePatchAsset;

            // A reference that has gained tree data since the patch created its own is patched instead, as the editor
            // would on opening the patch, so the set never ends up with two
            if (patch != null && patch.TreeDataBaseline == null)
            {
                patch.TakeReferenceTreeData(TechTreeReference.LoadTreeData(patch));
            }

            (List<string> features, List<string> noFeatures) = patch != null
                ? (patch.Features.ToList(), patch.ExcludedFeatures.ToList())
                : AssetFeatureLabels.ReadAll(
                    tree.BakedFiles
                        .Select(AssetFeatureLabels.EntryOf)
                        .Where(entry => entry != null)
                        .Select(entry => entry.labels),
                    ownerId
                );

            var written = new List<string>();
            foreach (TechTreeBakeOutput output in TechTreeBake.BuildOutputs(tree, ownerId))
            {
                string path = $"{folder}/{output.FileName}";
                File.WriteAllText(path, output.Json);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                if (group != null)
                {
                    AddressablesTools.MakeAddressable(group, path, output.FileName, output.Label);
                }

                written.Add(path);
            }

            // Applying an empty set would also strip the category label Redux content is stamped with on registration
            if (features.Count > 0 || noFeatures.Count > 0)
            {
                AssetFeatureLabels.Apply(written, ownerId, features, noFeatures);
            }

            if (patch != null)
            {
                string patchPath = WritePatch(patch, assetPath, ownerId);
                if (patchPath != null)
                {
                    written.Add(patchPath);
                }
            }

            // The files of nodes removed or renamed since the last bake would otherwise stay in the game
            foreach (string stale in tree.BakedFiles.Except(written))
            {
                AssetDatabase.DeleteAsset(stale);
            }

            Undo.RecordObject(tree, "Bake Tech Tree");
            tree.BakedFiles = written;
            EditorUtility.SetDirty(tree);
            SaveBakedAssets(tree, written, assetPath, group);
            Debug.Log($"[TechTreeBaker] Baked {written.Count} file(s) for {assetPath} into {folder}.");
            return new List<string>();
        }

        // Saves only what the bake changed: the tree, a patch's sidecar, and the Addressables groups and settings its
        // files are registered in, the tree's own group too since removing stale files changes it. Other dirty assets
        // stay unsaved, as the author left them, since builds bake too.
        private static void SaveBakedAssets(
            TechTreeAsset tree,
            List<string> written,
            string assetPath,
            AddressableAssetGroup treeGroup
        )
        {
            if (treeGroup != null)
            {
                AssetDatabase.SaveAssetIfDirty(treeGroup);
            }

            AssetDatabase.SaveAssetIfDirty(tree);
            string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var script = AssetDatabase.LoadAssetAtPath<TechTreePatchScript>($"{folder}/{tree.name}_Patch.asset");
            if (script != null)
            {
                AssetDatabase.SaveAssetIfDirty(script);
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return;

            foreach (AddressableAssetGroup group in written
                         .Select(path => settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path)))
                         .Where(entry => entry != null && entry.parentGroup != null)
                         .Select(entry => entry.parentGroup)
                         .Distinct())
            {
                AssetDatabase.SaveAssetIfDirty(group);
            }

            AssetDatabase.SaveAssetIfDirty(settings);
        }

        // In a mod the patch is a sidecar the mod's build writes, as a body's orbit is. Anywhere else it is a Lua file
        // beside the asset under the project's patch label. Returns the path of a Lua file written, or null.
        private static string WritePatch(TechTreePatchAsset patch, string assetPath, string ownerId)
        {
            string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (KSP2UnityTools.FindParentMod(patch) != null)
            {
                string scriptPath = $"{folder}/{patch.name}_Patch.asset";
                var script = AssetDatabase.LoadAssetAtPath<TechTreePatchScript>(scriptPath);
                if (script == null)
                {
                    script = ScriptableObject.CreateInstance<TechTreePatchScript>();
                    script.Patch = patch;
                    AssetDatabase.CreateAsset(script, scriptPath);
                }
                else if (script.Patch != patch)
                {
                    script.Patch = patch;
                    EditorUtility.SetDirty(script);
                }

                return null;
            }

            string luaPath = $"{folder}/{TechTreePatchBake.GetPatchFileName(patch)}";
            File.WriteAllText(luaPath, TechTreePatchBake.BuildLua(patch, ownerId));
            AssetDatabase.ImportAsset(luaPath, ImportAssetOptions.ForceUpdate);

            // A patch already registered stays in its group
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetEntry existing = settings != null ? settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(luaPath)) : null;
            AddressableAssetGroup group = existing != null
                ? existing.parentGroup
                : settings?.groups.FirstOrDefault(candidate => candidate != null && candidate.Name == PATCHES_GROUP_NAME);
            if (group == null)
            {
                Debug.LogWarning($"[TechTreeBaker] No {PATCHES_GROUP_NAME} group found, so {luaPath} is not registered.");
                return luaPath;
            }

            AddressablesTools.MakeAddressable(group, luaPath, luaPath, GalaxyEntry.PROJECT_PATCH_LABEL);
            return luaPath;
        }
    }
}
