using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;
using UnityEngine;
using VSwift.Modules.Behaviours;
using VSwift.Modules.Data;
using VSwift.Modules.Transformers;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Variants
{
    /// <summary>
    /// Errors when a <see cref="MaterialSwapper" /> source-material name isn't present on any Renderer in the part's prefab. The runtime swap loop silently skips unmatched entries, so the variant looks broken at runtime without an obvious cause.
    /// </summary>
    public sealed class TransformerMaterialMissingValidator : IPartValidator
    {
        private const string MATERIAL_INSTANCE_SUFFIX = " (Instance)";
        private const string MATERIAL_CLONE_SUFFIX = " (Clone)";

        /// <summary>Stable code emitted per unresolved source-material name.</summary>
        public const string Code = "TRANSFORMER_MATERIAL_MISSING";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            Data_PartSwitch data = VariantValidationHelper.FindData(context);
            if (data?.VariantSets == null || context.Prefab == null)
            {
                yield break;
            }
            Module_PartSwitch module = VariantValidationHelper.FindModule(context);

            var availableNames = new HashSet<string>();
            foreach (var renderer in context.Prefab.GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                if (renderer == null) continue;
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat == null || string.IsNullOrEmpty(mat.name)) continue;
                    availableNames.Add(StripRuntimeSuffixes(mat.name));
                }
            }

            foreach (var set in data.VariantSets)
            {
                if (set?.Variants == null) continue;
                foreach (var variant in set.Variants)
                {
                    if (variant?.Transformers == null) continue;
                    foreach (var transformer in variant.Transformers)
                    {
                        if (transformer is not MaterialSwapper ms || ms.Swaps == null)
                        {
                            continue;
                        }
                        foreach (var key in new List<string>(ms.Swaps.Keys))
                        {
                            if (string.IsNullOrEmpty(key))
                            {
                                continue;
                            }
                            if (availableNames.Contains(key))
                            {
                                continue;
                            }
                            string capturedKey = key;
                            MaterialSwapper capturedMs = ms;
                            var fix = new ValidationFix(
                                $"Remove '{capturedKey}' from Swaps",
                                () => VariantValidationHelper.RecordAndApply(module, "Remove material swap entry",
                                    () => capturedMs.Swaps.Remove(capturedKey)));
                            yield return new ValidationIssue(
                                Code,
                                ValidationSeverity.Error,
                                $"MaterialSwapper source '{key}' (variant '{variant.VariantId}' in set '{set.VariantSetId}') is not a material on any Renderer in the prefab.",
                                new[] { fix });
                        }
                    }
                }
            }
        }

        private static string StripRuntimeSuffixes(string name)
        {
            if (name.EndsWith(MATERIAL_INSTANCE_SUFFIX))
            {
                name = name.Substring(0, name.Length - MATERIAL_INSTANCE_SUFFIX.Length);
            }
            if (name.EndsWith(MATERIAL_CLONE_SUFFIX))
            {
                name = name.Substring(0, name.Length - MATERIAL_CLONE_SUFFIX.Length);
            }
            return name;
        }
    }
}
