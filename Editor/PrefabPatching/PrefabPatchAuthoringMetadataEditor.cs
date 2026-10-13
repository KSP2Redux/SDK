using System.IO;
using Ksp2UnityTools.Editor.LinkedAddressables;
using Ksp2UnityTools.Editor.Modding;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections;
using Ksp2UnityTools.PrefabPatchingAuthoring;
using PatchManager.PrefabPatching;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PrefabPatching
{

/// <summary>
/// Inspector for the authoring metadata embedded directly in a prefab-patch
/// variant.
/// </summary>
[CustomEditor(typeof(PrefabPatchAuthoringMetadata))]
internal sealed class PrefabPatchAuthoringMetadataEditor
    : UnityEditor.Editor
{
    private const string UXML_PATH =
        "/Assets/Windows/PrefabPatchAuthoringMetadataEditor.uxml";

    private HelpBox ownershipStatus;
    private HelpBox sourceStatus;
    private HelpBox manifestStatus;
    private HelpBox actionStatus;
    private Button selectJson;

    private PrefabPatchAuthoringMetadata Metadata =>
        (PrefabPatchAuthoringMetadata)target;

    public override VisualElement CreateInspectorGUI()
    {
        var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            SDKConfiguration.BasePath + UXML_PATH
        ).CloneTree();
        Ksp2UnityToolsStyles.Apply(root);

        ownershipStatus = root.Q<HelpBox>("ownership-status");
        sourceStatus = root.Q<HelpBox>("source-status");
        manifestStatus = root.Q<HelpBox>("manifest-status");
        actionStatus = root.Q<HelpBox>("action-status");
        actionStatus.style.display = DisplayStyle.None;

        var owningMod = root.Q<ObjectField>("owning-mod");
#if REDUX
        owningMod.style.display = DisplayStyle.None;
#else
        owningMod.objectType =
            PrefabPatchModAssetUtility.ModType ?? typeof(ScriptableObject);
        owningMod.RegisterValueChangedCallback(_ =>
        {
            RefreshStatus();
            BuildFeatures(root.Q("features-slot"));
        });
#endif
        BuildFeatures(root.Q("features-slot"));

        var variant = PrefabPatchAuthoringWorkflow.GetVariantAsset(Metadata);
        var openVariant = root.Q<Button>("open-variant");
        openVariant.SetEnabled(variant != null);
        openVariant.clicked += () => AssetDatabase.OpenAsset(variant);
        var compile = root.Q<Button>("compile");
        compile.SetEnabled(variant != null);
        compile.clicked += Compile;
        var preview = root.Q<Button>("preview");
        preview.SetEnabled(variant != null);
        preview.clicked += Preview;
        selectJson = root.Q<Button>("select-json");
        selectJson.clicked += SelectJson;

        RefreshStatus();
        return root;
    }

    // Bare feature ids belong to the owning mod, which Redux's own patches
    // have none of
    private void BuildFeatures(VisualElement slot)
    {
        slot.Clear();
#if REDUX
        Mod owner = null;
#else
        var owner = Metadata.OwningMod as Mod;
#endif
        slot.Add(
            InlineStringListBlock.Build(
                serializedObject.FindProperty(
                    nameof(PrefabPatchAuthoringMetadata.Features)
                ),
                "Required Features",
                entry => new FeatureField(entry, null, owner)
            )
        );
        slot.Add(
            InlineStringListBlock.Build(
                serializedObject.FindProperty(
                    nameof(PrefabPatchAuthoringMetadata.NoFeatures)
                ),
                "Excluded Features",
                entry => new FeatureField(entry, null, owner)
            )
        );
    }

    private void RefreshStatus()
    {
        var metadata = Metadata;
        SetOwnershipStatus(metadata);
        SetSourceStatus(metadata);
        SetManifestStatus(metadata);

        var manifestPath =
            PrefabPatchAuthoringWorkflow.GetOutputManifestPath(metadata);
        selectJson.SetEnabled(
            !string.IsNullOrWhiteSpace(manifestPath)
            && AssetDatabase.LoadAssetAtPath<TextAsset>(manifestPath) != null
        );
    }

    private void SetOwnershipStatus(PrefabPatchAuthoringMetadata metadata)
    {
#if REDUX
        Show(
            ownershipStatus,
            $"Owned by {PrefabPatchAuthoringWorkflow.ReduxModId}. Runtime "
                + "ownership comes from Redux's swinfo descriptor and "
                + $"'{PrefabPatchAuthoringWorkflow.ReduxPrefabPatchLabel}' "
                + "label.",
            HelpBoxMessageType.None
        );
#else
        if (
            !PrefabPatchModAssetUtility.TryGetInfo(
                metadata.OwningMod,
                false,
                out var info,
                out var error
            )
        )
        {
            Show(ownershipStatus, error, HelpBoxMessageType.Warning);
            return;
        }

        var group = info.AllGroup == null
            ? "missing all-assets Addressables group"
            : $"Addressables group '{info.AllGroup.Name}'";
        Show(
            ownershipStatus,
            $"Owned by {info.Id}; compiled JSON uses {group} and label "
                + $"'{info.PrefabPatchLabel}'. Runtime ownership comes from "
                + "this mod's swinfo descriptor.",
            info.AllGroup == null
                ? HelpBoxMessageType.Warning
                : HelpBoxMessageType.None
        );
#endif
    }

    private void SetSourceStatus(PrefabPatchAuthoringMetadata metadata)
    {
        var variant =
            PrefabPatchAuthoringWorkflow.GetVariantAsset(metadata);
        var basePrefab = variant == null
            ? null
            : PrefabUtility.GetCorrespondingObjectFromSource(variant)
                as GameObject;
        if (
            variant == null
            || basePrefab == null
            || !LinkedAddressableEditorIdentity.TryGet(
                    basePrefab,
                    out var identity
                )
        )
        {
            Show(
                sourceStatus,
                "This component must be on the root of a prefab variant based "
                    + "directly on a Redux SDK linked prefab.",
                HelpBoxMessageType.Error
            );
            return;
        }

        Show(
            sourceStatus,
            $"Linked target: {identity.Address}\n"
                + $"Descriptor: {identity.DescriptorPath}",
            HelpBoxMessageType.None
        );
    }

    private void SetManifestStatus(PrefabPatchAuthoringMetadata metadata)
    {
        var path =
            PrefabPatchAuthoringWorkflow.GetOutputManifestPath(metadata);
        var manifestAsset = string.IsNullOrWhiteSpace(path)
            ? null
            : AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        if (manifestAsset == null)
        {
            Show(
                manifestStatus,
                $"JSON has not been generated yet.\nOutput: {path}",
                HelpBoxMessageType.Warning
            );
            return;
        }

        try
        {
            var manifest =
                PrefabPatchJson.Deserialize<PrefabPatchManifest>(
                    manifestAsset.text
                );
            var fullPath = Path.GetFullPath(path);
            var timestamp = File.Exists(fullPath)
                ? File.GetLastWriteTime(fullPath).ToString("g")
                : "unknown";
            Show(
                manifestStatus,
                $"Compiled: {manifest.PatchName}\n"
                    + $"Operations: {manifest.Operations.Count}\n"
                    + $"Updated: {timestamp}",
                HelpBoxMessageType.Info
            );
        }
        catch (System.Exception exception)
        {
            Show(
                manifestStatus,
                $"The compiled JSON could not be read: {exception.Message}",
                HelpBoxMessageType.Error
            );
        }
    }

    private void Compile()
    {
        try
        {
            var metadata = SaveAndReload(Metadata);
            var result = PrefabPatchAuthoringWorkflow.Compile(metadata);
            PrefabPatchAutoCompiler.DiscardPending(result.VariantPath);
            Show(
                actionStatus,
                $"Compiled {result.Manifest.Operations.Count} operation(s) "
                    + $"to {result.OutputPath}",
                HelpBoxMessageType.Info
            );
        }
        catch (System.Exception exception)
        {
            ReportFailure(exception);
        }

        RefreshStatus();
    }

    private void Preview()
    {
        try
        {
            var metadata = SaveAndReload(Metadata);
            var plan = PrefabPatchEffectivePreviewStage.Show(metadata);
            Show(
                actionStatus,
                $"Preview contains {plan.OrderedPatchIds.Length} ordered "
                    + $"patch(es) and {plan.Diagnostics.Count} diagnostic(s).",
                HelpBoxMessageType.Info
            );
        }
        catch (System.Exception exception)
        {
            ReportFailure(exception);
        }
    }

    private void SelectJson()
    {
        var manifest = AssetDatabase.LoadAssetAtPath<TextAsset>(
            PrefabPatchAuthoringWorkflow.GetOutputManifestPath(Metadata)
        );
        Selection.activeObject = manifest;
        EditorGUIUtility.PingObject(manifest);
    }

    private static PrefabPatchAuthoringMetadata SaveAndReload(
        PrefabPatchAuthoringMetadata metadata
    )
    {
        var stage = PrefabStageUtility.GetPrefabStage(metadata.gameObject);
        if (stage != null)
        {
            PrefabUtility.SaveAsPrefabAsset(
                stage.prefabContentsRoot,
                stage.assetPath
            );
        }
        return PrefabPatchAuthoringWorkflow.GetPersistentMetadata(metadata)
            ?? throw new System.InvalidOperationException(
                "Could not reload prefab patch metadata from the variant."
            );
    }

    private void ReportFailure(System.Exception exception)
    {
        Show(actionStatus, exception.Message, HelpBoxMessageType.Error);
        Debug.LogException(exception);
    }

    private static void Show(
        HelpBox box,
        string text,
        HelpBoxMessageType type
    )
    {
        box.text = text;
        box.messageType = type;
        box.style.display = DisplayStyle.Flex;
    }
}
}
