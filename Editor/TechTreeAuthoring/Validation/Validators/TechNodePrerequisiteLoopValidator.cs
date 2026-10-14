using System;
using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Errors when a node ends up requiring itself through its prerequisites.
    /// </summary>
    /// <remarks>
    /// No node in a loop can ever be unlocked. Every node in the loop is reported.
    /// </remarks>
    public class TechNodePrerequisiteLoopValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for a node in a prerequisite loop.</summary>
        public const string Code = "TECHNODE_PREREQUISITE_LOOP";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            bool isInLoop = (context.Node.Data.RequiredTechNodeIDs ?? Array.Empty<string>())
                .Any(prerequisiteId => TechTreeEditing.WouldCreateLoop(context.Tree.Tree, context.Id, prerequisiteId, context.Tree.Reference));
            if (!isInLoop)
                yield break;

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                $"{context.Name} ends up requiring itself, so no node in that loop can ever be unlocked."
            );
        }
    }
}
