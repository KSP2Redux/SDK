using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation
{
    /// <summary>
    /// Discovers and caches every <see cref="IPlanetValidator" /> in this editor assembly.
    /// </summary>
    /// <remarks>
    /// Validators are registered by reflection. Drop a new file implementing <see cref="IPlanetValidator" /> with a public parameterless constructor and the next domain reload picks it up. The cache is cleared on the first access after each domain reload because static state does not survive reloads.
    /// </remarks>
    public static class PlanetValidatorRegistry
    {
        private static IReadOnlyList<IPlanetValidator> _cache;

        /// <summary>
        /// All discovered validators, in undefined order.
        /// </summary>
        public static IReadOnlyList<IPlanetValidator> Validators => _cache ??= Discover();

        private static IReadOnlyList<IPlanetValidator> Discover()
        {
            var found = new List<IPlanetValidator>();
            Type contract = typeof(IPlanetValidator);
            foreach (Type type in contract.Assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface || !contract.IsAssignableFrom(type))
                    continue;
                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    Debug.LogWarning($"[PlanetValidatorRegistry] '{type.FullName}' implements IPlanetValidator but has no public parameterless constructor; skipping.");
                    continue;
                }
                try
                {
                    found.Add((IPlanetValidator)Activator.CreateInstance(type));
                }
                catch (Exception e)
                {
                    Debug.LogError($"[PlanetValidatorRegistry] Failed to instantiate '{type.FullName}': {e}");
                }
            }
            return found;
        }
    }
}
