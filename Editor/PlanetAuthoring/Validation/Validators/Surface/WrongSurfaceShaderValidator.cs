using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Surface
{
    /// <summary>
    /// Warns when the surface material does not use the expected planet local shader.
    /// </summary>
    /// <remarks>
    /// Custom shaders won't bind the heightmap structured buffers PQSRenderer pushes each frame, so the surface
    /// will render with stale or missing per-layer data. The fix re-assigns <see cref="PlanetAuthoringShaders.Local" />.
    /// </remarks>
    public sealed class WrongSurfaceShaderValidator : IPlanetValidator
    {
        /// <summary>
        /// Stable code identifying issues emitted by this validator.
        /// </summary>
        public const string Code = "WRONG_SHADER";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null) yield break;
            PQS pqs = BodyResolver.FindPqsIncludingAsset(body);
            Material mat = pqs?.data?.materialSettings?.surfaceMaterial;
            if (mat == null || mat.shader == null) yield break;
            if (mat.shader.name == PlanetAuthoringShaders.Local) yield break;

            Material captured = mat;
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                $"Surface material '{mat.name}' uses shader '{mat.shader.name}' instead of '{PlanetAuthoringShaders.Local}'. The body will not render correctly until the expected shader is assigned.",
                new[] { new ValidationFix("Assign Redux local shader", () => AssignExpectedShader(captured)) });
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
