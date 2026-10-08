using System.Collections.Generic;
using System.Reflection;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Warns when the part has no generated drag cubes.
    /// </summary>
    /// <remarks>
    /// Drag cubes are stored on <see cref="Module_Drag" /> in <see cref="Data_Drag" />. Missing
    /// cubes leave the part without the authored aerodynamic shape data expected by the runtime.
    /// </remarks>
    public sealed class PartDragCubesMissingValidator : IPartValidator
    {
        /// <summary>Stable code emitted when a part has no drag cubes.</summary>
        public const string Code = "PART_DRAG_CUBES_MISSING";

        private static readonly FieldInfo ModuleDragDataField =
            typeof(Module_Drag).GetField("dataDrag", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            if (context?.Part == null)
            {
                yield break;
            }

            Module_Drag module = context.Part.GetComponent<Module_Drag>();
            if (module == null)
            {
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    "Part has no Module_Drag, so no drag cubes are set up. Generate drag cubes before exporting.");
                yield break;
            }

            if (CountDragCubes(module) > 0)
            {
                yield break;
            }

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                "Part has no drag cubes set up. Generate drag cubes before exporting.");
        }

        private static int CountDragCubes(Module_Drag module)
        {
            if (module.DataModules.TryGetByType(out Data_Drag dataDrag) && dataDrag?.cubes != null)
            {
                return dataDrag.cubes.Count;
            }

            return ModuleDragDataField?.GetValue(module) is Data_Drag reflectedData && reflectedData.cubes != null
                ? reflectedData.cubes.Count
                : 0;
        }
    }
}
