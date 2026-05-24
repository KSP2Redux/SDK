using System;
using System.Collections.Generic;
using KSP;
using KSP.OAB;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.PartAuthoring.StockStats;
using Ksp2UnityTools.Editor.PartAuthoring.Tools;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ksp2UnityTools.Editor.PartAuthoring.Wizards
{
    /// <summary>
    /// Creates a new part on disk from an <see cref="IPartArchetype" /> and a chosen
    /// (family, size, bucket) context, with rollback on any failure.
    /// </summary>
    /// <remarks>
    /// Each successful step appends the created asset's path to an internal list. On any
    /// exception during <see cref="Run" />, the steps are unwound in reverse and the
    /// partially-created assets removed, restoring the project to its pre-scaffold state.
    /// Addressables entries are tracked separately and removed via
    /// <c>AddressableAssetSettings.RemoveAssetEntry</c>.
    /// </remarks>
    public sealed class PartFolderScaffold
    {
        private readonly IPartArchetype _archetype;
        private readonly string _parentFolder;
        private readonly string _partName;
        private readonly MetaAssemblySizeFilterType _size;
        private readonly BucketResolution _bucket;
        private readonly HashSet<Type> _enabledModules;
        private readonly IReadOnlyDictionary<string, float> _valueOverrides;
        private readonly SourceMeshChoice _meshChoice;
        private readonly GameObject _sourcePrefab;
        private readonly GameObject _sourceFbxAsset;
        private readonly bool _tagDragCubeMesh;
        private readonly bool _fbxAutoScale;
        private readonly bool _fbxAutoRotate;

        private readonly List<string> _createdPaths = new();
        private string _addressableEntryGuid;
        private AddressableAssetGroup _addressableEntryGroup;

        public PartFolderScaffold(
            IPartArchetype archetype,
            string parentFolder,
            string partName,
            MetaAssemblySizeFilterType size,
            BucketResolution bucket,
            HashSet<Type> enabledModules = null,
            IReadOnlyDictionary<string, float> valueOverrides = null,
            SourceMeshChoice meshChoice = SourceMeshChoice.Skip,
            GameObject sourcePrefab = null,
            GameObject sourceFbxAsset = null,
            bool tagDragCubeMesh = true,
            bool fbxAutoScale = true,
            bool fbxAutoRotate = true)
        {
            _archetype = archetype ?? throw new ArgumentNullException(nameof(archetype));
            _parentFolder = parentFolder ?? throw new ArgumentNullException(nameof(parentFolder));
            _partName = partName ?? throw new ArgumentNullException(nameof(partName));
            _size = size;
            _bucket = bucket;
            _enabledModules = enabledModules;
            _valueOverrides = valueOverrides;
            _meshChoice = meshChoice;
            _sourcePrefab = sourcePrefab;
            _sourceFbxAsset = sourceFbxAsset;
            _tagDragCubeMesh = tagDragCubeMesh;
            _fbxAutoScale = fbxAutoScale;
            _fbxAutoRotate = fbxAutoRotate;
        }

        /// <summary>Runs the full create flow. Returns the saved prefab path. On any failure, rolls back and rethrows.</summary>
        public string Run()
        {
            GameObject tempRoot = null;
            try
            {
                string folder = CreateFolder();
                tempRoot = BuildPrefabGameObject();
                CorePartData core = AttachCorePartData(tempRoot);
                AddModules(tempRoot);
                AddAttachNodesToData(core);

                string prefabPath = SavePrefab(tempRoot, folder);
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabAsset == null)
                {
                    throw new InvalidOperationException($"Saved prefab at {prefabPath} could not be reloaded.");
                }
                CorePartData prefabCore = prefabAsset.GetComponent<CorePartData>();

                _archetype.SeedDefaults(prefabCore, _bucket);
                ApplyValueOverrides(prefabCore);
                EditorUtility.SetDirty(prefabAsset);
                AssetDatabase.SaveAssets();

                PartJsonSaver.Save(prefabCore);

                RegisterPrefabInAddressables(prefabCore, prefabPath);

                AssetDatabase.Refresh();
                return prefabPath;
            }
            catch
            {
                Rollback();
                throw;
            }
            finally
            {
                if (tempRoot != null)
                {
                    Object.DestroyImmediate(tempRoot);
                }
            }
        }

        private void ApplyValueOverrides(CorePartData core)
        {
            if (_valueOverrides == null || _valueOverrides.Count == 0 || core == null)
            {
                return;
            }
            foreach (KeyValuePair<string, float> pair in _valueOverrides)
            {
                StockFieldEntry entry = StockFieldPaths.Find(pair.Key);
                if (entry?.Copier == null)
                {
                    continue;
                }
                entry.Copier(core, pair.Value, out _);
            }
        }

        private string CreateFolder()
        {
            if (!AssetDatabase.IsValidFolder(_parentFolder))
            {
                throw new InvalidOperationException($"Parent folder not found: {_parentFolder}");
            }
            string folderPath = $"{_parentFolder}/{_partName}";
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                throw new InvalidOperationException($"Folder already exists: {folderPath}");
            }
            string guid = AssetDatabase.CreateFolder(_parentFolder, _partName);
            if (string.IsNullOrEmpty(guid))
            {
                throw new InvalidOperationException($"Failed to create folder: {folderPath}");
            }
            _createdPaths.Add(folderPath);
            return folderPath;
        }

        private GameObject BuildPrefabGameObject()
        {
            var root = new GameObject(_partName);
            var model = new GameObject("model");
            model.transform.SetParent(root.transform, false);
            var col = new GameObject("col");
            col.transform.SetParent(model.transform, false);
            InstantiateSourceMesh(model);
            if (_tagDragCubeMesh)
            {
                TagDragCubeRenderers(model);
            }
            return root;
        }

        private void InstantiateSourceMesh(GameObject modelRoot)
        {
            GameObject source = _meshChoice switch
            {
                SourceMeshChoice.FBX => _sourceFbxAsset,
                SourceMeshChoice.ExistingPrefab => _sourcePrefab,
                _ => null
            };
            if (source == null)
            {
                return;
            }
            var instance = PrefabUtility.InstantiatePrefab(source, modelRoot.transform) as GameObject;
            if (instance == null)
            {
                return;
            }
            if (_meshChoice == SourceMeshChoice.FBX)
            {
                instance.transform.localPosition = Vector3.zero;
                if (_fbxAutoRotate)
                {
                    instance.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                }
                if (_fbxAutoScale)
                {
                    instance.transform.localScale = Vector3.one * 100f;
                }
            }
        }

        private static void TagDragCubeRenderers(GameObject modelRoot)
        {
            Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
            foreach (Renderer r in renderers)
            {
                if (r == null)
                {
                    continue;
                }
                try
                {
                    r.gameObject.tag = "DragCubeMesh";
                }
                catch (UnityException)
                {
                    return;
                }
            }
        }

        private CorePartData AttachCorePartData(GameObject root)
        {
            CorePartData core = root.AddComponent<CorePartData>();
            if (core.Core != null)
            {
                if (core.Core.data == null)
                {
                    core.Core.data = new PartData();
                }
                core.Core.data.partName = _partName;
                core.Core.data.family = _archetype.Family ?? string.Empty;
                core.Core.data.sizeCategory = _size;
                core.Core.data.sizeKey = PartSizeRegistry.GetPartSizeKey(null, _size)
                                         ?? PartSizeRegistry.DefaultSizeKey;
            }
            return core;
        }

        private void AddModules(GameObject root)
        {
            foreach (Type moduleType in _archetype.DefaultModules)
            {
                if (_enabledModules != null && !_enabledModules.Contains(moduleType))
                {
                    continue;
                }
                if (root.GetComponent(moduleType) != null)
                {
                    continue;
                }
                root.AddComponent(moduleType);
            }
        }

        private void AddAttachNodesToData(CorePartData core)
        {
            if (core.Core?.data?.attachNodes == null)
            {
                return;
            }
            foreach (AttachNodeTemplate template in _archetype.DefaultAttachNodes)
            {
                string sizeKey = PartSizeRegistry.GetPartSizeKey(null, template.Size)
                                 ?? PartSizeRegistry.DefaultSizeKey;
                var def = new AttachNodeDefinition
                {
                    nodeID = template.NodeId,
                    position = template.LocalPosition,
                    orientation = template.LocalDirection,
                    sizeKey = sizeKey
                };
                core.Core.data.attachNodes.Add(def);
            }
        }

        private string SavePrefab(GameObject root, string folder)
        {
            string prefabPath = $"{folder}/{_partName}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Failed to save prefab at {prefabPath}");
            }
            _createdPaths.Add(prefabPath);
            return prefabPath;
        }

        private void RegisterPrefabInAddressables(CorePartData target, string prefabPath)
        {
            AddressableAssetGroup group = PartAuthoringAddressables.ResolveGroup(target);
            if (group == null)
            {
                EditorUtility.DisplayDialog(
                    "Addressables not registered",
                    $"No owning mod found and no '{PartAuthoringAddressables.PartsGroupName}' group exists. " +
                    "The prefab was created but not registered as addressable. Register it manually.",
                    "OK"
                );
                return;
            }
            AddressablesTools.MakeAddressable(group, prefabPath, _partName);
            _addressableEntryGroup = group;
            _addressableEntryGuid = AssetDatabase.AssetPathToGUID(prefabPath);
        }

        private void Rollback()
        {
            if (!string.IsNullOrEmpty(_addressableEntryGuid))
            {
                try
                {
                    AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
                    settings?.RemoveAssetEntry(_addressableEntryGuid);
                }
                catch
                {
                    // Best-effort rollback - failure here is logged below by the broader Refresh.
                }
            }
            for (int i = _createdPaths.Count - 1; i >= 0; i--)
            {
                try
                {
                    AssetDatabase.DeleteAsset(_createdPaths[i]);
                }
                catch
                {
                    // Best-effort rollback.
                }
            }
            AssetDatabase.Refresh();
        }
    }
}
