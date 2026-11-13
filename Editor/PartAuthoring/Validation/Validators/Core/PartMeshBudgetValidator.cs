using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Part triangle budget and authored LOD wiring review checks.
    /// </summary>
    public class PartMeshBudgetValidator : IPartValidator
    {
        // Review limits allow detailed engines and deployed mechanisms without treating them as broken.
        private const long TINY_TRIANGLES = 50000;
        private const long SMALL_TRIANGLES = 100000;
        private const long MEDIUM_TRIANGLES = 200000;
        private const long LARGE_TRIANGLES = 400000;

        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            if (context?.Prefab == null || context.Data == null)
                yield break;

            var lowerLods = new HashSet<Renderer>();
            foreach (LODGroup group in context.Prefab.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = group.GetLODs();
                for (int index = 1; index < lods.Length; index++)
                {
                    foreach (Renderer renderer in lods[index].renderers)
                    {
                        if (renderer != null)
                            lowerLods.Add(renderer);
                    }
                }
                // A renderer reused at LOD0 still contributes at maximum detail.
                if (lods.Length > 0)
                {
                    foreach (Renderer renderer in lods[0].renderers)
                    {
                        lowerLods.Remove(renderer);
                    }
                }
            }
            long triangles = 0;
            bool hasUnwiredLod = false;
            foreach (Renderer renderer in context.Prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (lowerLods.Contains(renderer))
                    continue;
                Mesh mesh = null;
                if (renderer is SkinnedMeshRenderer skinned)
                    mesh = skinned.sharedMesh;
                else if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter filter))
                    mesh = filter.sharedMesh;
                if (mesh == null)
                    continue;
                // Explicit LOD mesh names express author intent. Ordinary low-poly parts need no group.
                string name = renderer.name.ToUpperInvariant();
                hasUnwiredLod |= (name.EndsWith("_LOD1") || name.EndsWith("_LOD2") || name.EndsWith("_LOD3")) &&
                    renderer.GetComponentInParent<LODGroup>() == null;
                for (int index = 0; index < mesh.subMeshCount; index++)
                {
                    if (mesh.GetTopology(index) == MeshTopology.Triangles)
                        triangles += mesh.GetIndexCount(index) / 3;
                }
            }
            if (hasUnwiredLod)
                yield return new ValidationIssue("PART_LOD_GROUP_MISSING", ValidationSeverity.Warning,
                    "A renderer named _LOD1, _LOD2 or _LOD3 has no ancestor LODGroup. Check the imported LOD wiring.");

            string size = PartTextureSizeValidator.SizeClass(context);
            long limit = size switch
            {
                "XXS" or "XS" or "XSPLUS" => TINY_TRIANGLES,
                "S" or "SPLUS" => SMALL_TRIANGLES,
                "M" or "MPLUS" => MEDIUM_TRIANGLES,
                "L" or "LPLUS" or "XL" or "XLPLUS" or "XXL" or "XXXL" or "XXXXL" or "XXXXXL" or "XXXXXXL" => LARGE_TRIANGLES,
                _ => 0
            };
            if (limit > 0 && triangles > limit)
                yield return new ValidationIssue("PART_TRIANGLE_OUTLIER", ValidationSeverity.Warning,
                    $"Size {size} has {triangles:N0} triangles across renderer instances at LOD0 (review limit {limit:N0}). Inactive variants are included.");
        }
    }
}
