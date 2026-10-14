using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation
{
    /// <summary>
    /// Contract for a validator that checks one node of a tech tree.
    /// </summary>
    /// <remarks>
    /// Specialization of <see cref="IValidator{T}" /> for <see cref="TechNodeValidationContext" />. It runs once per node
    /// of the tree, never on reference nodes, and its issues are about that node, so the editor can mark and go to it.
    /// Implementations are auto-discovered by <see cref="ValidatorRegistry{T}" />, and concrete types must have a public
    /// parameterless constructor.
    /// </remarks>
    public interface ITechNodeValidator : IValidator<TechNodeValidationContext>
    {
    }
}
