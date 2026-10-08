using Ksp2UnityTools.LinkedAddressables;
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Ksp2UnityTools.Editor.LinkedAddressables
{
    [ScriptedImporter(18, Extension)]
    public sealed class LinkedAddressableAssetImporter : ScriptedImporter
    {
        public const string Extension = "bkaddressable";

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
                        + sourceObject.SourceType,
                    sourceObject.Target
                );
            }
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
