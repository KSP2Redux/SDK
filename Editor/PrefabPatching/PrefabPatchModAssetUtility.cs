using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Ksp2UnityTools.PrefabPatchingAuthoring;
using PatchManager.PrefabPatching;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ksp2UnityTools.Editor.PrefabPatching
{

internal readonly struct PrefabPatchModAssetInfo
{
    public PrefabPatchModAssetInfo(
        Object asset,
        string id,
        string folder,
        AddressableAssetGroup allGroup,
        string prefabPatchLabel
    )
    {
        Asset = asset;
        Id = id;
        Folder = folder;
        AllGroup = allGroup;
        PrefabPatchLabel = prefabPatchLabel;
    }

    public Object Asset { get; }
    public string Id { get; }
    public string Folder { get; }
    public AddressableAssetGroup AllGroup { get; }
    public string PrefabPatchLabel { get; }
    public string SuggestedOutputFolder => $"{Folder}/Patches/Prefabs";
}

/// <summary>
/// Accesses the Redux SDK Mod authoring asset without taking an assembly
/// reference on the predefined editor assembly that contains it.
/// </summary>
internal static class PrefabPatchModAssetUtility
{
    private const string ModTypeName = "Ksp2UnityTools.Editor.Modding.Mod";
    private static Type modType;

    internal static Type ModType =>
        modType ??= AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(assembly => assembly.GetType(ModTypeName, false))
            .FirstOrDefault(type => type != null);

    internal static Object FindDefaultModAsset(string rememberedGuid)
    {
        if (!string.IsNullOrWhiteSpace(rememberedGuid))
        {
            var remembered = LoadModAsset(
                AssetDatabase.GUIDToAssetPath(rememberedGuid)
            );
            if (remembered != null)
                return remembered;
        }

        if (
            Selection.activeObject != null
            && ModType?.IsInstanceOfType(Selection.activeObject) == true
        )
        {
            return Selection.activeObject;
        }

        var matches = AssetDatabase.FindAssets("t:Mod")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(LoadModAsset)
            .Where(asset => asset != null)
            .Take(2)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    internal static bool TryGetInfo(
        Object asset,
        bool requireAddressablesGroup,
        out PrefabPatchModAssetInfo info,
        out string error
    )
    {
        info = default;
        var type = ModType;
        if (type == null)
        {
            error =
                "The Redux SDK Mod authoring type is not available in this "
                + "project.";
            return false;
        }
        if (asset == null)
        {
            error = "Select the Redux SDK Mod asset that owns this patch.";
            return false;
        }
        if (!type.IsInstanceOfType(asset))
        {
            error =
                $"'{asset.name}' is not a Redux SDK Mod authoring asset.";
            return false;
        }

        var id = ReadPublicMember(type, asset, "id") as string;
        if (string.IsNullOrWhiteSpace(id))
        {
            error = "The selected Mod asset does not define a mod ID.";
            return false;
        }
        var assetPath = AssetDatabase.GetAssetPath(asset);
        var folder = Path.GetDirectoryName(assetPath)
            ?.Replace('\\', '/')
            ?.TrimEnd('/');
        if (
            string.IsNullOrWhiteSpace(folder)
            || (
                !string.Equals(folder, "Assets", StringComparison.Ordinal)
                && !folder.StartsWith("Assets/", StringComparison.Ordinal)
            )
        )
        {
            error =
                "The selected Mod asset must be stored inside this project's "
                + "Assets folder.";
            return false;
        }

        var allGroup =
            ReadPublicMember(type, asset, "allGroup")
                as AddressableAssetGroup;
        allGroup ??= AddressableAssetSettingsDefaultObject.Settings?.FindGroup(
            $"addressables_{id.Trim()}_all"
        );
        if (requireAddressablesGroup && allGroup == null)
        {
            error =
                $"The selected Mod asset has no all-assets Addressables group. "
                + $"Create or refresh its 'addressables_{id.Trim()}_all' group "
                + "before compiling prefab patches.";
            return false;
        }

        var prefabPatchLabel =
            ReadPublicMember(
                type,
                asset,
                "AddressablePrefabPatchLabel"
            ) as string;
        if (string.IsNullOrWhiteSpace(prefabPatchLabel))
        {
            prefabPatchLabel =
                id.Trim() + PrefabPatchSchema.AddressablesLabelSuffix;
        }

        info = new PrefabPatchModAssetInfo(
            asset,
            id.Trim(),
            folder,
            allGroup,
            prefabPatchLabel.Trim()
        );
        error = string.Empty;
        return true;
    }

    internal static PrefabPatchModAssetInfo ApplyOwnership(
        PrefabPatchAuthoringMetadata metadata,
        bool requireAddressablesGroup
    )
    {
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));
        if (
            !TryGetInfo(
                metadata.OwningMod,
                requireAddressablesGroup,
                out var info,
                out var error
            )
        )
        {
            throw new InvalidOperationException(error);
        }

        return info;
    }

    internal static string GetAssetGuid(Object asset)
    {
        var path = AssetDatabase.GetAssetPath(asset);
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : AssetDatabase.AssetPathToGUID(path);
    }

    internal static bool TryResolveOwnerByPrefabPatchLabel(
        string label,
        out string modId
    )
    {
        modId = null;
        if (string.IsNullOrWhiteSpace(label))
            return false;

        var matches = AssetDatabase.FindAssets("t:Mod")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(LoadModAsset)
            .Where(asset => asset != null)
            .Select(asset =>
                TryGetInfo(asset, false, out var info, out _)
                    ? info
                    : default
            )
            .Where(info =>
                !string.IsNullOrWhiteSpace(info.Id)
                && string.Equals(
                    info.PrefabPatchLabel,
                    label,
                    StringComparison.Ordinal
                )
            )
            .Select(info => info.Id)
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        if (matches.Length != 1)
            return false;
        modId = matches[0];
        return true;
    }

    private static Object LoadModAsset(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        var asset = AssetDatabase.LoadMainAssetAtPath(path);
        return ModType?.IsInstanceOfType(asset) == true ? asset : null;
    }

    private static object ReadPublicMember(
        Type type,
        Object instance,
        string name
    )
    {
        var field = type.GetField(
            name,
            BindingFlags.Public | BindingFlags.Instance
        );
        if (field != null)
            return field.GetValue(instance);
        return type.GetProperty(
            name,
            BindingFlags.Public | BindingFlags.Instance
        )?.GetValue(instance);
    }
}
}
