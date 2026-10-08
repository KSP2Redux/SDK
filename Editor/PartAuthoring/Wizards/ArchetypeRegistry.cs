using System;
using System.Collections.Generic;
using Ksp2UnityTools.Editor.Reflection;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Wizards
{
    /// <summary>
    /// Discovers every concrete <see cref="IPartArchetype" /> in the loaded assemblies and caches
    /// one instance per type. The wizard's archetype picker reads from <see cref="GetAll" />.
    /// </summary>
    /// <remarks>
    /// Discovery is backed by <see cref="ReduxTypeCache" /> so the registry stays correct across
    /// SDK-mode and plugin-side assemblies that <c>UnityEditor.TypeCache</c> mishandles under
    /// ThunderKit. The static cache zeros on domain reload, so the next call rebuilds.
    /// </remarks>
    public static class ArchetypeRegistry
    {
        private static List<IPartArchetype> _instances;

        /// <summary>Returns one instance per concrete archetype, sorted alphabetically by type name.</summary>
        public static IReadOnlyList<IPartArchetype> GetAll() => _instances ??= Build();

        /// <summary>Returns the cached instance of <paramref name="archetypeType" />, or null if not registered.</summary>
        public static IPartArchetype Find(Type archetypeType)
        {
            if (archetypeType == null) return null;
            var all = GetAll();
            for (var i = 0; i < all.Count; i++)
            {
                if (all[i].GetType() == archetypeType) return all[i];
            }
            return null;
        }

        private static List<IPartArchetype> Build()
        {
            var list = new List<IPartArchetype>();
            foreach (var t in ReduxTypeCache.GetTypesDerivedFrom<IPartArchetype>())
            {
                if (t.IsAbstract || t.IsInterface) continue;
                if (t.GetConstructor(Type.EmptyTypes) == null)
                {
                    Debug.LogWarning($"[ArchetypeRegistry] Skipping {t.FullName}. No public parameterless constructor.");
                    continue;
                }
                try
                {
                    list.Add((IPartArchetype)Activator.CreateInstance(t));
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ArchetypeRegistry] Failed to instantiate {t.FullName}: {e.Message}");
                }
            }
            list.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.GetType().Name, b.GetType().Name));
            return list;
        }
    }
}
