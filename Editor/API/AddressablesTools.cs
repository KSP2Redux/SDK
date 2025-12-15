using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace Ksp2UnityTools.Editor.API
{
    [PublicAPI]
    public static class AddressablesTools
    {
        public static void MakeAddressable(
            AddressableAssetGroup group,
            string assetPath,
            string name,
            params string[] labels
        )
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
            foreach (string label in labels)
            {
                EnsureLabelIsDefined(settings, label);
                entry.labels.Add(label);
            }

            entry.address = name;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        }

        public static void EnsureLabelIsDefined(AddressableAssetSettings settings, string label)
        {
            if (!settings.GetLabels().Contains(label))
            {
                settings.AddLabel(label);
            }
        }
    }
}