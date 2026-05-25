using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.LiftingSurface
{
    /// <summary>
    /// Errors when <c>nodeEnabled == true</c> but <c>attachNodeName</c> is empty.
    /// </summary>
    /// <remarks>
    /// The node gate is enabled but no node is named to check. The runtime either never fires the
    /// gate (lift always blocked) or always fires it (gate is no-op), depending on how empty
    /// strings flow through the comparison - either way the author's intent is broken.
    /// </remarks>
    public sealed class LiftSurfaceNodeNameEmptyValidator : IPartValidator
    {
        /// <summary>Stable code emitted when the node gate is on but unnamed.</summary>
        public const string Code = "LIFT_SURFACE_NODE_NAME_EMPTY";

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
                if (module is not Data_LiftingSurface lift)
                {
                    continue;
                }
                if (!lift.nodeEnabled || !string.IsNullOrEmpty(lift.attachNodeName))
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    "nodeEnabled is on but attachNodeName is empty. The lift node-gate has no node to check.");
            }
        }
    }
}
