using System;
using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Scatter;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.PlanetAuthoring.Windows;
using Ksp2UnityTools.Editor.Widgets;
using Uber.Scatter;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Planet
{
    /// <summary>
    /// Builds the Features tab: landmarks, decals, surface prefabs, scatter, and the objects the game creates with the
    /// body.
    /// </summary>
    /// <remarks>
    /// Landmarks, decal instances and spawners are child objects of the Local prefab. Each gets a card that holds its
    /// own inspector, built when the card is first unfolded. The sim and colony objects are cards over the body's
    /// serialized lists.
    /// </remarks>
    public static class FeaturesTab
    {
        private const string LIVE_OK_CLASS = "planet-live-ok";

        // From CreateCelestialBodiesFlowAction.CreatePredefinedSimObjects, which makes these objects at load.
        private static readonly Dictionary<string, string> SIM_OBJECT_TOOLTIPS = new()
        {
            ["RelativeTo"] = "The Name of another sim object to place this one against. Empty places it against the body. "
                + "The game creates objects without one first, and skips an object whose Relative To it cannot find.",
            ["ReferenceFrame"] = "Which frame of the object it is placed against. Body turns with it, as a building on the surface does. "
                + "Celestial and None are its non-rotating frame.",
            ["LocalPosition"] = "Position in that frame, in meters.",
            ["LocalRotation"] = "Rotation in that frame.",
            ["FixedGuid"] = "Derive the object's global ID from its Name, so it is the same in every save. Off gives it a new ID each game.",
        };

        // From CreateCelestialBodiesFlowAction.AddPredefinedColonyComponents and PopulationComponent.
        private static readonly Dictionary<string, string> COLONY_OBJECT_TOOLTIPS = new()
        {
            ["HasPopulationComponent"] = "Keep kerbals at this object, creating or reassigning one whenever it is below the fill limit.",
            ["PopulationFillLimit"] = "How many kerbals the object keeps.",
            ["PopulationFillVacancyDelay"] = "Seconds of game time between noticing a vacancy and filling it.",
            ["PopulationFillLimitEmptySubIdOnly"] = "Count only kerbals not assigned to one of the object's sub-objects toward the fill limit.",
            ["PopulationReuseNonVeterans"] = "Fill vacancies with existing non-veteran kerbals from the roster.",
            ["PopulationReuseVeterans"] = "Fill vacancies with existing veteran kerbals from the roster.",
            ["PopulationCreateNewNonVeterans"] = "Fill vacancies by creating new non-veteran kerbals.",
            ["PopulationCreateNewVeterans"] = "Fill vacancies by creating new veteran kerbals.",
            ["HasTelemetryComponent"] = "Make the object a CommNet node that can control vessels, with the space center's CommNet range.",
            ["IsCommNetSource"] = "Make it the source of the CommNet network. Needs Has Telemetry Component.",
        };

        /// <summary>
        /// Builds the tab into <paramref name="content" />.
        /// </summary>
        /// <param name="view">The inspector.</param>
        /// <param name="content">The tab content area.</param>
        public static void Build(PlanetInspectorView view, VisualElement content)
        {
            PQS pqs = view.Context.Pqs;
            if (pqs == null)
            {
                content.Add(new HelpBox("This body has no Local prefab with a PQS.", HelpBoxMessageType.Warning));
                return;
            }

            if (!PlanetInspectorView.CloneTemplate(content, "FeaturesTab.uxml"))
                return;

            BuildObjectCards(view, content.Q("landmark-cards"), "Landmarks", "No surface landmarks.",
                () => new List<SurfaceLandmark>(pqs.GetComponentsInChildren<SurfaceLandmark>(true)),
                LandmarkChips,
                landmark => (landmark.Latitude, landmark.Longitude),
                () => NewSurfaceLandmarkPromptWindow.Show(PlaceSurfaceLandmarkTool.Begin),
                "Create a surface landmark and place it on the planet. Needs the preview running.");
            content.Q<Button>("landmark-manager").clicked += PlanetAuthoringWindows.ShowLandmarkManager;

            var controller = pqs.GetComponent<PQSDecalController>();
            EnvironmentTab.AddInspector(content.Q("decal-slot"), controller, "This body has no PQSDecalController on its Local prefab.");
            if (controller != null)
            {
                BuildObjectCards(view, content.Q("decal-cards"), "Decal instances", "No decal instances.",
                    () => LiveDecals(controller),
                    decal => DecalChip(pqs, decal),
                    decal => (decal.LatLong.x, decal.LatLong.y),
                    null, null);
            }

            BuildObjectCards(view, content.Q("prefab-cards"), "Surface prefabs", "No surface prefab spawners.",
                () => new List<PrefabSpawner>(pqs.GetComponentsInChildren<PrefabSpawner>(true)),
                _ => null,
                spawner => LandmarkManagerWindow.LatLonFromTransform(spawner.transform, pqs.transform),
                null, null);

            BuildScatter(view, content, pqs);
            BuildLocalObjects(view, content);

            // The sim objects have no GameObjects to draw them, so the tab draws them in the preview while it is open.
            if (!view.Context.IsLive)
            {
                var gizmos = new SimObjectGizmos(view.Context);
                var anchor = new VisualElement();
                anchor.RegisterCallback<AttachToPanelEvent>(_ => gizmos.Attach());
                anchor.RegisterCallback<DetachFromPanelEvent>(_ => gizmos.Detach());
                content.Add(anchor);
            }
        }

        private static void BuildScatter(PlanetInspectorView view, VisualElement content, PQS pqs)
        {
            PqsTerrain terrain = ScatterSystemLocator.FindTerrain(pqs);
            VisualElement terrainFoldout = content.Q("scatter-terrain");
            PlanetInspectorView.SetShown(terrainFoldout, terrain != null);
            if (terrain != null)
            {
                terrainFoldout.Bind(new SerializedObject(terrain));
            }

            VegetationSystemPro system = ScatterSystemLocator.Find(pqs);
            EnvironmentTab.AddInspector(content.Q("scatter-slot"), system);

            var configure = content.Q<Button>("scatter-configure");
            configure.text = system == null ? "Add" : "Repair";
            configure.clicked += () =>
            {
                view.SetStatus(PlanetFeatureActions.ConfigureScatter(view.Context.Body));
                view.RebuildTab();
            };
        }

        // Cards over the body's serialized lists, which the game creates the objects from at load.
        private static void BuildLocalObjects(PlanetInspectorView view, VisualElement content)
        {
            SerializedObject body = view.Context.BodyObject;
            content.Q("sim-object-cards").Add(CardListSection.Build(body.FindProperty("core.data.LocalSimObjectsData"), new CardListSection.Config
            {
                Title = "Objects",
                AddButtonText = "+ Add",
                IdentityFieldName = "Name",
                BuildIdentityField = name => IdentityField(name, "The object's name. Relative To and colony objects refer to it by this, and so does the game's lookup by name."),
                ChipFieldName = "RelativeTo",
                ChipFormatter = relativeTo => string.IsNullOrEmpty(relativeTo.stringValue) ? null : $"relative to {relativeTo.stringValue}",
                BuildBody = (entry, cardBody) => BuildSimObjectBody(view, entry, cardBody),
            }));
            content.Q("colony-object-cards").Add(CardListSection.Build(body.FindProperty("core.data.LocalColonyObjectsData"), new CardListSection.Config
            {
                Title = "Colony components",
                AddButtonText = "+ Add",
                IdentityFieldName = "SimObjectName",
                BuildIdentityField = name => IdentityField(name, "The Name of the sim object these colony components go on."),
                BuildBody = (entry, cardBody) => AddChildFields(entry, cardBody, "SimObjectName", COLONY_OBJECT_TOOLTIPS),
            }));
        }

        // An object placed against the body in its Body frame is edited as latitude, longitude, altitude and heading,
        // the way landmarks are. Any other keeps its offset and rotation as stored, in its parent's frame.
        private static void BuildSimObjectBody(PlanetInspectorView view, SerializedProperty entry, VisualElement cardBody)
        {
            SerializedObject body = entry.serializedObject;
            string path = entry.propertyPath;
            SerializedProperty Field(string name) => body.FindProperty($"{path}.{name}");
            double Radius() => body.FindProperty("core.data.radius").doubleValue;

            cardBody.Add(TooltippedField(Field("RelativeTo")));
            cardBody.Add(TooltippedField(Field("ReferenceFrame")));

            var surface = new VisualElement();
            var latitude = new DoubleField("Latitude (deg)") { tooltip = "North positive." };
            var longitude = new DoubleField("Longitude (deg)") { tooltip = "East positive." };
            var altitude = new DoubleField("Altitude (m)") { tooltip = "Height above sea level, the body radius. Snap to Terrain sets it to the ground's." };
            var heading = new DoubleField("Heading (deg)") { tooltip = "The way the object faces: 0 north, 90 east." };
            foreach (DoubleField field in new[] { latitude, longitude, altitude, heading })
            {
                field.isDelayed = true;
                field.AddToClassList("unity-base-field__aligned");
                surface.Add(field);
            }

            var upright = new VisualElement();
            upright.AddToClassList("planet-inspector-field-row");
            var uprightLabel = new Label("Not upright on the surface, so Heading is not used.");
            uprightLabel.AddToClassList("sdk-hint");
            uprightLabel.AddToClassList("planet-inspector-field-row__field");
            upright.Add(uprightLabel);
            surface.Add(upright);

            var buttons = new VisualElement();
            buttons.AddToClassList("planet-inspector-field-row");
            surface.Add(buttons);
            cardBody.Add(surface);

            var offset = new VisualElement();
            offset.Add(TooltippedField(Field("LocalPosition")));
            offset.Add(TooltippedField(Field("LocalRotation")));
            cardBody.Add(offset);
            cardBody.Add(TooltippedField(Field("FixedGuid")));

            bool IsOnBody() =>
                string.IsNullOrEmpty(Field("RelativeTo").stringValue)
                && Field("ReferenceFrame").enumValueIndex == (int)KSP.Sim.TransformFrameType.Body;

            void Write(Vector3d position, Quaternion rotation)
            {
                body.Update();
                SimObjectGizmos.WritePosition(Field("LocalPosition"), position);
                Field("LocalRotation").quaternionValue = rotation;
                body.ApplyModifiedProperties();
            }

            // Position follows the three surface fields. Rotation follows Heading, unless the object is not upright,
            // in which case moving it keeps its rotation as it is.
            void WriteFromFields()
            {
                Vector3d position = SimObjectPlacement.ToPosition(latitude.value, longitude.value, altitude.value, Radius());
                Vector3d oldPosition = SimObjectGizmos.ReadPosition(Field("LocalPosition"));
                Quaternion oldRotation = Field("LocalRotation").quaternionValue;
                Quaternion rotation = SimObjectPlacement.IsUpright(oldPosition, oldRotation)
                    ? SimObjectPlacement.ToRotation(position, heading.value)
                    : oldRotation;
                Write(position, rotation);
            }

            foreach (DoubleField field in new[] { latitude, longitude, altitude })
            {
                field.RegisterValueChangedCallback(_ => WriteFromFields());
            }

            heading.RegisterValueChangedCallback(evt =>
            {
                Vector3d position = SimObjectGizmos.ReadPosition(Field("LocalPosition"));
                Write(position, SimObjectPlacement.ToRotation(position, evt.newValue));
            });

            var pick = new Button(() => PlanetSurfacePickTool.Begin(latLon =>
            {
                if (PlanetAuthoringSession.Active == null)
                    return;

                Vector3d direction = LatLon.GetRelSurfaceNVector(latLon.x, latLon.y);
                double ground = PlanetAuthoringSession.Active.Pqs.GetSurfaceHeight(direction, true) - Radius();
                Vector3d position = SimObjectPlacement.ToPosition(latLon.x, latLon.y, ground, Radius());
                Write(position, SimObjectPlacement.ToRotation(position, heading.value));
            }))
            {
                text = "Pick on Planet",
                tooltip = "Click the preview to place it on the ground there.",
            };
            var snap = new Button(() =>
            {
                if (PlanetAuthoringSession.Active == null)
                    return;

                Vector3d position = SimObjectGizmos.ReadPosition(Field("LocalPosition"));
                double ground = PlanetAuthoringSession.Active.Pqs.GetSurfaceHeight(position, true) - Radius();
                altitude.value = ground;
            })
            {
                text = "Snap to Terrain",
                tooltip = "Set Altitude to the ground's height here, sampled from the running preview.",
            };
            var makeUpright = new Button(() =>
            {
                Vector3d position = SimObjectGizmos.ReadPosition(Field("LocalPosition"));
                Write(position, SimObjectPlacement.ToRotation(position, heading.value));
            })
            {
                text = "Make Upright",
                tooltip = "Stand it upright on the surface, facing its current heading.",
            };
            buttons.Add(pick);
            buttons.Add(snap);
            upright.Add(makeUpright);

            void Refresh()
            {
                // A removed card's refresh can outlive it, still pointing at an index the list no longer has.
                if (cardBody.panel == null || Field("LocalPosition") == null)
                    return;

                bool onBody = IsOnBody();
                PlanetInspectorView.SetShown(surface, onBody);
                PlanetInspectorView.SetShown(offset, !onBody);
                if (!onBody)
                    return;

                Vector3d position = SimObjectGizmos.ReadPosition(Field("LocalPosition"));
                Quaternion rotation = Field("LocalRotation").quaternionValue;
                (double lat, double lon) = SimObjectPlacement.ToLatLon(position);
                latitude.SetValueWithoutNotify(lat);
                longitude.SetValueWithoutNotify(lon);
                altitude.SetValueWithoutNotify(SimObjectPlacement.ToAltitude(position, Radius()));
                bool isUpright = SimObjectPlacement.IsUpright(position, rotation);
                heading.SetValueWithoutNotify(isUpright ? SimObjectPlacement.ToHeading(position, rotation) : 0.0);
                heading.SetEnabled(isUpright);
                PlanetInspectorView.SetShown(upright, !isUpright);

                bool previewing = PlanetPreviewLauncher.IsPreviewing(view.Context.Body);
                pick.SetEnabled(previewing);
                snap.SetEnabled(previewing);
            }

            Refresh();
            cardBody.TrackSerializedObjectValue(body, _ => Refresh());
            view.AddTabRefresh(Refresh);
        }

        private static PropertyField TooltippedField(SerializedProperty property) =>
            new(property) { tooltip = SIM_OBJECT_TOOLTIPS.TryGetValue(property.name, out string tooltip) ? tooltip : null };

        private static TextField IdentityField(SerializedProperty property, string tooltip)
        {
            var field = new TextField { value = property.stringValue, isDelayed = true, tooltip = tooltip };
            field.RegisterValueChangedCallback(evt =>
            {
                property.serializedObject.Update();
                property.stringValue = evt.newValue ?? string.Empty;
                property.serializedObject.ApplyModifiedProperties();
            });
            return field;
        }

        // Next rather than NextVisible, since both lists are HideInInspector and NextVisible finds nothing under them.
        private static void AddChildFields(SerializedProperty entry, VisualElement cardBody, string identityField, Dictionary<string, string> tooltips)
        {
            SerializedProperty child = entry.Copy();
            SerializedProperty end = entry.GetEndProperty();
            bool enterChildren = true;
            while (child.Next(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                enterChildren = false;
                if (child.name != identityField)
                {
                    cardBody.Add(new PropertyField(child.Copy()) { tooltip = tooltips.TryGetValue(child.name, out string tooltip) ? tooltip : null });
                }
            }
        }

        // A card per child object. Add is left out where the Landmark Manager has no add of its own either, since new
        // decals and prefabs are placed through landmarks.
        private static void BuildObjectCards<T>(
            PlanetInspectorView view,
            VisualElement slot,
            string title,
            string emptyText,
            Func<List<T>> source,
            Func<T, string> describe,
            Func<T, (double Lat, double Lon)> locate,
            Action onAdd,
            string addTooltip)
            where T : Component
        {
            CardListSection.ListHandle handle = null;
            handle = CardListSection.BuildFromList(() => source(), new CardListSection.ListConfig<T>
            {
                Title = title,
                AddButtonText = "+ Add",
                AddButtonTooltip = addTooltip,
                EmptyHintText = emptyText,
                OnAddClicked = onAdd,
                BuildCard = (item, _) => BuildObjectCard(view, item, describe(item), locate(item), () => handle.Rebuild()),
            });

            if (onAdd == null)
            {
                PlanetInspectorView.SetShown(handle.Root.Q<Button>(), false);
            }

            slot.Add(handle.Root);
        }

        private static VisualElement BuildObjectCard<T>(PlanetInspectorView view, T item, string chips, (double Lat, double Lon) location, Action rebuild)
            where T : Component
        {
            VisualElement card = CardShell.Build(out CardShell.Slots slots, false);
            slots.Disclosure.AddToClassList(LIVE_OK_CLASS);

            var name = new Label(item.gameObject.name);
            name.AddToClassList("sdk-card__name-field");
            slots.Header.Add(name);

            if (!string.IsNullOrEmpty(chips))
            {
                var chip = new Label(chips);
                chip.AddToClassList("sdk-card__chip");
                slots.Header.Add(chip);
            }

            // Framing needs the preview's PQS, the one the scene view shows.
            bool previewing = PlanetPreviewLauncher.IsPreviewing(view.Context.Body);
            var look = new Button(() => SceneViewFraming.FrameAtLatLonAndAltitude(
                PlanetAuthoringSession.Active.Pqs, location.Lat, location.Lon, SurfaceFramingPrefs.AltitudeMeters, SceneFramingMode.Surface))
            {
                text = "Look",
                tooltip = previewing ? $"Frame the scene view on it, at {location.Lat:0.00}, {location.Lon:0.00}." : "Start the preview to look at it.",
            };
            look.AddToClassList("sdk-card__move-btn");
            look.SetEnabled(previewing);
            slots.Header.Add(look);

            GameObject gameObject = item.gameObject;
            var remove = new Button(() =>
            {
                if (!EditorUtility.DisplayDialog("Delete", $"Delete '{gameObject.name}'?", "Delete", "Cancel"))
                    return;

                Undo.DestroyObjectImmediate(gameObject);
                rebuild();
            }) { text = "X", tooltip = "Delete it. Undo brings it back." };
            remove.AddToClassList("sdk-card__remove-btn");
            slots.Header.Add(remove);

            // The object's inspector is built on first unfold, so a long list of folded cards stays cheap.
            slots.Disclosure.clicked += () =>
            {
                if (slots.Body.childCount == 0 && slots.Body.style.display.value == DisplayStyle.Flex)
                {
                    slots.Body.Add(new InspectorElement(item));
                }
            };

            return card;
        }

        private static List<PQSDecalInstance> LiveDecals(PQSDecalController controller)
        {
            var decals = new List<PQSDecalInstance>();
            foreach (PQSDecalInstance decal in controller.PqsDecalInstanceList)
            {
                if (decal != null)
                {
                    decals.Add(decal);
                }
            }

            return decals;
        }

        // A decal a landmark manages is edited through that landmark, so its card says whose it is.
        private static string DecalChip(PQS pqs, PQSDecalInstance decal)
        {
            foreach (SurfaceLandmark landmark in pqs.GetComponentsInChildren<SurfaceLandmark>(true))
            {
                if (landmark.ManagedDecal == decal)
                    return $"from {landmark.gameObject.name}";
            }

            return null;
        }

        private static string LandmarkChips(SurfaceLandmark landmark)
        {
            var parts = new List<string>();
            if (landmark.EnableDecal)
            {
                parts.Add("decal");
            }

            if (landmark.EnablePrefab)
            {
                parts.Add("prefab");
            }

            if (landmark.EnableDiscoverable)
            {
                parts.Add("discoverable");
            }

            return string.Join(", ", parts);
        }
    }
}
