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

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// Writes a tech tree's baked files into the project and registers them with Addressables.
    /// </summary>
    /// <remarks>
    /// A patch in a mod can be exported: its Lua is written into the mod's copied files for the author to edit by
    /// hand, and the mod build stops baking it.
    /// </remarks>
    public static class TechTreeBaker
    {
        // The group Redux's own Lua patches are in
        private const string PATCHES_GROUP_NAME = "PM Patches";

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
            if (tree is TechTreePatchAsset { BakeOnBuild: false } patch && !ConfirmExportOverwrite(patch, GetBakeLabel(tree)))
                return false;

            return BakeAndShowProblems(tree);
        }

        /// <summary>
        /// Exports a patch in a mod for hand editing, or takes it back to being baked by the mod build.
        /// </summary>
        /// <remarks>
        /// Exporting bakes the patch and writes its Lua into the mod's copied files. Taking it back deletes that file.
        /// Either asks first when the file was edited since the patch last wrote it.
        /// </remarks>
        /// <param name="patch">The patch.</param>
        /// <param name="bakeOnBuild">True to have the mod build bake the patch, false to export it.</param>
        /// <returns>True if the patch now bakes as asked, false if the author cancelled or the export bake failed.</returns>
        public static bool SetBakeOnBuild(TechTreePatchAsset patch, bool bakeOnBuild)
        {
            if (patch.BakeOnBuild == bakeOnBuild)
                return true;

            string exportPath = GetExportPath(patch);
            if (exportPath == null || !ConfirmExportOverwrite(patch, bakeOnBuild ? "Bake on Build" : "Export Patch"))
                return false;

            patch.BakeOnBuild = bakeOnBuild;
            EditorUtility.SetDirty(patch);
            if (!bakeOnBuild && !BakeAndShowProblems(patch))
            {
                patch.BakeOnBuild = true;
                EditorUtility.SetDirty(patch);
                AssetDatabase.SaveAssetIfDirty(patch);
                return false;
            }

            if (bakeOnBuild)
            {
                AssetDatabase.DeleteAsset(exportPath);
                TechTreePatchScript script = FindPatchScript(patch);
                if (script != null)
                {
                    script.ExportedHash = null;
                    EditorUtility.SetDirty(script);
                    AssetDatabase.SaveAssetIfDirty(script);
                }
            }

            AssetDatabase.SaveAssetIfDirty(patch);
            return true;
        }

        /// <summary>
        /// Gets where a patch's exported Lua goes: the mod's copied patches, which the mod build copies to the same
        /// path in the mod that the patch's sidecar generates.
        /// </summary>
        /// <param name="patch">The patch.</param>
        /// <returns>The asset path, or null when the patch is not in a mod.</returns>
        public static string GetExportPath(TechTreePatchAsset patch)
        {
            Mod mod = KSP2UnityTools.FindParentMod(patch);
            return mod != null ? $"{mod.Folder}/Copied/Patches/{TechTreePatchBake.GetPatchFileName(patch)}" : null;
        }

        /// <summary>
        /// Checks whether a file was changed since it was written with the hash.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <param name="writtenHash">The hash of what was written, or null when nothing was.</param>
        /// <returns>True if the file exists and differs from what was written, false otherwise.</returns>
        internal static bool IsEditedSince(string path, string writtenHash) =>
            File.Exists(path) && HashText(File.ReadAllText(path)) != writtenHash;

        // Line endings are left out, so a checkout that converts them is not taken for an edit
        internal static string HashText(string text) => Hash128.Compute(text.Replace("\r\n", "\n")).ToString();

        // True unless the exported Lua was edited since the patch wrote it and the author keeps the edits
        private static bool ConfirmExportOverwrite(TechTreePatchAsset patch, string title)
        {
            string path = GetExportPath(patch);
            if (path == null || !IsEditedSince(path, FindPatchScript(patch)?.ExportedHash))
                return true;

            return EditorUtility.DisplayDialog(
                title,
                $"{path} was edited since the patch last wrote it. Your edits will be lost.",
                "Discard Edits",
                "Cancel"
            );
        }

        private static bool BakeAndShowProblems(TechTreeAsset tree)
        {
            List<string> problems = BakeQuietly(tree);
            if (problems.Count == 0)
                return true;

            EditorUtility.DisplayDialog(GetBakeLabel(tree), $"Fix these first:\n\n{string.Join("\n", problems)}", "OK");
            return false;
        }

        // The sidecar sits beside its patch
        private static string GetPatchScriptPath(TechTreeAsset tree)
        {
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(tree))?.Replace('\\', '/');
            return $"{folder}/{tree.name}_Patch.asset";
        }

        private static TechTreePatchScript FindPatchScript(TechTreeAsset tree) =>
            AssetDatabase.LoadAssetAtPath<TechTreePatchScript>(GetPatchScriptPath(tree));

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
            TechTreePatchScript script = FindPatchScript(tree);
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

        // In a mod the patch is a sidecar the mod's build writes, as a body's orbit is, or once exported a Lua file in
        // the mod's copied files. Anywhere else it is a Lua file beside the asset under the project's patch label.
        // Returns the path of a Lua file the bake owns, or null.
        private static string WritePatch(TechTreePatchAsset patch, string assetPath, string ownerId)
        {
            string exportPath = GetExportPath(patch);
            if (exportPath != null)
            {
                string scriptPath = GetPatchScriptPath(patch);
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

                // The exported file is the author's from here, so a later bake never sweeps it away as stale
                if (!patch.BakeOnBuild)
                {
                    string lua = TechTreePatchBake.BuildLua(patch, ownerId);
                    Directory.CreateDirectory(Path.GetDirectoryName(exportPath)!);
                    File.WriteAllText(exportPath, lua);
                    AssetDatabase.ImportAsset(exportPath, ImportAssetOptions.ForceUpdate);
                    script.ExportedHash = HashText(lua);
                    EditorUtility.SetDirty(script);
                }

                return null;
            }

            string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
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
