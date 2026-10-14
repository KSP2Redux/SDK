using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation
{
    /// <summary>
    /// Contract for a validator that checks a tech tree as a whole.
    /// </summary>
    /// <remarks>
    /// Specialization of <see cref="IValidator{T}" /> for <see cref="TechTreeValidationContext" />. Its issues are about
    /// no one node. Implementations are auto-discovered by <see cref="ValidatorRegistry{T}" />, and concrete types must
    /// have a public parameterless constructor.
    /// </remarks>
    public interface ITechTreeValidator : IValidator<TechTreeValidationContext>
    {
    }
}
