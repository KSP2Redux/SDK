using System.Collections.Generic;
using System.IO;
using KSP;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Warns when the part's baked icon is not addressable under <c>{partName}_icon.png</c>.
    /// </summary>
    /// <remarks>
    /// The game looks a part's icon up by that address, so a baked icon outside Addressables, or
    /// under another address, is never shown. Only runs when the icon file exists.
    /// </remarks>
    public class PartIconNotAddressableValidator : IPartValidator
    {
        /// <summary>Stable code emitted when the icon is not reachable by its canonical address.</summary>
        public const string Code = "PART_ICON_NOT_ADDRESSABLE";

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

            string iconPath = $"{Path.GetDirectoryName(prefabPath)?.Replace('\\', '/')}/{partName}_icon.png";
            if (!File.Exists(iconPath))
                yield break;

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                yield break;

            string address = PartAuthoringAddressables.GetIconAddress(partName);
            AddressableAssetEntry entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(iconPath));
            if (entry != null && entry.address == address)
                yield break;

            string message = entry == null
                ? "Baked icon is not addressable, so the game cannot show it."
                : $"Icon address '{entry.address}' does not match '{address}', so the game cannot find it.";

            AddressableAssetGroup group = entry != null ? entry.parentGroup : PartAuthoringAddressables.ResolveIconGroup(part);
            if (group == null)
            {
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"{message} No icon group was found to add it to.");
                yield break;
            }

            var fix = new ValidationFix(
                entry == null ? $"Add to '{group.Name}' as '{address}'" : $"Set address -> '{address}'",
                () =>
                {
                    AddressablesTools.MakeAddressable(group, iconPath, address);
                    AssetDatabase.SaveAssets();
                });

            yield return new ValidationIssue(Code, ValidationSeverity.Warning, message, new[] { fix });
        }
    }
}
