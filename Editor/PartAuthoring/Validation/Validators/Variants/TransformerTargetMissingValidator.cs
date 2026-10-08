using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;
using VSwift.Modules.Behaviours;
using VSwift.Modules.Data;
using VSwift.Modules.Transformers;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Variants
{
    /// <summary>
    /// Errors when a <see cref="TransformActivator" /> entry's GameObject path doesn't resolve against the prefab hierarchy. The runtime silently skips unresolved entries so a typo just makes the transformer a no-op.
    /// </summary>
    /// <remarks>
    /// Lookup uses the same name-keyed flattened transform table the rest of the validation pipeline uses (<see cref="PartValidationContext.TransformByName" />). Path matching is by the final segment - the runtime's <c>Transform.Find</c>-style lookup may be more permissive, but a missing name is unambiguously broken.
    /// </remarks>
    public sealed class TransformerTargetMissingValidator : IPartValidator
    {
        /// <summary>Stable code emitted per unresolved path.</summary>
        public const string Code = "TRANSFORMER_TARGET_MISSING";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            Data_PartSwitch data = VariantValidationHelper.FindData(context);
            if (data?.VariantSets == null || context.Prefab == null)
            {
                yield break;
            }
            Module_PartSwitch module = VariantValidationHelper.FindModule(context);
            var nameLookup = context.TransformByName;

            foreach (var set in data.VariantSets)
            {
                if (set?.Variants == null) continue;
                foreach (var variant in set.Variants)
                {
                    if (variant?.Transformers == null) continue;
                    foreach (var transformer in variant.Transformers)
                    {
                        if (transformer is not TransformActivator ta || ta.Transforms == null)
                        {
                            continue;
                        }
                        for (int i = 0; i < ta.Transforms.Count; i++)
                        {
                            string path = ta.Transforms[i];
                            if (string.IsNullOrEmpty(path))
                            {
                                continue;
                            }
                            if (Resolves(context.Prefab.transform, path) || nameLookup.ContainsKey(GetLastSegment(path)))
                            {
                                continue;
                            }
                            int capturedIndex = i;
                            string capturedPath = path;
                            TransformActivator capturedTa = ta;
                            var fix = new ValidationFix(
                                $"Remove '{capturedPath}' from Transforms",
                                () => VariantValidationHelper.RecordAndApply(module, "Remove TransformActivator path", () =>
                                {
                                    if (capturedIndex < 0 || capturedIndex >= capturedTa.Transforms.Count) return;
                                    if (capturedTa.Transforms[capturedIndex] != capturedPath) return;
                                    capturedTa.Transforms.RemoveAt(capturedIndex);
                                }));
                            yield return new ValidationIssue(
                                Code,
                                ValidationSeverity.Error,
                                $"TransformActivator path '{path}' (variant '{variant.VariantId}' in set '{set.VariantSetId}') does not resolve against the prefab hierarchy.",
                                new[] { fix });
                        }
                    }
                }
            }
        }

        private static bool Resolves(UnityEngine.Transform root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path))
            {
                return false;
            }
            return root.Find(path) != null;
        }

        private static string GetLastSegment(string path)
        {
            int slash = path.LastIndexOf('/');
            return slash < 0 ? path : path.Substring(slash + 1);
        }
    }
}
