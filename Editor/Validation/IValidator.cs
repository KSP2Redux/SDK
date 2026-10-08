using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.Validation
{
    /// <summary>
    /// Contract for a validator that emits <see cref="ValidationIssue" /> findings against a context
    /// of type <typeparamref name="T" />.
    /// </summary>
    /// <remarks>
    /// Implementations are auto-discovered by <see cref="ValidatorRegistry{T}" /> via
    /// <see cref="Reflection.ReduxTypeCache" />. Concrete types must have a public parameterless
    /// constructor. Domain-specific sub-interfaces (e.g. IPlanetValidator, IPartValidator) extend
    /// this with their own filtering metadata.
    /// </remarks>
    /// <typeparam name="T">The validation context type the validator operates on.</typeparam>
    public interface IValidator<in T>
    {
        /// <summary>
        /// Categorizes the validator for refresh-frequency purposes.
        /// </summary>
        /// <remarks>
        /// Defaults to <see cref="ValidatorCost.Cheap" />. Expensive validators must override.
        /// </remarks>
        ValidatorCost Cost => ValidatorCost.Cheap;

        /// <summary>
        /// Runs the check against <paramref name="context" /> and returns any issues found.
        /// </summary>
        /// <remarks>
        /// Cheap validators are called on every refresh tick from open inspectors. Expensive
        /// validators run only when the user clicks "Run full validation" so heavy walks (texture
        /// pixels, large hash computes, big array scans) do not stall the inspector. Return an
        /// empty enumerable when nothing is wrong. Never return null.
        /// </remarks>
        /// <param name="context">The context to validate. May be a prefab asset, a scene instance, or a memoized context built once per validation run. Implementations should null-check before walking deep into the data.</param>
        /// <returns>Issues produced by this validator. Empty when the check passes.</returns>
        IEnumerable<ValidationIssue> Validate(T context);
    }
}
