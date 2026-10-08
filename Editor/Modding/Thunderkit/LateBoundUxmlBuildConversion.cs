using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.Modding.Thunderkit
{
    /// <summary>
    /// Replaces mod-owned generated UXML data with late-bound runtime proxies during an Addressables build.
    /// </summary>
    internal sealed class LateBoundUxmlBuildConversion : IDisposable
    {
        private const string LATE_BOUND_FULL_TYPE_NAME = "UitkForKsp2.Controls.LateBoundUxmlSerializedData";
        private const string LATE_BOUND_TYPE_NAME =
            "UitkForKsp2.Controls.LateBoundUxmlSerializedData, uitkforksp2.controls.Runtime";

        private static readonly FieldInfo VisualTreeField = typeof(VisualTreeAsset).GetField(
            "m_VisualTree",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        private static readonly Type VisualElementAssetType = typeof(VisualTreeAsset).Assembly.GetType(
            "UnityEngine.UIElements.VisualElementAsset",
            true
        );
        private static readonly FieldInfo ChildrenField = FindFieldInHierarchy(VisualElementAssetType, "m_Children");
        private static readonly PropertyInfo SerializedDataProperty = VisualElementAssetType.GetProperty(
            "serializedData",
            BindingFlags.Instance | BindingFlags.Public
        );
        private static readonly Type TemplateAssetType = typeof(VisualTreeAsset).Assembly.GetType(
            "UnityEngine.UIElements.TemplateAsset",
            true
        );
        private static readonly PropertyInfo SerializedDataOverridesProperty = TemplateAssetType.GetProperty(
            "serializedDataOverrides",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );
        private static readonly FieldInfo OverrideSerializedDataField = TemplateAssetType
            .GetNestedType("UxmlSerializedDataOverride", BindingFlags.Public | BindingFlags.NonPublic)
            .GetField("m_SerializedData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private sealed class AssetChanges
        {
            public VisualTreeAsset Asset;
            public bool WasDirty;
            public List<Action> RestoreActions = new();
        }

        private readonly List<AssetChanges> _changedAssets = new();

        private LateBoundUxmlBuildConversion()
        {
        }

        /// <summary>
        /// Gets the number of generated UXML data references replaced by this conversion.
        /// </summary>
        public int ConvertedReferenceCount => _changedAssets.Sum(asset => asset.RestoreActions.Count);

        /// <summary>
        /// Converts generated UXML data owned by the specified mod assemblies.
        /// </summary>
        /// <param name="groups">The Addressables groups included in the mod build.</param>
        /// <param name="modAssemblyNames">The mod assembly names whose generated UXML data must be converted.</param>
        /// <returns>The conversion scope that restores source assets when disposed.</returns>
        public static LateBoundUxmlBuildConversion Apply(
            IEnumerable<AddressableAssetGroup> groups,
            IReadOnlyCollection<string> modAssemblyNames
        )
        {
            var conversion = new LateBoundUxmlBuildConversion();
            var assemblyNames = new HashSet<string>(modAssemblyNames, StringComparer.OrdinalIgnoreCase);
            foreach (string assetPath in FindVisualTreeAssetPaths(groups))
            {
                var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);
                if (asset != null)
                {
                    conversion.ConvertAsset(asset, assemblyNames);
                }
            }

            return conversion;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            for (int assetIndex = _changedAssets.Count - 1; assetIndex >= 0; assetIndex--)
            {
                AssetChanges assetChanges = _changedAssets[assetIndex];
                if (assetChanges.Asset == null)
                {
                    continue;
                }

                for (int changeIndex = assetChanges.RestoreActions.Count - 1; changeIndex >= 0; changeIndex--)
                {
                    assetChanges.RestoreActions[changeIndex]();
                }

                if (!assetChanges.WasDirty)
                {
                    EditorUtility.ClearDirty(assetChanges.Asset);
                }
            }

            _changedAssets.Clear();
        }

        private void ConvertAsset(VisualTreeAsset asset, HashSet<string> modAssemblyNames)
        {
            var assetChanges = new AssetChanges
            {
                Asset = asset,
                WasDirty = EditorUtility.IsDirty(asset)
            };

            foreach (object element in TraverseElements(asset))
            {
                ConvertReference(
                    SerializedDataProperty.GetValue(element) as UxmlSerializedData,
                    value => SerializedDataProperty.SetValue(element, value),
                    modAssemblyNames,
                    assetChanges
                );

                if (!TemplateAssetType.IsInstanceOfType(element) ||
                    SerializedDataOverridesProperty.GetValue(element) is not IList overrides)
                {
                    continue;
                }

                for (int index = 0; index < overrides.Count; index++)
                {
                    int capturedIndex = index;
                    ConvertReference(
                        OverrideSerializedDataField.GetValue(overrides[index]) as UxmlSerializedData,
                        value =>
                        {
                            object item = overrides[capturedIndex];
                            OverrideSerializedDataField.SetValue(item, value);
                            overrides[capturedIndex] = item;
                        },
                        modAssemblyNames,
                        assetChanges
                    );
                }
            }

            if (assetChanges.RestoreActions.Count == 0)
            {
                return;
            }

            _changedAssets.Add(assetChanges);
            EditorUtility.SetDirty(asset);
        }

        private static void ConvertReference(
            UxmlSerializedData source,
            Action<UxmlSerializedData> setValue,
            HashSet<string> modAssemblyNames,
            AssetChanges assetChanges
        )
        {
            if (source == null ||
                string.Equals(source.GetType().FullName, LATE_BOUND_FULL_TYPE_NAME, StringComparison.Ordinal) ||
                !modAssemblyNames.Contains(source.GetType().Assembly.GetName().Name))
            {
                return;
            }

            UxmlSerializedData replacement = CreateLateBoundData(source);
            setValue(replacement);
            assetChanges.RestoreActions.Add(() => setValue(source));
        }

        private static IEnumerable<object> TraverseElements(VisualTreeAsset asset)
        {
            object root = VisualTreeField?.GetValue(asset);
            if (root == null || ChildrenField == null)
            {
                yield break;
            }

            var pending = new Stack<object>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                object current = pending.Pop();
                if (VisualElementAssetType.IsInstanceOfType(current))
                {
                    yield return current;
                }

                if (ChildrenField.GetValue(current) is not IList children)
                {
                    continue;
                }

                for (int index = children.Count - 1; index >= 0; index--)
                {
                    if (children[index] != null)
                    {
                        pending.Push(children[index]);
                    }
                }
            }
        }

        private static UxmlSerializedData CreateLateBoundData(UxmlSerializedData source)
        {
            var lateBoundType = Type.GetType(LATE_BOUND_TYPE_NAME, false);
            MethodInfo createMethod = lateBoundType?.GetMethod(
                "Create",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(UxmlSerializedData) },
                null
            );
            if (createMethod == null)
            {
                throw new InvalidOperationException(
                    "The installed UITK for KSP 2 package does not provide late-bound UXML support. " +
                    "Update uitkforksp2.controls before building this mod."
                );
            }

            return (UxmlSerializedData)createMethod.Invoke(null, new object[] { source });
        }

        private static FieldInfo FindFieldInHierarchy(Type type, string fieldName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly
                );
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        private static IEnumerable<string> FindVisualTreeAssetPaths(IEnumerable<AddressableAssetGroup> groups)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AddressableAssetGroup group in groups.Where(group => group != null))
            {
                foreach (AddressableAssetEntry entry in group.entries)
                {
                    string entryPath = entry.AssetPath;
                    if (AssetDatabase.IsValidFolder(entryPath))
                    {
                        foreach (string guid in AssetDatabase.FindAssets("t:VisualTreeAsset", new[] { entryPath }))
                        {
                            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
                        }

                        continue;
                    }

                    foreach (string dependencyPath in AssetDatabase.GetDependencies(entryPath, true))
                    {
                        if (AssetDatabase.GetMainAssetTypeAtPath(dependencyPath) == typeof(VisualTreeAsset))
                        {
                            paths.Add(dependencyPath);
                        }
                    }
                }
            }

            return paths;
        }
    }
}
