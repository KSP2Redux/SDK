using Ksp2UnityTools.LinkedAddressables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Ksp2UnityTools.Editor.LinkedAddressables
{
    [ScriptedImporter(19, Extension)]
    public sealed class LinkedAddressableAssetImporter : ScriptedImporter
    {
        public const string Extension = "bkaddressable";

        private const string MainAssemblyName = "Assembly-CSharp";
        private const string FirstPassAssemblyName = "Assembly-CSharp-firstpass";

        private static readonly Lazy<Dictionary<Type, string>> ScriptPaths =
            new(() =>
            {
                var paths = new Dictionary<Type, string>();
                foreach (var script in MonoImporter.GetAllRuntimeMonoScripts())
                {
                    var scriptType = script != null ? script.GetClass() : null;
                    if (scriptType != null && !paths.ContainsKey(scriptType))
                        paths.Add(scriptType, AssetDatabase.GetAssetPath(script).Replace('\\', '/'));
                }

                return paths;
            });

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var descriptor = ReadDescriptor(ctx.assetPath);
            if (!TryValidateDescriptor(descriptor, out var error))
            {
                ctx.LogImportError(error);
                return;
            }

            var assetType = Type.GetType(descriptor.AssetType, false);
            if (assetType == null)
            {
                ctx.LogImportError(
                    $"The linked Addressables type '{descriptor.AssetType}' could not be resolved."
                );
                return;
            }

            try
            {
                var graph = LinkedAddressableEditorGraph.Materialize(
                    ctx.assetPath,
                    descriptor
                );
                var sourceMap = LinkedAddressableSourceMap.Create(
                    descriptor,
                    graph
                );
                LinkedAddressableEditorResources.RewriteExternalStreams(
                    ctx,
                    descriptor,
                    graph,
                    sourceMap
                );
                AddMappedComponentsToImport(
                    ctx,
                    graph,
                    sourceMap
                );
                foreach (var subAsset in graph.SubAssets)
                    ctx.AddObjectToAsset(subAsset.Identifier, subAsset.Asset);

                ctx.AddObjectToAsset(
                    LinkedAddressableSourceMap.SubAssetIdentifier,
                    sourceMap
                );
                ctx.AddObjectToAsset(descriptor.StableId, graph.MainAsset);
                ctx.SetMainObject(graph.MainAsset);
            }
            catch (Exception exception)
            {
                ctx.LogImportError(
                    $"Could not materialize linked asset '{descriptor.Address}' as "
                        + $"'{assetType.FullName}': {exception}"
                );
            }
            finally
            {
                LinkedAddressableEditorSource.EndMaterialization();
            }
        }

        private static void AddMappedComponentsToImport(
            AssetImportContext ctx,
            LinkedAddressableMaterializedGraph graph,
            LinkedAddressableSourceMap sourceMap
        )
        {
            var roots = graph.SubAssets
                .Select(subAsset => subAsset.Asset)
                .Append(graph.MainAsset)
                .ToArray();
            var mappedComponents = sourceMap.Objects
                .Where(sourceObject =>
                    sourceObject.Target is Component
                    && !roots.Any(root =>
                        ReferenceEquals(
                            root,
                            sourceObject.Target
                        )
                    )
                )
                .OrderBy(sourceObject =>
                    sourceObject.SourcePathId
                );
            foreach (var sourceObject in mappedComponents)
            {
                ctx.AddObjectToAsset(
                    "component:"
                        + sourceObject.SourceBundleFileName
                        + ":"
                        + sourceObject.SourceSerializedFileName
                        + ":"
                        + sourceObject.SourcePathId
                        + ":"
                        + GetIdentifierTypeName(sourceObject.SourceType),
                    sourceObject.Target
                );
            }
        }

        // A component's identifier, and the local file ID Unity derives from it, has to stay what it was when prefab
        // variants of the linked asset were authored, or their overrides lose their targets. Game scripts compiled
        // into Assembly-CSharp and Assembly-CSharp-firstpass then. They moved into one game assembly, whose name and
        // build-generated version changed the type names and orphaned every override of a game script component.
        // Facades with the old names forward to it, and the identifier keeps the name the type had in the stock
        // bundles.
        private static string GetIdentifierTypeName(string sourceType)
        {
            var type = string.IsNullOrWhiteSpace(sourceType)
                ? null
                : Type.GetType(sourceType, false);
            if (type == null)
                return sourceType;

            var assemblyName = type.Assembly.GetName().Name;
            if (
                assemblyName == MainAssemblyName
                || assemblyName == FirstPassAssemblyName
                || Type.GetType($"{type.FullName}, {MainAssemblyName}", false) != type
            )
            {
                return sourceType;
            }

            var legacyAssembly = IsFirstPassScript(type)
                ? FirstPassAssemblyName
                : MainAssemblyName;
            return $"{type.FullName}, {legacyAssembly}, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
        }

        // Unity compiled scripts in these top-level folders into Assembly-CSharp-firstpass.
        private static bool IsFirstPassScript(Type type)
        {
            if (!ScriptPaths.Value.TryGetValue(type, out var path))
                return false;

            return path.StartsWith("Assets/Plugins/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Assets/Standard Assets/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Assets/Pro Standard Assets/", StringComparison.OrdinalIgnoreCase);
        }

        public static LinkedAddressableDescriptor ReadDescriptor(string assetPath)
        {
            var json = File.ReadAllText(assetPath);
            return JsonUtility.FromJson<LinkedAddressableDescriptor>(json);
        }

        private static bool TryValidateDescriptor(
            LinkedAddressableDescriptor descriptor,
            out string error
        )
        {
            if (descriptor == null)
            {
                error = "The linked Addressables descriptor is empty or invalid JSON.";
                return false;
            }

            if (descriptor.SchemaVersion != LinkedAddressableDescriptor.CurrentSchemaVersion)
            {
                error =
                    $"Unsupported linked Addressables schema version {descriptor.SchemaVersion}; "
                    + $"expected {LinkedAddressableDescriptor.CurrentSchemaVersion}.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(descriptor.StableId))
            {
                error = "The linked Addressables descriptor has no stable object identifier.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(descriptor.CatalogId))
            {
                error = "The linked Addressables descriptor has no catalog identity.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(descriptor.SourceId))
            {
                error = "The linked Addressables descriptor has no source identity.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(descriptor.Address))
            {
                error = "The linked Addressables descriptor has no address.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(descriptor.AssetType))
            {
                error = "The linked Addressables descriptor has no asset type.";
                return false;
            }

            error = null;
            return true;
        }

    }
}
