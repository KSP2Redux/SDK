using System.Collections.Generic;
using System.Reflection;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;

namespace Ksp2UnityTools.Editor.LinkedAddressables
{
    internal interface ILinkedAddressableExternalReferences
    {
        bool TryGetExternalReference(
            ObjectIdentifier objectId,
            out string internalFileName,
            out long sourcePathId
        );
    }

    internal sealed class LinkedAddressableExternalReferenceContext
    {
        public Dictionary<GUID, List<LinkedAddressableExternalReference>> References =
            new();
    }

    internal readonly struct LinkedAddressableExternalReference
    {
        public LinkedAddressableExternalReference(
            ObjectIdentifier objectId,
            string internalFileName,
            long sourcePathId
        )
        {
            ObjectId = objectId;
            InternalFileName = internalFileName;
            SourcePathId = sourcePathId;
        }

        public ObjectIdentifier ObjectId { get; }

        public string InternalFileName { get; }

        public long SourcePathId { get; }
    }

    internal sealed class FilterLinkedAddressableReferences : IBuildTask
    {
        private static readonly FieldInfo SceneReferencedObjectsField =
            typeof(SceneDependencyInfo).GetField(
                "m_ReferencedObjects",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

        private readonly LinkedAddressableExternalReferenceContext _context;

#pragma warning disable 649
        [InjectContext(ContextUsage.In)]
        private IDependencyData _dependencyData;

        [InjectContext(ContextUsage.In)]
        private IDeterministicIdentifiers _identifiers;
#pragma warning restore 649

        public FilterLinkedAddressableReferences(
            LinkedAddressableExternalReferenceContext context
        )
        {
            _context = context;
        }

        public int Version => 1;

        public ReturnCode Run()
        {
            if (_identifiers is not ILinkedAddressableExternalReferences externalReferences)
                return ReturnCode.Success;

            foreach (var pair in _dependencyData.AssetInfo)
            {
                var retained = Filter(
                    pair.Key,
                    pair.Value.referencedObjects,
                    externalReferences
                );
                if (retained == null)
                    continue;

                pair.Value.referencedObjects.Clear();
                pair.Value.referencedObjects.AddRange(retained);
            }

            var sceneUpdates = new List<
                KeyValuePair<GUID, SceneDependencyInfo>
            >();
            foreach (var pair in _dependencyData.SceneInfo)
            {
                var retained = Filter(
                    pair.Key,
                    pair.Value.referencedObjects,
                    externalReferences
                );
                if (retained == null)
                    continue;

                if (SceneReferencedObjectsField == null)
                    return ReturnCode.Error;

                object boxedSceneInfo = pair.Value;
                SceneReferencedObjectsField.SetValue(
                    boxedSceneInfo,
                    retained.ToArray()
                );
                sceneUpdates.Add(
                    new KeyValuePair<GUID, SceneDependencyInfo>(
                        pair.Key,
                        (SceneDependencyInfo)boxedSceneInfo
                    )
                );
            }

            foreach (var pair in sceneUpdates)
            {
                _dependencyData.SceneInfo[pair.Key] = pair.Value;
            }

            return ReturnCode.Success;
        }

        private List<ObjectIdentifier> Filter(
            GUID asset,
            IReadOnlyCollection<ObjectIdentifier> references,
            ILinkedAddressableExternalReferences externalReferences
        )
        {
            if (references == null || references.Count == 0)
                return null;

            var retained = new List<ObjectIdentifier>(references.Count);
            var external = new List<LinkedAddressableExternalReference>();
            foreach (var reference in references)
            {
                if (
                    externalReferences.TryGetExternalReference(
                        reference,
                        out string internalFileName,
                        out long sourcePathId
                    )
                )
                {
                    external.Add(
                        new LinkedAddressableExternalReference(
                            reference,
                            internalFileName,
                            sourcePathId
                        )
                    );
                    continue;
                }

                retained.Add(reference);
            }

            if (external.Count == 0)
                return null;

            _context.References[asset] = external;
            return retained;
        }
    }

    internal sealed class InjectLinkedAddressableReferences : IBuildTask
    {
        private readonly LinkedAddressableExternalReferenceContext _context;

#pragma warning disable 649
        [InjectContext]
        private IBundleWriteData _writeData;
#pragma warning restore 649

        public InjectLinkedAddressableReferences(
            LinkedAddressableExternalReferenceContext context
        )
        {
            _context = context;
        }

        public int Version => 1;

        public ReturnCode Run()
        {
            foreach (var pair in _writeData.AssetToFiles)
            {
                if (
                    pair.Value == null
                    || pair.Value.Count == 0
                    || !_context.References.TryGetValue(
                        pair.Key,
                        out var externalReferences
                    )
                    || !_writeData.FileToReferenceMap.TryGetValue(
                        pair.Value[0],
                        out var referenceMap
                    )
                )
                {
                    continue;
                }

                foreach (var externalReference in externalReferences)
                {
                    referenceMap.AddMapping(
                        externalReference.InternalFileName,
                        externalReference.SourcePathId,
                        externalReference.ObjectId,
                        true
                    );
                }
            }

            return ReturnCode.Success;
        }
    }
}
