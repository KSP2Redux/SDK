using System;
using System.Collections.Generic;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators;
using Ksp2UnityTools.Editor.Validation;
using Redux.Modules.Attributes;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Warns when the same transform name / path / group string is referenced by two or more
    /// distinct modules on the same part.
    /// </summary>
    /// <remarks>
    /// Most of the time this is intentional - a wheel transform is referenced by the suspension,
    /// motor, steering, and brakes modules of the same wheel assembly, for example. But the same
    /// pattern across modules with unrelated purposes (e.g. a docking port pointing at an engine
    /// thrust transform) usually signals authoring confusion. Warning, not Error, because there
    /// are legitimate reasons to share.
    /// </remarks>
    public sealed class PartTransformNameCollisionValidator : IPartValidator
    {
        /// <summary>Stable code emitted per colliding string.</summary>
        public const string Code = "PART_TRANSFORM_NAME_COLLISION";

        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            if (context?.Part == null)
            {
                yield break;
            }
            var modules = context.Modules;
            if (modules == null)
            {
                yield break;
            }

            var byValue = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            void Collect<T>() where T : Attribute
            {
                foreach (var fieldRef in ModuleFieldEnumerator.EnumerateStringFieldsWithAttribute<T>(modules))
                {
                    if (string.IsNullOrEmpty(fieldRef.Value))
                    {
                        continue;
                    }
                    if (!byValue.TryGetValue(fieldRef.Value, out var owners))
                    {
                        owners = new HashSet<string>(StringComparer.Ordinal);
                        byValue[fieldRef.Value] = owners;
                    }
                    owners.Add(fieldRef.Module.GetType().Name);
                }
            }

            Collect<TransformNameAttribute>();
            Collect<TransformPathAttribute>();
            Collect<TransformGroupAttribute>();

            foreach (var kv in byValue)
            {
                if (kv.Value.Count < 2)
                {
                    continue;
                }
                string moduleList = string.Join(", ", kv.Value);
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"Transform '{kv.Key}' is referenced by {kv.Value.Count} modules ({moduleList}). Sometimes intentional, sometimes a misdirected reference.");
            }
        }
    }
}
