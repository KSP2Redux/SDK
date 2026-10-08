using System.Collections.Generic;
using System.Linq;
using KSP;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation
{
    /// <summary>
    /// Discovers and caches every <see cref="IPlanetValidator" /> in the loaded editor assemblies.
    /// </summary>
    /// <remarks>
    /// Thin view over <see cref="ValidatorRegistry{T}" /> with <c>T = CoreCelestialBodyData</c>. The
    /// generic registry handles reflection, instantiation, and cache invalidation. This wrapper
    /// filters the result down to validators that additionally implement
    /// <see cref="IPlanetValidator" /> so the planet runner can read <c>AppliesTo</c> without an
    /// extra cast.
    /// </remarks>
    public static class PlanetValidatorRegistry
    {
        private static IReadOnlyList<IPlanetValidator> _cache;

        /// <summary>All discovered planet validators, in undefined order.</summary>
        public static IReadOnlyList<IPlanetValidator> Validators => _cache ??= Build();

        private static IReadOnlyList<IPlanetValidator> Build() =>
            ValidatorRegistry<CoreCelestialBodyData>.Validators
                .OfType<IPlanetValidator>()
                .ToList();
    }
}
