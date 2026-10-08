using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Validation
{
    /// <summary>
    /// Contract for a single mission-authoring validator.
    /// </summary>
    /// <remarks>
    /// Specialization of <see cref="IValidator{T}" /> for <see cref="MissionValidationContext" />.
    /// The generic interface supplies <c>Cost</c> and <c>Validate</c>. Implementations are
    /// auto-discovered by <see cref="ValidatorRegistry{T}" />. Concrete types must have a public
    /// parameterless constructor.
    /// </remarks>
    public interface IMissionValidator : IValidator<MissionValidationContext>
    {
    }
}
