using System.Collections.Generic;
using KSP.OAB;
using Ksp2UnityTools.Editor.Validation;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Conservative texture resolution review thresholds by authored part size class.
    /// </summary>
    public class PartTextureSizeValidator : IPartValidator
    {
        // Tiny parts may share 2K atlases. Only 4K and above warrants review.
        private const int TINY_MAX_DIMENSION = 2048;
        // Only very low resolution on every albedo of a large part warrants review.
        private const int LARGE_MIN_DIMENSION = 512;

        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            if (context?.Prefab == null || context.Data == null)
                yield break;

            string size = SizeClass(context);
            bool isTiny = size is "XS" or "XXS" or "XSPLUS";
            bool isLarge = size is "L" or "LPLUS" or "XL" or "XLPLUS" or "XXL" or "XXXL" or "XXXXL" or "XXXXXL" or "XXXXXXL";
            if (!isTiny && !isLarge)
                yield break;

            int largest = 0;
            var seen = new HashSet<Texture>();
            foreach (Material material in PartMaterialValidator.Materials(context))
            {
                foreach (string property in material.GetTexturePropertyNames())
                {
                    if (!material.HasProperty(property))
                        continue;
                    Texture texture = material.GetTexture(property);
                    if (texture == null)
                        continue;
                    int dimension = Mathf.Max(texture.width, texture.height);
                    if (property is "_MainTex" or "_BaseMap")
                        largest = Mathf.Max(largest, dimension);
                    if (!seen.Add(texture))
                        continue;
                    if (isTiny && dimension > TINY_MAX_DIMENSION)
                        yield return new ValidationIssue("PART_TEXTURE_SIZE_HIGH", ValidationSeverity.Warning,
                            $"Size {size} uses '{texture.name}' at {texture.width}x{texture.height}, above {TINY_MAX_DIMENSION}. Review atlas sharing and UV coverage before reducing it.");
                }
            }
            if (isLarge && largest > 0 && largest < LARGE_MIN_DIMENSION)
                yield return new ValidationIssue("PART_TEXTURE_SIZE_LOW", ValidationSeverity.Warning,
                    $"Size {size} has no albedo at least {LARGE_MIN_DIMENSION} pixels wide or high (largest {largest}). Review tiling and intended detail.");
        }

        internal static string SizeClass(PartValidationContext context)
        {
            return PartSizeRegistry.GetPartSizeKey(context.Data) switch
            {
                "XXS" => "XXS",
                "XS" => "XS",
                "XS+" => "XSPLUS",
                "SM" => "S",
                "SM+" => "SPLUS",
                "MD" => "M",
                "MD+" => "MPLUS",
                "LG" => "L",
                "LG+" => "LPLUS",
                "XL" => "XL",
                "XL+" => "XLPLUS",
                "2XL" or "XXL" => "XXL",
                "3XL" => "XXXL",
                "4XL" => "XXXXL",
                "5XL" => "XXXXXL",
                "6XL" => "XXXXXXL",
                _ => null
            };
        }
    }
}
