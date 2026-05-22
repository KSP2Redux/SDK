using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using KSP.Sim.Definitions;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators
{
    /// <summary>
    /// Walks <see cref="ModuleData" /> instances and their nested data classes looking for string
    /// fields decorated with a marker attribute. Used by every attribute-driven validator
    /// (TransformPath, TransformName, ResourceName, AttachNodeId, ExperimentName) so the
    /// reflection traversal is implemented once.
    /// </summary>
    /// <remarks>
    /// Recurses through nested classes, arrays, and List&lt;T&gt; so attributes on fields buried
    /// inside container records (e.g. ExperimentConfiguration.ResourcesCost[].ResourceName) are
    /// reached. Unity scalar types and primitives short-circuit to keep the walk bounded; a depth
    /// limit protects against accidental cycles.
    /// </remarks>
    internal static class ModuleFieldEnumerator
    {
        private const BindingFlags FIELD_FLAGS =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private const int MAX_DEPTH = 6;

        private static readonly Dictionary<Type, FieldInfo[]> _fieldsByType = new();

        /// <summary>One <see cref="string" />-typed field reference plus its live value.</summary>
        public readonly struct StringFieldRef
        {
            public StringFieldRef(ModuleData module, FieldInfo field, string value, string displayPath)
            {
                Module = module;
                Field = field;
                Value = value;
                DisplayPath = displayPath;
            }

            /// <summary>The top-level module that owns the field's containing graph.</summary>
            public ModuleData Module { get; }

            /// <summary>The leaf <see cref="string" /> field whose value is yielded.</summary>
            public FieldInfo Field { get; }

            /// <summary>Live value at walk time.</summary>
            public string Value { get; }

            /// <summary>Dotted / indexed path from the module root to the field, used in messages.</summary>
            public string DisplayPath { get; }
        }

        /// <summary>
        /// Yields every <see cref="string" />-typed field carrying <typeparamref name="TAttr" />
        /// anywhere in <paramref name="modules" /> or their nested data classes.
        /// </summary>
        /// <remarks>
        /// Empty / null entries in the module list are skipped. The per-type list of walkable
        /// fields is cached statically so per-tick validator runs do not re-walk reflection on
        /// every call.
        /// </remarks>
        public static IEnumerable<StringFieldRef> EnumerateStringFieldsWithAttribute<TAttr>(IReadOnlyList<ModuleData> modules)
            where TAttr : Attribute
        {
            if (modules == null)
            {
                yield break;
            }
            Type attrType = typeof(TAttr);
            foreach (ModuleData module in modules)
            {
                if (module == null)
                {
                    continue;
                }
                foreach (var hit in WalkObject(module, module, module.GetType().Name, attrType, depth: 0))
                {
                    yield return hit;
                }
            }
        }

        private static IEnumerable<StringFieldRef> WalkObject(ModuleData root, object current, string pathPrefix, Type attrType, int depth)
        {
            if (current == null || depth > MAX_DEPTH)
            {
                yield break;
            }
            FieldInfo[] fields = GetWalkableFields(current.GetType());
            foreach (FieldInfo field in fields)
            {
                string fieldPath = $"{pathPrefix}.{field.Name}";
                object value;
                try
                {
                    value = field.GetValue(current);
                }
                catch
                {
                    continue;
                }

                if (field.FieldType == typeof(string))
                {
                    if (field.IsDefined(attrType, inherit: true))
                    {
                        yield return new StringFieldRef(root, field, value as string, fieldPath);
                    }
                    continue;
                }

                if (value == null)
                {
                    continue;
                }

                if (IsScalarOrOpaque(field.FieldType))
                {
                    continue;
                }

                if (value is IList list)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        object item = list[i];
                        if (item == null)
                        {
                            continue;
                        }
                        if (item is string s)
                        {
                            // The list-element attribute lives on the list field itself.
                            if (field.IsDefined(attrType, inherit: true))
                            {
                                yield return new StringFieldRef(root, field, s, $"{fieldPath}[{i}]");
                            }
                            continue;
                        }
                        if (IsScalarOrOpaque(item.GetType()))
                        {
                            continue;
                        }
                        foreach (var hit in WalkObject(root, item, $"{fieldPath}[{i}]", attrType, depth + 1))
                        {
                            yield return hit;
                        }
                    }
                    continue;
                }

                foreach (var hit in WalkObject(root, value, fieldPath, attrType, depth + 1))
                {
                    yield return hit;
                }
            }
        }

        private static FieldInfo[] GetWalkableFields(Type type)
        {
            if (_fieldsByType.TryGetValue(type, out FieldInfo[] cached))
            {
                return cached;
            }
            var list = new List<FieldInfo>();
            foreach (FieldInfo field in type.GetFields(FIELD_FLAGS))
            {
                list.Add(field);
            }
            FieldInfo[] result = list.ToArray();
            _fieldsByType[type] = result;
            return result;
        }

        private static bool IsScalarOrOpaque(Type type)
        {
            if (type == null)
            {
                return true;
            }
            if (type.IsPrimitive || type.IsEnum)
            {
                return true;
            }
            if (type == typeof(string) || type == typeof(decimal))
            {
                return true;
            }
            if (type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4))
            {
                return true;
            }
            if (type == typeof(Vector2Int) || type == typeof(Vector3Int))
            {
                return true;
            }
            if (type == typeof(Quaternion) || type == typeof(Bounds) || type == typeof(Rect) ||
                type == typeof(Color) || type == typeof(Color32))
            {
                return true;
            }
            if (type == typeof(AnimationCurve) || type == typeof(FloatCurve) || type == typeof(Gradient))
            {
                return true;
            }
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return true;
            }
            string ns = type.Namespace;
            if (ns != null && (ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal)))
            {
                return true;
            }
            return false;
        }
    }
}
