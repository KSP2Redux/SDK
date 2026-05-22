using System;
using System.Collections.Generic;
using System.Reflection;
using Ksp2UnityTools.Editor.Reflection;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors
{
    /// <summary>
    /// Discovers and creates <see cref="IFieldRenderer" /> implementations registered via
    /// <see cref="FieldRendererAttribute" />. Keyed by (target type, <see cref="FieldRendererKind" />)
    /// so a single registry serves both array-element and direct-field dispatch.
    /// </summary>
    public static class FieldRendererRegistry
    {
        private readonly struct Key : IEquatable<Key>
        {
            public readonly Type Type;
            public readonly FieldRendererKind Kind;

            public Key(Type type, FieldRendererKind kind)
            {
                Type = type;
                Kind = kind;
            }

            public bool Equals(Key other) => Type == other.Type && Kind == other.Kind;
            public override bool Equals(object obj) => obj is Key other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Type, Kind);
        }

        private static Dictionary<Key, Type> _rendererTypeByKey;

        /// <summary>
        /// Attempts to create a fresh renderer instance for the given target type and kind.
        /// </summary>
        public static bool TryCreate(Type type, FieldRendererKind kind, out IFieldRenderer renderer)
        {
            renderer = null;
            if (type == null)
            {
                return false;
            }
            EnsureBuilt();
            if (!_rendererTypeByKey.TryGetValue(new Key(type, kind), out var rendererType))
            {
                return false;
            }
            try
            {
                renderer = Activator.CreateInstance(rendererType) as IFieldRenderer;
                return renderer != null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FieldRendererRegistry] Failed to construct {rendererType}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Drops the cached lookup. Next call rebuilds.
        /// </summary>
        public static void Invalidate()
        {
            _rendererTypeByKey = null;
        }

        private static void EnsureBuilt()
        {
            if (_rendererTypeByKey != null)
            {
                return;
            }
            _rendererTypeByKey = new Dictionary<Key, Type>();
            foreach (var rendererType in ReduxTypeCache.GetTypesWithAttribute<FieldRendererAttribute>())
            {
                if (!typeof(IFieldRenderer).IsAssignableFrom(rendererType))
                {
                    Debug.LogWarning(
                        $"[FieldRendererRegistry] {rendererType.FullName} carries [FieldRenderer] but does not implement IFieldRenderer; ignored.");
                    continue;
                }
                var attr = rendererType.GetCustomAttribute<FieldRendererAttribute>();
                if (attr?.Type == null)
                {
                    continue;
                }
                var key = new Key(attr.Type, attr.Kind);
                if (_rendererTypeByKey.TryGetValue(key, out var existing))
                {
                    Debug.LogWarning(
                        $"[FieldRendererRegistry] Duplicate renderer for ({attr.Type.Name}, {attr.Kind}): {existing.FullName} vs {rendererType.FullName}. Keeping the first.");
                    continue;
                }
                _rendererTypeByKey[key] = rendererType;
            }
        }
    }
}
