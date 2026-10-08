using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation
{
    /// <summary>
    /// Discovers and caches every <see cref="IPartValidator" /> in the loaded editor assemblies.
    /// </summary>
    /// <remarks>
    /// Thin view over <see cref="ValidatorRegistry{T}" /> with <c>T = PartValidationContext</c>. The
    /// generic registry handles reflection, instantiation, and cache invalidation. This wrapper
    /// filters the result down to <see cref="IPartValidator" /> so the part runner can hold the
    /// sub-interface type without an extra cast at the call site.
    /// </remarks>
    public static class PartValidatorRegistry
    {
        private static IReadOnlyList<IPartValidator> _cache;

        /// <summary>All discovered part validators, in undefined order.</summary>
        public static IReadOnlyList<IPartValidator> Validators => _cache ??= Build();

        private static IReadOnlyList<IPartValidator> Build() =>
            ValidatorRegistry<PartValidationContext>.Validators
                .OfType<IPartValidator>()
                .ToList();
    }
}
