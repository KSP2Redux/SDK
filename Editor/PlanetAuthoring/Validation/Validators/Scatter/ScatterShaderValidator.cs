using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using Ksp2UnityTools.Editor.Validation;
using Redux.CelestialBody;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Errors when a mesh item's prefab uses a shader the scatter renderer cannot drive.
    /// </summary>
    /// <remarks>
    /// <c>ShaderSelector.GetShaderControler</c> falls back to a different controller for an
    /// unrecognised shader. The item still spawns and still draws, so nothing looks broken, but every
    /// appearance setting in the inspector stops reaching the material.
    ///
    /// The supported set is the two opaque scatter shaders.
    /// <c>Scatter_Instanced_Indirect_Transparent</c> does not resolve from the asset catalog, so a
    /// prefab authored against it ends up with a null shader.
    ///
    /// Names are compared after mapping the project stand-in prefix back to the base game one, since
    /// a prefab in this repo legitimately carries the Redux name and is swapped at load.
    /// </remarks>
    public sealed class ScatterShaderValidator : IPlanetValidator
    {
        /// <summary>
        /// Stable code identifying issues emitted by this validator.
        /// </summary>
        public const string Code = "SCATTER_WRONG_SHADER";

        private static readonly string[] Supported =
        {
            "KSP2/Environment/Scatter/Scatter_Instanced_Indirect_Opaque",
            "KSP2/Environment/Scatter/Scatter_Instanced_Indirect_Opaque_Specular",
        };

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            foreach ((VegetationPackagePro package, VegetationItemInfoPro item) in ScatterValidatorHelper.Items(body))
            {
                if (item.PrefabType != VegetationPrefabType.Mesh || item.VegetationPrefab == null)
                    continue;

                // Gathered across the whole prefab before reporting, so several renderers sharing one
                // wrong shader come out as a single issue with a single fix.
                var offending = new SortedSet<string>();
                foreach (Renderer renderer in item.VegetationPrefab.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material == null || material.shader == null || IsSupported(material.shader.name))
                            continue;

                        offending.Add(material.shader.name);
                    }
                }

                if (offending.Count == 0)
                    continue;

                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"Item '{item.Name}' in package '{ScatterValidatorHelper.PackageLabel(package)}' uses "
                    + $"{string.Join(", ", offending)}. Scatter appearance settings only reach the two "
                    + "supported Scatter_Instanced_Indirect shaders, so they will silently do nothing.");
            }
        }

        private static bool IsSupported(string shaderName)
        {
            string resolved = ScatterShaderMapping.ResolveBaseGameShaderName(shaderName);
            foreach (string supported in Supported)
            {
                if (resolved == supported)
                    return true;
            }

            return false;
        }
    }
}
