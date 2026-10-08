using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Converter
{
    /// <summary>
    /// Errors when a Data_ResourceConverter has no entries in <c>FormulaDefinitions</c>.
    /// </summary>
    /// <remarks>
    /// PartComponentModule_ResourceConverter iterates FormulaDefinitions to enumerate the
    /// conversions the part performs. An empty list means the converter sits on the part with
    /// nothing to do - no inputs requested, no outputs produced.
    /// </remarks>
    public sealed class ConverterNoFormulasValidator : IPartValidator
    {
        /// <summary>Stable code emitted when the converter has no formulas.</summary>
        public const string Code = "CONVERTER_NO_FORMULAS";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            var modules = context?.ModuleDatas;
            if (modules == null)
            {
                yield break;
            }
            foreach (var module in modules)
            {
                if (module is not Data_ResourceConverter converter)
                {
                    continue;
                }
                if (converter.FormulaDefinitions != null && converter.FormulaDefinitions.Count > 0)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    "ResourceConverter has no FormulaDefinitions. The module sits idle - add at least one conversion formula.");
            }
        }
    }
}
