using System.Collections.Generic;
using KSP;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.Localization.Export;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.PlanetAuthoring.Validation;
using Ksp2UnityTools.Editor.PlanetAuthoring.Windows;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Planet
{
    /// <summary>
    /// Builds the General tab: identity, body type, size, rotation, orbit, flight and saving.
    /// </summary>
    public static class GeneralTab
    {
        private static readonly List<string> SOI_LABELS = new()
        {
            "Child of Body",
            "Child of Galactic Origin",
            "Force None",
            "Force",
        };

        /// <summary>
        /// Builds the tab into <paramref name="content" />.
        /// </summary>
        /// <param name="view">The inspector.</param>
        /// <param name="content">The tab content area.</param>
        public static void Build(PlanetInspectorView view, VisualElement content)
        {
            if (!PlanetInspectorView.CloneTemplate(content, "GeneralTab.uxml"))
                return;

            PlanetInspectorContext context = view.Context;
            SerializedObject body = context.BodyObject;
            content.Bind(body);

            BuildSoiField(content.Q("soi-calc-slot"), body);
            BuildOrbit(view, content);
            WireGravity(content, body);
            WireTerrainHeight(content, context);
            WireBlenderHeights(content, body);
            content.Q<Button>("save-json").clicked += view.SaveJson;
            content.Q<Button>("export-localizations").clicked += () => LocExportFlow.RunForAsset(context.Body);

            view.AddTabRefresh(() =>
            {
                PlanetInspectorView.SetShown(content.Q("section-star"), context.BodyClass == BodyClassFlags.Star);
                PlanetInspectorView.SetShown(content.Q("section-orbit"), context.BodyClass != BodyClassFlags.Star);
                PlanetInspectorView.SetShown(content.Q("terrain-heights"), context.BodyClass == BodyClassFlags.SolidSurface);
            });
        }

        // The orbit lives in an editor-only sidecar beside the Scaled prefab, since the game reads it from the galaxy
        // definition rather than the body.
        private static void BuildOrbit(PlanetInspectorView view, VisualElement content)
        {
            PlanetInspectorContext context = view.Context;
            GameObject prefab = context.ScaledPrefabAsset();
            CelestialBodyOrbitAuthoring orbit = prefab != null ? AuthoringSidecars.FindOrbit(prefab) : null;
            PlanetInspectorView.SetShown(content.Q("orbit-missing"), orbit == null);
            PlanetInspectorView.SetShown(content.Q("orbit-fields"), orbit != null);

            content.Q<Button>("orbit-create").clicked += () =>
            {
                if (prefab == null)
                {
                    view.SetStatus("Save the body as a prefab before giving it an orbit.");
                    return;
                }

                AuthoringSidecars.GetOrCreateOrbit(prefab);
                AssetDatabase.SaveAssets();
                view.RebuildTab();
            };

            var status = content.Q<Label>("galaxy-status");
            var write = content.Q<Button>("galaxy-write");
            if (orbit == null)
            {
                PlanetInspectorView.SetShown(write, false);
                PlanetInspectorView.ShowLabel(status, null);
                return;
            }

            var orbitObject = new SerializedObject(orbit);
            content.Q("orbit-fields").Bind(orbitObject);

            // Bodies a mod adds, or ones not made yet, are valid parents too, so any name can be typed.
            List<string> knownBodies = null;
            var parent = new AutocompleteField(orbitObject.FindProperty("ParentBody"), "Parent Body",
                () => knownBodies ??= GalaxyEntry.KnownBodyNames(context.BodyName))
            {
                tooltip = "The body this one orbits, by its name in the galaxy. Stock, project and mod bodies are suggested, and any name can be typed.",
            };
            content.Q("orbit-parent-slot").Add(parent);

            // Galaxies a mod creates at runtime are valid targets too, so any key can be typed.
            var galaxy = new AutocompleteField(orbitObject.FindProperty("GalaxyDefinitionKey"), "Galaxy",
                GalaxyEntry.KnownGalaxyKeys)
            {
                tooltip = "The key of the galaxy definition this body is added to. Stock's and the project's galaxies are suggested, and any key can be typed.",
            };
            content.Q("orbit-galaxy-slot").Add(galaxy);

            write.clicked += () => view.SetStatus(GalaxyEntry.WriteProjectPatch(orbit));
            view.AddTabRefresh(() =>
            {
                GalaxyEntryState state = GalaxyEntry.GetState(orbit);
                PlanetInspectorView.SetShown(write, state is GalaxyEntryState.Missing or GalaxyEntryState.OutOfDate or GalaxyEntryState.Written);
                PlanetInspectorView.ShowLabel(status, state switch
                {
                    GalaxyEntryState.NoOrbit => "Set a parent body to put this body in the galaxy.",
                    GalaxyEntryState.BuiltWithMod => $"The {GalaxyEntry.FindMod(orbit).id} build writes {orbit.PathInMod}, which adds this body to the galaxy.",
                    GalaxyEntryState.Missing => "The galaxy patch has not been written yet.",
                    GalaxyEntryState.OutOfDate => "The galaxy patch is out of date with this orbit.",
                    _ => $"{GalaxyEntry.ProjectPatchPath(orbit)} is up to date.",
                });
            });
        }

        private static void BuildSoiField(VisualElement slot, SerializedObject body)
        {
            SerializedProperty type = body.FindProperty("core.data.SphereOfInfluenceCalculationType");
            SerializedProperty forced = body.FindProperty("core.data.ForcedSphereOfInfluence");
            if (slot == null || type == null || forced == null)
                return;

            const int forceIndex = (int)SphereOfInfluenceCalculationType.Force;
            var dropdown = new DropdownField("SOI Calculation", SOI_LABELS, Mathf.Clamp(type.intValue, 0, SOI_LABELS.Count - 1))
            {
                tooltip = "How this body's sphere of influence is computed. Child of Body: the standard SOI relative to the parent. "
                    + "Child of Galactic Origin: the SOI reaches one light-year, for system primaries. Force None: no SOI. "
                    + "Force: the Forced SOI below.",
            };
            dropdown.AddToClassList("unity-base-field__aligned");

            var forcedField = new PropertyField(forced, "Forced SOI (m)") { tooltip = "The sphere of influence in meters, used when SOI Calculation is Force." };
            forcedField.BindProperty(forced);
            PlanetInspectorView.SetShown(forcedField, type.intValue == forceIndex);

            dropdown.RegisterValueChangedCallback(evt =>
            {
                int index = SOI_LABELS.IndexOf(evt.newValue);
                if (index < 0)
                    return;

                type.intValue = index;
                type.serializedObject.ApplyModifiedProperties();
                PlanetInspectorView.SetShown(forcedField, index == forceIndex);
            });

            slot.Add(dropdown);
            slot.Add(forcedField);
        }

        private static void WireGravity(VisualElement content, SerializedObject body)
        {
            content.Q<Button>("suggest-gravity").clicked += () =>
            {
                SerializedProperty radius = body.FindProperty("core.data.radius");
                SerializedProperty gravity = body.FindProperty("core.data.gravityASL");
                body.Update();
                gravity.doubleValue = MassGravityCalculatorWindow.RecommendGravityASL(radius.doubleValue);
                body.ApplyModifiedProperties();
            };
        }

        private static void WireTerrainHeight(VisualElement content, PlanetInspectorContext context)
        {
            var status = content.Q<Label>("recalc-terrain-height-status");
            PlanetInspectorView.ShowLabel(status, string.Empty);
            content.Q<Button>("recalc-terrain-height-button").clicked += () =>
            {
                PlanetInspectorView.ShowLabel(status, RecalculateTerrainHeight(context));
            };
        }

        private static string RecalculateTerrainHeight(PlanetInspectorContext context)
        {
            TerrainHeightRangeCalculator.Result result;
            try
            {
                EditorUtility.DisplayCancelableProgressBar("Recalculating terrain range", "Sampling heightmap...", 0f);
                result = TerrainHeightRangeCalculator.Compute(BodyResolver.FindPqs(context.Body));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (result.Cancelled)
                return $"Cancelled after {result.SampleCount:N0} samples.";
            if (!result.Success)
                return result.FailureReason;

            SerializedObject body = context.BodyObject;
            body.Update();
            body.FindProperty("core.data.MinTerrainHeight").doubleValue = result.MinHeight;
            body.FindProperty("core.data.MaxTerrainHeight").doubleValue = result.MaxHeight;
            body.ApplyModifiedProperties();
            return $"Updated. Min {result.MinHeight:0.0} m, max {result.MaxHeight:0.0} m. Sampled {result.SampleCount:N0} points, accurate within 1 m for features wider than 1 km.";
        }

        private static void WireBlenderHeights(VisualElement content, SerializedObject body)
        {
            var midLevel = content.Q<DoubleField>("blender-mid-level");
            var strength = content.Q<DoubleField>("blender-strength");
            var seaLevel = content.Q<DoubleField>("blender-sea-level");
            var target = content.Q<DoubleField>("blender-target");
            var targetKind = content.Q<DropdownField>("blender-target-kind");
            var result = content.Q<Label>("blender-result");
            var apply = content.Q<Button>("blender-apply");

            target.value = body.FindProperty("core.data.radius").doubleValue;

            PlanetCalculatorsWindow.HeightSolution Solve() => PlanetCalculatorsWindow.Solve(
                midLevel.value, strength.value, seaLevel.value, target.value, targetKind.index == 1);

            void Preview()
            {
                PlanetCalculatorsWindow.HeightSolution solution = Solve();
                bool valid = solution.Radius > 0.0;
                apply.SetEnabled(valid);
                PlanetInspectorView.ShowLabel(result, valid
                    ? $"Radius {solution.Radius:N1} m, Ocean Altitude {solution.OceanAltitude:N1} m, Terrain Height Scale {solution.TerrainHeightScale:N1} m."
                    : "Enter a mid-level and strength that give the mesh a size.");
            }

            foreach (DoubleField field in new[] { midLevel, strength, seaLevel, target })
            {
                field.RegisterValueChangedCallback(_ => Preview());
            }

            targetKind.RegisterValueChangedCallback(_ => Preview());
            apply.clicked += () =>
            {
                PlanetCalculatorsWindow.HeightSolution solution = Solve();
                body.Update();
                body.FindProperty("core.data.radius").doubleValue = solution.Radius;
                body.FindProperty("core.data.oceanAltitude").doubleValue = solution.OceanAltitude;
                body.FindProperty("core.data.TerrainHeightScale").doubleValue = solution.TerrainHeightScale;
                body.ApplyModifiedProperties();
            };
            Preview();
        }
    }
}
