using System;
using KSP;
using KSP.Rendering;
using KSP.VolumeCloud;
using Ksp2UnityTools.Editor.PlanetAuthoring.Atmosphere;
using Ksp2UnityTools.Editor.PlanetAuthoring.Clouds;
using Ksp2UnityTools.Editor.PlanetAuthoring.Ocean;
using Ksp2UnityTools.Editor.PlanetAuthoring.Validation;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Planet
{
    /// <summary>
    /// Builds the Environment tab: atmosphere, clouds, ocean, lighting, post-processing, heat and rings.
    /// </summary>
    /// <remarks>
    /// Each feature's section holds its physics, which is body data, and its look, which is the feature's own
    /// components and assets shown through their inspectors.
    /// </remarks>
    public static class EnvironmentTab
    {
        /// <summary>
        /// Builds the tab into <paramref name="content" />.
        /// </summary>
        /// <param name="view">The inspector.</param>
        /// <param name="content">The tab content area.</param>
        public static void Build(PlanetInspectorView view, VisualElement content)
        {
            if (!PlanetInspectorView.CloneTemplate(content, "EnvironmentTab.uxml"))
                return;

            PlanetInspectorContext context = view.Context;
            CoreCelestialBodyData body = context.Body;

            // The body binding goes first. The ocean's renderer fields are rebound to the renderer afterwards.
            content.Bind(context.BodyObject);

            bool notStar = context.BodyClass != BodyClassFlags.Star;
            bool solid = context.BodyClass == BodyClassFlags.SolidSurface;
            PlanetInspectorView.SetShown(content.Q("section-atmosphere"), notStar);
            PlanetInspectorView.SetShown(content.Q("section-clouds"), notStar);
            PlanetInspectorView.SetShown(content.Q("section-ocean"), solid);
            PlanetInspectorView.SetShown(content.Q("section-heat"), notStar);
            PlanetInspectorView.SetShown(content.Q("section-rings"), notStar);

            if (notStar)
            {
                BuildFeature(view, content, "atmosphere",
                    () => AtmosphereSetup.HasAtmosphere(body), PlanetFeatureActions.AddAtmosphere, PlanetFeatureActions.RemoveAtmosphere);
                if (AtmosphereSetup.HasAtmosphere(body))
                {
                    AddInspector(content.Q("atmosphere-look"), body.GetComponent<AtmosphereDataModelComponent>());
                }

                BuildFeature(view, content, "clouds",
                    () => CloudSetup.HasClouds(body), PlanetFeatureActions.AddClouds, PlanetFeatureActions.RemoveClouds);
                if (CloudSetup.HasClouds(body))
                {
                    AddInspector(content.Q("clouds-look"), CloudSetup.FindHelper(body));
                    AddInspector(content.Q("clouds-look"), body.GetComponent<ScaledCloudDataModelComponent>());
                }
            }

            if (solid && context.Pqs != null)
            {
                BuildFeature(view, content, "ocean",
                    () => OceanSetup.HasOcean(body), PlanetFeatureActions.AddOcean, PlanetFeatureActions.RemoveOcean);
                OceanSection.Wire(content, context.Pqs);
            }

            AddInspector(content.Q("lighting-slot"), body.GetComponent<CelestialBodyLighting>(), "This body has no CelestialBodyLighting component.");
            AddInspector(content.Q("post-slot"), body.GetComponent<CelestialBodyPostProcess>(), "This body has no CelestialBodyPostProcess component.");
        }

        // Wires a feature section's header: Add relabels to the refit verb once the feature exists, Remove shows only
        // then, and both rebuild the tab so the section's inspectors follow.
        private static void BuildFeature(
            PlanetInspectorView view,
            VisualElement content,
            string feature,
            Func<bool> exists,
            Func<CoreCelestialBodyData, string> add,
            Func<CoreCelestialBodyData, string> remove)
        {
            var addButton = content.Q<Button>($"{feature}-add");
            var removeButton = content.Q<Button>($"{feature}-remove");
            string refitVerb = feature == "ocean" ? "Re-wire" : "Refit";

            addButton.clicked += () =>
            {
                view.SetStatus(add(view.Context.Body));
                view.RebuildTab();
            };
            removeButton.clicked += () =>
            {
                string message = remove(view.Context.Body);
                if (message == null)
                    return;

                view.SetStatus(message);
                view.RebuildTab();
            };

            view.AddTabRefresh(() =>
            {
                bool present = exists();
                addButton.text = present ? refitVerb : "Add";
                PlanetInspectorView.SetShown(removeButton, present);
            });
        }

        /// <summary>
        /// Adds a component's or asset's own inspector to <paramref name="slot" />.
        /// </summary>
        /// <param name="slot">The element to add to.</param>
        /// <param name="target">The object to inspect, or null.</param>
        /// <param name="missingMessage">The hint to show instead when <paramref name="target" /> is null, or null for none.</param>
        public static void AddInspector(VisualElement slot, UnityEngine.Object target, string missingMessage = null)
        {
            if (slot == null)
                return;

            if (target == null)
            {
                if (!string.IsNullOrEmpty(missingMessage))
                {
                    var hint = new Label(missingMessage);
                    hint.AddToClassList("sdk-hint");
                    slot.Add(hint);
                }

                return;
            }

            slot.Add(new InspectorElement(target));
        }
    }
}
