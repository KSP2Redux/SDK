using Ksp2UnityTools.PrefabPatchingAuthoring;
using PatchManager.PrefabPatching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ksp2UnityTools.Editor.PrefabPatching;

/// <summary>
/// Read-only Unity stage showing the effective result of every compiled patch
/// manifest that targets the selected visual variant's linked stock prefab.
/// </summary>
public sealed class PrefabPatchEffectivePreviewStage : PreviewSceneStage
{
    private GameObject previewInstance;
    private string stageName;

    protected override GUIContent CreateHeaderContent() =>
        new(stageName ?? "Effective Prefab");

    public static PrefabPatchResolvedPlan Show(
        PrefabPatchAuthoringMetadata metadata
    )
    {
        metadata =
            PrefabPatchAuthoringWorkflow.GetPersistentMetadata(metadata)
            ?? throw new InvalidOperationException(
                "Prefab patch metadata must be stored on a persistent variant."
            );
        var compiled = PrefabVariantPatchCompiler.Compile(metadata);
        var manifests = FindManifestAssets()
            .Select(source => ReadManifest(source.Path, source.OwnerModId))
            .Where(value => value?.TargetPrefab != null)
            .Where(
                value =>
                    string.Equals(
                        value.TargetPrefab.Address,
                        compiled.Manifest.TargetPrefab.Address,
                        StringComparison.Ordinal
                    )
            )
            .OrderBy(value => value.PatchId, StringComparer.Ordinal)
            .ToArray();
        var plan = PrefabPatchResolver.Resolve(
            manifests,
            new HashSet<string>(
                manifests.Select(value => value.ModId),
                StringComparer.Ordinal
            ),
            Application.unityVersion,
            EditorUserBuildSettings.activeBuildTarget.ToString()
        );
        if (!plan.IsValid)
        {
            throw new InvalidOperationException(
                "Effective prefab preview plan is invalid:\n"
                    + string.Join(
                        "\n",
                        plan.Diagnostics.Select(
                            value => $"{value.Code}: {value.Message}"
                        )
                    )
            );
        }

        var basePrefab =
            PrefabUtility.GetCorrespondingObjectFromSource(
                PrefabPatchAuthoringWorkflow.GetVariantAsset(metadata)
            )
            as GameObject;
        var stage =
            CreateInstance<PrefabPatchEffectivePreviewStage>();
        StageUtility.GoToStage(stage, true);
        stage.Setup(basePrefab, plan);
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.FrameSelected();
            SceneView.lastActiveSceneView.Repaint();
        }
        return plan;
    }

    private void Setup(
        GameObject basePrefab,
        PrefabPatchResolvedPlan plan
    )
    {
        previewInstance = Instantiate(basePrefab);
        previewInstance.name = basePrefab.name + " (Effective Preview)";
        SetFlags(previewInstance.transform);
        SceneManager.MoveGameObjectToScene(previewInstance, scene);
        var expectedFingerprint =
            plan.TargetPrefab.StructuralFingerprint;
        try
        {
            plan.TargetPrefab.StructuralFingerprint =
                PrefabPatchStructure.Calculate(previewInstance);
            var result = PrefabPatchComposer.ApplySynchronously(
                previewInstance,
                plan,
                new Dictionary<string, Object>()
            );
            if (!result.Success)
                throw new InvalidOperationException(result.Failure);
        }
        finally
        {
            plan.TargetPrefab.StructuralFingerprint =
                expectedFingerprint;
        }

        stageName =
            $"{basePrefab.name} - {plan.OrderedPatchIds.Length} patch(es)";
        previewInstance.transform.position = Vector3.zero;
        Selection.activeObject = previewInstance;
    }

    private sealed class PreviewManifestAsset
    {
        public string Path;
        public string OwnerModId;
    }

    private static IEnumerable<PreviewManifestAsset> FindManifestAssets()
    {
        var settings =
            AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            yield break;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (
            var entry in settings.groups
                .Where(group => group != null)
                .SelectMany(group => group.entries)
        )
        {
            var path = AssetDatabase.GUIDToAssetPath(entry.guid);
            if (
                string.IsNullOrWhiteSpace(path)
                || AssetDatabase.LoadAssetAtPath<TextAsset>(path) == null
            )
            {
                continue;
            }

            foreach (var label in entry.labels)
            {
                string ownerModId;
#if REDUX
                if (
                    !string.Equals(
                        label,
                        PrefabPatchAuthoringWorkflow.ReduxPrefabPatchLabel,
                        StringComparison.Ordinal
                    )
                )
                {
                    continue;
                }
                ownerModId = PrefabPatchAuthoringWorkflow.ReduxModId;
#else
                if (
                    !PrefabPatchModAssetUtility
                        .TryResolveOwnerByPrefabPatchLabel(
                            label,
                            out ownerModId
                        )
                )
                {
                    continue;
                }
#endif
                var identity = ownerModId + "\0" + path;
                if (!seen.Add(identity))
                    continue;
                yield return new PreviewManifestAsset
                {
                    Path = path,
                    OwnerModId = ownerModId
                };
            }
        }
    }

    private static PrefabPatchManifest ReadManifest(
        string path,
        string ownerModId
    )
    {
        try
        {
            return PrefabPatchOwnership.Bind(
                PrefabPatchJson.Deserialize<PrefabPatchManifest>(
                    File.ReadAllText(path)
                ),
                ownerModId
            );
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[KSP2UnityTools.PrefabPatching] Skipping malformed preview manifest "
                    + $"'{path}': {exception.Message}"
            );
            return null;
        }
    }

    private static void SetFlags(Transform transform)
    {
        transform.gameObject.hideFlags =
            HideFlags.DontSave | HideFlags.NotEditable;
        foreach (Transform child in transform)
            SetFlags(child);
    }

    private void OnDestroy()
    {
        if (previewInstance != null)
            DestroyImmediate(previewInstance);
        base.OnCloseStage();
    }
}
