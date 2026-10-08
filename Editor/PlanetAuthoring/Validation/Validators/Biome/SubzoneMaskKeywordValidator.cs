using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEngine;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Biome
{
    /// <summary>
    /// Warns when the subzone mask state is inconsistent: keyword on without a mask, or mask assigned with keyword off.
    /// </summary>
    /// <remarks>
    /// The surface shader gates subzone sampling behind the <c>SUB_ZONES_ENABLED</c> keyword. A mask without the
    /// keyword wastes import work and confuses the inspector. The keyword without a mask reads black and silently
    /// suppresses every subzone layer.
    /// </remarks>
    public sealed class SubzoneMaskKeywordValidator : IPlanetValidator
    {
        /// <summary>Stable code emitted when the mask is assigned but the keyword is off.</summary>
        public const string CodePresentButDisabled = "SUBZONE_MASK_PRESENT_BUT_DISABLED";

        /// <summary>Stable code emitted when the keyword is on but no mask is assigned.</summary>
        public const string CodeEnabledButMissing = "SUBZONE_MASK_ENABLED_BUT_MISSING";

        private const string Keyword = "SUB_ZONES_ENABLED";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null) yield break;
            PQS pqs = BodyResolver.FindPqsIncludingAsset(body);
            var info = pqs?.data?.heightMapInfo;
            Material mat = pqs?.data?.materialSettings?.surfaceMaterial;
            if (info == null || mat == null) yield break;

            bool keywordOn = mat.IsKeywordEnabled(Keyword);
            bool hasMask = info.subZoneMask != null;

            if (hasMask && !keywordOn)
            {
                Material captured = mat;
                yield return new ValidationIssue(
                    CodePresentButDisabled,
                    ValidationSeverity.Warning,
                    "Subzone mask assigned but SUB_ZONES_ENABLED keyword is off. Enable the keyword or unassign the mask.",
                    new[] { new ValidationFix("Enable keyword", () => SetKeyword(captured, true)) });
            }
            else if (!hasMask && keywordOn)
            {
                Material captured = mat;
                yield return new ValidationIssue(
                    CodeEnabledButMissing,
                    ValidationSeverity.Warning,
                    "SUB_ZONES_ENABLED keyword is on but no subzone mask is assigned. Subzone layers will read black and never appear.",
                    new[] { new ValidationFix("Disable keyword", () => SetKeyword(captured, false)) });
            }
        }

        private static void SetKeyword(Material mat, bool enabled)
        {
            Undo.RecordObject(mat, "Toggle SUB_ZONES_ENABLED");
            if (enabled) mat.EnableKeyword(Keyword);
            else mat.DisableKeyword(Keyword);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssetIfDirty(mat);
        }
    }
}
