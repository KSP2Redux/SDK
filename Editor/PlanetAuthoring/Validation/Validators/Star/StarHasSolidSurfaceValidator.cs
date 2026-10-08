using System.Collections.Generic;
using KSP;
using UnityEditor;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Star
{
    /// <summary>
    /// Errors when a body has both <c>Is Star</c> and <c>Has Solid Surface</c> set.
    /// </summary>
    /// <remarks>
    /// Stars are scaled-space-only and never have a PQS-driven surface. The combination is contradictory and the runtime classifier will pick one or the other arbitrarily.
    /// </remarks>
    public sealed class StarHasSolidSurfaceValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "STAR_HAS_SOLID_SURFACE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null)
                yield break;
            var data = body.Core.data;
            if (!data.isStar || !data.hasSolidSurface)
                yield break;

            var fixes = new[]
            {
                new ValidationFix("Clear Has Solid Surface", () => ClearHasSolidSurface(body)),
                new ValidationFix("Clear Is Star", () => ClearIsStar(body)),
            };
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                "Body is flagged Is Star and Has Solid Surface. Stars do not have a solid surface.",
                fixes);
        }

        private static void ClearHasSolidSurface(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null)
                return;
            Undo.RecordObject(body, "Clear Has Solid Surface");
            body.Core.data.hasSolidSurface = false;
            EditorUtility.SetDirty(body);
        }

        private static void ClearIsStar(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null)
                return;
            Undo.RecordObject(body, "Clear Is Star");
            body.Core.data.isStar = false;
            EditorUtility.SetDirty(body);
        }
    }
}
