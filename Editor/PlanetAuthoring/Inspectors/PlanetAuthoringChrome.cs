using System;
using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Scatter;
using Ksp2UnityTools.Editor.Localization.Export;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.PlanetAuthoring.Validation;
using Ksp2UnityTools.Editor.Validation;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Shared wiring for the validation chip and Quick Tools that every body-targeting inspector
    /// hosts.
    /// </summary>
    /// <remarks>
    /// A celestial body is split across a scaled-space object and a local-space one, and an author
    /// works from whichever is selected. Both inspectors therefore carry the same chrome rather than
    /// making people reselect to reach a tool. The markup lives in
    /// Shared/PlanetAuthoringChrome.uxml.
    /// </remarks>
    internal static class PlanetAuthoringChrome
    {
        /// <summary>
        /// Wires the validation chip and every Quick Tools button inside <paramref name="root" />.
        /// </summary>
        /// <remarks>
        /// Every lookup is null tolerant, so an inspector that instances only part of the template
        /// still works.
        /// </remarks>
        /// <param name="root">The inspector root containing the chrome elements.</param>
        /// <param name="resolveBody">Resolves the body the chrome acts on. May return null.</param>
        public static void Wire(VisualElement root, Func<CoreCelestialBodyData> resolveBody)
        {
            WireButton(root, "quick-preview-controls", Windows.PreviewControlsWindow.ShowWindow);
            WireButton(root, "quick-landmark-manager", Windows.PlanetAuthoringWindows.ShowLandmarkManager);
            WireButton(root, "quick-export-localizations", () => LocExportFlow.RunForAsset(resolveBody()));

            var chipOpen = root.Q<Button>("validation-chip-open");
            if (chipOpen != null)
            {
                chipOpen.clicked += () => Windows.ValidationReportWindow.Open(resolveBody());
            }

            WireBake(root, resolveBody);
            WireScatter(root, resolveBody);
        }

        /// <summary>
        /// Updates the validation chip to reflect the body's current cheap and cached expensive issues.
        /// </summary>
        /// <remarks>
        /// Call this from the hosting inspector's periodic refresh. It is separate from
        /// <see cref="Wire" /> because inspectors already own a refresh cadence and running a second
        /// one here would double the validation work.
        /// </remarks>
        /// <param name="root">The inspector root containing the chip elements.</param>
        /// <param name="body">The body to report on, or null to blank the chip.</param>
        public static void RefreshValidationChip(VisualElement root, CoreCelestialBodyData body)
        {
            var label = root.Q<Label>("validation-chip-label");
            var openButton = root.Q<Button>("validation-chip-open");
            if (label == null)
            {
                return;
            }

            label.RemoveFromClassList("validation-chip--clean");
            label.RemoveFromClassList("validation-chip--issues");

            if (body == null)
            {
                label.text = string.Empty;
                openButton?.SetEnabled(false);
                return;
            }

            PlanetValidationReport cheap = PlanetValidationReport.Run(body, ValidatorCost.Cheap);
            IReadOnlyList<ValidationIssue> expensive = ValidationExpensiveCache.Get(body);
            int errors = cheap.ErrorCount;
            int warnings = cheap.WarningCount;
            int info = cheap.InfoCount;
            foreach (ValidationIssue issue in expensive)
            {
                switch (issue.Severity)
                {
                    case ValidationSeverity.Error: errors++; break;
                    case ValidationSeverity.Warning: warnings++; break;
                    case ValidationSeverity.Info: info++; break;
                }
            }

            if (errors + warnings + info == 0)
            {
                label.text = "Validation: clean";
                label.AddToClassList("validation-chip--clean");
            }
            else
            {
                label.text = $"Validation: {errors} error{(errors == 1 ? string.Empty : "s")}, "
                    + $"{warnings} warning{(warnings == 1 ? string.Empty : "s")}, {info} info";
                label.AddToClassList("validation-chip--issues");
            }

            openButton?.SetEnabled(true);
        }

        /// <summary>
        /// Refreshes the Quick Tools scatter button label for the current body.
        /// </summary>
        /// <param name="root">The inspector root containing the chrome elements.</param>
        /// <param name="body">The body to describe, or null.</param>
        public static void RefreshScatter(VisualElement root, CoreCelestialBodyData body)
        {
            var button = root.Q<Button>("quick-scatter-system");
            if (button == null)
            {
                return;
            }

            PQS pqs = body != null ? BodyResolver.FindPqs(body) : null;
            if (pqs == null)
            {
                button.SetEnabled(false);
                return;
            }

            button.SetEnabled(true);
            button.text = ScatterSystemLocator.Find(pqs) == null
                ? "Add Scatter System"
                : "Repair Scatter System";
        }

        private static void WireButton(VisualElement root, string name, Action handler)
        {
            var button = root.Q<Button>(name);
            if (button != null)
            {
                button.clicked += handler;
            }
        }

        private static void WireBake(VisualElement root, Func<CoreCelestialBodyData> resolveBody)
        {
            var button = root.Q<Button>("quick-bake-body-surface");
            if (button == null)
            {
                return;
            }

            // Runs with whatever the Body Surface Baking section last persisted, which is the point
            // of a quick tool. The full section stays in both inspectors for changing those settings.
            button.clicked += () =>
            {
                var status = root.Q<Label>("quick-tools-status");
                CoreCelestialBodyData body = resolveBody();
                if (body == null)
                {
                    SetStatus(status, "Bake failed: could not resolve a body.");
                    return;
                }

                var result = BodySurfaceBakeSection.BakeWithPersistedSettings(body);
                SetStatus(status, result.Success ? $"Baked to {result.ScaledFolder}." : $"Bake failed: {result.Error}");
            };
        }

        private static void WireScatter(VisualElement root, Func<CoreCelestialBodyData> resolveBody)
        {
            var button = root.Q<Button>("quick-scatter-system");
            if (button == null)
            {
                return;
            }

            button.clicked += () =>
            {
                CoreCelestialBodyData body = resolveBody();
                PQS pqs = body != null ? BodyResolver.FindPqs(body) : null;
                if (pqs == null)
                {
                    SetStatus(root.Q<Label>("quick-tools-status"), "No PQS on this body. Terrain scatter needs a solid surface.");
                    return;
                }

                ScatterSystemLocator.Configure(pqs, body);
                RefreshScatter(root, body);
            };
        }

        private static void SetStatus(Label status, string message)
        {
            if (status == null)
            {
                return;
            }

            status.text = message;
            status.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
