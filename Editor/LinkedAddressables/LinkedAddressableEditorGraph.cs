using Ksp2UnityTools.LinkedAddressables;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ksp2UnityTools.Editor.LinkedAddressables
{
    internal static class LinkedAddressableEditorGraph
    {
        public static LinkedAddressableMaterializedGraph Materialize(
            string assetPath,
            LinkedAddressableDescriptor descriptor
        )
        {
            LinkedAddressableEditorSource.BeginMaterialization();
            var linkedObjects = BuildLinkedObjectMap(assetPath);
            var source = LinkedAddressableEditorSource.LoadMainAsset(descriptor);
            var materialized = CreateProjectOwnedRoot(source, out var sourceToTarget);
            foreach (var pair in sourceToTarget)
                linkedObjects[pair.Key] = pair.Value;

            var roots = new List<UnityEngine.Object> { materialized };
            RemapSerializedReferences(roots, linkedObjects);
            var dependencies = MaterializeUnlinkedDependencies(
                roots,
                linkedObjects,
                sourceToTarget
            );
            roots.AddRange(
                dependencies
                    .Select(dependency => dependency.Asset)
                    .Where(root => !roots.Contains(root))
            );
            InitializeMaterializedFontAssets(
                roots,
                sourceToTarget
            );
            RestoreMappedActiveStates(sourceToTarget);
            RefreshMaterializedTextComponents(roots);
            var sourceObjects = CollectSourceObjects(
                source,
                roots,
                sourceToTarget
            );
            return new LinkedAddressableMaterializedGraph(
                materialized,
                dependencies,
                sourceObjects
            );
        }

        private static Dictionary<UnityEngine.Object, UnityEngine.Object> BuildLinkedObjectMap(
            string importingAssetPath
        )
        {
            var result = new Dictionary<UnityEngine.Object, UnityEngine.Object>(
                UnityObjectReferenceComparer.Instance
            );

            foreach (
                var descriptorPath in LinkedAddressableRuntimeManifestBuilder
                    .GetDescriptorPaths()
                    .Where(
                        path =>
                            !string.Equals(
                                path,
                                importingAssetPath,
                                StringComparison.OrdinalIgnoreCase
                            )
                    )
            )
            {
                var target = AssetDatabase.LoadMainAssetAtPath(descriptorPath);
                if (target == null)
                    continue;

                try
                {
                    var descriptor = LinkedAddressableAssetImporter.ReadDescriptor(
                        descriptorPath
                    );
                    var external = LinkedAddressableEditorSource.LoadMainAsset(descriptor);
                    MapEquivalentObjects(external, target, result);
                    if (
                        string.Compare(
                            descriptorPath,
                            importingAssetPath,
                            StringComparison.Ordinal
                        )
                        < 0
                    )
                    {
                        MapExistingSourceObjects(
                            descriptorPath,
                            external,
                            result
                        );
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"[KSP2UnityTools.LinkedAddressables] Could not use "
                            + $"'{descriptorPath}' while remapping linked references: "
                            + exception.Message
                    );
                }
            }

            return result;
        }

        private static void MapExistingSourceObjects(
            string descriptorPath,
            UnityEngine.Object externalRoot,
            IDictionary<UnityEngine.Object, UnityEngine.Object> result
        )
        {
            var sourceMap = AssetDatabase
                .LoadAllAssetsAtPath(descriptorPath)
                .OfType<LinkedAddressableSourceMap>()
                .SingleOrDefault();
            if (sourceMap == null)
                return;

            var targets = sourceMap
                .Objects.Where(sourceObject => sourceObject.Target != null)
                .GroupBy(
                    sourceObject =>
                        new LinkedAddressableExternalObjectKey(
                            sourceObject.SourcePathId,
                            sourceObject.SourceType
                        )
                )
                .ToDictionary(group => group.Key, group => group.First().Target);
            var externalObjects = EditorUtility
                .CollectDependencies(new[] { externalRoot })
                .Prepend(externalRoot)
                .Where(candidate => candidate != null)
                .Distinct(UnityObjectReferenceComparer.Instance);
            foreach (var external in externalObjects)
            {
                if (
                    result.ContainsKey(external)
                    || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        external,
                        out _,
                        out long sourcePathId
                    )
                )
                {
                    continue;
                }

                var key = new LinkedAddressableExternalObjectKey(
                    sourcePathId,
                    external.GetType().AssemblyQualifiedName
                );
                if (targets.TryGetValue(key, out var target))
                    result[external] = target;
            }
        }

        private static IReadOnlyList<LinkedAddressableMaterializedSubAsset>
            MaterializeUnlinkedDependencies(
                IList<UnityEngine.Object> roots,
                Dictionary<UnityEngine.Object, UnityEngine.Object> sourceToTarget,
                Dictionary<UnityEngine.Object, UnityEngine.Object>
                    currentSourceToTarget
            )
        {
            var result = new List<LinkedAddressableMaterializedSubAsset>();
            var ownedObjects = new HashSet<UnityEngine.Object>(
                UnityObjectReferenceComparer.Instance
            );
            foreach (var root in roots)
                AddOwnedObjects(root, ownedObjects);

            var identifiers = new Dictionary<string, UnityEngine.Object>(
                StringComparer.Ordinal
            );
            while (true)
            {
                // Native component callbacks (notably TMP UI initialization) can
                // add or surface hierarchy-owned components while dependencies are
                // remapped. Refresh ownership before every scan so those components
                // are not mistaken for external bundle assets.
                foreach (var root in roots)
                    AddOwnedObjects(root, ownedObjects);

                var dependency = EditorUtility
                    .CollectDependencies(roots.ToArray())
                    .FirstOrDefault(
                        candidate =>
                            IsUnlinkedExternalDependency(
                                candidate,
                                ownedObjects,
                                sourceToTarget
                            )
                    );
                if (dependency == null)
                    break;

                if (
                    dependency is Shader sourceShader
                    && TryFindPersistentShader(sourceShader.name, out var targetShader)
                )
                {
                    sourceToTarget[sourceShader] = targetShader;
                    currentSourceToTarget[sourceShader] = targetShader;
                    continue;
                }

                var identifier = GetDependencyIdentifier(dependency);
                if (
                    identifiers.TryGetValue(identifier, out var existing)
                    && !ReferenceEquals(existing, dependency)
                )
                {
                    throw new InvalidOperationException(
                        $"External dependencies '{existing.name}' and "
                            + $"'{dependency.name}' produced the same stable importer "
                            + $"identifier '{identifier}'."
                    );
                }

                var materialized = CreateProjectOwnedRoot(
                    dependency,
                    out var dependencyMap
                );
                var persistenceRoot = GetPersistenceRoot(materialized);
                sourceToTarget[dependency] = materialized;
                currentSourceToTarget[dependency] = materialized;
                foreach (var pair in dependencyMap)
                {
                    sourceToTarget[pair.Key] = pair.Value;
                    currentSourceToTarget[pair.Key] = pair.Value;
                }

                identifiers[identifier] = dependency;
                result.Add(
                    new LinkedAddressableMaterializedSubAsset(
                        identifier,
                        persistenceRoot
                    )
                );
                roots.Add(persistenceRoot);
                AddOwnedObjects(persistenceRoot, ownedObjects);
            }

            // Dependency discovery consults sourceToTarget, so an object cannot be
            // materialized twice even while cloned roots still contain source
            // references. Remap once after graph expansion instead of walking the
            // entire growing serialized graph after every dependency.
            RemapSerializedReferences(roots, sourceToTarget);
            return result;
        }

        private static UnityEngine.Object GetPersistenceRoot(
            UnityEngine.Object materialized
        )
        {
            if (materialized is Component component)
                return component.transform.root.gameObject;
            return materialized;
        }

        private static bool TryFindPersistentShader(
            string shaderName,
            out Shader shader
        )
        {
            shader = Shader.Find(shaderName);
            if (
                shader != null
                && (
                    EditorUtility.IsPersistent(shader)
                    || !string.IsNullOrWhiteSpace(
                        AssetDatabase.GetAssetPath(shader)
                    )
                )
            )
            {
                return true;
            }

            shader = AssetDatabase
                .LoadAllAssetsAtPath("Resources/unity_builtin_extra")
                .OfType<Shader>()
                .FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.name,
                            shaderName,
                            StringComparison.Ordinal
                        )
                );
            if (shader != null)
                return true;

            shader = AssetDatabase
                .FindAssets("t:Shader")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct(StringComparer.Ordinal)
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .OfType<Shader>()
                .FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.name,
                            shaderName,
                            StringComparison.Ordinal
                        )
                );
            return shader != null;
        }

        private static bool IsUnlinkedExternalDependency(
            UnityEngine.Object candidate,
            ISet<UnityEngine.Object> ownedObjects,
            IDictionary<UnityEngine.Object, UnityEngine.Object> sourceToTarget
        )
        {
            if (
                candidate == null
                || candidate is MonoScript
                || ownedObjects.Contains(candidate)
                || sourceToTarget.ContainsKey(candidate)
            )
            {
                return false;
            }

            if (
                (candidate is Component || candidate is GameObject)
                && !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    candidate,
                    out _,
                    out long _
                )
            )
            {
                // UI and TMP native callbacks can expose generated helper
                // hierarchy objects through CollectDependencies even when they are
                // not serialized source-bundle objects. Hierarchy objects without a
                // source path ID cannot be standalone linked assets or translations.
                return false;
            }

            return string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(candidate));
        }

        private static string GetDependencyIdentifier(UnityEngine.Object dependency)
        {
            if (
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    dependency,
                    out _,
                    out long localId
                )
            )
            {
                throw new InvalidOperationException(
                    $"External dependency '{dependency.name}' of type "
                        + $"'{dependency.GetType().FullName}' has no stable source local ID."
                );
            }

            return $"dependency:{localId}:{dependency.GetType().AssemblyQualifiedName}";
        }

        private static void AddOwnedObjects(
            UnityEngine.Object root,
            ISet<UnityEngine.Object> ownedObjects
        )
        {
            ownedObjects.Add(root);
            if (!(root is GameObject gameObject))
                return;

            foreach (var transform in gameObject.GetComponentsInChildren<Transform>(true))
            {
                ownedObjects.Add(transform);
                ownedObjects.Add(transform.gameObject);
                foreach (var component in transform.GetComponents<Component>())
                {
                    if (component != null)
                        ownedObjects.Add(component);
                }
            }
        }

        private static IReadOnlyList<LinkedAddressableMaterializedSourceObject>
            CollectSourceObjects(
                UnityEngine.Object mainSource,
                IEnumerable<UnityEngine.Object> roots,
                IReadOnlyDictionary<UnityEngine.Object, UnityEngine.Object>
                    sourceToTarget
            )
        {
            var ownedObjects = new HashSet<UnityEngine.Object>(
                UnityObjectReferenceComparer.Instance
            );
            foreach (var root in roots)
                AddOwnedObjects(root, ownedObjects);

            var result = new List<LinkedAddressableMaterializedSourceObject>();
            foreach (var pair in sourceToTarget)
            {
                if (
                    pair.Key == null
                    || pair.Value == null
                    || !ownedObjects.Contains(pair.Value)
                )
                {
                    continue;
                }

                if (
                    !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        pair.Key,
                        out _,
                        out long sourceLocalId
                    )
                )
                {
                    throw new InvalidOperationException(
                        $"External object '{pair.Key.name}' of type "
                            + $"'{pair.Key.GetType().FullName}' has no source local ID."
                    );
                }

                long ownerHintLocalId = 0;
                var ownerHint = GetHierarchyOwner(pair.Key);
                if (
                    ownerHint != null
                    && !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        ownerHint,
                        out _,
                        out ownerHintLocalId
                    )
                )
                {
                    throw new InvalidOperationException(
                        $"External hierarchy owner '{ownerHint.name}' has no source "
                            + "local ID."
                    );
                }

                result.Add(
                    new LinkedAddressableMaterializedSourceObject(
                        pair.Value,
                        sourceLocalId,
                        GetSourceClassId(pair.Key),
                        pair.Key.GetType().AssemblyQualifiedName,
                        pair.Key.name,
                        ownerHintLocalId,
                        GetSourceClassId(ownerHint),
                        ownerHint?.GetType().AssemblyQualifiedName,
                        ownerHint?.name,
                        pair.Key == mainSource,
                        IsBuiltinShaderSource(pair.Key)
                    )
                );
            }

            return result;
        }

        private static bool IsBuiltinShaderSource(UnityEngine.Object source)
        {
            if (!(source is Shader))
                return false;

            return string.Equals(
                AssetDatabase.GetAssetPath(source),
                "Resources/unity_builtin_extra",
                StringComparison.OrdinalIgnoreCase
            );
        }

        private static UnityEngine.Object GetHierarchyOwner(UnityEngine.Object source)
        {
            if (source is Component component)
                return component.transform.root.gameObject;
            if (source is GameObject gameObject)
                return gameObject.transform.root.gameObject;

            return null;
        }

        private static int GetSourceClassId(UnityEngine.Object source)
        {
            if (source == null)
                return 0;
            if (source is MonoBehaviour || source is ScriptableObject)
                return (int)AssetClassID.MonoBehaviour;
            return Enum.TryParse(
                source.GetType().Name,
                out AssetClassID classId
            )
                ? (int)classId
                : 0;
        }

        private static UnityEngine.Object CreateProjectOwnedRoot(
            UnityEngine.Object source,
            out Dictionary<UnityEngine.Object, UnityEngine.Object> sourceToTarget
        )
        {
            sourceToTarget = new Dictionary<UnityEngine.Object, UnityEngine.Object>(
                UnityObjectReferenceComparer.Instance
            );

            if (source is ScriptableObject sourceScriptableObject)
            {
                var target = ScriptableObject.CreateInstance(source.GetType());
                EditorUtility.CopySerializedManagedFieldsOnly(
                    sourceScriptableObject,
                    target
                );
                target.name = source.name;
                target.hideFlags = source.hideFlags;
                sourceToTarget[source] = target;
                return target;
            }

            if (source is GameObject sourceGameObject)
            {
                return ReconstructGameObject(sourceGameObject, sourceToTarget);
            }

            if (source is Component sourceComponent)
            {
                ReconstructGameObject(
                    sourceComponent.transform.root.gameObject,
                    sourceToTarget
                );
                return sourceToTarget[sourceComponent];
            }

            if (source is Texture2D sourceTexture)
            {
                var target = CloneTexture(sourceTexture);
                sourceToTarget[source] = target;
                return target;
            }

            if (source is Font sourceFont)
            {
                var target = CloneFont(sourceFont);
                sourceToTarget[source] = target;
                return target;
            }

            if (source is Material sourceMaterial)
            {
                var target = CloneMaterial(sourceMaterial);
                sourceToTarget[source] = target;
                return target;
            }

            if (source is Shader sourceShader)
            {
                var target = LinkedAddressablePreviewShaderFactory.Create(
                    sourceShader
                );
                sourceToTarget[source] = target;
                return target;
            }

            if (source is Mesh sourceMesh)
            {
                var target = CloneMesh(sourceMesh);
                sourceToTarget[source] = target;
                return target;
            }

            try
            {
                var target = UnityEngine.Object.Instantiate(source);
                target.name = source.name;
                target.hideFlags = source.hideFlags;
                sourceToTarget[source] = target;
                return target;
            }
            catch (UnityException exception)
            {
                throw new InvalidOperationException(
                    $"KSP2UnityTools cannot yet create a project-owned editor instance "
                        + $"for linked object '{source.name}' of native type "
                        + $"'{source.GetType().FullName}'. Returning the transient "
                        + "source-bundle object would create a broken serialized link.",
                    exception
                );
            }
        }

        private static Font CloneFont(Font source)
        {
            // Unity explicitly does not support Object.Instantiate for dynamic
            // fonts. A constructed Font accepts the same serialized state while
            // keeping its material and atlas references available for the normal
            // dependency materialization and remapping pass.
            var target = new Font();
            EditorUtility.CopySerialized(source, target);
            target.name = source.name;
            target.hideFlags = source.hideFlags;
            return target;
        }

        private static Texture2D CloneTexture(Texture2D source)
        {
            var hasPixels = source.width > 0 && source.height > 0;
            var hasMipMaps = hasPixels && source.mipmapCount > 1;
            var target = new Texture2D(
                hasPixels ? source.width : 1,
                hasPixels ? source.height : 1,
                TextureFormat.RGBA32,
                hasMipMaps,
                !source.isDataSRGB
            )
            {
                name = source.name,
                hideFlags = source.hideFlags,
                filterMode = source.filterMode,
                wrapMode = source.wrapMode,
                anisoLevel = source.anisoLevel,
                mipMapBias = source.mipMapBias
            };

            if (!hasPixels)
            {
                // Dynamic font atlases can be valid serialized Texture2D objects
                // with no allocated pixels. Unity cannot create a zero-sized
                // project-owned texture, so use an empty preview while the source
                // map retains the original external CAB/path ID for player builds.
                target.SetPixel(0, 0, Color.clear);
                target.Apply(false, false);
                return target;
            }

            var previous = RenderTexture.active;
            var temporary = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default,
                1,
                RenderTextureMemoryless.None,
                VRTextureUsage.None,
                false
            );
            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                target.ReadPixels(
                    new Rect(0, 0, source.width, source.height),
                    0,
                    0,
                    hasMipMaps
                );
                target.Apply(hasMipMaps, false);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }

            return target;
        }

        private static Mesh CloneMesh(Mesh source)
        {
            var target = new Mesh
            {
                name = source.name,
                hideFlags = source.hideFlags,
                indexFormat = source.indexFormat
            };
            var attributes = new List<VertexAttributeDescriptor>();
            source.GetVertexAttributes(attributes);
            target.SetVertexBufferParams(source.vertexCount, attributes.ToArray());
            for (var stream = 0; stream < source.vertexBufferCount; stream++)
            {
                var buffer = source.GetVertexBuffer(stream);
                try
                {
                    var data = ReadGraphicsBuffer(
                        buffer,
                        $"vertex stream {stream}",
                        source
                    );
                    target.SetVertexBufferData(
                        data,
                        0,
                        0,
                        data.Length,
                        stream,
                        MeshUpdateFlags.DontRecalculateBounds
                            | MeshUpdateFlags.DontValidateIndices
                    );
                }
                finally
                {
                    buffer.Dispose();
                }
            }

            var indexBuffer = source.GetIndexBuffer();
            try
            {
                var data = ReadGraphicsBuffer(indexBuffer, "index", source);
                var indexStride =
                    source.indexFormat == IndexFormat.UInt16 ? 2 : 4;
                target.SetIndexBufferParams(
                    data.Length / indexStride,
                    source.indexFormat
                );
                target.SetIndexBufferData(
                    data,
                    0,
                    0,
                    data.Length,
                    MeshUpdateFlags.DontRecalculateBounds
                        | MeshUpdateFlags.DontValidateIndices
                );
            }
            finally
            {
                indexBuffer.Dispose();
            }

            target.subMeshCount = source.subMeshCount;
            for (var subMesh = 0; subMesh < source.subMeshCount; subMesh++)
            {
                target.SetSubMesh(
                    subMesh,
                    source.GetSubMesh(subMesh),
                    MeshUpdateFlags.DontRecalculateBounds
                        | MeshUpdateFlags.DontValidateIndices
                );
            }

            target.bindposes = source.bindposes;
            CopyBlendShapes(source, target);
            target.bounds = source.bounds;
            target.UploadMeshData(false);
            return target;
        }

        private static byte[] ReadGraphicsBuffer(
            GraphicsBuffer buffer,
            string bufferDescription,
            Mesh source
        )
        {
            var request = AsyncGPUReadback.Request(buffer);
            request.WaitForCompletion();
            if (request.hasError)
            {
                throw new InvalidOperationException(
                    $"GPU readback failed for {bufferDescription} data on linked "
                        + $"mesh '{source.name}'."
                );
            }

            return request.GetData<byte>().ToArray();
        }

        private static void CopyBlendShapes(Mesh source, Mesh target)
        {
            if (source.blendShapeCount == 0)
                return;

            var deltaVertices = new Vector3[source.vertexCount];
            var deltaNormals = new Vector3[source.vertexCount];
            var deltaTangents = new Vector3[source.vertexCount];
            for (
                var shapeIndex = 0;
                shapeIndex < source.blendShapeCount;
                shapeIndex++
            )
            {
                var shapeName = source.GetBlendShapeName(shapeIndex);
                var frameCount = source.GetBlendShapeFrameCount(shapeIndex);
                for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    source.GetBlendShapeFrameVertices(
                        shapeIndex,
                        frameIndex,
                        deltaVertices,
                        deltaNormals,
                        deltaTangents
                    );
                    target.AddBlendShapeFrame(
                        shapeName,
                        source.GetBlendShapeFrameWeight(
                            shapeIndex,
                            frameIndex
                        ),
                        deltaVertices,
                        deltaNormals,
                        deltaTangents
                    );
                }
            }
        }

        private static Material CloneMaterial(Material source)
        {
            if (source.shader == null)
            {
                throw new InvalidOperationException(
                    $"Linked material '{source.name}' has no source shader."
                );
            }

            var target = new Material(source.shader)
            {
                name = source.name,
                hideFlags = source.hideFlags
            };
            target.CopyPropertiesFromMaterial(source);
            target.shader = source.shader;
            return target;
        }

        private static GameObject ReconstructGameObject(
            GameObject source,
            IDictionary<UnityEngine.Object, UnityEngine.Object> sourceToTarget
        )
        {
            var target = CreateHierarchy(source, null, sourceToTarget);
            CopyComponents(source, target, sourceToTarget);
            return target;
        }

        private static GameObject CreateHierarchy(
            GameObject source,
            Transform targetParent,
            IDictionary<UnityEngine.Object, UnityEngine.Object> sourceToTarget
        )
        {
            var transformTypes =
                source.transform is RectTransform
                    ? new[] { typeof(RectTransform) }
                    : Type.EmptyTypes;
            var target = new GameObject(source.name, transformTypes)
            {
                hideFlags = source.hideFlags,
                layer = source.layer
            };
            target.SetActive(false);
            if (targetParent != null)
                target.transform.SetParent(targetParent, false);

            try
            {
                target.tag = source.tag;
            }
            catch (UnityException)
            {
                target.tag = "Untagged";
            }

            GameObjectUtility.SetStaticEditorFlags(
                target,
                GameObjectUtility.GetStaticEditorFlags(source)
            );
            CopyTransform(source.transform, target.transform);
            sourceToTarget[source] = target;
            sourceToTarget[source.transform] = target.transform;

            for (var index = 0; index < source.transform.childCount; index++)
            {
                CreateHierarchy(
                    source.transform.GetChild(index).gameObject,
                    target.transform,
                    sourceToTarget
                );
            }

            return target;
        }

        private static void CopyTransform(Transform source, Transform target)
        {
            target.localPosition = source.localPosition;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;

            if (
                source is RectTransform sourceRectTransform
                && target is RectTransform targetRectTransform
            )
            {
                targetRectTransform.anchorMin = sourceRectTransform.anchorMin;
                targetRectTransform.anchorMax = sourceRectTransform.anchorMax;
                targetRectTransform.anchoredPosition3D =
                    sourceRectTransform.anchoredPosition3D;
                targetRectTransform.sizeDelta = sourceRectTransform.sizeDelta;
                targetRectTransform.pivot = sourceRectTransform.pivot;
            }
        }

        private static void CopyComponents(
            GameObject source,
            GameObject target,
            IDictionary<UnityEngine.Object, UnityEngine.Object> sourceToTarget
        )
        {
            var sourceComponents = source
                .GetComponents<Component>()
                .Where(
                    component =>
                        component != null && !(component is Transform)
                )
                .ToArray();
            var claimedTargets = new HashSet<Component>(
                UnityObjectReferenceComparer.Instance
            )
            {
                target.transform
            };
            var targets = new Dictionary<Component, Component>(
                UnityObjectReferenceComparer.Instance
            );
            var pending = sourceComponents.ToList();
            var failures = new Dictionary<Component, Exception>(
                UnityObjectReferenceComparer.Instance
            );
            while (pending.Count > 0)
            {
                var madeProgress = false;
                foreach (var sourceComponent in pending.ToArray())
                {
                    if (
                        HasPendingRequiredComponent(
                            sourceComponent,
                            pending,
                            target
                        )
                    )
                    {
                        continue;
                    }

                    var targetComponent = target
                        .GetComponents<Component>()
                        .FirstOrDefault(
                            candidate =>
                                candidate != null
                                && candidate.GetType()
                                    == sourceComponent.GetType()
                                && !claimedTargets.Contains(candidate)
                        );
                    try
                    {
                        if (targetComponent == null)
                        {
                            targetComponent = target.AddComponent(
                                sourceComponent.GetType()
                            );
                        }
                    }
                    catch (Exception exception)
                    {
                        failures[sourceComponent] = exception;
                        continue;
                    }

                    if (targetComponent == null)
                        continue;
                    claimedTargets.Add(targetComponent);
                    targets.Add(sourceComponent, targetComponent);
                    pending.Remove(sourceComponent);
                    failures.Remove(sourceComponent);
                    madeProgress = true;
                }

                if (!madeProgress)
                {
                    var details = string.Join(
                        "; ",
                        pending.Select(component =>
                            failures.TryGetValue(
                                component,
                                out var failure
                            )
                                ? $"{component.GetType().FullName}: "
                                    + failure.Message
                                : component.GetType().FullName
                        )
                    );
                    throw new InvalidOperationException(
                        $"Could not reconstruct component(s) on "
                            + $"'{source.name}': {details}."
                    );
                }
            }

            RestoreSourceComponentOrder(
                source,
                target,
                sourceComponents,
                targets
            );
            foreach (var sourceComponent in sourceComponents)
            {
                var targetComponent = targets[sourceComponent];
                if (sourceComponent is MonoBehaviour)
                {
                    EditorUtility.CopySerializedManagedFieldsOnly(
                        sourceComponent,
                        targetComponent
                    );
                    if (
                        sourceComponent is Behaviour sourceBehaviour
                        && targetComponent is Behaviour targetBehaviour
                    )
                    {
                        targetBehaviour.enabled = sourceBehaviour.enabled;
                    }
                }
                else
                {
                    EditorUtility.CopySerialized(sourceComponent, targetComponent);
                }

                targetComponent.hideFlags = sourceComponent.hideFlags;
                sourceToTarget[sourceComponent] = targetComponent;
            }

            for (var index = 0; index < source.transform.childCount; index++)
            {
                CopyComponents(
                    source.transform.GetChild(index).gameObject,
                    target.transform.GetChild(index).gameObject,
                    sourceToTarget
                );
            }
        }

        private static bool HasPendingRequiredComponent(
            Component sourceComponent,
            IReadOnlyCollection<Component> pending,
            GameObject target
        )
        {
            var targetComponents = target.GetComponents<Component>();
            foreach (
                var requiredType in GetRequiredComponentTypes(
                    sourceComponent.GetType()
                )
            )
            {
                if (
                    targetComponents.Any(component =>
                        component != null
                        && requiredType.IsAssignableFrom(
                            component.GetType()
                        )
                    )
                )
                {
                    continue;
                }

                if (
                    pending.Any(component =>
                        !ReferenceEquals(component, sourceComponent)
                        && requiredType.IsAssignableFrom(
                            component.GetType()
                        )
                    )
                )
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<Type> GetRequiredComponentTypes(
            Type componentType
        )
        {
            const BindingFlags flags =
                BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.Instance;
            foreach (
                var attribute in componentType
                    .GetCustomAttributes(
                        typeof(RequireComponent),
                        true
                    )
                    .Cast<RequireComponent>()
            )
            {
                foreach (var fieldName in new[]
                {
                    "m_Type0",
                    "m_Type1",
                    "m_Type2"
                })
                {
                    var requiredType = typeof(RequireComponent)
                        .GetField(fieldName, flags)
                        ?.GetValue(attribute) as Type;
                    if (requiredType != null)
                        yield return requiredType;
                }
            }
        }

        private static void RestoreSourceComponentOrder(
            GameObject source,
            GameObject target,
            IReadOnlyList<Component> sourceComponents,
            IReadOnlyDictionary<Component, Component> targets
        )
        {
            for (
                var sourceIndex = 0;
                sourceIndex < sourceComponents.Count;
                sourceIndex++
            )
            {
                var targetComponent = targets[sourceComponents[sourceIndex]];
                var desiredIndex = sourceIndex + 1;
                while (true)
                {
                    var currentComponents =
                        target.GetComponents<Component>();
                    var currentIndex = Array.IndexOf(
                        currentComponents,
                        targetComponent
                    );
                    if (currentIndex <= desiredIndex)
                        break;
                    if (!ComponentUtility.MoveComponentUp(targetComponent))
                    {
                        throw new InvalidOperationException(
                            $"Could not restore source component order for "
                                + $"'{targetComponent.GetType().FullName}' on "
                                + $"'{source.name}'."
                        );
                    }
                }
            }

            var expected = sourceComponents
                .Select(component => component.GetType())
                .ToArray();
            var actual = target
                .GetComponents<Component>()
                .Where(component => !(component is Transform))
                .Select(component => component.GetType())
                .ToArray();
            if (!expected.SequenceEqual(actual))
            {
                throw new InvalidOperationException(
                    $"Could not reproduce source component order on "
                        + $"'{source.name}'. Expected "
                        + $"'{string.Join(", ", expected.Select(type => type.FullName))}', "
                        + $"got "
                        + $"'{string.Join(", ", actual.Select(type => type.FullName))}'."
                );
            }
        }

        private static void InitializeMaterializedFontAssets(
            IEnumerable<UnityEngine.Object> roots,
            IReadOnlyDictionary<
                UnityEngine.Object,
                UnityEngine.Object
            > sourceToTarget
        )
        {
            var rootArray = roots.ToArray();
            var ownedObjects = EnumerateOwnedObjects(rootArray).ToArray();
            var pending = new Queue<UnityEngine.Object>(
                ownedObjects
                    .Where(candidate =>
                        IsTypeOrSubclass(
                            candidate.GetType(),
                            "TMPro.TMP_FontAsset"
                        )
                    )
                    .Concat(
                        sourceToTarget
                            .Where(pair =>
                                pair.Key is Component sourceComponent
                                && pair.Value is Component
                                && sourceComponent.gameObject.activeInHierarchy
                                && IsTypeOrSubclass(
                                    sourceComponent.GetType(),
                                    "TMPro.TMP_Text"
                                )
                            )
                            .Select(pair =>
                                ((Component)pair.Value).GetType()
                                    .GetProperty(
                                        "font",
                                        BindingFlags.Public
                                            | BindingFlags.Instance
                                    )
                                    ?.GetValue(pair.Value)
                                    as UnityEngine.Object
                            )
                            .Where(fontAsset => fontAsset != null)
                    )
                    .Distinct(UnityObjectReferenceComparer.Instance)
            );
            var initialized = new HashSet<UnityEngine.Object>(
                UnityObjectReferenceComparer.Instance
            );
            while (pending.Count > 0)
            {
                var fontAsset = pending.Dequeue();
                if (
                    fontAsset == null
                    || !initialized.Add(fontAsset)
                )
                {
                    continue;
                }

                var initialize = fontAsset
                    .GetType()
                    .GetMethod(
                        "ReadFontAssetDefinition",
                        BindingFlags.Public | BindingFlags.Instance
                    );
                if (initialize == null)
                {
                    throw new InvalidOperationException(
                        $"Could not initialize materialized TMP font asset "
                            + $"'{fontAsset.name}': ReadFontAssetDefinition is unavailable."
                    );
                }

                try
                {
                    RepairTextMeshProCharacterGlyphLinks(fontAsset);
                    SetMissingTextCoreUnitsPerEm(fontAsset);
                    initialize.Invoke(fontAsset, null);
                }
                catch (TargetInvocationException exception)
                {
                    throw new InvalidOperationException(
                        $"Could not initialize materialized TMP font asset "
                            + $"'{fontAsset.name}'.",
                        exception.InnerException ?? exception
                    );
                }
            }
        }

        private static void SetMissingTextCoreUnitsPerEm(
            UnityEngine.Object fontAsset
        )
        {
            const BindingFlags propertyFlags =
                BindingFlags.Public | BindingFlags.Instance;
            const BindingFlags fieldFlags =
                BindingFlags.NonPublic | BindingFlags.Instance;
            var faceInfoProperty = fontAsset
                .GetType()
                .GetProperty("faceInfo", propertyFlags);
            var boxedFaceInfo = faceInfoProperty?.GetValue(fontAsset);
            var unitsPerEmField = boxedFaceInfo
                ?.GetType()
                .GetField("m_UnitsPerEM", fieldFlags);
            if (
                faceInfoProperty?.CanWrite != true
                || unitsPerEmField == null
                || (int)unitsPerEmField.GetValue(boxedFaceInfo) != 0
            )
            {
                return;
            }

            unitsPerEmField.SetValue(boxedFaceInfo, 1000);
            faceInfoProperty.SetValue(fontAsset, boxedFaceInfo);
        }

        private static void RepairTextMeshProCharacterGlyphLinks(
            UnityEngine.Object fontAsset
        )
        {
            const BindingFlags flags =
                BindingFlags.Public | BindingFlags.Instance;
            var fontType = fontAsset.GetType();
            var glyphTable = fontType
                .GetProperty("glyphTable", flags)
                ?.GetValue(fontAsset) as System.Collections.IEnumerable;
            var characterTable = fontType
                .GetProperty("characterTable", flags)
                ?.GetValue(fontAsset) as System.Collections.IEnumerable;
            if (glyphTable == null || characterTable == null)
            {
                throw new InvalidOperationException(
                    $"Could not repair materialized TMP font asset "
                        + $"'{fontAsset.name}': glyph or character table is unavailable."
                );
            }

            var glyphs = glyphTable
                .Cast<object>()
                .Where(glyph => glyph != null)
                .ToArray();
            var characters = characterTable
                .Cast<object>()
                .Where(character => character != null)
                .ToArray();
            if (characters.Length == 0)
                return;
            var glyphIndexDefinition = glyphs
                .FirstOrDefault()
                ?.GetType()
                .GetProperty("index", flags);
            if (glyphIndexDefinition == null)
            {
                throw new InvalidOperationException(
                    $"Could not repair materialized TMP font asset "
                        + $"'{fontAsset.name}': glyph index is unavailable."
                );
            }

            var characterType = characters
                .FirstOrDefault()
                ?.GetType();
            var glyphIndexProperty = characterType?.GetProperty(
                "glyphIndex",
                flags
            );
            var glyphProperty = characterType?.GetProperty(
                "glyph",
                flags
            );
            var textAssetProperty = characterType?.GetProperty(
                "textAsset",
                flags
            );
            if (
                glyphIndexProperty == null
                || glyphProperty == null
                || !glyphProperty.CanWrite
            )
            {
                throw new InvalidOperationException(
                    $"Could not repair materialized TMP font asset "
                        + $"'{fontAsset.name}': character glyph properties "
                        + "are unavailable."
                );
            }

            var glyphsByIndex = new Dictionary<uint, object>();
            foreach (var glyph in glyphs)
            {
                glyphsByIndex[Convert.ToUInt32(
                    glyphIndexDefinition.GetValue(glyph)
                )] = glyph;
            }

            var unresolved = new List<uint>();
            foreach (var character in characters)
            {
                var glyphIndex = Convert.ToUInt32(
                    glyphIndexProperty.GetValue(character)
                );
                if (!glyphsByIndex.TryGetValue(glyphIndex, out var glyph))
                {
                    unresolved.Add(glyphIndex);
                    continue;
                }

                glyphProperty.SetValue(character, glyph);
                if (
                    textAssetProperty != null
                    && textAssetProperty.CanWrite
                )
                {
                    textAssetProperty.SetValue(character, fontAsset);
                }
            }

            if (unresolved.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Could not repair materialized TMP font asset "
                        + $"'{fontAsset.name}': {unresolved.Count} character(s) "
                        + "refer to missing glyph indices "
                        + $"[{string.Join(", ", unresolved.Distinct())}]."
                );
            }
        }

        private static void RestoreMappedActiveStates(
            IReadOnlyDictionary<UnityEngine.Object, UnityEngine.Object>
                sourceToTarget
        )
        {
            foreach (
                var pair in sourceToTarget
                    .Where(
                        pair =>
                            pair.Key is GameObject
                            && pair.Value is GameObject
                    )
                    .OrderByDescending(
                        pair => GetHierarchyDepth((GameObject)pair.Key)
                    )
            )
            {
                ((GameObject)pair.Value).SetActive(
                    ((GameObject)pair.Key).activeSelf
                );
            }
        }

        private static int GetHierarchyDepth(GameObject gameObject)
        {
            var depth = 0;
            for (
                var parent = gameObject.transform.parent;
                parent != null;
                parent = parent.parent
            )
            {
                depth++;
            }
            return depth;
        }

        private static void RefreshMaterializedTextComponents(
            IEnumerable<UnityEngine.Object> roots
        )
        {
            foreach (
                var text in EnumerateOwnedObjects(roots)
                    .OfType<Component>()
                    .Where(
                        candidate =>
                            IsTypeOrSubclass(
                                candidate.GetType(),
                                "TMPro.TMP_Text"
                            )
                    )
            )
            {
                var setAllDirty = text
                    .GetType()
                    .GetMethod(
                        "SetAllDirty",
                        BindingFlags.Public | BindingFlags.Instance
                    );
                setAllDirty?.Invoke(text, null);
            }
        }

        private static IEnumerable<UnityEngine.Object> EnumerateOwnedObjects(
            IEnumerable<UnityEngine.Object> roots
        )
        {
            var result = new HashSet<UnityEngine.Object>(
                UnityObjectReferenceComparer.Instance
            );
            foreach (var root in roots)
                AddOwnedObjects(root, result);
            return result;
        }

        private static bool IsTypeOrSubclass(Type type, string fullName)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (
                    string.Equals(
                        current.FullName,
                        fullName,
                        StringComparison.Ordinal
                    )
                )
                {
                    return true;
                }
            }
            return false;
        }

        private static void MapEquivalentObjects(
            UnityEngine.Object external,
            UnityEngine.Object target,
            IDictionary<UnityEngine.Object, UnityEngine.Object> result
        )
        {
            if (external == null || target == null)
                return;

            result[external] = target;
            if (!(external is GameObject externalGameObject))
                return;
            if (!(target is GameObject targetGameObject))
                return;

            MapGameObjectHierarchy(externalGameObject, targetGameObject, result);
        }

        private static void MapGameObjectHierarchy(
            GameObject external,
            GameObject target,
            IDictionary<UnityEngine.Object, UnityEngine.Object> result
        )
        {
            result[external] = target;
            result[external.transform] = target.transform;

            var externalComponents = external.GetComponents<Component>();
            var targetComponents = target.GetComponents<Component>();
            var componentCount = Math.Min(
                externalComponents.Length,
                targetComponents.Length
            );
            for (var index = 0; index < componentCount; index++)
            {
                var externalComponent = externalComponents[index];
                var targetComponent = targetComponents[index];
                if (
                    externalComponent != null
                    && targetComponent != null
                    && externalComponent.GetType() == targetComponent.GetType()
                )
                {
                    result[externalComponent] = targetComponent;
                }
            }

            var childCount = Math.Min(
                external.transform.childCount,
                target.transform.childCount
            );
            for (var index = 0; index < childCount; index++)
            {
                MapGameObjectHierarchy(
                    external.transform.GetChild(index).gameObject,
                    target.transform.GetChild(index).gameObject,
                    result
                );
            }
        }

        private static void RemapSerializedReferences(
            IEnumerable<UnityEngine.Object> roots,
            IReadOnlyDictionary<UnityEngine.Object, UnityEngine.Object> linkedObjects
        )
        {
            var ownedObjects = roots
                .SelectMany(EnumerateOwnedObjects)
                .Distinct(UnityObjectReferenceComparer.Instance)
                .ToArray();

            foreach (var ownedObject in ownedObjects)
            {
                try
                {
                    var serializedObject = new SerializedObject(ownedObject);
                    var property = serializedObject.GetIterator();
                    var changed = false;
                    while (property.Next(true))
                    {
                        if (
                            property.propertyType
                            != SerializedPropertyType.ObjectReference
                        )
                        {
                            continue;
                        }

                        var externalReference = property.objectReferenceValue;
                        UnityEngine.Object linkedReference = null;
                        var hasLinkedReference =
                            externalReference != null
                            && linkedObjects.TryGetValue(
                                externalReference,
                                out linkedReference
                            );

                        if (
                            externalReference == null
                            || !hasLinkedReference
                            || linkedReference == null
                        )
                        {
                            continue;
                        }

                        property.objectReferenceValue = linkedReference;
                        changed = true;
                    }

                    if (changed)
                        serializedObject.ApplyModifiedPropertiesWithoutUndo();
                }
                catch (ArgumentException)
                {
                    // Some native engine objects expose no SerializedObject surface.
                }
            }
        }

        private static IEnumerable<UnityEngine.Object> EnumerateOwnedObjects(
            UnityEngine.Object root
        )
        {
            if (!(root is GameObject gameObject))
            {
                yield return root;
                yield break;
            }

            foreach (var transform in gameObject.GetComponentsInChildren<Transform>(true))
            {
                yield return transform.gameObject;
                foreach (var component in transform.GetComponents<Component>())
                {
                    if (component != null)
                        yield return component;
                }
            }
        }

        private sealed class UnityObjectReferenceComparer
            : IEqualityComparer<UnityEngine.Object>
        {
            public static readonly UnityObjectReferenceComparer Instance =
                new UnityObjectReferenceComparer();

            public bool Equals(UnityEngine.Object left, UnityEngine.Object right)
            {
                if (ReferenceEquals(left, right))
                    return true;
                if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                    return false;

                return left == right;
            }

            public int GetHashCode(UnityEngine.Object value)
            {
                if (ReferenceEquals(value, null))
                    return 0;
                return value.GetHashCode();
            }
        }

        private readonly struct LinkedAddressableExternalObjectKey
            : IEquatable<LinkedAddressableExternalObjectKey>
        {
            public LinkedAddressableExternalObjectKey(long sourcePathId, string type)
            {
                SourcePathId = sourcePathId;
                Type = type ?? string.Empty;
            }

            private long SourcePathId { get; }

            private string Type { get; }

            public bool Equals(LinkedAddressableExternalObjectKey other)
            {
                return SourcePathId == other.SourcePathId
                    && string.Equals(Type, other.Type, StringComparison.Ordinal);
            }

            public override bool Equals(object other)
            {
                return other is LinkedAddressableExternalObjectKey key
                    && Equals(key);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (SourcePathId.GetHashCode() * 397)
                        ^ StringComparer.Ordinal.GetHashCode(Type);
                }
            }
        }
    }

    internal sealed class LinkedAddressableMaterializedGraph
    {
        public LinkedAddressableMaterializedGraph(
            UnityEngine.Object mainAsset,
            IReadOnlyList<LinkedAddressableMaterializedSubAsset> subAssets,
            IReadOnlyList<LinkedAddressableMaterializedSourceObject> sourceObjects
        )
        {
            MainAsset = mainAsset;
            SubAssets = subAssets;
            SourceObjects = sourceObjects;
        }

        public UnityEngine.Object MainAsset { get; }

        public IReadOnlyList<LinkedAddressableMaterializedSubAsset> SubAssets { get; }

        public IReadOnlyList<LinkedAddressableMaterializedSourceObject>
            SourceObjects { get; }
    }

    internal sealed class LinkedAddressableMaterializedSubAsset
    {
        public LinkedAddressableMaterializedSubAsset(
            string identifier,
            UnityEngine.Object asset
        )
        {
            Identifier = identifier;
            Asset = asset;
        }

        public string Identifier { get; }

        public UnityEngine.Object Asset { get; }
    }

    internal sealed class LinkedAddressableMaterializedSourceObject
    {
        public LinkedAddressableMaterializedSourceObject(
            UnityEngine.Object target,
            long sourceLocalId,
            int sourceClassId,
            string sourceType,
            string sourceName,
            long ownerHintLocalId,
            int ownerHintClassId,
            string ownerHintType,
            string ownerHintName,
            bool isMainSource,
            bool preferBuiltinBundle
        )
        {
            Target = target;
            SourceLocalId = sourceLocalId;
            SourceClassId = sourceClassId;
            SourceType = sourceType;
            SourceName = sourceName;
            OwnerHintLocalId = ownerHintLocalId;
            OwnerHintClassId = ownerHintClassId;
            OwnerHintType = ownerHintType;
            OwnerHintName = ownerHintName;
            IsMainSource = isMainSource;
            PreferBuiltinBundle = preferBuiltinBundle;
        }

        public UnityEngine.Object Target { get; }

        public long SourceLocalId { get; }

        public int SourceClassId { get; }

        public string SourceType { get; }

        public string SourceName { get; }

        public long OwnerHintLocalId { get; }

        public int OwnerHintClassId { get; }

        public string OwnerHintType { get; }

        public string OwnerHintName { get; }

        public bool IsMainSource { get; }

        public bool PreferBuiltinBundle { get; }
    }
}
