using System.Collections.Generic;
using System.IO;
using System.Text;
using KSP;
using Ksp2UnityTools.Editor.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Tools
{
    /// <summary>
    /// Syncs the containing folder name, prefab file name, JSON / icon file names, and addressable
    /// addresses when a part's <c>partName</c> changes, behind a confirmation dialog.
    /// </summary>
    /// <remarks>
    /// The SDK's address and filename conventions are derived from partName: the part lives in a
    /// folder named <c>{partName}</c>, the prefab is <c>{partName}.prefab</c> with address
    /// <c>{partName}</c>, the JSON sidecar is <c>{partName}.json</c> with address
    /// <c>{partName}.json</c>, the icon is <c>{partName}_icon.png</c> with address
    /// <c>{partName}_icon.png</c>. This helper renames only the items whose current name matches
    /// the canonical pattern, so manually-renamed files / folders are left alone.
    /// </remarks>
    public static class PartAddressRenameHelper
    {
        /// <summary>Single address or file rename item inside a <see cref="RenamePlan" />.</summary>
        public readonly struct RenameItem
        {
            /// <summary>Display label for the dialog (Folder, Prefab, JSON, Icon).</summary>
            public string Kind { get; }
            /// <summary>The asset's GUID. Used to re-resolve the current path after an earlier folder rename.</summary>
            public string Guid { get; }
            /// <summary>The path the asset had when the plan was built. Used for dialog display only.</summary>
            public string OldAssetPath { get; }
            /// <summary>Current addressable address, or null if the asset is not addressable.</summary>
            public string OldAddress { get; }
            /// <summary>Target addressable address. Equal to <see cref="OldAddress" /> when the address is not being changed.</summary>
            public string NewAddress { get; }
            /// <summary>New leaf name (folder name or filename incl. extension), or null if the file is not being renamed.</summary>
            public string NewFileName { get; }

            public RenameItem(string kind, string guid, string oldAssetPath, string oldAddress, string newAddress, string newFileName)
            {
                Kind = kind;
                Guid = guid;
                OldAssetPath = oldAssetPath;
                OldAddress = oldAddress;
                NewAddress = newAddress;
                NewFileName = newFileName;
            }

            /// <summary>Whether this item changes the addressable address.</summary>
            public bool AddressChanges => OldAddress != null && NewAddress != null && OldAddress != NewAddress;
            /// <summary>Whether this item renames the file or folder on disk.</summary>
            public bool FileRenames => !string.IsNullOrEmpty(NewFileName);
        }

        /// <summary>The set of changes a partName rename would apply.</summary>
        public readonly struct RenamePlan
        {
            /// <summary>The previous part name.</summary>
            public string OldName { get; }
            /// <summary>The new part name.</summary>
            public string NewName { get; }
            /// <summary>Rename items the user will be asked to confirm.</summary>
            public IReadOnlyList<RenameItem> Items { get; }
            /// <summary>Conflict messages (e.g. target name already exists) surfaced in the dialog.</summary>
            public IReadOnlyList<string> Conflicts { get; }
            /// <summary>True when the plan has at least one rename item to apply.</summary>
            public bool HasWork => Items != null && Items.Count > 0;

            public RenamePlan(string oldName, string newName, IReadOnlyList<RenameItem> items, IReadOnlyList<string> conflicts)
            {
                OldName = oldName;
                NewName = newName;
                Items = items;
                Conflicts = conflicts;
            }
        }

        /// <summary>
        /// Builds the rename plan for a partName change from <paramref name="oldName" /> to <paramref name="newName" />.
        /// </summary>
        /// <param name="target">The part whose name is changing.</param>
        /// <param name="oldName">The previous partName.</param>
        /// <param name="newName">The new partName.</param>
        /// <returns>The plan. Check <see cref="RenamePlan.HasWork" /> to see if any sync is needed.</returns>
        public static RenamePlan PlanRename(CorePartData target, string oldName, string newName)
        {
            var items = new List<RenameItem>();
            var conflicts = new List<string>();
            var empty = new RenamePlan(oldName, newName, items, conflicts);

            if (target == null) return empty;
            if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName)) return empty;
            if (oldName == newName) return empty;

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return empty;

            var prefabPath = PathUtils.GetPrefabOrAssetPath(target, target.gameObject);
            if (string.IsNullOrEmpty(prefabPath)) return empty;

            var folder = Path.GetDirectoryName(prefabPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder)) return empty;

            TryAddFolderItem(folder, oldName, newName, items, conflicts);
            TryAddPrefabItem(settings, prefabPath, folder, oldName, newName, items, conflicts);
            TryAddFileItem(settings, folder, $"{oldName}.json", $"{newName}.json", "JSON", items, conflicts);
            TryAddFileItem(settings, folder, $"{oldName}_icon.png", $"{newName}_icon.png", "Icon", items, conflicts);

            return new RenamePlan(oldName, newName, items, conflicts);
        }

        /// <summary>
        /// Shows a confirmation dialog summarising <paramref name="plan" />, then applies the rename if the user confirms.
        /// </summary>
        /// <param name="plan">The plan returned by <see cref="PlanRename" />.</param>
        /// <param name="showDialog">When false, skips the dialog and applies immediately.</param>
        /// <returns>True if the plan was applied or had no work, false if the user cancelled.</returns>
        public static bool ConfirmAndApply(RenamePlan plan, bool showDialog = true)
        {
            if (!plan.HasWork) return true;

            if (showDialog)
            {
                var go = EditorUtility.DisplayDialog(
                    "Sync Part Addresses?",
                    BuildDialogMessage(plan),
                    "Rename",
                    "Skip");
                if (!go) return false;
            }

            Apply(plan);
            return true;
        }

        private static void TryAddFolderItem(string folder, string oldName, string newName, List<RenameItem> items, List<string> conflicts)
        {
            var leaf = Path.GetFileName(folder);
            if (leaf != oldName) return;
            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;
            var newFolderPath = $"{parent}/{newName}";
            if (AssetDatabase.IsValidFolder(newFolderPath))
            {
                conflicts.Add($"Folder: '{newName}' already exists at '{parent}', skipped");
                return;
            }
            var guid = AssetDatabase.AssetPathToGUID(folder);
            if (string.IsNullOrEmpty(guid)) return;
            items.Add(new RenameItem("Folder", guid, folder, null, null, newName));
        }

        private static void TryAddPrefabItem(
            AddressableAssetSettings settings,
            string prefabPath,
            string folder,
            string oldName,
            string newName,
            List<RenameItem> items,
            List<string> conflicts)
        {
            var guid = AssetDatabase.AssetPathToGUID(prefabPath);
            if (string.IsNullOrEmpty(guid)) return;

            var entry = settings.FindAssetEntry(guid);
            var oldAddress = entry?.address;
            var newAddress = entry != null && entry.address == $"{oldName}.prefab" ? $"{newName}.prefab" : oldAddress;

            string newFileName = null;
            if (Path.GetFileNameWithoutExtension(prefabPath) == oldName)
            {
                var newPrefabPath = $"{folder}/{newName}.prefab";
                if (File.Exists(newPrefabPath))
                {
                    conflicts.Add($"Prefab: '{newName}.prefab' already exists, skipped");
                }
                else
                {
                    newFileName = $"{newName}.prefab";
                }
            }

            if (newFileName == null && (oldAddress == null || oldAddress == newAddress)) return;
            items.Add(new RenameItem("Prefab", guid, prefabPath, oldAddress, newAddress, newFileName));
        }

        private static void TryAddFileItem(
            AddressableAssetSettings settings,
            string folder,
            string oldFileName,
            string newFileName,
            string kind,
            List<RenameItem> items,
            List<string> conflicts)
        {
            var oldPath = $"{folder}/{oldFileName}";
            var newPath = $"{folder}/{newFileName}";
            if (!File.Exists(oldPath)) return;
            if (File.Exists(newPath))
            {
                conflicts.Add($"{kind}: '{newFileName}' already exists, skipped");
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(oldPath);
            if (string.IsNullOrEmpty(guid)) return;
            var entry = settings.FindAssetEntry(guid);
            var oldAddress = entry?.address;
            var newAddress = entry != null && entry.address == oldFileName ? newFileName : oldAddress;
            items.Add(new RenameItem(kind, guid, oldPath, oldAddress, newAddress, newFileName));
        }

        private static string BuildDialogMessage(RenamePlan plan)
        {
            var sb = new StringBuilder();
            sb.Append("Part renamed: '").Append(plan.OldName).Append("' -> '").Append(plan.NewName).AppendLine("'");
            sb.AppendLine();
            sb.AppendLine("Will update:");
            foreach (var item in plan.Items)
            {
                if (item.FileRenames)
                {
                    var kindLabel = item.Kind == "Folder" ? "Folder" : $"{item.Kind} file";
                    sb.Append("- ").Append(kindLabel).Append(" '").Append(Path.GetFileName(item.OldAssetPath))
                      .Append("' -> '").Append(item.NewFileName).AppendLine("'");
                }
                if (item.AddressChanges)
                {
                    sb.Append("- ").Append(item.Kind).Append(" address '").Append(item.OldAddress)
                      .Append("' -> '").Append(item.NewAddress).AppendLine("'");
                }
            }
            if (plan.Conflicts != null && plan.Conflicts.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Skipped:");
                foreach (var c in plan.Conflicts) sb.Append("- ").AppendLine(c);
            }
            return sb.ToString();
        }

        private static void Apply(RenamePlan plan)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return;

            // Folder rename runs first so subsequent path resolutions via GUID reflect the new location.
            foreach (var item in plan.Items)
            {
                if (item.Kind == "Folder" && item.FileRenames)
                {
                    RenameByGuid(item);
                }
            }

            foreach (var item in plan.Items)
            {
                if (item.Kind != "Folder" && item.FileRenames)
                {
                    RenameByGuid(item);
                }
            }

            foreach (var item in plan.Items)
            {
                if (!item.AddressChanges) continue;
                var entry = settings.FindAssetEntry(item.Guid);
                if (entry == null) continue;
                entry.address = item.NewAddress;
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true);
            }

            AssetDatabase.SaveAssets();
        }

        private static void RenameByGuid(RenameItem item)
        {
            var currentPath = AssetDatabase.GUIDToAssetPath(item.Guid);
            if (string.IsNullOrEmpty(currentPath)) return;
            // Folders have no extension to strip. GetFileNameWithoutExtension would truncate at the first dot.
            var newName = item.Kind == "Folder"
                ? item.NewFileName
                : Path.GetFileNameWithoutExtension(item.NewFileName);
            var error = AssetDatabase.RenameAsset(currentPath, newName);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"[PartAddressRenameHelper] Failed to rename '{currentPath}' to '{item.NewFileName}': {error}");
            }
        }
    }
}
