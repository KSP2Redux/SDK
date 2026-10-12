using System;
using System.Collections.Generic;
using System.Linq;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.Localization.Export;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Overlays;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.PlanetAuthoring.Validation;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Planet
{
    /// <summary>
    /// The planet inspector: one view of a whole celestial body, hosted on both its Scaled and its Local prefab.
    /// </summary>
    /// <remarks>
    /// The header carries identity, readiness chips, Quick Tools and the overlay pills. The tab bar swaps one tab's
    /// template into the content area at a time, as the part inspector does, so a tab's sections are only built
    /// while it is open.
    /// </remarks>
    public class PlanetInspectorView
    {
        private const string UXML_FOLDER = "/Assets/Windows/PlanetAuthoring/Inspectors/PlanetInspector/";
        private const string PQS_USS_PATH = "/Assets/Windows/PlanetAuthoring/Inspectors/PQSInspector.uss";
        private const string ACTIVE_TAB_KEY = "PlanetAuthoring.ActiveTab";
        private const string ACTIVE_CLASS = "is-active";
        private const string TAB_ACTIVE_CLASS = "sdk-tab--active";
        private const string LIVE_OK_CLASS = "planet-live-ok";
        private const int REFRESH_INTERVAL_MS = 1000;
        private const int MIN_SECTIONS_FOR_JUMP_ROW = 4;

        private static readonly (string Name, PreviewOverlayKind Kind)[] OVERLAYS =
        {
            ("overlay-biome", PreviewOverlayKind.BiomeMask),
            ("overlay-subzone", PreviewOverlayKind.SubzoneMask),
            ("overlay-slope", PreviewOverlayKind.Slope),
            ("overlay-altitude", PreviewOverlayKind.AltitudeBands),
            ("overlay-sea-level", PreviewOverlayKind.SeaLevel),
            ("overlay-active-layer", PreviewOverlayKind.ActiveLayer),
            ("overlay-science-region", PreviewOverlayKind.ScienceRegion),
            ("overlay-scatter-biome", PreviewOverlayKind.ScatterBiome),
        };

        private readonly List<Action> _tabRefreshes = new();
        private readonly List<Action> _drawRefreshes = new();
        private readonly Dictionary<ReadinessChip, Button> _chipButtons = new();
        private readonly List<ValidationIssue> _issues = new();

        private VisualElement _content;
        private VisualElement _jumpRow;
        private Button _jsonChip;
        private Button _galaxyChip;
        private Button _validationChip;
        private BodyJsonState _jsonState;
        private string _previewErrorKey;
        private PlanetInspectorTab _activeTab;

        private PlanetInspectorView(PlanetInspectorContext context)
        {
            Context = context;
            Root = new VisualElement();
        }

        /// <summary>
        /// Gets the bodies and serialized objects the inspector edits.
        /// </summary>
        public PlanetInspectorContext Context { get; }

        /// <summary>
        /// Gets the inspector's root element.
        /// </summary>
        public VisualElement Root { get; }

        /// <summary>
        /// Builds the inspector for <paramref name="context" />.
        /// </summary>
        /// <param name="context">The bodies and serialized objects to edit.</param>
        /// <returns>The inspector's root element.</returns>
        public static VisualElement Build(PlanetInspectorContext context)
        {
            var view = new PlanetInspectorView(context);
            view.Create();
            return view.Root;
        }

        /// <summary>
        /// Clones one of the planet inspector's templates into <paramref name="parent" />.
        /// </summary>
        /// <param name="parent">The element to clone into.</param>
        /// <param name="fileName">The template's file name, in the PlanetInspector folder.</param>
        /// <returns>True if the template loaded, false otherwise.</returns>
        public static bool CloneTemplate(VisualElement parent, string fileName)
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UXML_FOLDER + fileName);
            if (tree == null)
            {
                parent.Add(new HelpBox($"Failed to load {fileName}.", HelpBoxMessageType.Error));
                return false;
            }

            tree.CloneTree(parent);
            return true;
        }

        /// <summary>
        /// Shows a message under Quick Tools, or hides the line for an empty message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void SetStatus(string message) => ShowLabel(Root.Q<Label>("quick-tools-status"), message);

        /// <summary>
        /// Adds a refresh the open tab needs on the inspector's periodic refresh, dropped when the tab closes.
        /// </summary>
        /// <param name="refresh">The refresh.</param>
        public void AddTabRefresh(Action refresh)
        {
            _tabRefreshes.Add(refresh);
            refresh();
        }

        /// <summary>
        /// Opens <paramref name="tab" /> and unfolds and scrolls to one of its sections.
        /// </summary>
        /// <param name="tab">The tab.</param>
        /// <param name="section">The section foldout's name, or its text for a section built in code. Null scrolls to the tab's top.</param>
        public void JumpTo(PlanetInspectorTab tab, string section)
        {
            if (tab != _activeTab)
            {
                ShowTab(tab);
            }

            Foldout target = section == null
                ? null
                : _content.Q<Foldout>(section) ?? _content.Query<Foldout>().Where(foldout => foldout.text == section).First();
            ScrollTo(target, _content);
        }

        /// <summary>
        /// Rebuilds the open tab, for a change that alters which sections it has.
        /// </summary>
        public void RebuildTab() => ShowTab(_activeTab);

        private void Create()
        {
            if (!CloneTemplate(Root, "PlanetInspector.uxml"))
                return;

            Ksp2UnityToolsStyles.Apply(Root, PQS_USS_PATH, UXML_FOLDER + "PlanetInspector.uss");
            _content = Root.Q("planet-tab-content");
            _jumpRow = Root.Q("planet-jump-row");

            WireIdentity();
            WireReadiness();
            WireQuickTools();
            WireDraw();
            WireOverlays();
            WireTabBar();
            ApplyLiveHeader();

            // The body's class decides which tabs exist. Its fields are edited in General, so the tab bar follows them.
            foreach (string path in new[] { "core.data.isStar", "core.data.hasSolidSurface" })
            {
                SerializedProperty property = Context.BodyObject.FindProperty(path);
                if (property != null)
                {
                    Root.TrackPropertyValue(property, _ => OnBodyClassChanged());
                }
            }

            Root.TrackSerializedObjectValue(Context.BodyObject, _ => RefreshJsonState());
            Root.RegisterCallback<AttachToPanelEvent>(_ => Subscribe(true));
            Root.RegisterCallback<DetachFromPanelEvent>(_ => Subscribe(false));
            Root.schedule.Execute(Refresh).Every(REFRESH_INTERVAL_MS);

            RefreshJsonState();
            var remembered = (PlanetInspectorTab)SessionState.GetInt(ACTIVE_TAB_KEY, (int)PlanetInspectorTab.General);
            ShowTab(PlanetInspectorTabs.Resolve(remembered, Context.BodyClass));
            Refresh();
        }

        private void Subscribe(bool subscribe)
        {
            PreviewOverlayManager.StateChanged -= RefreshOverlays;
            ValidationExpensiveCache.Changed -= OnExpensiveCacheChanged;
            if (!subscribe)
                return;

            PreviewOverlayManager.StateChanged += RefreshOverlays;
            ValidationExpensiveCache.Changed += OnExpensiveCacheChanged;
        }

        private void OnExpensiveCacheChanged(CoreCelestialBodyData body) => RefreshReadiness();

        private void OnBodyClassChanged()
        {
            RefreshTabBar();
            if (!PlanetInspectorTabs.IsShown(_activeTab, Context.BodyClass))
            {
                ShowTab(PlanetInspectorTab.General);
            }
        }

        private void Refresh()
        {
            if (Context.Body == null)
                return;

            RefreshIdentity();
            if (Context.IsLive)
            {
                DisableLiveActions();
            }
            else
            {
                RefreshReadiness();
                RefreshPreview();
                RefreshDraw();
                RefreshOverlays();
            }

            foreach (Action refresh in _tabRefreshes)
            {
                refresh();
            }
        }

        // ----- Identity --------------------------------------------------------------------------------------------

        private void WireIdentity()
        {
            WireSide("editing-scaled", false);
            WireSide("editing-local", true);
        }

        private void WireSide(string name, bool local)
        {
            var button = Root.Q<Button>(name);
            if (button == null)
                return;

            button.EnableInClassList(ACTIVE_CLASS, Context.IsLocalSelected == local);
            button.SetEnabled(local ? Context.Pqs != null : Context.Body != null);
            button.clicked += () =>
            {
                if (Context.IsLocalSelected == local)
                    return;

                GameObject other = Context.OtherHalf();
                if (other != null)
                {
                    Selection.activeObject = other;
                }
            };
        }

        private void RefreshIdentity()
        {
            Root.Q<Label>("header-body-name").text = Context.BodyName;
            Root.Q<Label>("header-type-chip").text = Context.BodyClass switch
            {
                BodyClassFlags.Star => "Star",
                BodyClassFlags.GasGiant => "Gas giant",
                BodyClassFlags.SolidSurface => "Solid surface",
                _ => "No body data",
            };
            double radius = Context.Body.Data?.radius ?? 0.0;
            Root.Q<Label>("header-radius-chip").text = $"radius {radius:N0} m";

            var thumbnail = Root.Q("header-thumbnail");
            if (thumbnail.resolvedStyle.backgroundImage.texture != null)
                return;

            // The preview of the scaled material, which is generated in the background, so later refreshes retry.
            var renderer = Context.Body.GetComponent<MeshRenderer>();
            Material material = renderer != null ? renderer.sharedMaterial : null;
            Texture2D preview = material != null ? AssetPreview.GetAssetPreview(material) : null;
            if (preview != null)
            {
                thumbnail.style.backgroundImage = new StyleBackground(preview);
            }
        }

        // ----- Readiness -------------------------------------------------------------------------------------------

        private void WireReadiness()
        {
            VisualElement chips = Root.Q("readiness-chips");
            _jsonChip = NewChip(chips, () => JumpTo(PlanetInspectorTab.General, "section-output"));
            _galaxyChip = NewChip(chips, () => JumpTo(PlanetInspectorTab.General, "section-orbit"));
            foreach (ReadinessChip chip in PlanetReadiness.CHIPS)
            {
                ReadinessChip captured = chip;
                _chipButtons[chip] = NewChip(chips, () => JumpTo(captured.Tab, captured.Section));
            }

            _validationChip = NewChip(chips, () => Windows.ValidationReportWindow.Open(Context.Body));
            _validationChip.tooltip = "Open the Validation Report for this body.";
        }

        private static Button NewChip(VisualElement parent, Action onClick)
        {
            var button = new Button(onClick);
            button.AddToClassList("sdk-readiness-chip");
            parent.Add(button);
            return button;
        }

        private void RefreshJsonState()
        {
            // A live body was loaded from JSON rather than saved to it.
            if (Context.IsLive)
                return;

            _jsonState = BodyJsonExport.GetState(Context.Body);
            if (_jsonChip == null)
                return;

            SetChipState(_jsonChip, _jsonState == BodyJsonState.Saved ? ReadinessState.Ready : ReadinessState.Warning);
            _jsonChip.text = _jsonState switch
            {
                BodyJsonState.Saved => "Body JSON saved",
                BodyJsonState.OutOfDate => "Body JSON out of date",
                BodyJsonState.Missing => "Body JSON missing",
                _ => "Not a prefab",
            };
            _jsonChip.tooltip = _jsonState == BodyJsonState.NoPrefab
                ? "Save the body as a prefab to give it a JSON."
                : BodyJsonExport.GetPath(Context.Body);

            var status = Root.Q<Label>("json-status");
            if (status != null)
            {
                status.text = _jsonChip.text + ".";
            }
        }

        private void RefreshReadiness()
        {
            if (_validationChip == null || Context.Body == null)
                return;

            _issues.Clear();
            _issues.AddRange(PlanetValidationReport.Run(Context.Body, ValidatorCost.Cheap).Issues);
            _issues.AddRange(ValidationExpensiveCache.Get(Context.Body));

            // A full run reports a bake that never happened itself, so the cheap stand-in only covers the gap before one.
            if (_issues.All(issue => issue.Code != "SURFACE_BAKE_DRIFT"))
            {
                _issues.AddRange(PlanetReadiness.CheapSurfaceBakeIssues(Context));
            }

            RefreshGalaxyChip();
            var matched = new List<ValidationIssue>();
            foreach (KeyValuePair<ReadinessChip, Button> pair in _chipButtons)
            {
                bool applies = pair.Key.AppliesTo(Context);
                pair.Value.style.display = applies ? DisplayStyle.Flex : DisplayStyle.None;
                if (!applies)
                    continue;

                matched.Clear();
                ReadinessState state = PlanetReadiness.Evaluate(pair.Key, _issues, matched);
                SetChipState(pair.Value, state);
                pair.Value.text = PlanetReadiness.Describe(pair.Key, state, matched.Count);
                pair.Value.tooltip = matched.Count == 0
                    ? "Open the section."
                    : string.Join("\n", matched.Select(issue => issue.Message));
            }

            int errors = _issues.Count(issue => issue.Severity == ValidationSeverity.Error);
            int warnings = _issues.Count(issue => issue.Severity == ValidationSeverity.Warning);
            int info = _issues.Count(issue => issue.Severity == ValidationSeverity.Info);
            _validationChip.text = errors + warnings + info == 0 ? "No issues" : $"✕ {errors}  ·  ⚠ {warnings}  ·  ⓘ {info}";
            SetChipState(_validationChip, errors > 0 ? ReadinessState.Error : warnings + info > 0 ? ReadinessState.Warning : ReadinessState.Ready);
        }

        private void RefreshGalaxyChip()
        {
            bool applies = Context.BodyClass != BodyClassFlags.Star;
            Show(_galaxyChip, applies);
            if (!applies)
                return;

            GameObject prefab = Context.ScaledPrefabAsset();
            CelestialBodyOrbitAuthoring orbit = prefab != null ? AuthoringSidecars.FindOrbit(prefab) : null;
            GalaxyEntryState state = GalaxyEntry.GetState(orbit);
            bool ready = state is GalaxyEntryState.Written or GalaxyEntryState.BuiltWithMod;
            SetChipState(_galaxyChip, ready ? ReadinessState.Ready : ReadinessState.Warning);
            _galaxyChip.text = state switch
            {
                GalaxyEntryState.NoOrbit => "Not in galaxy",
                GalaxyEntryState.Missing => "Galaxy patch missing",
                GalaxyEntryState.OutOfDate => "Galaxy patch out of date",
                _ => "In galaxy",
            };
        }

        private static void SetChipState(VisualElement chip, ReadinessState state)
        {
            chip.EnableInClassList("is-ok", state == ReadinessState.Ready);
            chip.EnableInClassList("is-warn", state == ReadinessState.Warning);
            chip.EnableInClassList("is-error", state == ReadinessState.Error);
        }

        // ----- Quick Tools -----------------------------------------------------------------------------------------

        private void WireQuickTools()
        {
            Wire("quick-preview", () => SetStatus(PlanetPreviewLauncher.Toggle(Context.Body)));
            Wire("quick-preview-controls", Windows.PreviewControlsWindow.ShowWindow);
            Wire("quick-save-json", SaveJson);
            Wire("quick-export-localizations", () => LocExportFlow.RunForAsset(Context.Body));
            Wire("quick-bake-body-surface", () =>
            {
                var result = BodySurfaceBakeSection.BakeWithPersistedSettings(Context.Body);
                SetStatus(result.Success ? $"Baked to {result.ScaledFolder}." : $"Bake failed: {result.Error}");
            });
            Wire("quick-open-scene", () =>
            {
                CoreCelestialBodyData sceneBody = PlanetPreviewLauncher.OpenAuthoringScene(Context.Body);
                if (sceneBody == null)
                {
                    SetStatus("No authoring scene was found next to this body's prefab.");
                    return;
                }

                Selection.activeObject = Context.IsLocalSelected && BodyResolver.FindPqs(sceneBody) != null
                    ? BodyResolver.FindPqs(sceneBody).gameObject
                    : sceneBody.gameObject;
            });

            // Only a prefab asset has a scene to open. A scene instance is already in it.
            Show(Root.Q("quick-open-scene"), PrefabUtility.IsPartOfPrefabAsset(Context.Body));
        }

        /// <summary>
        /// Saves the body's JSON and reports where it went.
        /// </summary>
        public void SaveJson()
        {
            SetStatus(BodyJsonExport.Save(Context.Body));
            RefreshJsonState();
        }

        private void Wire(string name, Action handler)
        {
            var button = Root.Q<Button>(name);
            if (button != null)
            {
                button.clicked += handler;
            }
        }

        private void RefreshPreview()
        {
            var button = Root.Q<Button>("quick-preview");
            Show(Root.Q("quick-bake-body-surface"), Context.BodyClass == BodyClassFlags.SolidSurface);
            if (PlanetPreviewLauncher.IsPreviewing(Context.Body))
            {
                button.text = "Disable Preview";
                button.SetEnabled(true);
                ShowPreviewErrors(null);
                return;
            }

            PlanetAuthoringSession.ReadinessReport report = PlanetAuthoringSession.CheckReadiness(Context.Body);
            button.text = "Enable Preview";
            button.SetEnabled(report.IsReady);
            ShowPreviewErrors(report.IsReady ? null : report.Errors);
        }

        // Rebuilt only when the set of errors changes, so the Fix buttons are not replaced under the pointer.
        private void ShowPreviewErrors(IReadOnlyList<PlanetAuthoringSession.ReadinessError> errors)
        {
            string key = errors == null ? string.Empty : string.Join("\n", errors.Select(error => error.Message));
            if (key == _previewErrorKey)
                return;

            _previewErrorKey = key;
            VisualElement container = Root.Q("preview-readiness");
            container.Clear();
            if (errors == null)
                return;

            foreach (PlanetAuthoringSession.ReadinessError error in errors)
            {
                var row = new VisualElement();
                row.AddToClassList("body-inspector-error-row");
                var label = new Label($"Preview: {error.Message}");
                label.AddToClassList("body-inspector-error-label");
                label.AddToClassList("sdk-hint");
                row.Add(label);
                if (error.Code == PlanetAuthoringSession.ReadinessErrorCode.NoPqsData)
                {
                    var fix = new Button(CreateEmptyPqsData) { text = "Fix" };
                    fix.AddToClassList("body-inspector-error-fix");
                    row.Add(fix);
                }

                container.Add(row);
            }
        }

        private void CreateEmptyPqsData()
        {
            if (Context.Pqs == null)
                return;

            string prefabPath = AssetDatabase.GetAssetPath(Context.Body);
            if (string.IsNullOrEmpty(prefabPath))
            {
                prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(Context.Body.gameObject);
            }

            string folder = string.IsNullOrEmpty(prefabPath) ? "Assets" : System.IO.Path.GetDirectoryName(prefabPath)?.Replace('\\', '/');
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{PlanetAuthoringNaming.PqsData(Context.BodyName)}");
            var data = ScriptableObject.CreateInstance<PQSData>();
            AssetDatabase.CreateAsset(data, assetPath);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(Context.Pqs, "Assign PQSData");
            Context.Pqs.data = data;
            EditorUtility.SetDirty(Context.Pqs);
            _previewErrorKey = null;
            RebuildTab();
        }

        // ----- Draw ------------------------------------------------------------------------------------------------

        // What the preview draws is preview state rather than body data, so its toggles sit in the header with the
        // overlays rather than in the sections that author each feature.
        private void WireDraw()
        {
            _drawRefreshes.Add(PlanetSectionChrome.WireDrawToggle(Root.Q<Button>("draw-game-look"), session => session.GameLookDriver));
            _drawRefreshes.Add(PlanetSectionChrome.WireDrawToggle(Root.Q<Button>("draw-atmosphere"), session => session.AtmosphereDriver));
            _drawRefreshes.Add(PlanetSectionChrome.WireDrawToggle(Root.Q<Button>("draw-clouds"), session => session.CloudDriver));
            _drawRefreshes.Add(PlanetSectionChrome.WireDrawToggle(Root.Q<Button>("draw-ocean"), session => session.OceanDriver));
            _drawRefreshes.Add(PlanetSectionChrome.WireDrawToggle(Root.Q<Button>("draw-scatter"), session => session.ScatterDriver));
        }

        private void RefreshDraw()
        {
            bool solid = Context.BodyClass == BodyClassFlags.SolidSurface;
            bool notStar = Context.BodyClass != BodyClassFlags.Star;
            Show(Root.Q("draw-atmosphere"), notStar);
            Show(Root.Q("draw-clouds"), notStar);
            Show(Root.Q("draw-ocean"), solid);
            Show(Root.Q("draw-scatter"), solid);
            foreach (Action refresh in _drawRefreshes)
            {
                refresh();
            }
        }

        // ----- Overlays --------------------------------------------------------------------------------------------

        private void WireOverlays()
        {
            foreach ((string name, PreviewOverlayKind kind) in OVERLAYS)
            {
                Wire(name, () =>
                {
                    PreviewOverlayManager.SetEnabled(kind, !PreviewOverlayManager.IsEnabled(kind));
                    SceneView.RepaintAll();
                });
            }
        }

        private void RefreshOverlays()
        {
            bool solid = Context.BodyClass == BodyClassFlags.SolidSurface;
            Show(Root.Q("overlay-row"), solid);
            if (!solid)
                return;

            bool previewing = PlanetPreviewLauncher.IsPreviewing(Context.Body);
            foreach ((string name, PreviewOverlayKind kind) in OVERLAYS)
            {
                var pill = Root.Q<Button>(name);
                pill.SetEnabled(previewing);
                pill.EnableInClassList(ACTIVE_CLASS, previewing && PreviewOverlayManager.IsEnabled(kind));
                pill.tooltip = previewing ? null : "Start the preview to use overlays.";
            }
        }

        // ----- Tabs ------------------------------------------------------------------------------------------------

        private void WireTabBar()
        {
            foreach (PlanetInspectorTab tab in PlanetInspectorTabs.ALL)
            {
                PlanetInspectorTab captured = tab;
                Wire(TabButtonName(tab), () => ShowTab(captured));
            }

            RefreshTabBar();
        }

        private static string TabButtonName(PlanetInspectorTab tab) => $"tab-{tab.ToString().ToLowerInvariant()}";

        private void RefreshTabBar()
        {
            foreach (PlanetInspectorTab tab in PlanetInspectorTabs.ALL)
            {
                var button = Root.Q<Button>(TabButtonName(tab));
                Show(button, PlanetInspectorTabs.IsShown(tab, Context.BodyClass));
                button.EnableInClassList(TAB_ACTIVE_CLASS, tab == _activeTab);
            }
        }

        private void ShowTab(PlanetInspectorTab tab)
        {
            _activeTab = tab;
            SessionState.SetInt(ACTIVE_TAB_KEY, (int)tab);
            RefreshTabBar();

            _tabRefreshes.Clear();
            _content.Unbind();
            _content.Clear();
            _jumpRow.Clear();
            switch (tab)
            {
                case PlanetInspectorTab.General:
                    GeneralTab.Build(this, _content);
                    break;
                case PlanetInspectorTab.Terrain:
                    TerrainTab.Build(this, _content);
                    break;
                case PlanetInspectorTab.Surface:
                    SurfaceTab.Build(this, _content);
                    break;
                case PlanetInspectorTab.Environment:
                    EnvironmentTab.Build(this, _content);
                    break;
                case PlanetInspectorTab.Features:
                    FeaturesTab.Build(this, _content);
                    break;
                case PlanetInspectorTab.Science:
                    ScienceTab.Build(this, _content);
                    break;
            }

            PlanetSectionChrome.MoveActionsIntoHeaders(_content);
            if (Context.IsLive)
            {
                _content.Query(className: "planet-section-actions").ForEach(actions => Show(actions, false));
            }

            // Embedded inspectors build their sections once attached, so the links wait a frame for them.
            _content.schedule.Execute(BuildJumpRow).StartingIn(50);
        }

        // A live body is for cross-checking, so only its fields stay editable. Anything that writes files, adds or removes
        // a feature or drives the authoring preview is turned off.
        private void ApplyLiveHeader()
        {
            bool live = Context.IsLive;
            Show(Root.Q("live-banner"), live);
            foreach (string row in new[] { "readiness-row", "quick-tools-row", "draw-row", "overlay-row", "preview-readiness" })
            {
                if (live)
                {
                    Show(Root.Q(row), false);
                }
            }
        }

        // Runs again on every refresh, since embedded inspectors build their buttons after the tab opens and rebuild
        // some of them as their lists change.
        private void DisableLiveActions() =>
            _content.Query<Button>().ForEach(button => button.SetEnabled(button.ClassListContains(LIVE_OK_CLASS)));

        private void BuildJumpRow()
        {
            if (Context.IsLive)
            {
                DisableLiveActions();
            }

            _jumpRow.Clear();
            List<Foldout> sections = _content.Query<Foldout>()
                .Where(foldout => IsSection(foldout) && foldout.resolvedStyle.display != DisplayStyle.None && !HasSectionAncestor(foldout))
                .ToList();
            if (sections.Count < MIN_SECTIONS_FOR_JUMP_ROW)
                return;

            var label = new Label("Jump to");
            label.AddToClassList("planet-inspector-jump-label");
            _jumpRow.Add(label);
            foreach (Foldout section in sections)
            {
                Foldout captured = section;
                var link = new Button(() => ScrollTo(captured, _content)) { text = section.text };
                link.AddToClassList("planet-inspector-jump-link");
                _jumpRow.Add(link);
            }
        }

        private static bool IsSection(VisualElement element) =>
            element.ClassListContains("sdk-section") || element.ClassListContains("pqs-inspector-section")
            || element.ClassListContains("body-inspector-section");

        private bool HasSectionAncestor(VisualElement element)
        {
            for (VisualElement parent = element.parent; parent != null && parent != _content; parent = parent.parent)
            {
                if (parent is Foldout && IsSection(parent))
                    return true;
            }

            return false;
        }

        // The inspector window's own scroll view scrolls, once the unfolded section has been laid out.
        private void ScrollTo(Foldout section, VisualElement fallback)
        {
            if (section != null)
            {
                section.value = true;
            }

            VisualElement target = section ?? fallback;
            Root.schedule.Execute(() => Root.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(target)).StartingIn(50);
        }

        private static void Show(VisualElement element, bool visible)
        {
            if (element != null)
            {
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>
        /// Shows a label with a message, or hides it for an empty one.
        /// </summary>
        /// <param name="label">The label.</param>
        /// <param name="message">The message.</param>
        public static void ShowLabel(Label label, string message)
        {
            if (label == null)
                return;

            label.text = message;
            label.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>
        /// Shows or hides an element.
        /// </summary>
        /// <param name="element">The element, or null.</param>
        /// <param name="visible">True to show it, false to hide it.</param>
        public static void SetShown(VisualElement element, bool visible) => Show(element, visible);
    }
}
