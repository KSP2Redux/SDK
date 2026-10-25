using System.Collections.Generic;
using KSP;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Errors when the part prefab has no addressables entry.
    /// </summary>
    /// <remarks>
    /// The game finds a part's prefab by its <c>{partName}.prefab</c> address, so a prefab outside
    /// Addressables leaves the part without a model. The fix registers it in the part's parts
    /// group under that address.
    /// </remarks>
    public class PartPrefabNotAddressableValidator : IPartValidator
    {
        /// <summary>Stable code emitted when the prefab is not addressable.</summary>
        public const string Code = "PART_PREFAB_NOT_ADDRESSABLE";

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
            if (settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(prefabPath)) != null)
                yield break;

            string address = PartAuthoringAddressables.GetPrefabAddress(partName);
            AddressableAssetGroup group = PartAuthoringAddressables.ResolveGroup(part);
            if (group == null)
            {
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"Prefab is not addressable, so the game cannot load the part. No parts group was found to add it to. Register it manually as '{address}'.");
                yield break;
            }

            var fix = new ValidationFix(
                $"Add to '{group.Name}' as '{address}'",
                () =>
                {
                    AddressablesTools.MakeAddressable(group, prefabPath, address);
                    AssetDatabase.SaveAssets();
                });

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                $"Prefab is not addressable, so the game cannot load the part. It should be '{address}'.",
                new[] { fix });
        }
    }
}
