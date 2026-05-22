using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation
{
    /// <summary>
    /// Contract for a single part-authoring validator.
    /// </summary>
    /// <remarks>
    /// Specialization of <see cref="IValidator{T}" /> for <see cref="PartValidationContext" />. The
    /// generic interface supplies <see cref="IValidator{T}.Cost" /> and
    /// <see cref="IValidator{T}.Validate" />. Implementations are auto-discovered by
    /// <see cref="PartValidatorRegistry" />. Concrete types must have a public parameterless
    /// constructor.
    ///
    /// Parts do not carry a class taxonomy analogous to planet's <c>BodyClassFlags</c>, so
    /// validators that only apply to certain module sets perform their own short-circuit checks
    /// inside <see cref="IValidator{T}.Validate" /> (typically by walking
    /// <see cref="PartValidationContext.Modules" /> for the relevant <c>ModuleData</c> subtype).
    /// </remarks>
    public interface IPartValidator : IValidator<PartValidationContext>
    {
    }
}
