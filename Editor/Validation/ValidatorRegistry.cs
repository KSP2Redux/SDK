using System;
using System.Collections.Generic;
using Ksp2UnityTools.Editor.Reflection;
using UnityEngine;

namespace Ksp2UnityTools.Editor.Validation
{
    /// <summary>
    /// Discovers and caches every <see cref="IValidator{T}" /> implementation in the loaded
    /// editor-side assemblies for the given context type <typeparamref name="T" />.
    /// </summary>
    /// <remarks>
    /// Discovery is backed by <see cref="ReduxTypeCache" /> so SDK-mode and plugin-side assemblies
    /// participate without explicit registration. The per-T static cache resets on domain reload
    /// automatically (static state is lost), and <see cref="ReduxTypeCache" /> rebuilds on the
    /// same event, so the next access produces a fresh validator list.
    /// </remarks>
    /// <typeparam name="T">The validation context type whose validators to discover.</typeparam>
    public static class ValidatorRegistry<T>
    {
        private static IReadOnlyList<IValidator<T>> _cache;

        /// <summary>All discovered validators for <typeparamref name="T" />, in undefined order.</summary>
        public static IReadOnlyList<IValidator<T>> Validators => _cache ??= Build();

        private static IReadOnlyList<IValidator<T>> Build()
        {
            var found = new List<IValidator<T>>();
            foreach (Type type in ReduxTypeCache.GetTypesDerivedFrom<IValidator<T>>())
            {
                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    Debug.LogWarning($"[ValidatorRegistry<{typeof(T).Name}>] '{type.FullName}' implements IValidator<{typeof(T).Name}> but has no public parameterless constructor; skipping.");
                    continue;
                }
                try
                {
                    found.Add((IValidator<T>)Activator.CreateInstance(type));
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ValidatorRegistry<{typeof(T).Name}>] Failed to instantiate '{type.FullName}': {e}");
                }
            }
            return found;
        }
    }
}
