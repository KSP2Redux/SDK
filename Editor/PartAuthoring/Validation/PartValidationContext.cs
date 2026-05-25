using System;
using System.Collections.Generic;
using KSP;
using KSP.Sim.Definitions;
using KSP.Sim.ResourceSystem;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation
{
    /// <summary>
    /// Memoized inputs shared across all validators in a single run.
    /// </summary>
    /// <remarks>
    /// Built once per validation tick by the runner. Lazy fields amortize the cost of expensive
    /// prefab walks (transform flattening, collider scan, resource container indexing) across
    /// every validator that consumes them. Validators never instantiate this directly. The runner
    /// owns construction so the per-run memoization scope matches one inspector tick.
    /// </remarks>
    public sealed class PartValidationContext
    {
        private readonly Lazy<Bounds> _prefabBounds;
        private readonly Lazy<IReadOnlyDictionary<string, Transform>> _transformByName;
        private readonly Lazy<IReadOnlyList<Collider>> _colliders;
        private readonly Lazy<IReadOnlyDictionary<string, ContainedResourceDefinition>> _storedResources;
        private readonly Lazy<IReadOnlyList<ModuleData>> _modules;

        /// <summary>
        /// Constructs a context wrapping <paramref name="part" />.
        /// </summary>
        /// <param name="part">The part whose validation surfaces the context exposes. Null produces an empty context whose memoized fields return empty collections.</param>
        /// <param name="isProjectScanAvailable">When true, Full-scope cross-part validators may walk the project's other parts. Set false when running in an SDK environment without the full Redux part catalog.</param>
        public PartValidationContext(CorePartData part, bool isProjectScanAvailable = true)
        {
            Part = part;
            Prefab = part != null ? part.gameObject : null;
            IsProjectScanAvailable = isProjectScanAvailable;

            _prefabBounds = new Lazy<Bounds>(BuildPrefabBounds);
            _transformByName = new Lazy<IReadOnlyDictionary<string, Transform>>(BuildTransformByName);
            _colliders = new Lazy<IReadOnlyList<Collider>>(BuildColliders);
            _storedResources = new Lazy<IReadOnlyDictionary<string, ContainedResourceDefinition>>(BuildStoredResources);
            _modules = new Lazy<IReadOnlyList<ModuleData>>(BuildModules);
        }

        /// <summary>The validated part.</summary>
        public CorePartData Part { get; }

        /// <summary>The prefab GameObject the validated part lives on.</summary>
        public GameObject Prefab { get; }

        /// <summary>The part's core. Null when the part is null.</summary>
        public PartCore Core => Part != null ? Part.Core : null;

        /// <summary>The part's data block. Null when the part or core is null.</summary>
        public PartData Data => Part != null ? Part.Data : null;

        /// <summary>The part's <see cref="ModuleData" /> records at edit-time.</summary>
        /// <remarks>
        /// Sourced from every <see cref="PartBehaviourModule" /> on the prefab and that module's
        /// live <c>DataModules</c> dictionary. <c>PartCore.modules</c> is only populated by the
        /// runtime JSON loader and stays empty during authoring, so walking it directly returns
        /// nothing for the part the author is editing. Aggregating <c>DataModules</c> here keeps
        /// the per-validator code simple while letting validators run against the data the author
        /// actually sees in the inspector.
        /// </remarks>
        public IReadOnlyList<ModuleData> ModuleDatas => _modules.Value;

        /// <summary>Union of all Renderer bounds on the prefab, in part-local space.</summary>
        public Bounds PrefabBounds => _prefabBounds.Value;

        /// <summary>Flattened name-to-Transform lookup for every descendant of the prefab.</summary>
        /// <remarks>
        /// Names that collide use the first occurrence in a depth-first walk. Callers that need
        /// to detect collisions should compare against the raw <see cref="Prefab" /> hierarchy.
        /// </remarks>
        public IReadOnlyDictionary<string, Transform> TransformByName => _transformByName.Value;

        /// <summary>Every Collider attached to the prefab or its descendants.</summary>
        public IReadOnlyList<Collider> Colliders => _colliders.Value;

        /// <summary>Name-keyed view of <see cref="PartData.resourceContainers" />.</summary>
        public IReadOnlyDictionary<string, ContainedResourceDefinition> StoredResources => _storedResources.Value;

        /// <summary>
        /// True when Full-scope cross-part validators may walk other parts in the project.
        /// </summary>
        /// <remarks>
        /// Gates checks like duplicate part names, transform-name collisions across the catalog,
        /// and addressable-registration verification. Validators that consume this should
        /// short-circuit when it is false so SDK environments without the full catalog stay quiet.
        /// </remarks>
        public bool IsProjectScanAvailable { get; }

        private Bounds BuildPrefabBounds()
        {
            if (Prefab == null)
            {
                return default;
            }
            var renderers = Prefab.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers.Length == 0)
            {
                return default;
            }
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        private IReadOnlyDictionary<string, Transform> BuildTransformByName()
        {
            var dict = new Dictionary<string, Transform>();
            if (Prefab == null)
            {
                return dict;
            }
            foreach (var t in Prefab.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (!dict.ContainsKey(t.name))
                {
                    dict[t.name] = t;
                }
            }
            return dict;
        }

        private IReadOnlyList<Collider> BuildColliders()
        {
            if (Prefab == null)
            {
                return Array.Empty<Collider>();
            }
            return Prefab.GetComponentsInChildren<Collider>(includeInactive: true);
        }

        private IReadOnlyDictionary<string, ContainedResourceDefinition> BuildStoredResources()
        {
            var dict = new Dictionary<string, ContainedResourceDefinition>();
            var containers = Data?.resourceContainers;
            if (containers == null)
            {
                return dict;
            }
            foreach (var container in containers)
            {
                if (!string.IsNullOrEmpty(container.name) && !dict.ContainsKey(container.name))
                {
                    dict[container.name] = container;
                }
            }
            return dict;
        }

        private IReadOnlyList<ModuleData> BuildModules()
        {
            if (Prefab == null) return Array.Empty<ModuleData>();
            var list = new List<ModuleData>();
            foreach (var module in Prefab.GetComponents<PartBehaviourModule>())
            {
                if (module == null || module.DataModules == null) continue;
                foreach (var data in module.DataModules.Values)
                {
                    if (data != null) list.Add(data);
                }
            }
            return list;
        }
    }
}
