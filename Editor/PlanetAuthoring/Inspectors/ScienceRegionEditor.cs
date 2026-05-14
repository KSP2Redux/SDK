using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using KSP;
using KSP.Game.Science;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.PlanetAuthoring.Windows;
using Ksp2UnityTools.Editor.ScriptableObjects;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// UI Toolkit inspector for <see cref="ScienceRegionData" />.
    /// </summary>
    /// <remarks>
    /// Replaces the legacy IMGUI editor with a structured layout: Identity, Source map (with the
    /// Import &amp; cluster colors entry point), Regions table, Discoverables summary, and Bake.
    /// Preserves the existing bake pipeline via <see cref="ScienceRegionBaker" />.
    /// </remarks>
    [CustomEditor(typeof(ScienceRegionData))]
    public class ScienceRegionEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "/Assets/Windows/ScienceRegionInspector.uxml";
        private const string UssPath = "/Assets/Windows/ScienceRegionInspector.uss";

        // Two regions whose colors are within this normalized distance trigger a color-collision warning.
        private const float ColorCollisionTolerance = ScienceRegionConstants.ColorCollisionTolerance;

        private VisualElement _root;
        private Label _sourceMapStatsLabel;
        private VisualElement _sourceMapWarnings;
        private Label _regionsSummaryLabel;
        private VisualElement _regionsList;
        private Label _discoverablesSummaryLabel;
        private VisualElement _discoverablesList;
        private Label _bakePathsLabel;
        private Label _bakeStatusLabel;
        private int _lastKnownRegionCount = -1;
        private int _lastKnownDiscoverableCount = -1;

        private ScienceRegionData Target => target as ScienceRegionData;

        private void OnEnable()
        {
            // Editor.OnSceneGUI isn't called for ScriptableObject editors, so subscribe to the
            // SceneView callback ourselves while this editor is alive. PreviewOverlayLegend does
            // the same thing for the overlay legend.
            SceneView.duringSceneGui += OnSceneViewGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneViewGui;
        }

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            _root = new VisualElement();

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                _root.Add(new Label("Failed to load ScienceRegionInspector.uxml"));
                return _root;
            }
            tree.CloneTree(_root);
            Ksp2UnityToolsStyles.Apply(_root, UssPath);

            _sourceMapStatsLabel = _root.Q<Label>("source-map-stats-label");
            _sourceMapWarnings = _root.Q<VisualElement>("source-map-warnings");
            _regionsSummaryLabel = _root.Q<Label>("regions-summary-label");
            _regionsList = _root.Q<VisualElement>("regions-list");
            _discoverablesSummaryLabel = _root.Q<Label>("discoverables-summary-label");
            _discoverablesList = _root.Q<VisualElement>("discoverables-list");
            _bakePathsLabel = _root.Q<Label>("bake-paths-label");
            _bakeStatusLabel = _root.Q<Label>("bake-status-label");

            _root.Q<Button>("import-and-cluster-button").clicked += OnImportAndClusterClicked;
            _root.Q<Button>("add-region-button").clicked += OnAddRegionClicked;
            _root.Q<Button>("recluster-button").clicked += OnImportAndClusterClicked;
            _root.Q<Button>("place-discoverable-button").clicked += OnPlaceDiscoverableClicked;
            _root.Q<Button>("open-discoverable-manager-button").clicked += OnOpenDiscoverableManagerClicked;
            _root.Q<Button>("bake-button").clicked += OnBakeClicked;

            // Source-map property field reacts to assignment by refreshing stats & warnings.
            _root.Q<PropertyField>("source-map-field").RegisterValueChangeCallback(_ => RefreshSourceMapSection());

            _root.Bind(serializedObject);
            RefreshAll();
            // Source-map stats and bake-status labels are read-only summaries that can drift while
            // the inspector is open (external bake, re-import). Polling them is safe. Regions /
            // discoverables lists rebuild on demand only, so mid-typing TextFields don't get blown
            // away by a tick.
            _root.schedule.Execute(RefreshReadOnlySections).Every(500);

            return _root;
        }

        private void RefreshAll()
        {
            if (Target == null) return;
            RefreshSourceMapSection();
            RefreshRegionsSection();
            RefreshDiscoverablesSection();
            RefreshBakeStatus();
        }

        private void RefreshReadOnlySections()
        {
            if (Target == null) return;
            RefreshSourceMapSection();
            RefreshBakeStatus();

            // Detect external mutations (Import & cluster apply, undo/redo, Place Discoverable
            // tool, etc.) by tracking the collection counts. Mid-edit TextFields aren't disturbed
            // because in-place edits don't change count. Any region-array change refreshes BOTH
            // sections: adding a discoverable region (MapId<0) needs the Discoverables section to
            // rebuild, and deleting one needs the Regions section's collision warnings to drop
            // their stale references.
            var regionCount = Target.information?.ScienceRegionDefinitions?.Length ?? 0;
            var discoverableCount = Target.discoverables?.Count ?? 0;
            var regionsChanged = regionCount != _lastKnownRegionCount;
            var positionsChanged = discoverableCount != _lastKnownDiscoverableCount;
            if (regionsChanged) _lastKnownRegionCount = regionCount;
            if (positionsChanged) _lastKnownDiscoverableCount = discoverableCount;
            if (regionsChanged)
            {
                RefreshRegionsSection();
                RefreshDiscoverablesSection();
            }
            else if (positionsChanged)
            {
                RefreshDiscoverablesSection();
            }
        }

        // ----- Source map section -------------------------------------------

        private void RefreshSourceMapSection()
        {
            if (Target == null || _sourceMapStatsLabel == null) return;
            _sourceMapWarnings?.Clear();
            if (Target.scienceRegionMap == null)
            {
                _sourceMapStatsLabel.text = "No source texture assigned. Drag an equirectangular color-coded image into the Region map slot to begin.";
                return;
            }
            var src = Target.scienceRegionMap;
            _sourceMapStatsLabel.text = $"{src.width} x {src.height}    {src.format}    Pixels: {src.width * src.height:N0}";

            if (!src.isReadable)
                AddSourceMapWarning("Texture is not Read/Write enabled. The bake step's GetPixels() call will return an empty map. Enable Read/Write in the importer.");
        }

        private void AddSourceMapWarning(string text)
        {
            var label = new Label(text)
            {
                style = { color = new Color(1.0f, 0.75f, 0.45f) },
            };
            label.AddToClassList("sdk-hint");
            _sourceMapWarnings.Add(label);
        }

        // ----- Regions section ----------------------------------------------

        private void RefreshRegionsSection()
        {
            if (Target == null || _regionsList == null) return;
            _regionsList.Clear();
            var defs = Target.information?.ScienceRegionDefinitions;
            var bakedCount = CountBakedRegions(defs);
            _regionsSummaryLabel.text = bakedCount == 0
                ? "No baked-map regions defined. Use 'Import & cluster colors' or '+ Add region'."
                : $"{bakedCount} baked-map region{(bakedCount == 1 ? string.Empty : "s")} defined. Discoverable-only regions live in the Discoverables section below.";

            if (defs == null) return;
            for (var i = 0; i < defs.Length; i++)
            {
                if (defs[i] == null || defs[i].MapId < 0) continue;
                var capturedIndex = i;
                _regionsList.Add(BuildRegionRow(defs[i], capturedIndex, defs));
            }
        }

        private static int CountBakedRegions(ScienceRegionData.ExtendedScienceRegionDefinition[] defs)
        {
            if (defs == null) return 0;
            var n = 0;
            foreach (var def in defs)
            {
                if (def != null && def.MapId >= 0)
                {
                    n++;
                }
            }
            return n;
        }

        private VisualElement BuildRegionRow(
            ScienceRegionData.ExtendedScienceRegionDefinition def,
            int index,
            ScienceRegionData.ExtendedScienceRegionDefinition[] all)
        {
            var row = new VisualElement();
            row.AddToClassList("science-region-region-row");

            var collisionIdx = FindNearestColorCollision(def, index, all);
            if (collisionIdx >= 0)
                row.AddToClassList("science-region-region-row--collision");

            var header = new VisualElement();
            header.AddToClassList("science-region-region-row-header");

            // ColorField at its native size. Earlier attempts to shrink it via CSS broke the
            // widget's internal layout (label area + preview + dropdown all collapsed).
            var swatch = new ColorField
            {
                value = def.RegionColor,
                showAlpha = false,
                showEyeDropper = true,
                style = { width = 70f, flexShrink = 0f, marginRight = 6f },
            };
            swatch.RegisterValueChangedCallback(evt => OnRegionColorChanged(index, evt.newValue));
            header.Add(swatch);

            var idField = new TextField
            {
                value = def.Id,
                isDelayed = true,
                tooltip = "Region identifier. Matches ScienceRegionId on discoverables and the localization key under Science/Regions/.",
                style = { flexGrow = 1f, flexShrink = 1f, minWidth = 60f },
            };
            idField.RegisterValueChangedCallback(evt => OnRegionIdChanged(index, evt.newValue));
            header.Add(idField);

            // Separate Label + plain IntegerField so the field's built-in label doesn't fight the
            // inline row layout (it tries to claim 40% of width and overflows in narrow rows).
            var mapIdLabel = new Label("MapId")
            {
                style = { marginLeft = 8f, marginRight = 4f, unityTextAlign = TextAnchor.MiddleLeft },
            };
            header.Add(mapIdLabel);

            var mapIdField = new IntegerField
            {
                value = def.MapId,
                isDelayed = true,
                tooltip = "Byte index written into the baked map. Each region needs a unique MapId in [1, 255].",
                style = { width = 44f, flexShrink = 0f },
            };
            mapIdField.RegisterValueChangedCallback(evt => OnRegionMapIdChanged(index, evt.newValue));
            header.Add(mapIdField);

            var deleteBtn = new Button(() => OnRegionDeleteClicked(index))
            {
                text = "X",
                tooltip = "Delete this region.",
                style = { width = 22f, flexShrink = 0f, marginLeft = 4f },
            };
            header.Add(deleteBtn);

            row.Add(header);

            var scalars = new VisualElement();
            scalars.AddToClassList("science-region-region-row-scalars");
            scalars.Add(BuildScalarField("Atm", def.AtmosphereScalar, v => OnRegionAtmChanged(index, v)));
            scalars.Add(BuildScalarField("Splash", def.SplashedScalar, v => OnRegionSplChanged(index, v)));
            scalars.Add(BuildScalarField("Land", def.LandedScalar, v => OnRegionLndChanged(index, v)));
            row.Add(scalars);

            if (collisionIdx >= 0)
            {
                var warn = new Label($"Color within {ColorCollisionTolerance:0.00} of '{all[collisionIdx].Id}' (MapId {all[collisionIdx].MapId}). Bake may misclassify boundary pixels.");
                warn.AddToClassList("science-region-region-row-warning");
                row.Add(warn);
            }
            return row;
        }

        private static VisualElement BuildScalarField(string label, float value, Action<float> onChange)
        {
            // Each scalar is a label + plain FloatField inside a flex-grow container so all three
            // share row width equally without each FloatField fighting its own internal label slot.
            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexGrow = 1f,
                    flexBasis = 0f,
                    marginRight = 6f,
                },
            };

            var lbl = new Label(label)
            {
                style = { minWidth = 44f, unityTextAlign = TextAnchor.MiddleLeft },
            };
            container.Add(lbl);

            var field = new FloatField
            {
                value = value,
                isDelayed = true,
                style = { flexGrow = 1f, minWidth = 30f },
            };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            container.Add(field);

            return container;
        }

        private int FindNearestColorCollision(
            ScienceRegionData.ExtendedScienceRegionDefinition self,
            int selfIndex,
            ScienceRegionData.ExtendedScienceRegionDefinition[] all)
        {
            // Only baked-map regions participate in the source-map nearest-color match. Discoverable
            // regions (MapId < 0) have no pixel coverage, so colliding with one doesn't affect the
            // bake at all. Skip them on both sides of the comparison.
            if (self == null || self.MapId < 0) return -1;
            var toleranceSq = ColorCollisionTolerance * ColorCollisionTolerance * 3f;
            for (var i = 0; i < all.Length; i++)
            {
                if (i == selfIndex) continue;
                if (all[i] == null || all[i].MapId < 0) continue;
                var other = all[i].RegionColor;
                var dr = self.RegionColor.r - other.r;
                var dg = self.RegionColor.g - other.g;
                var db = self.RegionColor.b - other.b;
                if (dr * dr + dg * dg + db * db <= toleranceSq) return i;
            }
            return -1;
        }

        private void OnRegionColorChanged(int index, Color value)
        {
            MutateDefinitions((defs) => defs[index].RegionColor = value, "Set region color");
            RefreshRegionsSection();
        }

        private void OnRegionIdChanged(int index, string value)
        {
            MutateDefinitions((defs) => defs[index].Id = value, "Set region Id");
        }

        private void OnRegionMapIdChanged(int index, int value)
        {
            MutateDefinitions((defs) => defs[index].MapId = Mathf.Clamp(value, 0, 255), "Set region MapId");
        }

        private void OnRegionAtmChanged(int index, float value)
        {
            MutateDefinitions((defs) => defs[index].AtmosphereScalar = value, "Set atmosphere scalar");
        }

        private void OnRegionSplChanged(int index, float value)
        {
            MutateDefinitions((defs) => defs[index].SplashedScalar = value, "Set splashed scalar");
        }

        private void OnRegionLndChanged(int index, float value)
        {
            MutateDefinitions((defs) => defs[index].LandedScalar = value, "Set landed scalar");
        }

        private void OnRegionDeleteClicked(int index)
        {
            if (Target?.information?.ScienceRegionDefinitions == null) return;
            if (!EditorUtility.DisplayDialog(
                    "Delete region",
                    $"Delete region '{Target.information.ScienceRegionDefinitions[index].Id}'?",
                    "Delete", "Cancel"))
                return;
            Undo.RecordObject(Target, "Delete region");
            var defs = Target.information.ScienceRegionDefinitions;
            var next = new ScienceRegionData.ExtendedScienceRegionDefinition[defs.Length - 1];
            Array.Copy(defs, 0, next, 0, index);
            Array.Copy(defs, index + 1, next, index, defs.Length - index - 1);
            Target.information.ScienceRegionDefinitions = next;
            EditorUtility.SetDirty(Target);
            RefreshRegionsSection();
        }

        private void OnAddRegionClicked()
        {
            if (Target == null) return;
            if (Target.information == null)
                Target.information = new ScienceRegionData.ScienceRegionDataInformation();

            Undo.RecordObject(Target, "Add region");
            var defs = Target.information.ScienceRegionDefinitions ?? Array.Empty<ScienceRegionData.ExtendedScienceRegionDefinition>();
            var nextMapId = 1;
            foreach (var d in defs)
            {
                nextMapId = Mathf.Max(nextMapId, d.MapId + 1);
            }
            var next = new ScienceRegionData.ExtendedScienceRegionDefinition[defs.Length + 1];
            Array.Copy(defs, next, defs.Length);
            next[^1] = new ScienceRegionData.ExtendedScienceRegionDefinition
            {
                Id = $"Region {defs.Length + 1}",
                MapId = nextMapId,
                AtmosphereScalar = 1f,
                SplashedScalar = 1f,
                LandedScalar = 1f,
                RegionColor = Color.gray,
            };
            Target.information.ScienceRegionDefinitions = next;
            EditorUtility.SetDirty(Target);
            RefreshRegionsSection();
        }

        private void MutateDefinitions(Action<ScienceRegionData.ExtendedScienceRegionDefinition[]> mutator, string undoLabel)
        {
            if (Target?.information?.ScienceRegionDefinitions == null) return;
            Undo.RecordObject(Target, undoLabel);
            mutator(Target.information.ScienceRegionDefinitions);
            EditorUtility.SetDirty(Target);
        }

        // ----- Discoverables section ----------------------------------------

        private void RefreshDiscoverablesSection()
        {
            if (Target == null || _discoverablesList == null) return;
            _discoverablesList.Clear();

            var defs = Target.information?.ScienceRegionDefinitions;
            var discoverableRegionCount = CountDiscoverableRegions(defs);
            var orphanPositionCount = CountOrphanPositions(Target.discoverables, defs);

            _discoverablesSummaryLabel.text = discoverableRegionCount == 0 && orphanPositionCount == 0
                ? "No discoverables. Use '+ Place new on planet' below or the Discoverable Manager to add some."
                : $"{discoverableRegionCount} discoverable{(discoverableRegionCount == 1 ? string.Empty : "s")}. Edit the name to rename the discoverable.";

            _discoverablesList.Add(BuildShowOrbsToggle());

            if (defs != null)
            {
                for (var i = 0; i < defs.Length; i++)
                {
                    if (defs[i] == null || defs[i].MapId >= 0) continue;
                    var capturedIndex = i;
                    _discoverablesList.Add(BuildMergedDiscoverableRow(defs[i], capturedIndex));
                }
            }

            // Positions that reference a non-discoverable region (orphan from new flow).
            // Surface them so the artist can clean up legacy data that referenced baked regions.
            if (orphanPositionCount > 0 && Target.discoverables != null)
            {
                var orphanHeader = new Label("Orphan positions (point at a baked or missing region)")
                {
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Bold,
                        color = new Color(1.0f, 0.75f, 0.45f),
                        marginTop = 8f,
                        marginBottom = 2f,
                    },
                };
                _discoverablesList.Add(orphanHeader);

                for (var i = 0; i < Target.discoverables.Count; i++)
                {
                    var pos = Target.discoverables[i];
                    if (pos == null) continue;
                    if (HasDiscoverableRegion(defs, pos.ScienceRegionId)) continue;
                    var capturedIndex = i;
                    _discoverablesList.Add(BuildOrphanPositionRow(pos, capturedIndex));
                }
            }
        }

        private static int CountDiscoverableRegions(ScienceRegionData.ExtendedScienceRegionDefinition[] defs)
        {
            if (defs == null) return 0;
            var n = 0;
            foreach (var def in defs)
            {
                if (def != null && def.MapId < 0)
                {
                    n++;
                }
            }
            return n;
        }

        private static int CountOrphanPositions(
            List<CelestialBodyDiscoverablePosition> positions,
            ScienceRegionData.ExtendedScienceRegionDefinition[] defs)
        {
            if (positions == null) return 0;
            var n = 0;
            foreach (var p in positions)
            {
                if (p == null) continue;
                if (!HasDiscoverableRegion(defs, p.ScienceRegionId))
                {
                    n++;
                }
            }
            return n;
        }

        private static bool HasDiscoverableRegion(
            ScienceRegionData.ExtendedScienceRegionDefinition[] defs, string regionId)
        {
            if (defs == null || string.IsNullOrEmpty(regionId)) return false;
            foreach (var def in defs)
            {
                if (def != null && def.MapId < 0 && def.Id == regionId) return true;
            }
            return false;
        }

        private VisualElement BuildMergedDiscoverableRow(
            ScienceRegionData.ExtendedScienceRegionDefinition region, int regionIndex)
        {
            var row = new VisualElement();
            row.AddToClassList("science-region-region-row");

            // Top line: editable name + delete.
            var top = new VisualElement();
            top.AddToClassList("science-region-region-row-header");

            var nameField = new TextField
            {
                value = region.Id,
                isDelayed = true,
                tooltip = "Discoverable name (also the region's Id, used as the localization key under Science/Regions/). Renaming cascades to every position that references this discoverable.",
                style = { flexGrow = 1f, minWidth = 60f },
            };
            nameField.RegisterValueChangedCallback(evt => OnDiscoverableRegionRenamed(regionIndex, evt.previousValue, evt.newValue));
            top.Add(nameField);

            var posIndex = FindMatchingPositionIndex(region.Id);
            var del = new Button(() => OnDiscoverableCascadeDelete(regionIndex, region.Id))
            {
                text = "X",
                tooltip = "Delete this discoverable (region + position).",
                style = { width = 22f, flexShrink = 0f },
            };
            top.Add(del);
            row.Add(top);

            // Scalars line: Atm / Splash / Land.
            var scalars = new VisualElement();
            scalars.AddToClassList("science-region-region-row-scalars");
            scalars.Add(BuildScalarField("Atm", region.AtmosphereScalar, v => OnRegionAtmChanged(regionIndex, v)));
            scalars.Add(BuildScalarField("Splash", region.SplashedScalar, v => OnRegionSplChanged(regionIndex, v)));
            scalars.Add(BuildScalarField("Land", region.LandedScalar, v => OnRegionLndChanged(regionIndex, v)));
            row.Add(scalars);

            // Position section: aligned Lat / Lon fields, then Pick + Copy/Paste + Radius/Go,
            // mirroring the Preview Controls "Surface point" layout so artists find both surfaces
            // familiar.
            var positionSection = new VisualElement
            {
                style = { marginTop = 6f },
            };
            if (posIndex >= 0)
            {
                var pos = Target.discoverables[posIndex];
                var (lat, lon) = ComputeLatLon(pos);
                var capturedPosIndex = posIndex;

                var latField = new FloatField("Latitude (°)")
                {
                    value = (float)lat,
                    isDelayed = true,
                    tooltip = "Surface point latitude in degrees. -90 = south pole, +90 = north pole.",
                };
                latField.AddToClassList("sdk-field");
                latField.AddToClassList("unity-base-field__aligned");
                latField.RegisterValueChangedCallback(evt =>
                    OnDiscoverableLatLonEdited(capturedPosIndex, evt.newValue, (float)ComputeLatLon(Target.discoverables[capturedPosIndex]).lon));
                positionSection.Add(latField);

                var lonField = new FloatField("Longitude (°)")
                {
                    value = (float)lon,
                    isDelayed = true,
                    tooltip = "Surface point longitude in degrees. East-positive, range [-180, 180].",
                };
                lonField.AddToClassList("sdk-field");
                lonField.AddToClassList("unity-base-field__aligned");
                lonField.RegisterValueChangedCallback(evt =>
                    OnDiscoverableLatLonEdited(capturedPosIndex, (float)ComputeLatLon(Target.discoverables[capturedPosIndex]).lat, evt.newValue));
                positionSection.Add(lonField);

                var altitude = DecomposeAltitude(pos);
                var altField = new FloatField("Altitude (m)")
                {
                    value = (float)altitude,
                    isDelayed = true,
                    tooltip = "Height above the rendered terrain at this lat/lon. 0 snaps to surface. Use a positive value for a floating discoverable (e.g., an orbital beacon). Editing lat/lon preserves this altitude.",
                };
                altField.AddToClassList("sdk-field");
                altField.AddToClassList("unity-base-field__aligned");
                altField.RegisterValueChangedCallback(evt => OnDiscoverableAltitudeEdited(capturedPosIndex, evt.newValue));
                positionSection.Add(altField);

                var pickBtn = new Button(() => OnDiscoverablePickClicked(capturedPosIndex))
                {
                    text = "Pick on planet",
                    tooltip = "Activate the surface-pick tool. Click on the planet to set this discoverable's lat/lon from the surface hit.",
                };
                pickBtn.AddToClassList("sdk-action-button");
                positionSection.Add(pickBtn);

                var copyPasteRow = new VisualElement();
                copyPasteRow.AddToClassList("sdk-button-row");
                var copyBtn = new Button(() => OnDiscoverableCopyClicked(capturedPosIndex))
                {
                    text = "Copy",
                    tooltip = "Copy lat,lon to the clipboard as a comma-separated pair.",
                };
                copyPasteRow.Add(copyBtn);
                var pasteBtn = new Button(() => OnDiscoverablePasteClicked(capturedPosIndex))
                {
                    text = "Paste",
                    tooltip = "Paste a comma-separated lat,lon pair from the clipboard, replacing this discoverable's position.",
                };
                copyPasteRow.Add(pasteBtn);
                positionSection.Add(copyPasteRow);

                var radiusField = new FloatField("Radius (m)")
                {
                    value = (float)pos.Radius,
                    isDelayed = true,
                    tooltip = "Detection radius in meters. A vessel within this radius of the discoverable's surface position triggers this discoverable's region scalars.",
                };
                radiusField.AddToClassList("sdk-field");
                radiusField.AddToClassList("unity-base-field__aligned");
                radiusField.RegisterValueChangedCallback(evt => OnDiscoverablePositionRadiusChanged(capturedPosIndex, evt.newValue));
                positionSection.Add(radiusField);

                // Two framing modes: top-down disc view (Above) and 1 m ground-level view (Surface).
                // Two buttons in a row, equal width, matching the Copy/Paste pattern.
                var goRow = new VisualElement();
                goRow.AddToClassList("sdk-button-row");
                var aboveBtn = new Button(() => FrameAbove(Target.discoverables[capturedPosIndex]))
                {
                    text = "Look from above",
                    tooltip = "Frame the SceneView camera looking down at this discoverable. Altitude scales with the discoverable's radius so the disc fills the view.",
                };
                goRow.Add(aboveBtn);
                var surfaceBtn = new Button(() => FrameFromSurface(Target.discoverables[capturedPosIndex]))
                {
                    text = "Look from surface",
                    tooltip = "Frame the SceneView camera 1 m above the surface at this discoverable's lat/lon. Useful for inspecting the actual terrain the discoverable sits on.",
                };
                goRow.Add(surfaceBtn);
                positionSection.Add(goRow);
            }
            else
            {
                var noPos = new Label("(no position placed - re-place via the Place Discoverable tool)")
                {
                    style =
                    {
                        color = new Color(1.0f, 0.75f, 0.45f),
                        unityFontStyleAndWeight = FontStyle.Italic,
                    },
                };
                positionSection.Add(noPos);
            }
            row.Add(positionSection);
            return row;
        }

        private static VisualElement BuildLabeledFloat(string label, float value, float fieldWidth, Action<float> onChange)
        {
            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginRight = 6f,
                },
            };

            var lbl = new Label(label)
            {
                style = { marginRight = 4f, unityTextAlign = TextAnchor.MiddleLeft },
            };
            container.Add(lbl);

            var field = new FloatField
            {
                value = value,
                isDelayed = true,
                style = { width = fieldWidth },
            };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            container.Add(field);

            return container;
        }

        private static (double lat, double lon) ComputeLatLon(CelestialBodyDiscoverablePosition pos)
        {
            // Body-local position is stored in the LatLon convention: SphericalVector then SwapYAndZ
            // yields (cos(lat)*cos(lon), sin(lat), cos(lat)*sin(lon)). Inverse: lat = asin(y/r),
            // lon = atan2(z, x). Earlier my atan2 args were swapped, which produced bogus longitudes.
            Vector3d p = pos.Position;
            var r = Math.Sqrt(p.x * p.x + p.y * p.y + p.z * p.z);
            if (r < 1e-3) return (0, 0);
            var lat = Math.Asin(p.y / r) * 180.0 / Math.PI;
            var lon = Math.Atan2(p.z, p.x) * 180.0 / Math.PI;
            return (lat, lon);
        }

        // Lat/lon edits preserve the discoverable's existing altitude above surface so an artist
        // who deliberately floated one (e.g., a low-orbit beacon) doesn't get snapped back to the
        // ground when they nudge longitude. The Pick handler resets altitude to 0 because the
        // surface hit IS the "where I clicked" intent.
        private void OnDiscoverableLatLonEdited(int posIndex, float lat, float lon)
        {
            if (Target?.discoverables == null || posIndex < 0 || posIndex >= Target.discoverables.Count) return;
            var altitude = DecomposeAltitude(Target.discoverables[posIndex]);
            SetDiscoverablePosition(posIndex, lat, lon, altitude);
        }

        private void OnDiscoverableAltitudeEdited(int posIndex, float altitude)
        {
            if (Target?.discoverables == null || posIndex < 0 || posIndex >= Target.discoverables.Count) return;
            var (lat, lon) = ComputeLatLon(Target.discoverables[posIndex]);
            SetDiscoverablePosition(posIndex, lat, lon, altitude);
        }

        private void SetDiscoverablePosition(int posIndex, double lat, double lon, double altitudeAboveSurface)
        {
            var localRadial = LatLon.GetRelSurfaceNVector(lat, lon);
            var terrainDistance = SampleTerrainDistance(localRadial);
            if (terrainDistance <= 0) terrainDistance = Target.discoverables[posIndex].Position.magnitude;
            Undo.RecordObject(Target, "Move discoverable");
            Target.discoverables[posIndex].Position = localRadial.normalized * (terrainDistance + altitudeAboveSurface);
            EditorUtility.SetDirty(Target);
            RefreshDiscoverablesSection();
        }

        private static double SampleTerrainDistance(Vector3d localRadial)
        {
            var planet = PlanetAuthoringSession.Active?.Pqs;
            if (planet == null) return 0;
            var t = planet.GetSurfaceHeight(localRadial.normalized, true);
            if (t > 0) return t;
            return BodyResolver.FindBody(planet)?.Data?.radius ?? 0;
        }

        private static double DecomposeAltitude(CelestialBodyDiscoverablePosition pos)
        {
            var (lat, lon) = ComputeLatLon(pos);
            var localRadial = LatLon.GetRelSurfaceNVector(lat, lon);
            var terrainDistance = SampleTerrainDistance(localRadial);
            if (terrainDistance <= 0) return 0;
            return pos.Position.magnitude - terrainDistance;
        }

        private void OnDiscoverablePickClicked(int posIndex)
        {
            if (PlanetAuthoringSession.Active?.Pqs == null)
            {
                EditorUtility.DisplayDialog(
                    "Pick on planet",
                    "Start a planet preview session first. Surface picking needs an active body.",
                    "OK");
                return;
            }
            var captured = posIndex;
            // Pick resets altitude to 0 because the surface hit IS the artist's intent ("place
            // here"). To preserve a floating altitude, edit lat/lon via the FloatFields instead.
            PlanetSurfacePickTool.Begin(latLon => SetDiscoverablePosition(captured, latLon.x, latLon.y, 0));
        }

        private void OnDiscoverableCopyClicked(int posIndex)
        {
            if (Target?.discoverables == null || posIndex < 0 || posIndex >= Target.discoverables.Count) return;
            var (lat, lon) = ComputeLatLon(Target.discoverables[posIndex]);
            EditorGUIUtility.systemCopyBuffer =
                $"{lat.ToString("0.000000", CultureInfo.InvariantCulture)},{lon.ToString("0.000000", CultureInfo.InvariantCulture)}";
        }

        private void OnDiscoverablePasteClicked(int posIndex)
        {
            var clip = EditorGUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(clip)) return;
            var parts = clip.Split(',');
            if (parts.Length != 2) return;
            if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)) return;
            if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)) return;
            OnDiscoverableLatLonEdited(posIndex, lat, lon);
        }

        private VisualElement BuildOrphanPositionRow(CelestialBodyDiscoverablePosition pos, int index)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingTop = 2f,
                    paddingBottom = 2f,
                    paddingLeft = 4f,
                    marginBottom = 1f,
                    backgroundColor = new Color(0.22f, 0.18f, 0.16f, 0.45f),
                    borderTopLeftRadius = 2f,
                    borderTopRightRadius = 2f,
                    borderBottomLeftRadius = 2f,
                    borderBottomRightRadius = 2f,
                },
            };

            var label = new Label($"-> '{pos.ScienceRegionId ?? "(null)"}'   {FormatLatLon(pos)}   r {pos.Radius:0}m")
            {
                style = { flexGrow = 1f },
            };
            row.Add(label);

            var del = new Button(() => OnDiscoverableDeleteClicked(index))
            {
                text = "X",
                tooltip = "Delete this orphan position. The region it pointed at (if any) is left alone.",
                style = { width = 22f },
            };
            row.Add(del);
            return row;
        }

        private int FindMatchingPositionIndex(string regionId)
        {
            if (string.IsNullOrEmpty(regionId) || Target.discoverables == null) return -1;
            for (var i = 0; i < Target.discoverables.Count; i++)
            {
                if (Target.discoverables[i] != null && Target.discoverables[i].ScienceRegionId == regionId)
                {
                    return i;
                }
            }
            return -1;
        }

        private static string FormatLatLon(CelestialBodyDiscoverablePosition pos)
        {
            Vector3d p = pos.Position;
            var r = Math.Sqrt(p.x * p.x + p.y * p.y + p.z * p.z);
            if (r < 1e-3) return "(at body center)";
            var lat = Math.Asin(p.y / r) * 180.0 / Math.PI;
            var lon = Math.Atan2(p.x, p.z) * 180.0 / Math.PI;
            return $"({lat:0.00}°, {lon:0.00}°)";
        }

        private void OnDiscoverableRegionRenamed(int regionIndex, string oldId, string newId)
        {
            if (Target?.information?.ScienceRegionDefinitions == null) return;
            if (string.IsNullOrEmpty(newId) || newId == oldId) return;
            Undo.RecordObject(Target, "Rename discoverable");
            Target.information.ScienceRegionDefinitions[regionIndex].Id = newId;
            // Cascade rename to every position that referenced the old id so they stay linked.
            if (Target.discoverables != null)
            {
                foreach (var pos in Target.discoverables)
                {
                    if (pos != null && pos.ScienceRegionId == oldId)
                    {
                        pos.ScienceRegionId = newId;
                    }
                }
            }
            EditorUtility.SetDirty(Target);
            RefreshDiscoverablesSection();
        }

        private void OnDiscoverablePositionRadiusChanged(int posIndex, float value)
        {
            if (Target?.discoverables == null || posIndex < 0 || posIndex >= Target.discoverables.Count) return;
            Undo.RecordObject(Target, "Edit discoverable radius");
            Target.discoverables[posIndex].Radius = Math.Max(0.0, value);
            EditorUtility.SetDirty(Target);
        }

        private void OnDiscoverableCascadeDelete(int regionIndex, string regionId)
        {
            if (Target?.information?.ScienceRegionDefinitions == null) return;
            if (!EditorUtility.DisplayDialog(
                    "Delete discoverable",
                    $"Delete discoverable '{regionId}'? Both the region and its position will be removed.",
                    "Delete", "Cancel"))
            {
                return;
            }
            Undo.RecordObject(Target, "Delete discoverable");
            var defs = Target.information.ScienceRegionDefinitions;
            var next = new ScienceRegionData.ExtendedScienceRegionDefinition[defs.Length - 1];
            Array.Copy(defs, 0, next, 0, regionIndex);
            Array.Copy(defs, regionIndex + 1, next, regionIndex, defs.Length - regionIndex - 1);
            Target.information.ScienceRegionDefinitions = next;
            // Remove all positions referencing this region.
            if (Target.discoverables != null)
            {
                Target.discoverables.RemoveAll(p => p != null && p.ScienceRegionId == regionId);
            }
            EditorUtility.SetDirty(Target);
            RefreshDiscoverablesSection();
        }

        // Top-down framing scaled to the discoverable's radius so the whole disc fills the view.
        private void FrameAbove(CelestialBodyDiscoverablePosition pos)
        {
            var (lat, lon) = ComputeLatLon(pos);
            var altitude = Math.Max(100.0, pos.Radius * 3.0);
            SceneViewFraming.FrameAtLatLonAndAltitude(PlanetAuthoringSession.Active?.Pqs, lat, lon, altitude);
        }

        // Ground-level framing at the user-configured surface altitude (Preview Controls slider).
        // Sharing the value keeps both Surface jump buttons in agreement, and the user can dial it
        // up if 2 m still clips terrain at the body they are authoring.
        private void FrameFromSurface(CelestialBodyDiscoverablePosition pos)
        {
            var (lat, lon) = ComputeLatLon(pos);
            SceneViewFraming.FrameAtLatLonAndAltitude(PlanetAuthoringSession.Active?.Pqs, lat, lon, SurfaceFramingPrefs.AltitudeMeters, SceneFramingMode.Surface);
        }

        private void OnDiscoverableDeleteClicked(int index)
        {
            if (Target?.discoverables == null || index < 0 || index >= Target.discoverables.Count) return;
            if (!EditorUtility.DisplayDialog(
                    "Delete discoverable",
                    $"Delete discoverable '{Target.discoverables[index].ScienceRegionId ?? "(unset)"}' at index {index}?",
                    "Delete", "Cancel"))
            {
                return;
            }
            Undo.RecordObject(Target, "Delete discoverable");
            Target.discoverables.RemoveAt(index);
            EditorUtility.SetDirty(Target);
            RefreshDiscoverablesSection();
        }

        // ----- Bake section -------------------------------------------------

        private void RefreshBakeStatus()
        {
            if (Target == null || _bakeStatusLabel == null) return;
            RefreshBakePathsPreview();

            var baked = ScienceRegionAssetLocator.FindBakedMap(Target);
            if (baked == null)
            {
                _bakeStatusLabel.text = "Not yet baked.";
                _bakeStatusLabel.style.color = new Color(1.0f, 0.75f, 0.45f);
                return;
            }
            var bakedPath = AssetDatabase.GetAssetPath(baked);
            if (string.IsNullOrEmpty(bakedPath)) return;
            var fullPath = Path.GetFullPath(bakedPath);
            if (!File.Exists(fullPath))
            {
                _bakeStatusLabel.text = "Baked asset missing from disk.";
                return;
            }
            var bakeTime = File.GetLastWriteTime(fullPath);
            var stale = ScienceRegionAssetLocator.IsBakeStale(Target, baked);
            if (stale)
            {
                _bakeStatusLabel.text = $"Source modified since last bake ({bakeTime.ToString("g", CultureInfo.CurrentCulture)}). Re-bake recommended.";
                _bakeStatusLabel.style.color = new Color(1.0f, 0.75f, 0.45f);
            }
            else
            {
                _bakeStatusLabel.text = $"Last baked: {bakeTime.ToString("g", CultureInfo.CurrentCulture)}";
                _bakeStatusLabel.style.color = StyleKeyword.Null;
            }
        }

        private void RefreshBakePathsPreview()
        {
            if (_bakePathsLabel == null || Target == null) return;
            var body = Target.information?.BodyName;
            if (string.IsNullOrWhiteSpace(body))
            {
                _bakePathsLabel.text = "Bake will write next to this asset once Body name is set.";
                return;
            }
            var regions = ScienceRegionBaker.ComposeFileStem(body, ScienceRegionBaker.RegionsJsonSuffix);
            var discoverables = ScienceRegionBaker.ComposeFileStem(body, ScienceRegionBaker.DiscoverablesJsonSuffix);
            var baked = ScienceRegionBaker.ComposeFileStem(body, ScienceRegionBaker.BakedMapSuffix);
            _bakePathsLabel.text = $"Writes next to this asset: {regions}.json, {discoverables}.json, {baked}.asset";
        }

        private void OnBakeClicked()
        {
            if (Target == null) return;
            var bakedPath = ScienceRegionBaker.Bake(Target);
            if (!string.IsNullOrEmpty(bakedPath))
            {
                Debug.Log($"[ScienceRegionEditor] Baked '{Target.name}' to {bakedPath}.");
            }
            RefreshBakeStatus();
        }

        private void OnImportAndClusterClicked()
        {
            ImportAndClusterColorsWindow.Open(Target);
        }

        private void OnPlaceDiscoverableClicked()
        {
            if (Target == null) return;
            if (PlanetAuthoringSession.Active?.Pqs == null)
            {
                EditorUtility.DisplayDialog(
                    "Place Discoverable",
                    "Start a planet preview session first. The Place Discoverable tool needs an active body to click on.",
                    "OK");
                return;
            }
            PlaceDiscoverableTool.Begin(Target);
        }

        private void OnOpenDiscoverableManagerClicked()
        {
            SurfaceManagerWindow.ShowWindow();
        }

        // ----- Scene-view discoverable handles + orbs -----------------------

        private VisualElement BuildShowOrbsToggle()
        {
            var sidecar = PlanetAuthoringRegistry.Instance.GetOrCreateScienceRegion(Target);
            var toggle = new Toggle("Show orbs in scene")
            {
                value = sidecar == null || sidecar.ShowDiscoverableOrbs,
                tooltip = "Render each discoverable as a sphere (sized by its Radius) in the Scene view. Free-move handles and name labels stay visible either way.",
            };
            toggle.AddToClassList("sdk-field");
            toggle.RegisterValueChangedCallback(evt =>
            {
                var s = PlanetAuthoringRegistry.Instance.GetOrCreateScienceRegion(Target);
                if (s == null) return;
                Undo.RecordObject(s, "Toggle discoverable orbs");
                s.ShowDiscoverableOrbs = evt.newValue;
                EditorUtility.SetDirty(s);
                SceneView.RepaintAll();
            });
            return toggle;
        }

        // Uniform color for every orb and handle. The name label next to each one disambiguates,
        // so we don't need per-discoverable color cycling or a legend.
        private static readonly Color OrbColor = new(0.55f, 0.85f, 1.0f);

        private void OnSceneViewGui(SceneView _)
        {
            if (Target?.discoverables == null || Target.discoverables.Count == 0) return;
            var session = PlanetAuthoringSession.Active;
            var planet = session?.Pqs;
            if (planet == null) return;
            var body = BodyResolver.FindBody(planet);
            if (body == null) return;

            var showOrbs = PlanetAuthoringRegistry.Instance.FindScienceRegion(Target)?.ShowDiscoverableOrbs ?? true;

            for (var i = 0; i < Target.discoverables.Count; i++)
            {
                var pos = Target.discoverables[i];
                if (pos == null) continue;
                var worldPos = body.transform.position + body.transform.rotation * (Vector3)pos.Position;

                if (showOrbs && pos.Radius > 0)
                {
                    DrawDiscoverableOrb(worldPos, (float)pos.Radius);
                }
                DrawDiscoverableLabel(worldPos, pos.ScienceRegionId);
                HandleDiscoverableMove(i, pos, worldPos, planet);
            }
        }

        // Three perpendicular great-circle wireframes give a recognizable sphere outline at the
        // discoverable's Radius without the cost of a solid mesh.
        private static void DrawDiscoverableOrb(Vector3 center, float radius)
        {
            Handles.color = new Color(OrbColor.r, OrbColor.g, OrbColor.b, 0.18f);
            Handles.DrawSolidDisc(center, Vector3.up, radius);
            Handles.color = new Color(OrbColor.r, OrbColor.g, OrbColor.b, 0.85f);
            Handles.DrawWireDisc(center, Vector3.up, radius);
            Handles.DrawWireDisc(center, Vector3.right, radius);
            Handles.DrawWireDisc(center, Vector3.forward, radius);
        }

        private static GUIStyle _labelFg;
        private static GUIStyle _labelShadow;
        private static GUIStyle LabelFg => _labelFg ??= new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white } };
        private static GUIStyle LabelShadow => _labelShadow ??= new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.black } };

        // Draw the discoverable name in screen space so we can stack a black drop-shadow under a
        // white bold label. Handles.Label respects Handles.color but has no background or shadow,
        // and the editor's bold yellow loses contrast against tan/rocky terrain.
        private static void DrawDiscoverableLabel(Vector3 worldPos, string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var guiPos = HandleUtility.WorldToGUIPoint(worldPos);
            if (float.IsInfinity(guiPos.x) || float.IsInfinity(guiPos.y)) return;
            var size = LabelFg.CalcSize(new GUIContent(id));
            Handles.BeginGUI();
            var shadowRect = new Rect(guiPos.x + 1, guiPos.y + 1, size.x, size.y);
            var fgRect = new Rect(guiPos.x, guiPos.y, size.x, size.y);
            GUI.Label(shadowRect, id, LabelShadow);
            GUI.Label(fgRect, id, LabelFg);
            Handles.EndGUI();
        }

        private void HandleDiscoverableMove(int index, CelestialBodyDiscoverablePosition pos, Vector3 worldPos, PQS planet)
        {
            var refSize = HandleUtility.GetHandleSize(worldPos) * 0.08f;
            Handles.color = OrbColor;
            EditorGUI.BeginChangeCheck();
            _ = Handles.FreeMoveHandle(worldPos, refSize, Vector3.zero, Handles.SphereHandleCap);
            if (!EditorGUI.EndChangeCheck()) return;

            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            if (!PlanetSurfaceHit.TryHit(planet, ray, out _, out var hitLatLon, out _)) return;
            // Preserve the discoverable's current altitude so a deliberately-floated one stays
            // floating as it drags across the surface.
            var altitude = DecomposeAltitude(pos);
            SetDiscoverablePosition(index, hitLatLon.x, hitLatLon.y, altitude);
        }
    }

    /// <summary>
    /// Extension helpers for <see cref="VisualElement" /> used by the Science Region inspector.
    /// </summary>
    internal static class VisualElementExtensions
    {
        /// <summary>
        /// Adds <paramref name="className" /> to <paramref name="element" /> and returns the element for chaining.
        /// </summary>
        /// <param name="element">The element to add the class to.</param>
        /// <param name="className">The USS class name to add.</param>
        /// <typeparam name="T">The concrete <see cref="VisualElement" /> subtype.</typeparam>
        /// <returns>The same element for fluent chaining.</returns>
        public static T WithClass<T>(this T element, string className) where T : VisualElement
        {
            element.AddToClassList(className);
            return element;
        }
    }
}
