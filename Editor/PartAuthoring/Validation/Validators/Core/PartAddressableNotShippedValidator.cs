using System.Collections.Generic;
using System.IO;
using KSP;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.Modding;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Errors when the part's prefab, JSON or icon is addressable in a group the build leaves out.
    /// </summary>
    /// <remarks>
    /// A mod build switches off every group the mod does not own, the Default Local Group
    /// included, so an asset made addressable from its inspector checkbox lands in a group that
    /// never ships. Outside a mod the check is the group's own Include in Build setting. The fix
    /// moves the entry, keeping its address and labels.
    /// </remarks>
    public class PartAddressableNotShippedValidator : IPartValidator
    {
        /// <summary>Stable code emitted for each part asset sitting in a group that does not ship.</summary>
        public const string Code = "PART_ADDRESSABLE_NOT_SHIPPED";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            CorePartData part = context?.Part;
            string partName = context?.Data?.partName;
            if (part == null || string.IsNullOrWhiteSpace(partName))
                yield break;

            string prefabPath = PartPathResolver.ResolvePrefabPath(part);
            if (string.IsNullOrEmpty(prefabPath))
                yield break;

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                yield break;

            Mod owner = PartAuthoringAddressables.FindOwningMod(part);
            string folder = Path.GetDirectoryName(prefabPath)?.Replace('\\', '/');
            string[] paths = { prefabPath, $"{folder}/{partName}.json", $"{folder}/{partName}_icon.png" };
            foreach (string path in paths)
            {
                AddressableAssetEntry entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
                if (entry == null || entry.parentGroup == null)
                    continue;
                if (PartAuthoringAddressables.IsShippedGroup(entry.parentGroup, owner))
                    continue;

                string reason = owner != null
                    ? $"which the {owner.id} mod build leaves out"
                    : "which is excluded from the build";
                string message = $"'{entry.address}' is in group '{entry.parentGroup.Name}', {reason}, so it never ships.";

                AddressableAssetGroup target = path == paths[2]
                    ? PartAuthoringAddressables.ResolveIconGroup(part)
                    : PartAuthoringAddressables.ResolveGroup(part);
                if (target == null || !PartAuthoringAddressables.IsShippedGroup(target, owner))
                {
                    yield return new ValidationIssue(Code, ValidationSeverity.Error, $"{message} No parts group that ships was found to move it to.");
                    continue;
                }

                string address = entry.address;
                var fix = new ValidationFix(
                    $"Move to '{target.Name}'",
                    () =>
                    {
                        AddressablesTools.MakeAddressable(target, path, address);
                        AssetDatabase.SaveAssets();
                    });

                yield return new ValidationIssue(Code, ValidationSeverity.Error, message, new[] { fix });
            }
        }
    }
}
