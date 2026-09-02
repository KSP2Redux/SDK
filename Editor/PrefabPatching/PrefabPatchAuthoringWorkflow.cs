using Ksp2UnityTools.Editor.LinkedAddressables;
using Ksp2UnityTools.PrefabPatchingAuthoring;
using PatchManager.PrefabPatching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Ksp2UnityTools.Editor.PrefabPatching
{

/// <summary>
/// Guided editor workflow for creating, editing, and compiling a visual prefab
/// patch from a KSP2UnityTools linked prefab.
/// </summary>
public static class PrefabPatchAuthoringWorkflow
{
    private const string MenuPath =
        "Assets/Redux SDK/Create Prefab Patch from Linked Prefab";

#if REDUX
    internal const string ReduxModId = "Ksp2Redux";
    internal const string ReduxPrefabPatchLabel =
        "redux_prefab_patches";
    internal const string ReduxOutputFolder =
        "Assets/ReduxAssets/Patches/Prefabs";
#endif

    [MenuItem(MenuPath, false, 2050)]
    private static void CreateFromSelection()
    {
        PrefabPatchCreationWindow.Open(Selection.activeObject as GameObject);
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateCreateFromSelection()
    {
        return Selection.activeObject is GameObject prefab
            && TryGetLinkedRoot(prefab, out _);
    }

    public static PrefabPatchCreationResult Create(
        GameObject linkedPrefab,
        string outputFolder,
        string patchName,
        bool openVariant,
        Object owningMod = null
    )
    {
#if REDUX
        var modId = ReduxModId;
#else
        if (
            !PrefabPatchModAssetUtility.TryGetInfo(
                owningMod,
                true,
                out var modInfo,
                out var modError
            )
        )
        {
            throw new InvalidOperationException(modError);
        }
        var modId = modInfo.Id;
#endif
        var diagnostics = Validate(
            linkedPrefab,
            outputFolder,
            modId,
            patchName
        );
        if (diagnostics.Count > 0)
            throw new InvalidOperationException(
                "Could not create prefab patch:\n- "
                    + string.Join("\n- ", diagnostics)
            );

        outputFolder = NormalizeAssetFolder(outputFolder);
        EnsureAssetFolder(outputFolder);
        var stem = BuildFileStem(modId, patchName);
        var variantPath = $"{outputFolder}/{stem}.prefab";
        var manifestPath = $"{outputFolder}/{stem}.prefabpatch.json";
        foreach (var path in new[] { variantPath, manifestPath })
        {
            if (
                File.Exists(path)
                || AssetDatabase.LoadMainAssetAtPath(path) != null
            )
            {
                throw new InvalidOperationException(
                    $"An asset already exists at '{path}'. Choose a different "
                        + "patch name or output folder."
                );
            }
        }

        GameObject instance = null;
        try
        {
            instance =
                PrefabUtility.InstantiatePrefab(linkedPrefab) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"Could not instantiate linked prefab '{linkedPrefab.name}'."
                );
            }

            var metadata =
                instance.AddComponent<PrefabPatchAuthoringMetadata>();
            metadata.OwningMod = owningMod;
            metadata.PatchName = patchName.Trim();
            metadata.AutoCompileOnSave = true;

            var variant = PrefabUtility.SaveAsPrefabAsset(
                instance,
                variantPath
            );
            if (
                variant == null
                || PrefabUtility.GetPrefabAssetType(variant)
                    != PrefabAssetType.Variant
            )
            {
                throw new InvalidOperationException(
                    $"Unity did not create a prefab variant at '{variantPath}'."
                );
            }

            // Instantiating some stock UI prefabs runs OnValidate callbacks
            // (notably Scrollbar) that create incidental property overrides
            // before the user has edited anything. A newly created patch must
            // start empty, so discard that seed state while preserving the
            // variant's base relationship.
            PrefabUtility.SetPropertyModifications(
                variant,
                Array.Empty<PropertyModification>()
            );
            EditorUtility.SetDirty(variant);
            AssetDatabase.SaveAssetIfDirty(variant);

            var persistentMetadata =
                variant.GetComponent<PrefabPatchAuthoringMetadata>();
            if (persistentMetadata == null)
            {
                throw new InvalidOperationException(
                    $"Unity did not preserve prefab patch metadata on "
                        + $"'{variantPath}'."
                );
            }

            var compiled = Compile(persistentMetadata);
            PrefabPatchAutoCompiler.DiscardPending(variantPath);
            Selection.activeObject = variant;
            EditorGUIUtility.PingObject(variant);

            if (openVariant)
            {
                AssetDatabase.OpenAsset(variant);
                SceneView.lastActiveSceneView?.ShowNotification(
                    new GUIContent(
                        "Prefab patch ready. Save the variant to rebuild JSON."
                    ),
                    5
                );
            }

            return new PrefabPatchCreationResult
            {
                Metadata = persistentMetadata,
                Variant = variant,
                Compiled = compiled,
                VariantPath = variantPath,
                ManifestPath = manifestPath
            };
        }
        catch
        {
            DeleteIfPresent(manifestPath);
            DeleteIfPresent(variantPath);
            throw;
        }
        finally
        {
            if (instance != null)
                Object.DestroyImmediate(instance);
        }
    }

    public static PrefabPatchCompileResult Compile(
        PrefabPatchAuthoringMetadata metadata
    )
    {
        var result = PrefabVariantPatchCompiler.Compile(metadata);
        Debug.Log(
            $"[KSP2UnityTools.PrefabPatching] Compiled '{result.Manifest.PatchId}' "
                + $"with {result.Manifest.Operations.Count} operation(s) to "
                + $"'{result.OutputPath}'."
        );
        return result;
    }

    public static string GetOutputManifestPath(
        PrefabPatchAuthoringMetadata metadata
    )
    {
        var variantPath = GetVariantPath(metadata);
        if (string.IsNullOrWhiteSpace(variantPath))
            return string.Empty;
        return Path.ChangeExtension(variantPath, null)
                .Replace('\\', '/')
            + ".prefabpatch.json";
    }

    internal static string GetVariantPath(
        PrefabPatchAuthoringMetadata metadata
    )
    {
        if (metadata == null)
            return string.Empty;
        var path = AssetDatabase.GetAssetPath(metadata);
        if (!string.IsNullOrWhiteSpace(path))
            return path.Replace('\\', '/');

        var stage = PrefabStageUtility.GetPrefabStage(metadata.gameObject);
        return (stage?.assetPath ?? string.Empty).Replace('\\', '/');
    }

    internal static GameObject GetVariantAsset(
        PrefabPatchAuthoringMetadata metadata
    )
    {
        var path = GetVariantPath(metadata);
        return string.IsNullOrWhiteSpace(path)
            ? null
            : AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    internal static PrefabPatchAuthoringMetadata GetPersistentMetadata(
        PrefabPatchAuthoringMetadata metadata
    )
    {
        return GetVariantAsset(metadata)
            ?.GetComponent<PrefabPatchAuthoringMetadata>();
    }

    internal static List<string> Validate(
        GameObject linkedPrefab,
        string outputFolder,
        string modId,
        string patchName
    )
    {
        var diagnostics = new List<string>();
        if (!TryGetLinkedRoot(linkedPrefab, out _))
        {
            diagnostics.Add(
                "Select the root GameObject of a KSP2UnityTools linked prefab."
            );
        }
        if (!IsIdentifier(modId))
        {
            diagnostics.Add(
                "Mod ID must start with a letter or number and contain only "
                    + "letters, numbers, '.', '_' or '-'."
            );
        }
        if (!IsIdentifier(patchName))
        {
            diagnostics.Add(
                "Patch name must start with a letter or number and contain only "
                    + "letters, numbers, '.', '_' or '-'."
            );
        }
        var normalizedFolder = NormalizeAssetFolder(outputFolder);
        if (
            !string.Equals(normalizedFolder, "Assets", StringComparison.Ordinal)
            && !normalizedFolder.StartsWith(
                "Assets/",
                StringComparison.Ordinal
            )
        )
        {
            diagnostics.Add("Output folder must be inside this project's Assets.");
        }
        return diagnostics;
    }

    internal static bool TryGetLinkedRoot(
        GameObject prefab,
        out LinkedAddressableEditorObjectIdentity identity
    )
    {
        identity = default;
        if (
            prefab == null
            || !EditorUtility.IsPersistent(prefab)
            || !LinkedAddressableEditorIdentity.TryGet(prefab, out identity)
        )
        {
            return false;
        }

        var path = AssetDatabase.GetAssetPath(prefab);
        return ReferenceEquals(
                AssetDatabase.LoadMainAssetAtPath(path),
                prefab
            )
            || AssetDatabase.LoadMainAssetAtPath(path) == prefab;
    }

    internal static string DefaultPatchName(GameObject prefab)
    {
        if (TryGetLinkedRoot(prefab, out var identity))
        {
            var name = Path.GetFileNameWithoutExtension(identity.Address);
            if (!string.IsNullOrWhiteSpace(name))
                return ToIdentifier(name);
        }
        return ToIdentifier(prefab?.name ?? "prefab-patch");
    }

    private static bool IsIdentifier(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && Regex.IsMatch(
                value.Trim(),
                "^[A-Za-z0-9][A-Za-z0-9._-]*$",
                RegexOptions.CultureInvariant
            );
    }

    private static string BuildFileStem(string modId, string patchName)
    {
        return ToIdentifier(modId) + "-" + ToIdentifier(patchName);
    }

    private static string ToIdentifier(string value)
    {
        var normalized = Regex.Replace(
            value ?? string.Empty,
            "[^A-Za-z0-9._-]+",
            "-"
        ).Trim('-', '.', '_');
        return string.IsNullOrWhiteSpace(normalized)
            ? "prefab-patch"
            : normalized;
    }

    private static string NormalizeAssetFolder(string value)
    {
        return (value ?? string.Empty).Replace('\\', '/').TrimEnd('/');
    }

    private static void EnsureAssetFolder(string folder)
    {
        var parts = folder.Split('/');
        var current = parts[0];
        for (var index = 1; index < parts.Length; index++)
        {
            var next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[index]);
            current = next;
        }
    }

    private static void DeleteIfPresent(string path)
    {
        if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
            AssetDatabase.DeleteAsset(path);
    }
}

public sealed class PrefabPatchCreationResult
{
    public PrefabPatchAuthoringMetadata Metadata;
    public GameObject Variant;
    public PrefabPatchCompileResult Compiled;
    public string VariantPath;
    public string ManifestPath;
}

internal sealed class PrefabPatchCreationWindow : EditorWindow
{
    private const string OwningModPreference =
        "KSP2UnityTools.PrefabPatching.LastAuthoringModGuid";
    private const string OutputPreference =
        "KSP2UnityTools.PrefabPatching.LastAuthoringOutput";

    [SerializeField]
    private GameObject linkedPrefab;

    private ObjectField prefabField;
    private ObjectField owningModField;
    private TextField patchNameField;
    private TextField outputField;
    private Toggle openVariantToggle;
    private HelpBox validationBox;
    private Button createButton;

    public static void Open(GameObject prefab)
    {
        var window = GetWindow<PrefabPatchCreationWindow>(true);
        window.titleContent = new GUIContent("Create Prefab Patch");
        window.minSize = new Vector2(480f, 360f);
        window.linkedPrefab = prefab;
        window.Show();
        window.SetLinkedPrefab(prefab);
    }

    public void CreateGUI()
    {
        var root = rootVisualElement;
        root.Clear();
        root.style.paddingLeft = 12f;
        root.style.paddingRight = 12f;
        root.style.paddingTop = 10f;
        root.style.paddingBottom = 10f;

        var title = new Label("Create a visual prefab patch");
        title.style.fontSize = 16f;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        root.Add(title);
        root.Add(
            new HelpBox(
                "This creates a prefab variant with patch metadata on its root "
                    + "and a compiled JSON manifest. Edit the variant normally; "
                    + "saving it keeps the JSON in sync.",
                HelpBoxMessageType.Info
            )
        );

        prefabField = new ObjectField("Linked prefab")
        {
            objectType = typeof(GameObject),
            allowSceneObjects = false,
            value = linkedPrefab
        };
#if !REDUX
        var defaultMod = PrefabPatchModAssetUtility.FindDefaultModAsset(
            EditorPrefs.GetString(OwningModPreference, string.Empty)
        );
        owningModField = new ObjectField("Owning mod")
        {
            objectType =
                PrefabPatchModAssetUtility.ModType ?? typeof(ScriptableObject),
            allowSceneObjects = false,
            value = defaultMod
        };
#endif
        patchNameField = new TextField("Patch name")
        {
            value =
                PrefabPatchAuthoringWorkflow.DefaultPatchName(linkedPrefab)
        };
        outputField = new TextField("Output folder")
        {
            value = GetDefaultOutputFolder(
#if REDUX
                null
#else
                defaultMod
#endif
            )
        };

        root.Add(prefabField);
#if REDUX
        root.Add(
            new HelpBox(
                $"Redux project detected. This patch will be owned by "
                    + $"{PrefabPatchAuthoringWorkflow.ReduxModId} "
                    + "and its compiled manifest will be placed in the "
                    + "'PM Prefab Patches' Addressables group.",
                HelpBoxMessageType.Info
            )
        );
#else
        root.Add(owningModField);
#endif
        root.Add(patchNameField);

        var outputRow = new VisualElement();
        outputRow.style.flexDirection = FlexDirection.Row;
        outputRow.Add(outputField);
        outputField.style.flexGrow = 1f;
        outputRow.Add(
            new Button(ChooseOutputFolder)
            {
                text = "Browse…",
                style = { marginLeft = 5f }
            }
        );
        root.Add(outputRow);

        openVariantToggle = new Toggle("Open variant after creation")
        {
            value = true
        };
        root.Add(openVariantToggle);

        validationBox = new HelpBox(
            string.Empty,
            HelpBoxMessageType.Error
        );
        validationBox.style.display = DisplayStyle.None;
        root.Add(validationBox);

        var spacer = new VisualElement();
        spacer.style.flexGrow = 1f;
        root.Add(spacer);

        var actions = new VisualElement();
        actions.style.flexDirection = FlexDirection.Row;
        actions.style.justifyContent = Justify.FlexEnd;
        actions.Add(new Button(Close) { text = "Cancel" });
        createButton = new Button(CreatePatch)
        {
            text = "Create Patch",
            style = { marginLeft = 5f }
        };
        actions.Add(createButton);
        root.Add(actions);

        prefabField.RegisterValueChangedCallback(evt =>
        {
            linkedPrefab = evt.newValue as GameObject;
            patchNameField.value =
                PrefabPatchAuthoringWorkflow.DefaultPatchName(linkedPrefab);
            RefreshValidation();
        });
#if !REDUX
        owningModField.RegisterValueChangedCallback(evt =>
        {
            if (
                PrefabPatchModAssetUtility.TryGetInfo(
                    evt.newValue,
                    false,
                    out var info,
                    out _
                )
            )
            {
                outputField.value = info.SuggestedOutputFolder;
            }
            RefreshValidation();
        });
#endif
        patchNameField.RegisterValueChangedCallback(_ => RefreshValidation());
        outputField.RegisterValueChangedCallback(_ => RefreshValidation());
        RefreshValidation();
    }

    private void SetLinkedPrefab(GameObject prefab)
    {
        linkedPrefab = prefab;
        if (prefabField == null)
            return;

        prefabField.SetValueWithoutNotify(prefab);
        patchNameField?.SetValueWithoutNotify(
            PrefabPatchAuthoringWorkflow.DefaultPatchName(prefab)
        );
        RefreshValidation();
    }

    private void RefreshValidation()
    {
#if REDUX
        var modId = PrefabPatchAuthoringWorkflow.ReduxModId;
#else
        var modId = "selected-mod";
        var modDiagnostic = string.Empty;
        if (
            PrefabPatchModAssetUtility.TryGetInfo(
                owningModField?.value,
                true,
                out var modInfo,
                out var modError
            )
        )
        {
            modId = modInfo.Id;
        }
        else
        {
            modDiagnostic = modError;
        }
#endif
        var diagnostics = PrefabPatchAuthoringWorkflow.Validate(
            linkedPrefab,
            outputField?.value,
            modId,
            patchNameField?.value
        );
#if !REDUX
        if (!string.IsNullOrWhiteSpace(modDiagnostic))
            diagnostics.Insert(0, modDiagnostic);
#endif
        var valid = diagnostics.Count == 0;
        createButton?.SetEnabled(valid);
        if (validationBox == null)
            return;
        validationBox.style.display = valid
            ? DisplayStyle.None
            : DisplayStyle.Flex;
        validationBox.text = string.Join("\n", diagnostics);
    }

    private void ChooseOutputFolder()
    {
        var initial = Path.GetFullPath(
            string.IsNullOrWhiteSpace(outputField.value)
                ? "Assets"
                : outputField.value
        );
        var selected = EditorUtility.OpenFolderPanel(
            "Prefab Patch Output Folder",
            initial,
            string.Empty
        );
        if (string.IsNullOrWhiteSpace(selected))
            return;
        var relative = FileUtil.GetProjectRelativePath(selected);
        if (string.IsNullOrWhiteSpace(relative))
        {
            EditorUtility.DisplayDialog(
                "Create Prefab Patch",
                "Choose a folder inside this project's Assets folder.",
                "OK"
            );
            return;
        }
        outputField.value = relative.Replace('\\', '/');
    }

    private void CreatePatch()
    {
        try
        {
#if REDUX
            Object owningMod = null;
#else
            if (
                !PrefabPatchModAssetUtility.TryGetInfo(
                    owningModField.value,
                    true,
                    out var modInfo,
                    out var modError
                )
            )
            {
                throw new InvalidOperationException(modError);
            }
            var owningMod = modInfo.Asset;
            EditorPrefs.SetString(
                OwningModPreference,
                PrefabPatchModAssetUtility.GetAssetGuid(owningMod)
            );
            EditorPrefs.SetString(OutputPreference, outputField.value.Trim());
#endif
            var result = PrefabPatchAuthoringWorkflow.Create(
                linkedPrefab,
                outputField.value,
                patchNameField.value,
                openVariantToggle.value,
                owningMod
            );
            Debug.Log(
                $"[KSP2UnityTools.PrefabPatching] Created prefab patch variant "
                    + $"'{result.VariantPath}' from '{linkedPrefab.name}'."
            );
            Close();
        }
        catch (Exception exception)
        {
            validationBox.text = exception.Message;
            validationBox.style.display = DisplayStyle.Flex;
            Debug.LogException(exception);
        }
    }

    private static string GetDefaultOutputFolder(Object modAsset)
    {
#if REDUX
        return PrefabPatchAuthoringWorkflow.ReduxOutputFolder;
#else
        if (
            PrefabPatchModAssetUtility.TryGetInfo(
                modAsset,
                false,
                out var info,
                out _
            )
        )
        {
            return info.SuggestedOutputFolder;
        }
        return EditorPrefs.GetString(
            OutputPreference,
            "Assets/PrefabPatches"
        );
#endif
    }
}

internal static class PrefabPatchAutoCompiler
{
    private static readonly HashSet<string> ForcedVariantPaths =
        new(StringComparer.Ordinal);

    internal static void QueueVariant(string variantPath)
    {
        if (string.IsNullOrWhiteSpace(variantPath))
            return;
        ForcedVariantPaths.Add(variantPath.Replace('\\', '/'));
        QueueCompile();
    }

    internal static void DiscardPending(string variantPath)
    {
        ForcedVariantPaths.Remove(
            (variantPath ?? string.Empty).Replace('\\', '/')
        );
        if (ForcedVariantPaths.Count == 0)
        {
            EditorApplication.delayCall -= CompilePending;
        }
    }

    private static void QueueCompile()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        EditorApplication.delayCall -= CompilePending;
        EditorApplication.delayCall += CompilePending;
    }

    private static void CompilePending()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        var variantPaths = ForcedVariantPaths.ToArray();
        ForcedVariantPaths.Clear();
        var candidates =
            new HashSet<PrefabPatchAuthoringMetadata>();
        foreach (var variantPath in variantPaths)
        {
            var metadata = AssetDatabase
                .LoadAssetAtPath<GameObject>(variantPath)
                ?.GetComponent<PrefabPatchAuthoringMetadata>();
            if (metadata != null)
                candidates.Add(metadata);
        }

        foreach (var metadata in candidates)
        {
            if (
                metadata == null
                || !metadata.AutoCompileOnSave
            )
            {
                continue;
            }

            try
            {
                var result = PrefabPatchAuthoringWorkflow.Compile(metadata);
                SceneView.lastActiveSceneView?.ShowNotification(
                    new GUIContent(
                        $"Compiled {result.Manifest.Operations.Count} prefab "
                            + "patch operation(s)."
                    ),
                    3
                );
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[KSP2UnityTools.PrefabPatching] Auto-compile failed for "
                        + $"'{AssetDatabase.GetAssetPath(metadata)}': "
                        + exception
                );
            }
        }
    }
}

[InitializeOnLoad]
internal static class PrefabPatchPrefabStageSaveHook
{
    static PrefabPatchPrefabStageSaveHook()
    {
        PrefabStage.prefabSaved -= OnPrefabSaved;
        PrefabStage.prefabSaved += OnPrefabSaved;
    }

    private static void OnPrefabSaved(GameObject prefabRoot)
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        var path = stage?.assetPath;
        if (string.IsNullOrWhiteSpace(path))
            path = AssetDatabase.GetAssetPath(prefabRoot);
        PrefabPatchAutoCompiler.QueueVariant(path);
    }
}

internal sealed class PrefabPatchAssetSaveHook
    : AssetModificationProcessor
{
    private static string[] OnWillSaveAssets(string[] paths)
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            foreach (
                var path in paths.Where(path =>
                    path.EndsWith(
                        ".prefab",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            )
            {
                PrefabPatchAutoCompiler.QueueVariant(path);
            }
        }
        return paths;
    }
}
}
