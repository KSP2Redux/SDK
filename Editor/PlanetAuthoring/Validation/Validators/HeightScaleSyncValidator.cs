using System;
using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators
{
    /// <summary>
    /// Warns when <c>CelestialBodyData.TerrainHeightScale</c> diverges from <c>PQSData.heightMapInfo.heightMapScale</c>.
    /// </summary>
    /// <remarks>
    /// The renderer reads <c>heightMapScale</c>. <c>TerrainHeightScale</c> on CelestialBodyData is a data copy only. Mismatch is informational, not load-bearing, but should stay in sync to avoid confusion downstream.
    /// </remarks>
    public sealed class HeightScaleSyncValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "HEIGHT_SCALE_MISMATCH";

        private const double Threshold = 0.0001;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null)
                yield break;

            var pqs = body.GetComponentInChildren<PQS>(true);
            if (pqs == null || pqs.data == null || pqs.data.heightMapInfo == null)
                yield break;
            if (body.Core?.data == null)
                yield break;

            float pqsScale = pqs.data.heightMapInfo.heightMapScale;
            double bodyScale = body.Core.data.TerrainHeightScale;
            if (Math.Abs((double)pqsScale - bodyScale) <= Threshold)
                yield break;

            string message = $"Terrain Height Scale on CelestialBodyData ({bodyScale:0.####}) does not match heightMapScale on PQSData ({pqsScale:0.####}). The renderer reads heightMapScale. CelestialBodyData.TerrainHeightScale is a data copy only.";

            PQSData pqsData = pqs.data;
            var fixes = new[]
            {
                new ValidationFix("Copy PQSData → body", () => SyncBodyToPqsData(body, pqsData)),
                new ValidationFix("Copy body → PQSData", () => SyncPqsDataToBody(body, pqsData)),
            };

            yield return new ValidationIssue(Code, ValidationSeverity.Warning, message, fixes);
        }

        private static void SyncBodyToPqsData(CoreCelestialBodyData body, PQSData pqsData)
        {
            if (body == null || body.Core?.data == null || pqsData == null)
                return;
            Undo.RecordObject(body, "Sync TerrainHeightScale");
            body.Core.data.TerrainHeightScale = pqsData.heightMapInfo.heightMapScale;
            EditorUtility.SetDirty(body);
        }

        private static void SyncPqsDataToBody(CoreCelestialBodyData body, PQSData pqsData)
        {
            if (body == null || body.Core?.data == null || pqsData == null)
                return;
            Undo.RecordObject(pqsData, "Sync heightMapScale");
            pqsData.heightMapInfo.heightMapScale = (float)body.Core.data.TerrainHeightScale;
            EditorUtility.SetDirty(pqsData);
            AssetDatabase.SaveAssetIfDirty(pqsData);
        }
    }
}
