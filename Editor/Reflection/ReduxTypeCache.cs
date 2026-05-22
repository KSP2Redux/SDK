using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace Ksp2UnityTools.Editor.Reflection
{
    /// <summary>
    /// Manual reflection cache that mirrors the surface of Unity's <c>UnityEditor.TypeCache</c> for
    /// the parts of it we need.
    /// </summary>
    /// <remarks>
    /// Why not <c>UnityEditor.TypeCache</c>: the SDK ships into contexts where KSP2 is imported via
    /// ThunderKit, and that import process breaks Unity's TypeCache for the imported types
    /// (incomplete or empty results). This cache walks <see cref="AppDomain.CurrentDomain" />
    /// directly so type discovery stays correct across all SDK environments.
    ///
    /// The cache builds lazily on first query and rebuilds on every domain reload via
    /// <see cref="AssemblyReloadEvents" />. Per-query results are memoized so repeated lookups
    /// against the same base type or attribute type are cheap.
    /// </remarks>
    [InitializeOnLoad]
    public static class ReduxTypeCache
    {
        private static Dictionary<Type, List<Type>> _derivedTypesByBase;
        private static Dictionary<Type, List<Type>> _typesByAttribute;
        private static List<Type> _allConcreteTypes;

        static ReduxTypeCache()
        {
            AssemblyReloadEvents.afterAssemblyReload += Invalidate;
        }

        /// <summary>
        /// Returns every concrete (non-abstract) type that derives from <typeparamref name="TBase" />.
        /// </summary>
        public static IReadOnlyList<Type> GetTypesDerivedFrom<TBase>()
        {
            return GetTypesDerivedFrom(typeof(TBase));
        }

        /// <summary>
        /// Returns every concrete (non-abstract) type that derives from <paramref name="baseType" />.
        /// </summary>
        public static IReadOnlyList<Type> GetTypesDerivedFrom(Type baseType)
        {
            if (baseType == null)
            {
                return Array.Empty<Type>();
            }
            EnsureBuilt();
            return _derivedTypesByBase.TryGetValue(baseType, out var list)
                ? list
                : (IReadOnlyList<Type>)Array.Empty<Type>();
        }

        /// <summary>
        /// Returns every concrete type that carries the given attribute, inheritance respected.
        /// </summary>
        public static IReadOnlyList<Type> GetTypesWithAttribute<TAttribute>() where TAttribute : Attribute
        {
            return GetTypesWithAttribute(typeof(TAttribute));
        }

        /// <summary>
        /// Returns every concrete type that carries the given attribute, inheritance respected.
        /// </summary>
        public static IReadOnlyList<Type> GetTypesWithAttribute(Type attributeType)
        {
            if (attributeType == null)
            {
                return Array.Empty<Type>();
            }
            EnsureBuilt();
            if (_typesByAttribute.TryGetValue(attributeType, out var cached))
            {
                return cached;
            }
            var result = _allConcreteTypes
                .Where(t => t.IsDefined(attributeType, inherit: true))
                .ToList();
            _typesByAttribute[attributeType] = result;
            return result;
        }

        /// <summary>
        /// Forces the cache to drop its built state. The next query rebuilds. Useful for tests or
        /// for code paths that load assemblies at runtime.
        /// </summary>
        public static void Invalidate()
        {
            _derivedTypesByBase = null;
            _typesByAttribute = null;
            _allConcreteTypes = null;
        }

        private static void EnsureBuilt()
        {
            if (_derivedTypesByBase != null)
            {
                return;
            }
            _derivedTypesByBase = new Dictionary<Type, List<Type>>();
            _typesByAttribute = new Dictionary<Type, List<Type>>();
            _allConcreteTypes = new List<Type>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic)
                {
                    continue;
                }
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }
                foreach (var type in types)
                {
                    if (type == null || type.IsAbstract || type.IsInterface)
                    {
                        continue;
                    }
                    _allConcreteTypes.Add(type);
                    for (var baseType = type.BaseType; baseType != null && baseType != typeof(object); baseType = baseType.BaseType)
                    {
                        if (!_derivedTypesByBase.TryGetValue(baseType, out var list))
                        {
                            _derivedTypesByBase[baseType] = list = new List<Type>();
                        }
                        list.Add(type);
                    }
                    foreach (var iface in type.GetInterfaces())
                    {
                        if (!_derivedTypesByBase.TryGetValue(iface, out var list))
                        {
                            _derivedTypesByBase[iface] = list = new List<Type>();
                        }
                        list.Add(type);
                    }
                }
            }
        }
    }
}
