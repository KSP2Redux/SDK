using System;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    public static partial class SurfaceAuthoringBuilder
    {
        // 13x13 vertex grid per leaf quad → 12 triangles per leaf edge. Mirrors PQSGeometry.cacheSideVertCount.
        private static int LeafTrianglesPerEdge => PQSGeometry.cacheSideVertCount - 1;

        /// <summary>
        /// Builds the Subdivision foldout exposing the per-body LOD cap with live zone-radius and triangle-size readouts.
        /// </summary>
        /// <remarks>
        /// Gives artists live trade-off feedback as they tune the per-body Max Level Override.
        /// Zone-radius readout is color-tiered against the <c>SubdivisionDetailZoneTooSmall</c>
        /// validator's 200 m floor: warn under 200 m, error under 50 m. Triangle-size readout
        /// has no equivalent grounded threshold and stays uncolored.
        /// </remarks>
        /// <param name="pqs">The PQS whose body radius drives the readouts.</param>
        /// <param name="pqsDataSO">SerializedObject wrapping the bound PQSData.</param>
        /// <returns>The populated Subdivision foldout.</returns>
        public static Foldout BuildSubdivisionSection(PQS pqs, SerializedObject pqsDataSO)
        {
            var foldout = new Foldout { text = "Subdivision", value = true };
            foldout.AddToClassList("pqs-inspector-section");

            var overrideField = BindPropertyField(
                pqsDataSO,
                "subdivisionMaxLevelOverride",
                "Max Level Override",
                "Per-body cap on PQS quad-tree depth. 0 uses the global PQSGlobalSettings value. Lower this on small bodies so leaf quads stay sensibly sized and the camera gets a max-detail zone wider than a meter."
            );
            foldout.Add(overrideField);

            var effectiveLabel = new Label { tooltip = "Quad-tree depth used after the per-body override is applied." };
            var zoneLabel = new Label { tooltip = "Radius around the camera where every quad has reached max subdivision." };
            var triangleLabel = new Label { tooltip = "Approximate visible triangle edge length at max subdivision. 12 triangles per leaf-quad edge." };
            effectiveLabel.AddToClassList("pqs-inspector-readout");
            zoneLabel.AddToClassList("pqs-inspector-readout");
            triangleLabel.AddToClassList("pqs-inspector-readout");
            foldout.Add(effectiveLabel);
            foldout.Add(zoneLabel);
            foldout.Add(triangleLabel);

            void Refresh() => UpdateReadouts(pqs, pqsDataSO, effectiveLabel, zoneLabel, triangleLabel);
            Refresh();

            // Re-run when the override field changes so the artist sees the trade live.
            var prop = pqsDataSO?.FindProperty("subdivisionMaxLevelOverride");
            if (prop != null)
                overrideField.TrackPropertyValue(prop, _ => Refresh());

            return foldout;
        }

        private static void UpdateReadouts(PQS pqs, SerializedObject pqsDataSO, Label effectiveLabel, Label zoneLabel, Label triangleLabel)
        {
            // pqs.CoreCelestialBodyData isn't wired at edit time (sibling-scene-root layout).
            // BodyResolver walks the scene to find the body root.
            CoreCelestialBodyData body = pqs != null ? BodyResolver.FindBodyIncludingAsset(pqs) : null;
            double radius = body?.Core?.data?.radius ?? 0.0;
            PQSGlobalSettings.SubdivData sd = ResolveGlobalSubdivData(pqs);

            int overrideValue = pqsDataSO?.FindProperty("subdivisionMaxLevelOverride")?.intValue ?? 0;

            zoneLabel.RemoveFromClassList("pqs-inspector-readout--warn");
            zoneLabel.RemoveFromClassList("pqs-inspector-readout--error");

            if (sd == null)
            {
                effectiveLabel.text = "Effective max level: - (global PQSGlobalSettings unavailable)";
                zoneLabel.text = "Zone radius: -";
                triangleLabel.text = "Leaf triangle size: -";
                return;
            }

            int effective = overrideValue > 0
                ? Math.Clamp(overrideValue, sd.minLevel + 1, sd.maxLevel)
                : sd.maxLevel;
            string source = overrideValue > 0 ? "override" : "global";
            effectiveLabel.text = $"Effective max level: {effective} ({source})";

            if (radius <= 0)
            {
                zoneLabel.text = "Zone radius: (set body radius to compute)";
                triangleLabel.text = "Leaf triangle size: (set body radius to compute)";
                return;
            }

            double pow = Math.Pow(2, effective - 1);
            double zone = radius * sd.minDetailMultiplier * sd.subdivisionThreshold / pow;
            double leafEdge = radius * Math.PI / 2.0 / Math.Pow(2, effective);
            double triangle = leafEdge / LeafTrianglesPerEdge;

            zoneLabel.text = $"Zone radius: {FormatMeters(zone)}";
            triangleLabel.text = $"Leaf triangle size: ~{FormatMeters(triangle)}";

            // Zone color tiers: 200 m matches the SubdivisionDetailZoneTooSmall validator's
            // visual + collision floor. Below 50 m the sharp-ring artifact and physics step are
            // far worse than at the 200 m boundary, so promote to error rather than warn.
            // Triangle size has no equivalent grounded threshold and is left uncolored.
            if (zone < 50) zoneLabel.AddToClassList("pqs-inspector-readout--error");
            else if (zone < 200) zoneLabel.AddToClassList("pqs-inspector-readout--warn");
        }

        private static PQSGlobalSettings.SubdivData ResolveGlobalSubdivData(PQS pqs)
        {
            if (pqs?.settings != null) return pqs.settings.subdivisionInfo.subdivData;
            return EditorPqsBootstrap.PQSGlobalSettings?.subdivisionInfo.subdivData;
        }

        private static string FormatMeters(double m)
        {
            if (m >= 1000) return $"{m / 1000:0.##} km";
            if (m >= 1) return $"{m:0.##} m";
            if (m >= 0.01) return $"{m * 100:0.#} cm";
            return $"{m * 1000:0.#} mm";
        }
    }
}
