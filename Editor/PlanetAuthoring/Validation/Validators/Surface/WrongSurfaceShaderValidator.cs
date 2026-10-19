using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEngine;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Surface
{
    /// <summary>
    /// Warns when the surface material does not use the expected planet local shader, and reports one whose shader
    /// does not compile.
    /// </summary>
    /// <remarks>
    /// Custom shaders won't bind the heightmap structured buffers PQSRenderer pushes each frame, so the surface
    /// will render with stale or missing per-layer data. The fix re-assigns <see cref="PlanetAuthoringShaders.Local" />.
    /// A shader with compile errors keeps its name and properties, but its material reads back no texture or value
    /// written to it, so every surface field in the inspector reverts as soon as the inspector rebuilds.
    /// </remarks>
    public class WrongSurfaceShaderValidator : IPlanetValidator
    {
        /// <summary>
        /// Code for a surface material on another shader than <see cref="PlanetAuthoringShaders.Local" />.
        /// </summary>
        public const string WRONG_SHADER_CODE = "WRONG_SHADER";

        /// <summary>
        /// Code for a surface material whose shader has compile errors.
        /// </summary>
        public const string SHADER_ERRORS_CODE = "SURFACE_SHADER_ERRORS";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null)
                return new List<ValidationIssue>();

            PQS pqs = BodyResolver.FindPqsIncludingAsset(body);
            return Check(PlanetAuthoringSession.AuthoredData(pqs)?.materialSettings?.surfaceMaterial);
        }

        /// <summary>
        /// Checks the shader of a surface material.
        /// </summary>
        /// <param name="material">The surface material, or null.</param>
        /// <returns>The issues found.</returns>
        public static IEnumerable<ValidationIssue> Check(Material material)
        {
            if (material == null || material.shader == null)
                yield break;

            if (material.shader.name != PlanetAuthoringShaders.Local)
            {
                Material captured = material;
                yield return new ValidationIssue(
                    WRONG_SHADER_CODE,
                    ValidationSeverity.Warning,
                    $"Surface material '{material.name}' uses shader '{material.shader.name}' instead of '{PlanetAuthoringShaders.Local}'. The body will not render correctly until the expected shader is assigned.",
                    new[] { new ValidationFix("Assign Redux local shader", () => AssignExpectedShader(captured)) });
            }

            if (ShaderUtil.ShaderHasError(material.shader))
            {
                yield return new ValidationIssue(
                    SHADER_ERRORS_CODE,
                    ValidationSeverity.Error,
                    $"Surface material '{material.name}' uses '{material.shader.name}', which has compile errors, so the material keeps none of the textures and values written to it and the body does not render. Select the shader to see its errors, and update the Redux SDK package if they name a missing include.");
            }
        }

        private static void AssignExpectedShader(Material mat)
        {
            Shader shader = Shader.Find(PlanetAuthoringShaders.Local);
            if (shader == null) return;
            Undo.RecordObject(mat, "Assign Redux local shader");
            mat.shader = shader;
            EditorUtility.SetDirty(mat);
        }
    }
}
