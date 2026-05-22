using KSP;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation
{
    /// <summary>
    /// Contract for a single planet-authoring validator.
    /// </summary>
    /// <remarks>
    /// Specialization of <see cref="IValidator{T}" /> for <see cref="CoreCelestialBodyData" />. The
    /// generic interface supplies <see cref="IValidator{T}.Cost" /> and
    /// <see cref="IValidator{T}.Validate" />. This sub-interface adds the body-class filter the
    /// planet runner uses to skip validators that do not apply to a given body's class.
    /// Implementations are auto-discovered by <see cref="PlanetValidatorRegistry" />. Concrete types
    /// must have a public parameterless constructor.
    /// </remarks>
    public interface IPlanetValidator : IValidator<CoreCelestialBodyData>
    {
        /// <summary>
        /// Body classes the validator applies to.
        /// </summary>
        /// <remarks>
        /// The runner classifies each body via <see cref="BodyClassClassifier" /> and skips validators
        /// whose mask does not include the body's class. Defaults to <see cref="BodyClassFlags.All" />
        /// for validators that work on any body. PQS / biome / decal / science-region validators
        /// should narrow to <see cref="BodyClassFlags.SolidSurface" />. Star-specific checks should narrow
        /// to <see cref="BodyClassFlags.Star" />, and so on.
        /// </remarks>
        BodyClassFlags AppliesTo => BodyClassFlags.All;
    }
}
