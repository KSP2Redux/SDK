using System.Collections.Generic;
using KSP;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Decouple
{
    /// <summary>
    /// Errors when <c>ejectionForce &lt; 0</c>.
    /// </summary>
    /// <remarks>
    /// Module_Decouple writes <c>ejectionForce</c> straight into <c>EjectionImpulse</c> and applies
    /// it as a separation impulse along the node normal. A negative value reverses the impulse
    /// direction, pushing parts together instead of apart. The Negate fix flips the sign.
    /// </remarks>
    public sealed class DecoupleEjectionForceNegativeValidator : IPartValidator
    {
        /// <summary>Stable code emitted when ejection force is negative.</summary>
        public const string Code = "DECOUPLE_EJECTION_FORCE_NEGATIVE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            CorePartData target = context?.Part;
            var modules = context?.ModuleDatas;
            if (modules == null || target == null)
            {
                yield break;
            }
            foreach (var module in modules)
            {
                if (module is not Data_Decouple decouple)
                {
                    continue;
                }
                if (decouple.ejectionForce >= 0f)
                {
                    continue;
                }
                Data_Decouple captured = decouple;
                var fix = new ValidationFix(
                    $"Negate ejectionForce -> {-decouple.ejectionForce:0.###}",
                    () =>
                    {
                        Undo.RecordObject(target, "Negate decoupler ejection force");
                        captured.ejectionForce = -captured.ejectionForce;
                        EditorUtility.SetDirty(target);
                    });
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"Decoupler ejectionForce is {decouple.ejectionForce:0.###}. Negative values push parts together instead of apart.",
                    new[] { fix });
            }
        }
    }
}
