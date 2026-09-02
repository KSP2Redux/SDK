using System.IO;
using Ksp2UnityTools.Editor.LinkedAddressables;
using Ksp2UnityTools.PrefabPatchingAuthoring;
using PatchManager.PrefabPatching;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
    private bool showAdvanced;
    private string statusMessage;
    private MessageType statusType;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "This component configures the prefab patch compiler. Make visual "
                + "changes on this prefab variant; saving the variant rebuilds "
                + "the runtime JSON when Auto Compile On Save is enabled.",
            MessageType.Info
        );

#if REDUX
        DrawSection(
            "Patch",
            nameof(PrefabPatchAuthoringMetadata.PatchName)
        );
        EditorGUILayout.HelpBox(
            $"Owned by {PrefabPatchAuthoringWorkflow.ReduxModId}. Runtime "
                + "ownership comes from Redux's swinfo descriptor and "
                + $"'{PrefabPatchAuthoringWorkflow.ReduxPrefabPatchLabel}' "
                + "label.",
            MessageType.None
        );
#else
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Patch", EditorStyles.boldLabel);
        DrawOwningModProperty();
        DrawProperties(nameof(PrefabPatchAuthoringMetadata.PatchName));
        DrawModOwnershipStatus(
            (PrefabPatchAuthoringMetadata)target
        );
#endif
        DrawSection(
            "Composition",
            nameof(PrefabPatchAuthoringMetadata.Pass),
            nameof(PrefabPatchAuthoringMetadata.Ordering)
        );
        DrawSection(
            "Output",
            nameof(PrefabPatchAuthoringMetadata.AutoCompileOnSave)
        );

        showAdvanced = EditorGUILayout.Foldout(
            showAdvanced,
            "Dependencies & Advanced Ordering",
            true
        );
        if (showAdvanced)
        {
            EditorGUI.indentLevel++;
            DrawProperties(
                nameof(PrefabPatchAuthoringMetadata.NeedsMods),
                nameof(PrefabPatchAuthoringMetadata.ConflictsMods),
                nameof(PrefabPatchAuthoringMetadata.NeedsPatches),
                nameof(PrefabPatchAuthoringMetadata.ConflictsPatches),
                nameof(PrefabPatchAuthoringMetadata.BeforePatches),
                nameof(PrefabPatchAuthoringMetadata.AfterPatches),
                nameof(PrefabPatchAuthoringMetadata.BeforeMods),
                nameof(PrefabPatchAuthoringMetadata.AfterMods),
                nameof(PrefabPatchAuthoringMetadata.ConfigurationInputs)
            );
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();

        var metadata = (PrefabPatchAuthoringMetadata)target;
        DrawSourceStatus(metadata);
        DrawManifestStatus(metadata);

        if (!string.IsNullOrWhiteSpace(statusMessage))
            EditorGUILayout.HelpBox(statusMessage, statusType);

        EditorGUILayout.Space();
        var variant =
            PrefabPatchAuthoringWorkflow.GetVariantAsset(metadata);
        using (new EditorGUI.DisabledScope(variant == null))
        {
            if (
                GUILayout.Button(
                    "Open Variant for Editing",
                    GUILayout.Height(28f)
                )
            )
            {
                AssetDatabase.OpenAsset(variant);
            }

            if (GUILayout.Button("Compile JSON Now", GUILayout.Height(28f)))
                Compile(metadata);

            if (GUILayout.Button("Open Effective Prefab Preview"))
            {
                try
                {
                    metadata = SaveAndReload(metadata);
                    var plan =
                        PrefabPatchEffectivePreviewStage.Show(metadata);
                    statusMessage =
                        $"Preview contains {plan.OrderedPatchIds.Length} "
                        + $"ordered patch(es) and {plan.Diagnostics.Count} "
                        + "diagnostic(s).";
                    statusType = MessageType.Info;
                }
                catch (System.Exception exception)
                {
                    ReportFailure(exception);
                }
            }
        }

        var manifestPath =
            PrefabPatchAuthoringWorkflow.GetOutputManifestPath(metadata);
        using (
            new EditorGUI.DisabledScope(
                string.IsNullOrWhiteSpace(manifestPath)
                || AssetDatabase.LoadAssetAtPath<TextAsset>(manifestPath) == null
            )
        )
        {
            if (GUILayout.Button("Select Compiled JSON"))
            {
                var manifest = AssetDatabase.LoadAssetAtPath<TextAsset>(
                    manifestPath
                );
                Selection.activeObject = manifest;
                EditorGUIUtility.PingObject(manifest);
            }
        }
    }

    private void DrawSection(string label, params string[] properties)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        DrawProperties(properties);
    }

    private void DrawProperties(params string[] properties)
    {
        foreach (var propertyName in properties)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property, true);
        }
    }

    private static void DrawSourceStatus(
        PrefabPatchAuthoringMetadata metadata
    )
    {
        var variant =
            PrefabPatchAuthoringWorkflow.GetVariantAsset(metadata);
        var basePrefab =
            PrefabUtility.GetCorrespondingObjectFromSource(variant)
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
            EditorGUILayout.HelpBox(
                "This component must be on the root of a prefab variant based "
                    + "directly on a KSP2UnityTools linked prefab.",
                MessageType.Error
            );
            return;
        }

        EditorGUILayout.HelpBox(
            $"Linked target: {identity.Address}\n"
                + $"Descriptor: {identity.DescriptorPath}",
            MessageType.None
        );
    }

#if !REDUX
    private void DrawOwningModProperty()
    {
        var property = serializedObject.FindProperty(
            nameof(PrefabPatchAuthoringMetadata.OwningMod)
        );
        var selected = EditorGUILayout.ObjectField(
            property.displayName,
            property.objectReferenceValue,
            PrefabPatchModAssetUtility.ModType ?? typeof(ScriptableObject),
            false
        );
        if (selected != property.objectReferenceValue)
            property.objectReferenceValue = selected;
    }

    private static void DrawModOwnershipStatus(
        PrefabPatchAuthoringMetadata metadata
    )
    {
        if (
            !PrefabPatchModAssetUtility.TryGetInfo(
                metadata.OwningMod,
                false,
                out var info,
                out var error
            )
        )
        {
            EditorGUILayout.HelpBox(error, MessageType.Warning);
            return;
        }

        var group = info.AllGroup == null
            ? "missing all-assets Addressables group"
            : $"Addressables group '{info.AllGroup.Name}'";
        EditorGUILayout.HelpBox(
            $"Owned by {info.Id}; compiled JSON uses {group} and label "
                + $"'{info.PrefabPatchLabel}'. Runtime ownership comes from "
                + "this mod's swinfo descriptor.",
            info.AllGroup == null ? MessageType.Warning : MessageType.None
        );
    }
#endif

    private static void DrawManifestStatus(
        PrefabPatchAuthoringMetadata metadata
    )
    {
        var path =
            PrefabPatchAuthoringWorkflow.GetOutputManifestPath(metadata);
        var manifestAsset = string.IsNullOrWhiteSpace(path)
            ? null
            : AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        if (manifestAsset == null)
        {
            EditorGUILayout.HelpBox(
                $"JSON has not been generated yet.\nOutput: {path}",
                MessageType.Warning
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
            EditorGUILayout.HelpBox(
                $"Compiled: {manifest.PatchName}\n"
                    + $"Operations: {manifest.Operations.Count}\n"
                    + $"Updated: {timestamp}",
                MessageType.Info
            );
        }
        catch (System.Exception exception)
        {
            EditorGUILayout.HelpBox(
                $"The compiled JSON could not be read: {exception.Message}",
                MessageType.Error
            );
        }
    }

    private void Compile(PrefabPatchAuthoringMetadata metadata)
    {
        try
        {
            metadata = SaveAndReload(metadata);
            var result = PrefabPatchAuthoringWorkflow.Compile(metadata);
            PrefabPatchAutoCompiler.DiscardPending(result.VariantPath);
            statusMessage =
                $"Compiled {result.Manifest.Operations.Count} operation(s) to "
                + result.OutputPath;
            statusType = MessageType.Info;
        }
        catch (System.Exception exception)
        {
            ReportFailure(exception);
        }
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
        statusMessage = exception.Message;
        statusType = MessageType.Error;
        Debug.LogException(exception);
    }
}
}
