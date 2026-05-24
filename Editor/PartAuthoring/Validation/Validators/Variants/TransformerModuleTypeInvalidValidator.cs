using System.Collections.Generic;
using System.Linq;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.Reflection;
using Ksp2UnityTools.Editor.Validation;
using VSwift.Modules.Data;
using VSwift.Modules.Transformers;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Variants
{
    /// <summary>
    /// Errors when a <see cref="ModuleDefinitionTransformer.BehaviourType" /> string doesn't resolve to a known <see cref="PartBehaviourModule" /> type.
    /// </summary>
    public sealed class TransformerModuleTypeInvalidValidator : IPartValidator
    {
        /// <summary>Stable code emitted per unresolved BehaviourType.</summary>
        public const string Code = "TRANSFORMER_MODULE_TYPE_INVALID";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            Data_PartSwitch data = VariantValidationHelper.FindData(context);
            if (data?.VariantSets == null)
            {
                yield break;
            }
            var known = new HashSet<string>(
                ReduxTypeCache.GetTypesDerivedFrom<PartBehaviourModule>().Select(t => t.Name));

            foreach (var set in data.VariantSets)
            {
                if (set?.Variants == null) continue;
                foreach (var variant in set.Variants)
                {
                    if (variant?.Transformers == null) continue;
                    foreach (var transformer in variant.Transformers)
                    {
                        if (transformer is not ModuleDefinitionTransformer mdt)
                        {
                            continue;
                        }
                        if (string.IsNullOrEmpty(mdt.BehaviourType))
                        {
                            yield return new ValidationIssue(
                                Code,
                                ValidationSeverity.Error,
                                $"ModuleDefinitionTransformer in variant '{variant.VariantId}' (set '{set.VariantSetId}') has no BehaviourType set.");
                            continue;
                        }
                        if (known.Contains(mdt.BehaviourType))
                        {
                            continue;
                        }
                        yield return new ValidationIssue(
                            Code,
                            ValidationSeverity.Error,
                            $"ModuleDefinitionTransformer BehaviourType '{mdt.BehaviourType}' (variant '{variant.VariantId}' in set '{set.VariantSetId}') is not a known PartBehaviourModule type.");
                    }
                }
            }
        }
    }
}
