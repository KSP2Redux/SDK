using System.Collections.Generic;
using KSP;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.IO;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Warns when the part prefab's addressable address does not match the canonical
    /// <c>{partName}.prefab</c> form.
    /// </summary>
    /// <remarks>
    /// The runtime parts loader and patch tooling look up parts by the <c>{partName}.prefab</c>
    /// address. Drift between partName and address (usually caused by renaming partName without
    /// updating the addressable entry) makes the part unreachable at load time. The fix updates
    /// the entry's address to match the current partName.
    /// </remarks>
    public sealed class PartPrefabAddressMismatchValidator : IPartValidator
    {
        /// <summary>Stable code emitted when the prefab address does not match the canonical form.</summary>
        public const string Code = "PART_PREFAB_ADDRESS_MISMATCH";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            PartData data = context?.Data;
            if (data == null || string.IsNullOrWhiteSpace(data.partName))
            {
                yield break;
            }
            CorePartData target = context.Part;
            if (target == null) yield break;

            string prefabPath = PathUtils.GetPrefabOrAssetPath(target, target.gameObject);
            if (string.IsNullOrEmpty(prefabPath)) yield break;

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) yield break;

            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
            if (string.IsNullOrEmpty(guid)) yield break;

            AddressableAssetEntry entry = settings.FindAssetEntry(guid);
            if (entry == null) yield break;

            string expectedAddress = $"{data.partName}.prefab";
            if (entry.address == expectedAddress) yield break;

            string currentAddress = entry.address;
            var fix = new ValidationFix(
                $"Set address -> '{expectedAddress}'",
                () =>
                {
                    AddressableAssetEntry liveEntry = settings.FindAssetEntry(guid);
                    if (liveEntry == null) return;
                    liveEntry.address = expectedAddress;
                    settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, liveEntry, true);
                    AssetDatabase.SaveAssets();
                });

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                $"Prefab address '{currentAddress}' does not match the canonical form '{expectedAddress}'.",
                new[] { fix });
        }
    }
}
