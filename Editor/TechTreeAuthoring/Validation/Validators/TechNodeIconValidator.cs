using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Warns on a node with no icon, or one whose icon address no tech node icon in the project has.
    /// </summary>
    public class TechNodeIconValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for a node with no icon.</summary>
        public const string ICON_EMPTY_CODE = "TECHNODE_ICON_EMPTY";

        /// <summary>Stable code emitted for an icon address the project does not know.</summary>
        public const string ICON_UNKNOWN_CODE = "TECHNODE_ICON_UNKNOWN";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            string icon = context.Node.Data.IconID;
            if (string.IsNullOrEmpty(icon))
            {
                yield return new ValidationIssue(ICON_EMPTY_CODE, ValidationSeverity.Warning, $"{context.Name} has no icon.");
                yield break;
            }

            HashSet<string> known = context.Tree.KnownIcons;
            if (known != null && !known.Contains(icon))
            {
                yield return new ValidationIssue(
                    ICON_UNKNOWN_CODE,
                    ValidationSeverity.Warning,
                    $"{context.Name} uses the icon {icon}, which no tech node icon in the project has."
                );
            }
        }
    }
}
