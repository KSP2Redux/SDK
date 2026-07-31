using BundleKit.LinkedAddressables.Editor;
using Ksp2UnityTools.PrefabPatchingAuthoring;
using PatchManager.PrefabPatching;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ksp2UnityTools.Editor.PrefabPatching;

/// <summary>
/// Strict Unity-prefab-variant frontend for the public prefab patch schema.
/// Unsupported overrides fail compilation with source-specific diagnostics.
/// </summary>
public static class PrefabVariantPatchCompiler
{
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
            addressablesLabel
        );
    }

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
        string addressablesLabel = null
    )
    {
        var diagnostics = new List<string>();
        if (variant == null)
            diagnostics.Add("A prefab variant asset is required.");
        if (string.IsNullOrWhiteSpace(modId))
            diagnostics.Add("An owning mod ID is required.");
        if (string.IsNullOrWhiteSpace(patchName))
            diagnostics.Add("A patch name is required.");
        if (
            string.IsNullOrWhiteSpace(outputPath)
            || !outputPath.StartsWith("Assets/", StringComparison.Ordinal)
            || !outputPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
        )
        {
            diagnostics.Add(
                "Output path must be an Assets-relative .json path."
            );
        }

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
            || !LinkedAddressableEditorIdentity.TryGet(
                basePrefab,
                out var rootIdentity
            )
        )
        {
            throw new PrefabPatchCompileException(
                new[]
                {
                    $"Variant '{variantPath}' is not based directly on a "
                        + "BundleKit linked prefab with a public source map."
                }
            );
        }

        var patchNameValue = patchName.Trim();
        var manifest = new PrefabPatchManifest
        {
            PatchName = patchNameValue,
            Pass = pass,
            Ordering = ordering,
            TargetPrefab = BuildPrefabIdentity(basePrefab, rootIdentity),
            NeedsMods = Sorted(needsMods),
            ConflictsMods = Sorted(conflictsMods),
            NeedsPatches = Sorted(needsPatches),
            ConflictsPatches = Sorted(conflictsPatches),
            BeforePatches = Sorted(beforePatches),
            AfterPatches = Sorted(afterPatches),
            BeforeMods = Sorted(beforeMods),
            AfterMods = Sorted(afterMods),
            ConfigurationInputs = Sorted(configurationInputs)
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
            manifest.Operations[index].PatchId = patchNameValue;
        manifest.DeclaredCapabilities = manifest.Operations
            .Select(value => value.Kind.ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        manifest.ManifestHash = PrefabPatchJson.CalculateManifestHash(manifest);

        EnsureParentFolder(outputPath);
        File.WriteAllText(outputPath, PrefabPatchJson.Serialize(manifest) + "\n");
        AssetDatabase.ImportAsset(
            outputPath,
            ImportAssetOptions.ForceSynchronousImport
        );
        if (makeAddressable)
            MakeManifestAddressable(
                outputPath,
                targetAddressablesGroup,
                string.IsNullOrWhiteSpace(addressablesLabel)
                    ? modId.Trim()
                        + PrefabPatchSchema.AddressablesLabelSuffix
                    : addressablesLabel.Trim()
            );
        AssetDatabase.SaveAssets();
        PrefabPatchOwnership.Bind(manifest, modId);
        return new PrefabPatchCompileResult
        {
            Manifest = manifest,
            OutputPath = outputPath,
            VariantPath = variantPath,
            BaseDescriptorPath = rootIdentity.DescriptorPath
        };
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
        foreach (
            var modification in (
                PrefabUtility.GetPropertyModifications(variant)
                ?? Array.Empty<PropertyModification>()
            )
                .Where(value => !PrefabUtility.IsDefaultOverride(value))
                .Where(value => !IsPatchOwnedAddition(value.target))
                .Where(value => !IsRedundantSourceValue(value))
                .OrderBy(value => SourceSortKey(value.target), StringComparer.Ordinal)
                .ThenBy(value => value.propertyPath, StringComparer.Ordinal)
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
                    !TryObjectReference(
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
                        modification.target,
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
                return double.TryParse(
                        modification.value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var floating
                    )
                    && Math.Abs(sourceProperty.doubleValue - floating)
                        <= 1e-9;
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
                target.CanonicalKey + ":" + component.GetType().AssemblyQualifiedName;
            ordinals.TryGetValue(ordinalKey, out var ordinal);
            ordinals[ordinalKey] = ordinal + 1;
            var fragment = CompileComponent(
                component,
                "component-"
                    + PrefabPatchJson
                        .Sha256(
                            target.CanonicalKey
                                + ":"
                                + component.GetType().AssemblyQualifiedName
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
            TransformType = gameObject.transform
                .GetType()
                .AssemblyQualifiedName,
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
            ComponentType = componentType.AssemblyQualifiedName
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
                    SerializedType =
                        property.managedReferenceValue
                            .GetType()
                            .AssemblyQualifiedName
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
                        ColorKeys = gradient.colorKeys,
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

    private static PrefabPatchValue JsonValue(object value, Type type) =>
        new()
        {
            Kind = PrefabPatchValueKind.Json,
            SerializedType = type.AssemblyQualifiedName,
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
        if (
            sourceObject == null
            || !LinkedAddressableEditorIdentity.TryGet(
                sourceObject,
                out var identity
            )
        )
        {
            error =
                "does not resolve to a BundleKit CAB/path-ID source identity.";
            return false;
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
            ObjectType = NormalizeTypeName(identity.SourceType),
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
                ExpectedType = identity.SourceType
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
            Address = entry.address,
            ExpectedType = value.GetType().AssemblyQualifiedName
        };
        return true;
    }

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
                        ObjectType =
                            component.GetType().AssemblyQualifiedName
                    };
                }
                else
                {
                    target = new PrefabPatchObjectTarget
                    {
                        Kind = PrefabPatchTargetKind.PatchOwned,
                        OwnerPatchId = patchId,
                        ObjectId = objectId,
                        ObjectType = value.GetType().AssemblyQualifiedName,
                        RuntimeLocator =
                            value is Transform transform
                                ? new PrefabPatchRuntimeLocator
                                {
                                    TargetKind =
                                        PrefabPatchRuntimeTargetKind.Component,
                                    ComponentType =
                                        transform
                                            .GetType()
                                            .AssemblyQualifiedName,
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
                            + PrefabPatchJson
                                .Sha256(
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

    private static PrefabPatchPrefabIdentity BuildPrefabIdentity(
        GameObject basePrefab,
        LinkedAddressableEditorObjectIdentity identity
    )
    {
        return new PrefabPatchPrefabIdentity
        {
            Address = identity.Address,
            AssetType = NormalizeTypeName(identity.SourceType),
            StructuralFingerprint = PrefabPatchStructure.Calculate(basePrefab)
        };
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
                    double.TryParse(
                        recordedValue,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var floating
                    )
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

    private static string[] Sorted(IEnumerable<string> values) =>
        (values ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static string OperationId(params string[] parts) =>
        "op-"
        + PrefabPatchJson.Sha256(
            string.Join("|", parts.Select(value => value ?? ""))
        ).Substring(0, 16);

    private static string SourceSortKey(Object value)
    {
        return LinkedAddressableEditorIdentity.TryGet(
            value,
            out var identity
        )
            ? identity.ToString()
            : value?.GetType().AssemblyQualifiedName ?? "";
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

    private static void MakeManifestAddressable(
        string outputPath,
        AddressableAssetGroup requestedGroup,
        string addressablesLabel
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
    }
}
