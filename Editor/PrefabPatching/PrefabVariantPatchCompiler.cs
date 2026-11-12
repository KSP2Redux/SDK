using Ksp2UnityTools.Editor.LinkedAddressables;
using Ksp2UnityTools.Editor.Modding;
using Ksp2UnityTools.PrefabPatchingAuthoring;
using PatchManager.PrefabPatching;
using Redux.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using SpaceWarp2.API.Features;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ksp2UnityTools.Editor.PrefabPatching
{

/// <summary>
/// Strict Unity-prefab-variant frontend for the public prefab patch schema.
/// Unsupported overrides fail compilation with source-specific diagnostics.
/// </summary>
public static class PrefabVariantPatchCompiler
{
    /// <summary>
    /// Compile and save the manifest owned by persistent variant metadata.
    /// </summary>
    /// <param name="metadata">The persistent authoring metadata.</param>
    /// <returns>The compiled manifest and its authoring paths.</returns>
    public static PrefabPatchCompileResult Compile(
        PrefabPatchAuthoringMetadata metadata
    )
    {
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));
        metadata =
            PrefabPatchAuthoringWorkflow.GetPersistentMetadata(metadata);
        if (metadata == null)
        {
            throw new InvalidOperationException(
                "Prefab patch metadata must be stored on a persistent prefab "
                    + "variant root."
            );
        }
#if !REDUX
        var modInfo = PrefabPatchModAssetUtility.ApplyOwnership(
            metadata,
            true
        );
        var modId = modInfo.Id;
        var addressablesLabel = modInfo.PrefabPatchLabel;
#else
        var modId = PrefabPatchAuthoringWorkflow.ReduxModId;
        var addressablesLabel =
            PrefabPatchAuthoringWorkflow.ReduxPrefabPatchLabel;
#endif
        var variant =
            PrefabPatchAuthoringWorkflow.GetVariantAsset(metadata);
        var outputPath =
            PrefabPatchAuthoringWorkflow.GetOutputManifestPath(metadata);

        return Compile(
            variant,
            outputPath,
            modId,
            metadata.PatchName,
            metadata.Pass,
            metadata.Ordering,
            metadata.NeedsMods,
            metadata.ConflictsMods,
            metadata.NeedsPatches,
            metadata.ConflictsPatches,
            metadata.BeforePatches,
            metadata.AfterPatches,
            metadata.BeforeMods,
            metadata.AfterMods,
            metadata.ConfigurationInputs,
            true,
#if REDUX
            null,
#else
            modInfo.AllGroup,
#endif
            addressablesLabel,
            metadata.Features,
            metadata.NoFeatures
        );
    }

    /// <summary>
    /// Compile a variant and write its manifest through the normal authoring workflow.
    /// </summary>
    /// <param name="variant">The persistent prefab variant.</param>
    /// <param name="outputPath">The Assets-relative JSON output path.</param>
    /// <param name="modId">The owning mod ID.</param>
    /// <param name="patchName">The patch name within the mod.</param>
    /// <param name="pass">The execution pass.</param>
    /// <param name="ordering">The ordering bucket within the pass.</param>
    /// <param name="needsMods">The required mod IDs.</param>
    /// <param name="conflictsMods">The conflicting mod IDs.</param>
    /// <param name="needsPatches">The required patch IDs.</param>
    /// <param name="conflictsPatches">The conflicting patch IDs.</param>
    /// <param name="beforePatches">The patch IDs that must follow this patch.</param>
    /// <param name="afterPatches">The patch IDs that must precede this patch.</param>
    /// <param name="beforeMods">The mod IDs whose patches must follow this patch.</param>
    /// <param name="afterMods">The mod IDs whose patches must precede this patch.</param>
    /// <param name="configurationInputs">The configuration inputs included in cache identity.</param>
    /// <param name="makeAddressable">True to register the output manifest.</param>
    /// <param name="targetAddressablesGroup">The requested mod group, when applicable.</param>
    /// <param name="addressablesLabel">The discovery label, or null for the owning mod default.</param>
    /// <param name="features">The required features.</param>
    /// <param name="noFeatures">The excluded features.</param>
    /// <returns>The compiled manifest and its authoring paths.</returns>
    public static PrefabPatchCompileResult Compile(
        GameObject variant,
        string outputPath,
        string modId,
        string patchName,
        PrefabPatchPass pass = PrefabPatchPass.Default,
        PrefabPatchOrdering ordering = PrefabPatchOrdering.Default,
        IEnumerable<string> needsMods = null,
        IEnumerable<string> conflictsMods = null,
        IEnumerable<string> needsPatches = null,
        IEnumerable<string> conflictsPatches = null,
        IEnumerable<string> beforePatches = null,
        IEnumerable<string> afterPatches = null,
        IEnumerable<string> beforeMods = null,
        IEnumerable<string> afterMods = null,
        IEnumerable<string> configurationInputs = null,
        bool makeAddressable = true,
        AddressableAssetGroup targetAddressablesGroup = null,
        string addressablesLabel = null,
        IEnumerable<string> features = null,
        IEnumerable<string> noFeatures = null
    )
    {
        if (
            string.IsNullOrWhiteSpace(outputPath)
            || !outputPath.StartsWith("Assets/", StringComparison.Ordinal)
            || !outputPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new PrefabPatchCompileException(
                new[] { "Output path must be an Assets-relative .json path." }
            );
        }

        var bakedMetadata = GetBakedBuiltinMetadata(variant, outputPath, modId);
        var manifest = CompileManifest(
            variant, modId, patchName, pass, ordering, needsMods, conflictsMods,
            needsPatches, conflictsPatches, beforePatches, afterPatches,
            beforeMods, afterMods, configurationInputs, features, noFeatures
        );
        EnsureParentFolder(outputPath);
        File.WriteAllText(outputPath, PrefabPatchJson.Serialize(manifest) + "\n");
        AssetDatabase.ImportAsset(
            outputPath,
            ImportAssetOptions.ForceSynchronousImport
        );
        if (bakedMetadata != null)
        {
            RemoveBakedManifestEntry(bakedMetadata);
            Debug.LogWarning(
                $"Prefab patch '{outputPath}' is baked into ordinary source. "
                    + $"Its JSON remains authoring evidence only. Edit "
                    + $"'{AssetDatabase.GetAssetPath(bakedMetadata.BakedSource)}' "
                    + "and its complementary source directly."
            );
        }
        else if (makeAddressable)
        {
            MakeManifestAddressable(
                outputPath,
                targetAddressablesGroup,
                string.IsNullOrWhiteSpace(addressablesLabel)
                    ? modId.Trim() + PrefabPatchSchema.AddressablesLabelSuffix
                    : addressablesLabel.Trim(),
                manifest.Features,
                manifest.NoFeatures
            );
        }
        if (bakedMetadata == null)
            AssetDatabase.SaveAssets();
        PrefabPatchOwnership.Bind(manifest, modId);
        return new PrefabPatchCompileResult
        {
            Manifest = manifest,
            OutputPath = outputPath,
            VariantPath = AssetDatabase.GetAssetPath(variant),
            BaseDescriptorPath = AssetDatabase.GetAssetPath(
                PrefabUtility.GetCorrespondingObjectFromSource(variant)
            )
        };
    }

    /// <summary>
    /// Compile current variant overrides without writing, importing or saving assets.
    /// </summary>
    /// <remarks>
    /// Linked sources retain their external address. Ordinary prefab sources require
    /// a persistent GUID and local ID plus an explicit Addressables entry with
    /// either a unique address or a strictly complementary ordinary prefab pair.
    /// Locators and the fingerprint describe the current authoring base.
    /// </remarks>
    /// <param name="variant">The persistent prefab variant.</param>
    /// <param name="modId">The owning mod ID.</param>
    /// <param name="patchName">The patch name within the mod.</param>
    /// <param name="pass">The execution pass.</param>
    /// <param name="ordering">The ordering bucket within the pass.</param>
    /// <param name="needsMods">The required mod IDs.</param>
    /// <param name="conflictsMods">The conflicting mod IDs.</param>
    /// <param name="needsPatches">The required patch IDs.</param>
    /// <param name="conflictsPatches">The conflicting patch IDs.</param>
    /// <param name="beforePatches">The patch IDs that must follow this patch.</param>
    /// <param name="afterPatches">The patch IDs that must precede this patch.</param>
    /// <param name="beforeMods">The mod IDs whose patches must follow this patch.</param>
    /// <param name="afterMods">The mod IDs whose patches must precede this patch.</param>
    /// <param name="configurationInputs">The configuration inputs included in cache identity.</param>
    /// <param name="features">The required features.</param>
    /// <param name="noFeatures">The excluded features.</param>
    /// <returns>The ownership-free manifest compiled from current persistent source objects.</returns>
    public static PrefabPatchManifest CompileManifest(
        GameObject variant,
        string modId,
        string patchName,
        PrefabPatchPass pass = PrefabPatchPass.Default,
        PrefabPatchOrdering ordering = PrefabPatchOrdering.Default,
        IEnumerable<string> needsMods = null,
        IEnumerable<string> conflictsMods = null,
        IEnumerable<string> needsPatches = null,
        IEnumerable<string> conflictsPatches = null,
        IEnumerable<string> beforePatches = null,
        IEnumerable<string> afterPatches = null,
        IEnumerable<string> beforeMods = null,
        IEnumerable<string> afterMods = null,
        IEnumerable<string> configurationInputs = null,
        IEnumerable<string> features = null,
        IEnumerable<string> noFeatures = null
    )
    {
        var diagnostics = new List<string>();
        if (variant == null || !EditorUtility.IsPersistent(variant))
            diagnostics.Add("A persistent prefab variant asset is required.");
        if (string.IsNullOrWhiteSpace(modId))
            diagnostics.Add("An owning mod ID is required.");
        if (string.IsNullOrWhiteSpace(patchName))
            diagnostics.Add("A patch name is required.");
        if (diagnostics.Count > 0)
            throw new PrefabPatchCompileException(diagnostics);
        var variantPath = AssetDatabase.GetAssetPath(variant);
        if (
            PrefabUtility.GetPrefabAssetType(variant)
            != PrefabAssetType.Variant
        )
        {
            throw new PrefabPatchCompileException(
                new[]
                {
                    $"'{variantPath}' is not a prefab variant. Visual prefab "
                        + "patches must preserve their declared authoring base."
                }
            );
        }

        var basePrefab =
            PrefabUtility.GetCorrespondingObjectFromSource(variant)
            as GameObject;
        if (
            basePrefab == null
            || !TryPrefabIdentity(basePrefab, out var rootIdentity)
        )
        {
            throw new PrefabPatchCompileException(
                new[]
                {
                    $"Variant '{variantPath}' is not based directly on a "
                        + "mapped linked prefab or an ordinary prefab with a "
                        + "feature-unambiguous explicit Addressables entry and persistent identity."
                }
            );
        }

        var patchNameValue = patchName.Trim();
        var manifest = new PrefabPatchManifest
        {
            PatchName = patchNameValue,
            Pass = pass,
            Ordering = ordering,
            TargetPrefab = rootIdentity,
            NeedsMods = Sorted(needsMods),
            ConflictsMods = Sorted(conflictsMods),
            NeedsPatches = Sorted(needsPatches),
            ConflictsPatches = Sorted(conflictsPatches),
            BeforePatches = Sorted(beforePatches),
            AfterPatches = Sorted(afterPatches),
            BeforeMods = Sorted(beforeMods),
            AfterMods = Sorted(afterMods),
            ConfigurationInputs = Sorted(configurationInputs),
            Features = QualifiedFeatures(modId, features),
            NoFeatures = QualifiedFeatures(modId, noFeatures)
        };

        CompileProperties(
            variant,
            basePrefab,
            patchNameValue,
            variantPath,
            manifest,
            diagnostics
        );
        CompileAddedObjects(
            variant,
            basePrefab,
            patchNameValue,
            variantPath,
            manifest,
            diagnostics
        );
        CompileRemovedObjects(
            variant,
            basePrefab,
            patchNameValue,
            variantPath,
            manifest,
            diagnostics
        );
        CompileAddedComponents(
            variant,
            basePrefab,
            patchNameValue,
            variantPath,
            manifest,
            diagnostics
        );
        CompileRemovedComponents(
            variant,
            basePrefab,
            patchNameValue,
            variantPath,
            manifest,
            diagnostics
        );
        if (diagnostics.Count > 0)
            throw new PrefabPatchCompileException(diagnostics);

        for (var index = 0; index < manifest.Operations.Count; index++)
        {
            manifest.Operations[index].PatchId = patchNameValue;
        }
        manifest.DeclaredCapabilities = manifest.Operations
            .Select(value => value.Kind.ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        manifest.ManifestHash = PrefabPatchJson.CalculateManifestHash(manifest);

        return manifest;
    }

    private static void CompileProperties(
        GameObject variant,
        GameObject basePrefab,
        string patchId,
        string variantPath,
        PrefabPatchManifest manifest,
        ICollection<string> diagnostics
    )
    {
        var emittedContainers = new HashSet<string>(StringComparer.Ordinal);
        var authoredObjects = variant.GetComponentsInChildren<Component>(true)
            .Where(component => component != null)
            .Cast<Object>()
            .Concat(variant.GetComponentsInChildren<Transform>(true)
                .Select(transform => (Object)transform.gameObject))
            .ToArray();
        foreach (
            var modification in (
                PrefabUtility.GetPropertyModifications(variant)
                ?? Array.Empty<PropertyModification>()
            )
                .Where(value => !PrefabUtility.IsDefaultOverride(value))
                .Where(value => !IsPatchOwnedAddition(value.target))
                // Shader property IDs depend on the editor or player process. The effects component rebuilds them
                // from the authored parameter names in Awake, so serializing these caches creates unstable patches.
                .Where(value => value.target?.GetType().FullName != "KSP.VFX.ThrottleBlendshapeData"
                    || value.propertyPath?.EndsWith(".ParamHash", StringComparison.Ordinal) != true)
                .Where(value => !IsRedundantSourceValue(value))
                .OrderBy(value => SourceSortKey(value.target), StringComparer.Ordinal)
                // A collection must be resized before applying its new children.
                .ThenBy(
                    value => value.propertyPath.Replace(".Array.size", ".Array.!size"),
                    StringComparer.Ordinal
                )
        )
        {
            if (
                !TryStockTarget(
                    basePrefab,
                    modification.target,
                    out var target,
                    out var targetError
                )
            )
            {
                diagnostics.Add(
                    $"{variantPath}: property '{modification.propertyPath}' "
                        + targetError
                );
                continue;
            }

            // Emit complete curves, gradients and quaternions from their serialized children.
            // Applying quaternion components separately normalizes incomplete rotations.
            var authoredTarget = authoredObjects.FirstOrDefault(candidate =>
                PrefabUtility.GetCorrespondingObjectFromSource(candidate)
                    == modification.target);
            if (authoredTarget != null && TryCompileContainerProperty(
                authoredTarget, modification.propertyPath, target, patchId,
                variantPath, manifest, emittedContainers, diagnostics))
                continue;

            var operation = new PrefabPatchOperation
            {
                OperationId = OperationId(
                    patchId,
                    "property",
                    target.CanonicalKey,
                    modification.propertyPath
                ),
                PatchId = patchId,
                Target = target,
                PropertyPath = modification.propertyPath,
                AuthoringAssetPath = variantPath,
                AuthoringPropertyPath = modification.propertyPath
            };
            if (modification.objectReference != null)
            {
                if (
                    !TryPropertyObjectReference(
                        basePrefab,
                        patchId,
                        modification.objectReference,
                        out var reference,
                        out var referenceError
                    )
                )
                {
                    diagnostics.Add(
                        $"{variantPath}: object-reference override "
                            + $"'{modification.propertyPath}' {referenceError}"
                    );
                    continue;
                }

                operation.Kind = PrefabPatchOperationKind.SetObjectReference;
                operation.ObjectReference = reference;
            }
            else if (
                modification.target is GameObject
                && modification.propertyPath == "m_IsActive"
            )
            {
                operation.Kind = PrefabPatchOperationKind.SetActive;
                if (
                    !TryParseBool(modification.value, out var active)
                )
                {
                    diagnostics.Add(
                        $"{variantPath}: active override "
                            + $"'{modification.value}' is not boolean."
                    );
                    continue;
                }

                operation.Value = PrefabPatchValue.FromBoolean(active);
            }
            else
            {
                operation.Kind = PrefabPatchOperationKind.SetValue;
                if (
                    !TryPropertyValue(
                        authoredTarget ?? modification.target,
                        modification.propertyPath,
                        modification.value,
                        out var value,
                        out var valueError
                    )
                )
                {
                    diagnostics.Add(
                        $"{variantPath}: property override "
                            + $"'{modification.propertyPath}' {valueError}"
                    );
                    continue;
                }

                operation.Value = value;
            }

            manifest.Operations.Add(operation);
        }
    }

    private static bool TryCompileContainerProperty(
        Object authoredTarget,
        string propertyPath,
        PrefabPatchObjectTarget target,
        string patchId,
        string variantPath,
        PrefabPatchManifest manifest,
        HashSet<string> emittedContainers,
        ICollection<string> diagnostics
    )
    {
        using var authored = new SerializedObject(authoredTarget);
        while (!string.IsNullOrEmpty(propertyPath))
        {
            var property = authored.FindProperty(propertyPath);
            if (property != null && property.propertyType is
                SerializedPropertyType.Gradient or SerializedPropertyType.AnimationCurve or SerializedPropertyType.Quaternion)
            {
                if (!emittedContainers.Add(target.CanonicalKey + ":" + propertyPath))
                    return true;
                if (!TrySerializedPropertyValue(property, out var value, out var error))
                {
                    diagnostics.Add($"{variantPath}: property '{propertyPath}' {error}");
                    return true;
                }
                manifest.Operations.Add(new PrefabPatchOperation
                {
                    OperationId = OperationId(patchId, "property", target.CanonicalKey, propertyPath),
                    PatchId = patchId,
                    Target = target,
                    Kind = PrefabPatchOperationKind.SetValue,
                    PropertyPath = propertyPath,
                    Value = value,
                    AuthoringAssetPath = variantPath,
                    AuthoringPropertyPath = propertyPath
                });
                return true;
            }
            var separator = propertyPath.LastIndexOf('.');
            propertyPath = separator < 0 ? null : propertyPath.Substring(0, separator);
        }
        return false;
    }

    private static bool IsRedundantSourceValue(
        PropertyModification modification
    )
    {
        if (
            modification?.target == null
            || string.IsNullOrWhiteSpace(modification.propertyPath)
        )
        {
            return false;
        }

        using var serializedObject = new SerializedObject(modification.target);
        var sourceProperty = serializedObject.FindProperty(
            modification.propertyPath
        );
        if (sourceProperty == null)
            return false;

        if (
            sourceProperty.propertyType
            == SerializedPropertyType.ObjectReference
        )
        {
            return sourceProperty.objectReferenceValue
                == modification.objectReference;
        }

        if (modification.objectReference != null)
            return false;

        switch (sourceProperty.propertyType)
        {
            case SerializedPropertyType.Boolean:
                return TryParseBool(modification.value, out var boolean)
                    && sourceProperty.boolValue == boolean;
            case SerializedPropertyType.ArraySize:
                return int.TryParse(
                        modification.value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var arraySize
                    )
                    && sourceProperty.intValue == arraySize;
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.Enum:
                return long.TryParse(
                        modification.value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var integer
                    )
                    && sourceProperty.longValue == integer;
            case SerializedPropertyType.Float:
                return TryParseFloatingPoint(modification.value, out var floating)
                    && !double.IsNaN(floating)
                    && !double.IsInfinity(floating)
                    && !double.IsNaN(sourceProperty.doubleValue)
                    && !double.IsInfinity(sourceProperty.doubleValue)
                    && (sourceProperty.numericType == SerializedPropertyNumericType.Double
                        ? BitConverter.DoubleToInt64Bits(sourceProperty.doubleValue)
                            == BitConverter.DoubleToInt64Bits(floating)
                        : BitConverter.SingleToInt32Bits(sourceProperty.floatValue)
                            == BitConverter.SingleToInt32Bits((float)floating));
            case SerializedPropertyType.String:
                return string.Equals(
                    sourceProperty.stringValue,
                    modification.value,
                    StringComparison.Ordinal
                );
            default:
                return false;
        }
    }

    private static bool IsPatchOwnedAddition(Object target)
    {
        if (target is Component component)
        {
            return PrefabUtility.IsAddedComponentOverride(component)
                || PrefabUtility.IsAddedGameObjectOverride(
                    component.gameObject
                );
        }
        return target is GameObject gameObject
            && PrefabUtility.IsAddedGameObjectOverride(gameObject);
    }

    private static void CompileAddedObjects(
        GameObject variant,
        GameObject basePrefab,
        string patchId,
        string variantPath,
        PrefabPatchManifest manifest,
        ICollection<string> diagnostics
    )
    {
        var added = PrefabUtility.GetAddedGameObjects(variant)
            .Cast<object>()
            .Select(
                value => GetMember<GameObject>(value, "instanceGameObject")
            )
            .Where(value => value != null)
            .ToList();
        var addedSet = new HashSet<GameObject>(added);
        foreach (
            var gameObject in added
                .Where(
                    value =>
                        value.transform.parent == null
                        || !addedSet.Contains(
                            value.transform.parent.gameObject
                        )
                )
                .OrderBy(
                    value =>
                        AnimationUtility.CalculateTransformPath(
                            value.transform,
                            variant.transform
                        ),
                    StringComparer.Ordinal
                )
        )
        {
            PrefabPatchObjectTarget parent = null;
            if (gameObject.transform.parent != variant.transform)
            {
                var sourceParent =
                    PrefabUtility.GetCorrespondingObjectFromSource(
                        gameObject.transform.parent.gameObject
                    );
                if (
                    !TryStockTarget(
                        basePrefab,
                        sourceParent,
                        out parent,
                        out var parentError
                    )
                )
                {
                    diagnostics.Add(
                        $"{variantPath}: added object '{gameObject.name}' "
                            + $"parent {parentError}"
                    );
                    continue;
                }
            }
            else
            {
                TryStockTarget(
                    basePrefab,
                    basePrefab,
                    out parent,
                    out _
                );
            }

            var fragment = CompileFragment(
                gameObject,
                basePrefab,
                patchId,
                variantPath,
                diagnostics
            );
            if (fragment == null)
                continue;
            manifest.Operations.Add(
                new PrefabPatchOperation
                {
                    OperationId = OperationId(
                        patchId,
                        "add-object",
                        parent?.CanonicalKey,
                        fragment.ObjectId
                    ),
                    PatchId = patchId,
                    Kind = PrefabPatchOperationKind.AddObject,
                    Target = parent,
                    AddedObject = fragment,
                    AuthoringAssetPath = variantPath
                }
            );
        }
    }

    private static void CompileRemovedObjects(
        GameObject variant,
        GameObject basePrefab,
        string patchId,
        string variantPath,
        PrefabPatchManifest manifest,
        ICollection<string> diagnostics
    )
    {
        foreach (
            var wrapper in PrefabUtility.GetRemovedGameObjects(variant)
                .Cast<object>()
        )
        {
            var removed = GetMember<GameObject>(wrapper, "assetGameObject");
            if (
                !TryStockTarget(
                    basePrefab,
                    removed,
                    out var target,
                    out var error
                )
            )
            {
                diagnostics.Add(
                    $"{variantPath}: removed GameObject '{removed?.name}' {error}"
                );
                continue;
            }

            manifest.Operations.Add(
                new PrefabPatchOperation
                {
                    OperationId = OperationId(
                        patchId,
                        "suppress",
                        target.CanonicalKey
                    ),
                    PatchId = patchId,
                    Kind = PrefabPatchOperationKind.SuppressObject,
                    Target = target,
                    AuthoringAssetPath = variantPath
                }
            );
        }
    }

    private static void CompileAddedComponents(
        GameObject variant,
        GameObject basePrefab,
        string patchId,
        string variantPath,
        PrefabPatchManifest manifest,
        ICollection<string> diagnostics
    )
    {
        var ordinals = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (
            var wrapper in PrefabUtility.GetAddedComponents(variant)
                .Cast<object>()
        )
        {
            var component = GetMember<Component>(
                wrapper,
                "instanceComponent"
            );
            if (
                component == null
                || IsAuthoringComponent(component)
                || PrefabUtility.IsAddedGameObjectOverride(
                    component.gameObject
                )
            )
            {
                // Components on an added GameObject are serialized once inside
                // that object's fragment. Unity also reports them through
                // GetAddedComponents, but they have no inherited stock target.
                continue;
            }
            var sourceObject =
                PrefabUtility.GetCorrespondingObjectFromSource(
                    component.gameObject
                );
            if (
                !TryStockTarget(
                    basePrefab,
                    sourceObject,
                    out var target,
                    out var targetError
                )
            )
            {
                diagnostics.Add(
                    $"{variantPath}: added component "
                        + $"'{component.GetType().FullName}' target {targetError}"
                );
                continue;
            }

            var ordinalKey =
                target.CanonicalKey + ":" + StableTypeName(component.GetType());
            ordinals.TryGetValue(ordinalKey, out var ordinal);
            ordinals[ordinalKey] = ordinal + 1;
            var fragment = CompileComponent(
                component,
                "component-"
                    + Sha256
                        .OfString(
                            target.CanonicalKey
                                + ":"
                                + StableTypeName(component.GetType())
                                + ":"
                                + ordinal
                        )
                        .Substring(0, 16),
                basePrefab,
                patchId,
                variantPath,
                diagnostics
            );
            if (fragment == null)
                continue;
            manifest.Operations.Add(
                new PrefabPatchOperation
                {
                    OperationId = OperationId(
                        patchId,
                        "add-component",
                        target.CanonicalKey,
                        fragment.ComponentId
                    ),
                    PatchId = patchId,
                    Kind = PrefabPatchOperationKind.AddComponent,
                    Target = target,
                    AddedComponent = fragment,
                    AuthoringAssetPath = variantPath
                }
            );
        }
    }

    private static void CompileRemovedComponents(
        GameObject variant,
        GameObject basePrefab,
        string patchId,
        string variantPath,
        PrefabPatchManifest manifest,
        ICollection<string> diagnostics
    )
    {
        foreach (
            var wrapper in PrefabUtility.GetRemovedComponents(variant)
                .Cast<object>()
        )
        {
            var component = GetMember<Component>(wrapper, "assetComponent");
            if (
                !TryStockTarget(
                    basePrefab,
                    component,
                    out var target,
                    out var error
                )
            )
            {
                diagnostics.Add(
                    $"{variantPath}: removed component "
                        + $"'{component?.GetType().FullName}' {error}"
                );
                continue;
            }

            manifest.Operations.Add(
                new PrefabPatchOperation
                {
                    OperationId = OperationId(
                        patchId,
                        "remove-component",
                        target.CanonicalKey
                    ),
                    PatchId = patchId,
                    Kind = PrefabPatchOperationKind.RemoveComponent,
                    Target = target,
                    AuthoringAssetPath = variantPath
                }
            );
        }
    }

    private static PrefabPatchObjectFragment CompileFragment(
        GameObject gameObject,
        GameObject basePrefab,
        string patchId,
        string variantPath,
        ICollection<string> diagnostics
    )
    {
        var marker = gameObject.GetComponent<PrefabPatchAuthoringObjectId>();
        var objectId = marker?.Id;
        if (marker != null && string.IsNullOrWhiteSpace(objectId))
        {
            // Unity 6 can expose a newly added managed component through the
            // prefab override API before its public property reflects the
            // serialized backing field. Read the authoring data directly.
            using var serializedMarker = new SerializedObject(marker);
            objectId = serializedMarker.FindProperty("id")?.stringValue;
        }
        if (string.IsNullOrWhiteSpace(objectId))
        {
            var componentSummary = string.Join(
                ", ",
                gameObject
                    .GetComponents<Component>()
                    .Select(
                        value =>
                            value == null
                                ? "<missing-script>"
                                : value.GetType().AssemblyQualifiedName
                    )
            );
            diagnostics.Add(
                $"{variantPath}: added GameObject '{gameObject.name}' must "
                    + "have a PrefabPatchAuthoringObjectId with an explicit "
                    + "non-empty "
                    + $"ID. Components seen: {componentSummary}."
            );
            return null;
        }

        var fragment = new PrefabPatchObjectFragment
        {
            ObjectId = objectId,
            Name = gameObject.name,
            TransformType = StableTypeName(gameObject.transform.GetType()),
            Active = gameObject.activeSelf,
            Layer = gameObject.layer,
            Tag = gameObject.tag,
            IsStatic = gameObject.isStatic,
            LocalPosition = Vector3Value(gameObject.transform.localPosition),
            LocalRotation = QuaternionValue(gameObject.transform.localRotation),
            LocalScale = Vector3Value(gameObject.transform.localScale)
        };
        if (gameObject.transform is RectTransform rectTransform)
        {
            fragment.AnchorMin = Vector2Value(rectTransform.anchorMin);
            fragment.AnchorMax = Vector2Value(rectTransform.anchorMax);
            fragment.AnchoredPosition = Vector2Value(
                rectTransform.anchoredPosition
            );
            fragment.SizeDelta = Vector2Value(rectTransform.sizeDelta);
            fragment.Pivot = Vector2Value(rectTransform.pivot);
        }
        var componentOrdinal = 0;
        foreach (var component in gameObject.GetComponents<Component>())
        {
            if (
                component == null
                || component is Transform
                || IsAuthoringComponent(component)
            )
            {
                continue;
            }

            var compiled = CompileComponent(
                component,
                $"{objectId}:component-{componentOrdinal++}",
                basePrefab,
                patchId,
                variantPath,
                diagnostics
            );
            if (compiled != null)
                fragment.Components.Add(compiled);
        }

        foreach (
            Transform child in gameObject.transform.Cast<Transform>()
                .OrderBy(value => value.GetSiblingIndex())
        )
        {
            var compiled = CompileFragment(
                child.gameObject,
                basePrefab,
                patchId,
                variantPath,
                diagnostics
            );
            if (compiled != null)
                fragment.Children.Add(compiled);
        }

        return fragment;
    }

    private static PrefabPatchComponentFragment CompileComponent(
        Component component,
        string componentId,
        GameObject basePrefab,
        string patchId,
        string variantPath,
        ICollection<string> diagnostics
    )
    {
        if (component == null)
        {
            diagnostics.Add(
                $"{variantPath}: an added component has a missing script."
            );
            return null;
        }

        var componentType = component.GetType();
        if (
            componentType.IsAbstract
            || !typeof(Component).IsAssignableFrom(componentType)
            || typeof(Transform).IsAssignableFrom(componentType)
        )
        {
            diagnostics.Add(
                $"{variantPath}: added component "
                    + $"'{componentType.AssemblyQualifiedName}' cannot be "
                    + "constructed as a runtime Component."
            );
            return null;
        }

        var fragment = new PrefabPatchComponentFragment
        {
            ComponentId = componentId,
            ComponentType = StableTypeName(componentType)
        };
        try
        {
            using var source = new SerializedObject(component);
            source.Update();
            var iterator = source.GetIterator();
            var enterChildren = true;
            while (iterator.Next(enterChildren))
            {
                enterChildren = true;
                if (IsComponentInfrastructureProperty(iterator.propertyPath))
                {
                    enterChildren = false;
                    continue;
                }
                if (
                    iterator.propertyType
                        == SerializedPropertyType.ObjectReference
                    || iterator.propertyType
                        == SerializedPropertyType.ExposedReference
                )
                {
                    enterChildren = false;
                    var referencedObject =
                        iterator.propertyType
                            == SerializedPropertyType.ExposedReference
                        ? iterator.exposedReferenceValue
                        : iterator.objectReferenceValue;
                    if (referencedObject == null)
                        continue;
                    if (
                        !TryComponentObjectReference(
                            basePrefab,
                            patchId,
                            referencedObject,
                            out var reference,
                            out var error
                        )
                    )
                    {
                        diagnostics.Add(
                            $"{variantPath}: added component "
                                + $"'{componentType.FullName}' property "
                                + $"'{iterator.propertyPath}' {error}"
                        );
                        return null;
                    }

                    fragment.References.Add(
                        new PrefabPatchSerializedReference
                        {
                            PropertyPath = iterator.propertyPath,
                            Reference = reference
                        }
                    );
                    continue;
                }

                if (
                    !TrySerializedPropertyValue(
                        iterator,
                        out var serializedValue,
                        out var valueError
                    )
                )
                {
                    if (valueError != null)
                    {
                        diagnostics.Add(
                            $"{variantPath}: added component "
                                + $"'{componentType.FullName}' property "
                                + $"'{iterator.propertyPath}' {valueError}"
                        );
                        return null;
                    }
                    enterChildren =
                        iterator.propertyType
                            == SerializedPropertyType.Generic
                        || iterator.propertyType
                            == SerializedPropertyType.ManagedReference;
                    continue;
                }

                fragment.Values.Add(
                    new PrefabPatchSerializedValue
                    {
                        PropertyPath = iterator.propertyPath,
                        Value = serializedValue
                    }
                );
                enterChildren =
                    iterator.propertyType
                        == SerializedPropertyType.ManagedReference;
            }

            return fragment;
        }
        catch (Exception exception)
        {
            diagnostics.Add(
                $"{variantPath}: could not serialize added component "
                    + $"'{componentType.AssemblyQualifiedName}': "
                    + exception.Message
            );
            return null;
        }
    }

    private static bool IsComponentInfrastructureProperty(string path) =>
        path
            is "m_ObjectHideFlags"
                or "m_CorrespondingSourceObject"
                or "m_PrefabInstance"
                or "m_PrefabAsset"
                or "m_GameObject"
                or "m_Name"
                or "m_EditorHideFlags"
                or "m_EditorClassIdentifier"
                or "m_Script";

    private static bool TrySerializedPropertyValue(
        SerializedProperty property,
        out PrefabPatchValue value,
        out string error
    )
    {
        value = null;
        error = null;
        switch (property.propertyType)
        {
            case SerializedPropertyType.Generic:
                // Serialized children are emitted individually.
                return false;
            case SerializedPropertyType.ManagedReference:
                if (property.managedReferenceValue == null)
                    return false;
                value = new PrefabPatchValue
                {
                    Kind = PrefabPatchValueKind.ManagedReference,
                    SerializedType = StableTypeName(
                        property.managedReferenceValue.GetType()
                    )
                };
                return true;
            case SerializedPropertyType.Boolean:
                value = PrefabPatchValue.FromBoolean(property.boolValue);
                return true;
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.Enum:
            case SerializedPropertyType.LayerMask:
            case SerializedPropertyType.Character:
            case SerializedPropertyType.FixedBufferSize:
                value = PrefabPatchValue.FromInteger(property.longValue);
                return true;
            case SerializedPropertyType.ArraySize:
                value = new PrefabPatchValue
                {
                    Kind = PrefabPatchValueKind.ArraySize,
                    Integer = property.intValue
                };
                return true;
            case SerializedPropertyType.Float:
                value = PrefabPatchValue.FromFloat(property.doubleValue);
                return true;
            case SerializedPropertyType.String:
                value = PrefabPatchValue.FromString(property.stringValue);
                return true;
            case SerializedPropertyType.Vector2:
                value = Vector2Value(property.vector2Value);
                return true;
            case SerializedPropertyType.Vector3:
                value = Vector3Value(property.vector3Value);
                return true;
            case SerializedPropertyType.Vector4:
                value = new PrefabPatchValue
                {
                    Kind = PrefabPatchValueKind.Vector4,
                    X = property.vector4Value.x,
                    Y = property.vector4Value.y,
                    Z = property.vector4Value.z,
                    W = property.vector4Value.w
                };
                return true;
            case SerializedPropertyType.Quaternion:
                value = QuaternionValue(property.quaternionValue);
                return true;
            case SerializedPropertyType.Color:
                value = new PrefabPatchValue
                {
                    Kind = PrefabPatchValueKind.Color,
                    X = property.colorValue.r,
                    Y = property.colorValue.g,
                    Z = property.colorValue.b,
                    W = property.colorValue.a
                };
                return true;
            case SerializedPropertyType.AnimationCurve:
                value = JsonValue(
                    new
                    {
                        Keys = property.animationCurveValue.keys,
                        PreWrapMode =
                            (int)property.animationCurveValue.preWrapMode,
                        PostWrapMode =
                            (int)property.animationCurveValue.postWrapMode
                    },
                    typeof(AnimationCurve)
                );
                return true;
            case SerializedPropertyType.Gradient:
            {
                var gradient = property.gradientValue;
                value = JsonValue(
                    new
                    {
                        ColorKeys = gradient.colorKeys.Select(key => new
                        {
                            color = new
                            {
                                key.color.r,
                                key.color.g,
                                key.color.b,
                                key.color.a
                            },
                            key.time
                        }).ToArray(),
                        AlphaKeys = gradient.alphaKeys,
                        Mode = (int)gradient.mode
                    },
                    typeof(Gradient)
                );
                return true;
            }
            case SerializedPropertyType.Hash128:
                value = JsonValue(
                    property.hash128Value.ToString(),
                    typeof(Hash128)
                );
                return true;
        }

        try
        {
            var boxed = property.boxedValue;
            if (boxed == null)
                return false;
            value = JsonValue(boxed, boxed.GetType());
            return true;
        }
        catch (Exception exception)
        {
            error =
                $"uses serialized type '{property.propertyType}' which could "
                + $"not be captured generically: {exception.Message}";
            return false;
        }
    }

    // Assembly-qualified type names without the assembly's version, culture and key. The game assembly's version is
    // generated on every build, and keeping it changed every patch, and every ID hashed from a type name, on each
    // compile. The runtime resolves the shortened names the same way.
    private static readonly System.Text.RegularExpressions.Regex AssemblyDetails = new(
        @", Version=[^,\]]*, Culture=[^,\]]*, PublicKeyToken=[^,\]]*",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant
    );

    private static string StableTypeName(Type type) =>
        StripAssemblyDetails(type.AssemblyQualifiedName);

    private static string StripAssemblyDetails(string typeName) =>
        string.IsNullOrEmpty(typeName)
            ? typeName
            : AssemblyDetails.Replace(typeName, string.Empty);

    private static PrefabPatchValue JsonValue(object value, Type type) =>
        new()
        {
            Kind = PrefabPatchValueKind.Json,
            SerializedType = StableTypeName(type),
            String = JsonConvert.SerializeObject(
                value,
                PrefabPatchJson.Settings
            )
        };

    private static bool TryStockTarget(
        GameObject baseRoot,
        Object sourceObject,
        out PrefabPatchObjectTarget target,
        out string error
    )
    {
        target = null;
        error = null;
        if (sourceObject == null)
        {
            error = "has no persistent source object.";
            return false;
        }

        string sourceType;
        if (LinkedAddressableEditorIdentity.TryGet(sourceObject, out var identity))
        {
            sourceType = NormalizeTypeName(identity.SourceType);
        }
        else
        {
            if (!TryOrdinaryPrefabObject(baseRoot, out var rootGuid, out _)
                || !TryOrdinaryPrefabObject(sourceObject, out var sourceGuid, out _)
                || !string.Equals(rootGuid, sourceGuid, StringComparison.Ordinal))
            {
                error = "does not resolve to a mapped linked object or a persistent "
                    + "object in the declared ordinary prefab source.";
                return false;
            }
            sourceType = sourceObject.GetType().FullName;
        }

        var gameObject = sourceObject switch
        {
            GameObject value => value,
            Component value => value.gameObject,
            _ => null
        };
        if (gameObject == null)
        {
            error =
                $"has unsupported target type '{sourceObject.GetType().FullName}'.";
            return false;
        }

        PrefabPatchRuntimeLocator locator;
        try
        {
            var path = PrefabPatchStructure.GetSiblingPath(
                baseRoot.transform,
                gameObject.transform
            );
            if (sourceObject is Component component)
            {
                var components = gameObject.GetComponents(component.GetType());
                var ordinal = Array.IndexOf(components, component);
                if (ordinal < 0)
                    throw new InvalidOperationException(
                        "Component ordinal could not be determined."
                    );
                locator = new PrefabPatchRuntimeLocator
                {
                    SiblingIndices = path,
                    TargetKind = PrefabPatchRuntimeTargetKind.Component,
                    ComponentType =
                        component.GetType().FullName,
                    ComponentOrdinal = ordinal,
                    DisplayPath = AnimationUtility.CalculateTransformPath(
                        gameObject.transform,
                        baseRoot.transform
                    )
                };
            }
            else
            {
                locator = new PrefabPatchRuntimeLocator
                {
                    SiblingIndices = path,
                    TargetKind = PrefabPatchRuntimeTargetKind.GameObject,
                    DisplayPath = AnimationUtility.CalculateTransformPath(
                        gameObject.transform,
                        baseRoot.transform
                    )
                };
            }
        }
        catch (Exception exception)
        {
            error = "has no valid structural locator: " + exception.Message;
            return false;
        }

        target = new PrefabPatchObjectTarget
        {
            Kind = PrefabPatchTargetKind.Stock,
            ObjectType = sourceType,
            RuntimeLocator = locator
        };
        return true;
    }

    private static bool TryObjectReference(
        Object value,
        out PrefabPatchObjectReference reference,
        out string error
    )
    {
        reference = null;
        error = null;
        if (LinkedAddressableEditorIdentity.TryGet(value, out var identity))
        {
            reference = new PrefabPatchObjectReference
            {
                Address = identity.Address,
                ExpectedType = StripAssemblyDetails(identity.SourceType)
            };
            return true;
        }

        var path = AssetDatabase.GetAssetPath(value);
        var guid = AssetDatabase.AssetPathToGUID(path);
        var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
        var entry =
            string.IsNullOrWhiteSpace(guid)
                ? null
                : addressableSettings?.FindAssetEntry(guid);
        if (entry == null || string.IsNullOrWhiteSpace(entry.address))
        {
            error =
                $"references '{path}' but it is neither linked nor an "
                + "explicit mod-owned Addressable entry.";
            return false;
        }

        reference = new PrefabPatchObjectReference
        {
            // Imported model meshes need their subasset key, not the model root.
            Address = AssetDatabase.IsSubAsset(value)
                ? $"{entry.address}[{value.name}]"
                : entry.address,
            ExpectedType = StableTypeName(value.GetType())
        };
        return true;
    }

    private static bool TryPropertyObjectReference(
        GameObject basePrefab,
        string patchId,
        Object value,
        out PrefabPatchObjectReference reference,
        out string error
    ) => TryOrdinaryPrefabObject(basePrefab, out _, out _)
        ? TryComponentObjectReference(basePrefab, patchId, value, out reference, out error)
        : TryObjectReference(value, out reference, out error);

    private static bool TryComponentObjectReference(
        GameObject basePrefab,
        string patchId,
        Object value,
        out PrefabPatchObjectReference reference,
        out string error
    )
    {
        reference = null;
        error = null;
        var gameObject = value switch
        {
            GameObject candidate => candidate,
            Component candidate => candidate.gameObject,
            _ => null
        };
        if (gameObject != null)
        {
            var marker =
                gameObject.GetComponent<PrefabPatchAuthoringObjectId>();
            if (
                marker != null
                && PrefabUtility.IsAddedGameObjectOverride(gameObject)
            )
            {
                var objectId = ReadAuthoringObjectId(marker);
                if (string.IsNullOrWhiteSpace(objectId))
                {
                    error =
                        $"references added object '{gameObject.name}' without "
                        + "a PrefabPatchAuthoringObjectId.";
                    return false;
                }

                PrefabPatchObjectTarget target;
                if (
                    value is Component component
                    && component is not Transform
                    && !IsAuthoringComponent(component)
                )
                {
                    var components = gameObject
                        .GetComponents<Component>()
                        .Where(
                            candidate =>
                                candidate != null
                                && candidate is not Transform
                                && !IsAuthoringComponent(candidate)
                        )
                        .ToArray();
                    var ordinal = Array.IndexOf(components, component);
                    if (ordinal < 0)
                    {
                        error =
                            $"references component '{component.GetType().FullName}' "
                            + "whose patch component ID could not be determined.";
                        return false;
                    }
                    target = new PrefabPatchObjectTarget
                    {
                        Kind = PrefabPatchTargetKind.PatchComponent,
                        OwnerPatchId = patchId,
                        ComponentId =
                            $"{objectId}:component-{ordinal}",
                        ObjectType = StableTypeName(component.GetType())
                    };
                }
                else
                {
                    target = new PrefabPatchObjectTarget
                    {
                        Kind = PrefabPatchTargetKind.PatchOwned,
                        OwnerPatchId = patchId,
                        ObjectId = objectId,
                        ObjectType = StableTypeName(value.GetType()),
                        RuntimeLocator =
                            value is Transform transform
                                ? new PrefabPatchRuntimeLocator
                                {
                                    TargetKind =
                                        PrefabPatchRuntimeTargetKind.Component,
                                    ComponentType =
                                        StableTypeName(transform.GetType()),
                                    ComponentOrdinal = 0,
                                    SiblingIndices = Array.Empty<int>()
                                }
                                : new PrefabPatchRuntimeLocator
                                {
                                    TargetKind =
                                        PrefabPatchRuntimeTargetKind.GameObject,
                                    SiblingIndices = Array.Empty<int>()
                                }
                    };
                }

                reference = PrefabPatchObjectReference.FromTarget(
                    target,
                    value.GetType().AssemblyQualifiedName
                );
                return true;
            }

            var source =
                PrefabUtility.GetCorrespondingObjectFromSource(value);
            if (source == null && TryOrdinaryPrefabObject(basePrefab, out _, out _))
                source = value;
            if (
                source != null
                && TryStockTarget(
                    basePrefab,
                    source,
                    out var stockTarget,
                    out _
                )
            )
            {
                reference = PrefabPatchObjectReference.FromTarget(
                    stockTarget,
                    value.GetType().AssemblyQualifiedName
                );
                return true;
            }

            if (
                value is Component addedComponent
                && PrefabUtility.IsAddedComponentOverride(addedComponent)
            )
            {
                var sourceHost =
                    PrefabUtility.GetCorrespondingObjectFromSource(
                        addedComponent.gameObject
                    );
                if (
                    TryStockTarget(
                        basePrefab,
                        sourceHost,
                        out var hostTarget,
                        out var hostError
                    )
                )
                {
                    var variantRoot =
                        PrefabUtility.GetOutermostPrefabInstanceRoot(
                            addedComponent.gameObject
                        );
                    var matching = PrefabUtility
                        .GetAddedComponents(variantRoot)
                        .Cast<object>()
                        .Select(
                            wrapper =>
                                GetMember<Component>(
                                    wrapper,
                                    "instanceComponent"
                                )
                        )
                        .Where(
                            candidate =>
                                candidate != null
                                && candidate.gameObject
                                    == addedComponent.gameObject
                                && candidate.GetType()
                                    == addedComponent.GetType()
                        )
                        .ToList();
                    var ordinal = matching.IndexOf(addedComponent);
                    if (ordinal >= 0)
                    {
                        var componentId =
                            "component-"
                            + Sha256
                                .OfString(
                                    hostTarget.CanonicalKey
                                        + ":"
                                        + addedComponent
                                            .GetType()
                                            .AssemblyQualifiedName
                                        + ":"
                                        + ordinal
                                )
                                .Substring(0, 16);
                        reference = PrefabPatchObjectReference.FromTarget(
                            new PrefabPatchObjectTarget
                            {
                                Kind =
                                    PrefabPatchTargetKind.PatchComponent,
                                OwnerPatchId = patchId,
                                ComponentId = componentId,
                                ObjectType =
                                    addedComponent
                                        .GetType()
                                        .AssemblyQualifiedName
                            },
                            addedComponent
                                .GetType()
                                .AssemblyQualifiedName
                        );
                        return true;
                    }
                }

                error =
                    $"references added component "
                    + $"'{addedComponent.GetType().FullName}' but its stable "
                    + $"patch component ID could not be derived: {hostError}";
                return false;
            }
        }

        return TryObjectReference(value, out reference, out error);
    }

    private static string ReadAuthoringObjectId(
        PrefabPatchAuthoringObjectId marker
    )
    {
        if (marker == null)
            return null;
        if (!string.IsNullOrWhiteSpace(marker.Id))
            return marker.Id;
        using var serializedMarker = new SerializedObject(marker);
        return serializedMarker.FindProperty("id")?.stringValue;
    }

    private static bool IsAuthoringComponent(Component component) =>
        component is PrefabPatchAuthoringMetadata
        or PrefabPatchAuthoringObjectId;

    private static bool TryPrefabIdentity(
        GameObject basePrefab,
        out PrefabPatchPrefabIdentity identity
    )
    {
        identity = null;
        string address;
        string sourceType;
        if (LinkedAddressableEditorIdentity.TryGet(basePrefab, out var linked))
        {
            address = linked.Address;
            sourceType = NormalizeTypeName(linked.SourceType);
        }
        else
        {
            if (!TryOrdinaryPrefabObject(basePrefab, out var guid, out _)
                || AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(basePrefab)) != basePrefab)
                return false;

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var entry = settings?.FindAssetEntry(guid);
            if (entry == null || !string.Equals(entry.guid, guid, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(entry.address))
                return false;

            // Resolve the exact GUID registration. Names and recovered aliases
            // do not establish which persistent prefab owns an authoring base.
            var siblings = settings.groups.Where(group => group != null)
                .SelectMany(group => group.entries)
                .Where(candidate => string.Equals(candidate.address, entry.address, StringComparison.Ordinal))
                .ToArray();
            if (siblings.Length != 1 && !IsComplementarySourcePair(siblings))
                return false;

            address = entry.address;
            sourceType = typeof(GameObject).FullName;
        }

        identity = new PrefabPatchPrefabIdentity
        {
            Address = address,
            AssetType = sourceType,
            StructuralFingerprint = PrefabPatchStructure.Calculate(basePrefab)
        };
        return true;
    }

    private static bool TryOrdinaryPrefabObject(
        Object value,
        out string guid,
        out long localId
    )
    {
        guid = null;
        localId = 0;
        if (value == null || value is not (GameObject or Component)
            || !EditorUtility.IsPersistent(value))
            return false;

        string path = AssetDatabase.GetAssetPath(value);
        return !string.IsNullOrWhiteSpace(path)
            && (path.StartsWith("Assets/", StringComparison.Ordinal)
                || path.StartsWith("Packages/", StringComparison.Ordinal))
            && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
            && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out guid, out localId)
            && !string.IsNullOrWhiteSpace(guid)
            && localId != 0
            && string.Equals(AssetDatabase.AssetPathToGUID(path), guid, StringComparison.Ordinal)
            && string.Equals(AssetDatabase.GUIDToAssetPath(guid), path, StringComparison.Ordinal);
    }

    private static bool TryPropertyValue(
        Object target,
        string propertyPath,
        string recordedValue,
        out PrefabPatchValue value,
        out string error
    )
    {
        value = null;
        error = null;
        using var serializedObject = new SerializedObject(target);
        serializedObject.Update();
        var property = serializedObject.FindProperty(propertyPath);
        if (property == null)
        {
            error = "is not present in the target SerializedObject.";
            return false;
        }

        // PropertyModification.target is the persistent source object for a
        // prefab override. Reading the SerializedProperty from it therefore
        // returns the stock value, not the value Unity recorded on the
        // variant. Scalar prefab overrides carry their authored value in
        // PropertyModification.value and must be parsed from there.
        switch (property.propertyType)
        {
            case SerializedPropertyType.Boolean:
                if (TryParseBool(recordedValue, out var boolean))
                {
                    value = PrefabPatchValue.FromBoolean(boolean);
                    return true;
                }
                break;
            case SerializedPropertyType.ArraySize:
                if (
                    int.TryParse(
                        recordedValue,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var arraySize
                    )
                )
                {
                    value = new PrefabPatchValue
                    {
                        Kind = PrefabPatchValueKind.ArraySize,
                        Integer = arraySize
                    };
                    return true;
                }
                break;
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.Enum:
            case SerializedPropertyType.LayerMask:
            case SerializedPropertyType.Character:
            case SerializedPropertyType.FixedBufferSize:
                if (
                    long.TryParse(
                        recordedValue,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var integer
                    )
                )
                {
                    value = PrefabPatchValue.FromInteger(integer);
                    return true;
                }
                break;
            case SerializedPropertyType.Float:
                if (
                    TryParseFloatingPoint(recordedValue, out var floating)
                )
                {
                    value = PrefabPatchValue.FromFloat(floating);
                    return true;
                }
                break;
            case SerializedPropertyType.String:
                value = PrefabPatchValue.FromString(recordedValue);
                return true;
        }

        if (
            TrySerializedPropertyValue(
                property,
                out value,
                out var serializationError
            )
        )
            return true;
        error =
            serializationError
            ?? $"uses container type '{property.propertyType}' without a "
                + "serializable leaf value.";
        return false;
    }

    private static bool TryParseFloatingPoint(string value, out double result)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            return false;
        // Mono can discard the sign of zero, including negative underflow.
        if (result == 0d && value.TrimStart().StartsWith("-", StringComparison.Ordinal))
            result = BitConverter.Int64BitsToDouble(long.MinValue);
        return true;
    }

    private static bool TryParseBool(string value, out bool result)
    {
        if (bool.TryParse(value, out result))
            return true;
        if (value == "1")
        {
            result = true;
            return true;
        }
        if (value == "0")
        {
            result = false;
            return true;
        }
        return false;
    }

    private static PrefabPatchValue Vector3Value(Vector3 value) =>
        new()
        {
            Kind = PrefabPatchValueKind.Vector3,
            X = value.x,
            Y = value.y,
            Z = value.z
        };

    private static PrefabPatchValue Vector2Value(Vector2 value) =>
        new()
        {
            Kind = PrefabPatchValueKind.Vector2,
            X = value.x,
            Y = value.y
        };

    private static PrefabPatchValue QuaternionValue(Quaternion value) =>
        new()
        {
            Kind = PrefabPatchValueKind.Quaternion,
            X = value.x,
            Y = value.y,
            Z = value.z,
            W = value.w
        };

    // Bare ids belong to the owning mod, as PrefabPatchBuilder.Feature has them
    private static string[] QualifiedFeatures(
        string modId,
        IEnumerable<string> features
    ) =>
        Sorted(
            (features ?? Array.Empty<string>())
                .Where(feature => !string.IsNullOrWhiteSpace(feature))
                .Select(feature => FeatureRegistry.Qualify(modId, feature.Trim()))
        );

    private static string[] Sorted(IEnumerable<string> values) =>
        (values ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static string OperationId(params string[] parts) =>
        "op-"
        + Sha256.OfString(
            string.Join("|", parts.Select(value => value ?? ""))
        ).Substring(0, 16);

    private static string SourceSortKey(Object value)
    {
        if (LinkedAddressableEditorIdentity.TryGet(value, out var identity))
            return identity.ToString();
        if (TryOrdinaryPrefabObject(value, out var guid, out var localId))
            return $"prefab:{guid}:{localId.ToString(CultureInfo.InvariantCulture)}:{value.GetType().FullName}";
        return value == null ? "" : value.GetType().AssemblyQualifiedName;
    }

    private static string NormalizeTypeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;
        var resolved = Type.GetType(value, false);
        if (resolved != null)
            return resolved.FullName;
        var comma = value.IndexOf(',');
        return comma < 0 ? value : value.Substring(0, comma).Trim();
    }

    private static T GetMember<T>(object instance, string name)
        where T : class
    {
        var type = instance.GetType();
        var field = type.GetField(
            name,
            BindingFlags.Instance | BindingFlags.Public
        );
        if (field != null)
            return field.GetValue(instance) as T;
        var property = type.GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public
        );
        if (property != null)
            return property.GetValue(instance) as T;
        throw new InvalidOperationException(
            $"Unity override wrapper '{type.FullName}' has no public member "
                + $"'{name}'."
        );
    }

    private static void EnsureParentFolder(string assetPath)
    {
        var directory = Path.GetDirectoryName(assetPath)
            ?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(directory))
            return;
        var parts = directory.Split('/');
        var current = parts[0];
        for (var index = 1; index < parts.Length; index++)
        {
            var next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[index]);
            current = next;
        }
    }

    private static bool IsComplementarySourcePair(AddressableAssetEntry[] entries)
    {
        if (entries.Length != 2 || entries[0].guid == entries[1].guid || entries[0].parentGroup != entries[1].parentGroup)
            return false;
        foreach (var entry in entries)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.AssetPath);
            if (prefab == null || PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.Regular
                || AssetDatabase.LoadMainAssetAtPath(entry.AssetPath) != prefab
                || !TryOrdinaryPrefabObject(prefab, out var guid, out _) || guid != entry.guid
                || LinkedAddressableEditorIdentity.TryGet(prefab, out _))
                return false;
        }
        return Redux.Features.FeatureAssetIndex.TryGetComplementaryDefault(
            FeatureSelector.FromConfigTags(FeatureCatalog.REDUX_MOD_ID, entries[0].labels),
            FeatureSelector.FromConfigTags(FeatureCatalog.REDUX_MOD_ID, entries[1].labels), out _);
    }

    private static PrefabPatchAuthoringMetadata GetBakedBuiltinMetadata(
        GameObject variant, string outputPath, string modId)
    {
#if REDUX
        var metadata = variant == null ? null : variant.GetComponent<PrefabPatchAuthoringMetadata>();
        if (metadata == null || string.IsNullOrEmpty(metadata.BakedAuthoringGuid)
            || AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(variant)) != metadata.BakedAuthoringGuid)
            return null;
        if (modId != PrefabPatchAuthoringWorkflow.ReduxModId
            || outputPath != PrefabPatchAuthoringWorkflow.GetOutputManifestPath(metadata))
            throw new InvalidOperationException("A baked built-in authoring variant cannot publish another runtime manifest.");
        ValidateBakedBuiltinMetadata(metadata);
        return metadata;
#else
        return null;
#endif
    }

    private static void RemoveBakedManifestEntry(PrefabPatchAuthoringMetadata metadata)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var entry = settings.FindAssetEntry(metadata.BakedManifestGuid);
        if (entry != null)
        {
            var group = entry.parentGroup;
            if (!settings.RemoveAssetEntry(metadata.BakedManifestGuid))
                throw new InvalidOperationException("Could not exclude the baked built-in manifest from Addressables.");
            AssetDatabase.SaveAssetIfDirty(group);
            AssetDatabase.SaveAssetIfDirty(settings);
        }
        if (settings.FindAssetEntry(metadata.BakedManifestGuid, true) != null)
            throw new InvalidOperationException("An Addressables folder still includes the baked manifest. Exclude that discovery before building.");
    }

#if REDUX
    private static readonly string[] BUILTIN_AUTHORING_ROOTS =
    {
        PrefabPatchAuthoringWorkflow.ReduxOutputFolder,
        "Assets/ReduxAssets/Plumes/Patches"
    };

    private static bool IsBuiltinAuthoringPath(string path) =>
        !string.IsNullOrEmpty(path)
        && BUILTIN_AUTHORING_ROOTS.Any(root => path.StartsWith($"{root}/", StringComparison.Ordinal));

    /// <summary>
    /// Persist a built-in manifest retirement after its ordinary source pair is published and verified.
    /// </summary>
    /// <remarks>
    /// The caller must first qualify independent saved-graph readback and canonical publication for this exact
    /// pair. Existence and feature labels alone do not establish baked behavior. The historical manifest hash
    /// authorizes activation only. Subsequent edits belong to the ordinary source pair and require no old capture.
    /// </remarks>
    /// <param name="metadata">The exact persistent built-in authoring owner from the accepted publication.</param>
    /// <param name="manifestGuid">The actual manifest GUID retired by that publication.</param>
    /// <param name="manifestHash">The ownership-free manifest hash qualified by the source save.</param>
    /// <param name="source">The published ordinary prefab containing the patch behavior.</param>
    /// <param name="alternative">The published ordinary prefab excluding the complementary feature.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the authoring owner, manifest, source identities or publication entries differ.
    /// </exception>
    public static void RetirePublishedBuiltinManifest(PrefabPatchAuthoringMetadata metadata,
        string manifestGuid, string manifestHash, GameObject source, GameObject alternative)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Source publication retirement requires an idle stopped editor.");
        if (metadata == null || PrefabPatchAuthoringWorkflow.GetPersistentMetadata(metadata) != metadata
            || !IsBuiltinAuthoringPath(PrefabPatchAuthoringWorkflow.GetVariantPath(metadata)))
            throw new InvalidOperationException("Retirement requires the persistent built-in authoring owner.");
        string output = PrefabPatchAuthoringWorkflow.GetOutputManifestPath(metadata);
        if (string.IsNullOrEmpty(manifestGuid) || AssetDatabase.AssetPathToGUID(output) != manifestGuid
            || string.IsNullOrEmpty(manifestHash) || manifestHash.Length != 64)
            throw new InvalidOperationException("Retirement requires the observed manifest GUID and qualified hash.");
        var variant = PrefabPatchAuthoringWorkflow.GetVariantAsset(metadata);
        var compiled = CompileManifest(variant, PrefabPatchAuthoringWorkflow.ReduxModId, metadata.PatchName,
            metadata.Pass, metadata.Ordering, metadata.NeedsMods, metadata.ConflictsMods,
            metadata.NeedsPatches, metadata.ConflictsPatches, metadata.BeforePatches, metadata.AfterPatches,
            metadata.BeforeMods, metadata.AfterMods, metadata.ConfigurationInputs, metadata.Features, metadata.NoFeatures);
        if (compiled.ManifestHash != manifestHash)
            throw new InvalidOperationException("The qualified source-save manifest differs from current normal compilation.");
        PrefabPatchOwnership.Bind(compiled, PrefabPatchAuthoringWorkflow.ReduxModId);
        ValidatePublishedBuiltinPair(metadata, source, alternative, true);
        string variantPath = AssetDatabase.GetAssetPath(variant);
        string authoringGuid = AssetDatabase.AssetPathToGUID(variantPath);
        if (!string.IsNullOrEmpty(metadata.BakedAuthoringGuid)
            && (metadata.BakedAuthoringGuid != authoringGuid || metadata.BakedManifestGuid != manifestGuid
                || metadata.BakedManifestHash != manifestHash || metadata.BakedSource != source || metadata.BakedAlternative != alternative))
            throw new InvalidOperationException("The authoring owner already records a different publication.");

        using (var serialized = new SerializedObject(metadata))
        {
            serialized.FindProperty("_bakedAuthoringGuid").stringValue = authoringGuid;
            serialized.FindProperty("_bakedManifestGuid").stringValue = manifestGuid;
            serialized.FindProperty("_bakedManifestHash").stringValue = manifestHash;
            serialized.FindProperty("_bakedSource").objectReferenceValue = source;
            serialized.FindProperty("_bakedAlternative").objectReferenceValue = alternative;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorUtility.SetDirty(metadata);
        AssetDatabase.SaveAssetIfDirty(metadata);
        AssetDatabase.ImportAsset(variantPath, ImportAssetOptions.ForceSynchronousImport);
        metadata = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(authoringGuid))
            .GetComponent<PrefabPatchAuthoringMetadata>();
        if (metadata == null || metadata.BakedAuthoringGuid != authoringGuid || metadata.BakedManifestGuid != manifestGuid
            || metadata.BakedManifestHash != manifestHash || metadata.BakedSource != source || metadata.BakedAlternative != alternative)
            throw new InvalidOperationException("Saved authoring retirement differs from the accepted publication.");
        ValidateBakedBuiltinMetadata(metadata);
        RemoveBakedManifestEntry(metadata);
    }

    /// <summary>
    /// Reject duplicate built-in discovery before the normal Addressables build collects its entries.
    /// </summary>
    /// <returns>The number of persistent authoring owners with validated retirements.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a retired authoring owner's published pair or manifest identity is missing or its entry reappears.
    /// </exception>
    public static int ValidateBakedBuiltinManifestRetirements()
    {
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", BUILTIN_AUTHORING_ROOTS))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var metadata = prefab == null ? null : prefab.GetComponent<PrefabPatchAuthoringMetadata>();
            if (metadata == null || string.IsNullOrEmpty(metadata.BakedAuthoringGuid) || metadata.BakedAuthoringGuid != guid)
                continue;
            ValidateBakedBuiltinMetadata(metadata);
            if (AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(metadata.BakedManifestGuid, true) != null)
                throw new InvalidOperationException("A baked built-in manifest is discoverable again. Compile its authoring owner before building.");
            count++;
        }
        return count;
    }

    private static void ValidateBakedBuiltinMetadata(PrefabPatchAuthoringMetadata metadata)
    {
        if (metadata == null || AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(metadata)) != metadata.BakedAuthoringGuid
            || string.IsNullOrEmpty(metadata.BakedManifestGuid) || metadata.BakedManifestHash?.Length != 64
            || AssetDatabase.AssetPathToGUID(PrefabPatchAuthoringWorkflow.GetOutputManifestPath(metadata)) != metadata.BakedManifestGuid)
            throw new InvalidOperationException("The baked built-in authoring or manifest identity is missing or changed.");
        ValidatePublishedBuiltinPair(metadata, metadata.BakedSource, metadata.BakedAlternative);
    }

    private static void ValidatePublishedBuiltinPair(PrefabPatchAuthoringMetadata metadata, GameObject source, GameObject alternative,
        bool requireRemovedEntry = false)
    {
        if (source == null || alternative == null || source == alternative
            || AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(source)) != source
            || AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(alternative)) != alternative
            || PrefabUtility.GetPrefabAssetType(source) != PrefabAssetType.Regular
            || PrefabUtility.GetPrefabAssetType(alternative) != PrefabAssetType.Regular
            || source.GetComponent<PrefabPatchAuthoringMetadata>() != null || alternative.GetComponent<PrefabPatchAuthoringMetadata>() != null
            || !TryOrdinaryPrefabObject(source, out var sourceGuid, out _)
            || !TryOrdinaryPrefabObject(alternative, out var alternativeGuid, out _)
            || PrefabUtility.GetCorrespondingObjectFromSource(metadata.gameObject) != alternative)
            throw new InvalidOperationException("Retirement requires the saved ordinary pair and the original authoring base.");
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            throw new InvalidOperationException("Source Addressables settings are missing.");
        var current = settings.FindAssetEntry(sourceGuid);
        var excluding = settings.FindAssetEntry(alternativeGuid);
        if (current == null || excluding == null || string.IsNullOrWhiteSpace(current.address) || current.address != excluding.address)
            throw new InvalidOperationException("Published complementary sources must have explicit entries under the same primary address.");
        var siblings = settings.groups.Where(group => group != null).SelectMany(group => group.entries)
            .Where(entry => entry.address == current.address).ToArray();
        if (!IsComplementarySourcePair(siblings)
            || !Redux.Features.FeatureAssetIndex.TryGetComplementaryDefault(
                FeatureSelector.FromConfigTags(FeatureCatalog.REDUX_MOD_ID, current.labels),
                FeatureSelector.FromConfigTags(FeatureCatalog.REDUX_MOD_ID, excluding.labels), out bool currentIsDefault)
            || currentIsDefault || (requireRemovedEntry && settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(
                PrefabPatchAuthoringWorkflow.GetOutputManifestPath(metadata)), true) != null))
            throw new InvalidOperationException("The exact published pair or completed manifest discovery retirement is missing.");
        foreach (var prefab in new[] { source, alternative })
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Assets/DoNotDistribute/", StringComparison.Ordinal))
                throw new InvalidOperationException("Retirement cannot use a probe or external source prefab.");
        }
    }
#endif

    private static void MakeManifestAddressable(
        string outputPath,
        AddressableAssetGroup requestedGroup,
        string addressablesLabel,
        IReadOnlyCollection<string> features,
        IReadOnlyCollection<string> noFeatures
    )
    {
        var addressableSettings =
            AddressableAssetSettingsDefaultObject.Settings;
        if (addressableSettings == null)
        {
            throw new InvalidOperationException(
                "Addressables settings do not exist; the compiled manifest "
                    + "cannot be discovered at runtime."
            );
        }

        var guid = AssetDatabase.AssetPathToGUID(outputPath);
#if REDUX
        const string reduxGroupName = "PM Prefab Patches";
        var targetGroup = addressableSettings.FindGroup(reduxGroupName);
        if (targetGroup == null)
        {
            throw new InvalidOperationException(
                $"The Redux Addressables group '{reduxGroupName}' does not "
                    + "exist. Create or restore it before compiling prefab "
                    + "patches."
                );
        }
#else
        var targetGroup =
            requestedGroup ?? addressableSettings.DefaultGroup;
#endif
        var entry = addressableSettings.CreateOrMoveEntry(guid, targetGroup);
        entry.address = outputPath;
        entry.SetLabel(addressablesLabel, true, true);
        addressableSettings.SetDirty(
            AddressableAssetSettings.ModificationEvent.EntryModified,
            entry,
            true
        );
#if REDUX
        // Redux content states its feature on the asset, and the manifest's
        // own features say which. A patch with none keeps any label it was
        // given by hand.
        if (features.Count == 0)
        {
            if (!entry.labels.Any(label =>
                    label.StartsWith(
                        FeatureSelector.FEATURE_TAG_PREFIX,
                        StringComparison.Ordinal
                    )))
            {
                Debug.LogError(
                    $"The Redux prefab patch '{outputPath}' requires no "
                        + "feature. Set its Required Features so campaigns "
                        + "without them skip it."
                );
            }

            return;
        }

        AssetFeatureLabels.Apply(
            new[] { outputPath },
            FeatureCatalog.REDUX_MOD_ID,
            features,
            noFeatures
        );
#endif
    }
}
}
