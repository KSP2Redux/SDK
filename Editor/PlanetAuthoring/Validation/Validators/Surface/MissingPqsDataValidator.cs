using System.Collections.Generic;
using System.IO;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.IO;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Surface
{
    /// <summary>
    /// Errors when a solid-surface body has a PQS in its hierarchy but no <c>PQSData</c> asset assigned.
    /// </summary>
    /// <remarks>
    /// Without PQSData the heightmap stack, biome mask, and surface material are all missing, and the renderer
    /// NREs during boot. The fix mirrors the readiness-panel one-click action, creating an empty PQSData asset
    /// next to the body prefab and binding it to the PQS.
    /// </remarks>
    public sealed class MissingPqsDataValidator : IPlanetValidator
    {
        /// <summary>
        /// Stable code identifying issues emitted by this validator.
        /// </summary>
        public const string Code = "MISSING_PQS_DATA";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null) yield break;
            PQS pqs = BodyResolver.FindPqsIncludingAsset(body);
            if (pqs == null) yield break;
            if (pqs.data != null) yield break;

            CoreCelestialBodyData captured = body;
            PQS capturedPqs = pqs;
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                $"PQS on '{body.Core.data.bodyName}' has no PQSData assigned. Click the fix to create an empty one next to the prefab.",
                new[] { new ValidationFix("Create empty PQSData", () => CreateEmptyPqsData(captured, capturedPqs)) });
        }

        private static void CreateEmptyPqsData(CoreCelestialBodyData body, PQS pqs)
        {
            string bodyName = body.Core?.data?.bodyName;
            if (string.IsNullOrEmpty(bodyName))
                bodyName = body.gameObject != null ? body.gameObject.name : "Body";

            string prefabPath = PathUtils.GetPrefabOrAssetPath(body, body.gameObject);
            string dir = !string.IsNullOrEmpty(prefabPath) ? Path.GetDirectoryName(prefabPath) : "Assets";
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(dir + "/" + bodyName + "_PQSData.asset");

            var data = ScriptableObject.CreateInstance<PQSData>();
            AssetDatabase.CreateAsset(data, assetPath);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(pqs, "Assign PQSData");
            pqs.data = data;
            EditorUtility.SetDirty(pqs);
        }
    }
}
