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
    /// Errors when the part's JSON sidecar is not addressable or lacks the <c>parts_data</c> label.
    /// </summary>
    /// <remarks>
    /// The game discovers parts by loading every asset labelled <c>parts_data</c>, so a JSON
    /// without an entry or without the label is never loaded and the part does not exist in game.
    /// A missing JSON is reported by <see cref="PartJsonOutOfDateValidator" /> instead.
    /// </remarks>
    public class PartJsonNotAddressableValidator : IPartValidator
    {
        /// <summary>Stable code emitted when the JSON is not loadable by label.</summary>
        public const string Code = "PART_JSON_NOT_ADDRESSABLE";

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

            string jsonPath = $"{Path.GetDirectoryName(prefabPath)?.Replace('\\', '/')}/{partName}.json";
            if (!File.Exists(jsonPath))
                yield break;

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                yield break;

            AddressableAssetEntry entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(jsonPath));
            if (entry != null && entry.labels.Contains(PartAuthoringAddressables.PartsDataLabel))
                yield break;

            string message = entry == null
                ? "Part JSON is not addressable, so the game never loads the part."
                : $"Part JSON is addressable but lacks the '{PartAuthoringAddressables.PartsDataLabel}' label the game loads parts by, so the part never loads.";

            // An existing entry keeps its group and address. Whether that group ships is a separate check.
            AddressableAssetGroup group = entry != null ? entry.parentGroup : PartAuthoringAddressables.ResolveGroup(part);
            if (group == null)
            {
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"{message} No parts group was found to add it to.");
                yield break;
            }

            string address = entry != null ? entry.address : PartAuthoringAddressables.GetJsonAddress(partName);
            var fix = new ValidationFix(
                entry == null ? $"Add to '{group.Name}' as '{address}'" : $"Add the '{PartAuthoringAddressables.PartsDataLabel}' label",
                () =>
                {
                    AddressablesTools.MakeAddressable(group, jsonPath, address, PartAuthoringAddressables.PartsDataLabel);
                    AssetDatabase.SaveAssets();
                });

            yield return new ValidationIssue(Code, ValidationSeverity.Error, message, new[] { fix });
        }
    }
}
