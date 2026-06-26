using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KSP;
using KSP.OAB;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets;
using Ksp2UnityTools.Editor.PartAuthoring.StockStats;
using Ksp2UnityTools.Editor.PartAuthoring.Windows;
using Ksp2UnityTools.Editor.PartAuthoring.Wizards.ArchetypeTemplates;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Wizards
{
    /// <summary>
    /// Stepwise wizard for creating a new part: pick an archetype, pick a size, name and place it,
    /// pick a source mesh, review defaults, then run <see cref="PartFolderScaffold" />.
    /// </summary>
    /// <remarks>
    /// Mirrors the planet-authoring single-page wizard's lifecycle (load UXML, query elements,
    /// wire callbacks, rollback discipline lives in the scaffold). Differs in flow shape because
    /// part creation has enough discrete decisions (archetype -> size -> identity -> mesh -> defaults
    /// -> review) that a stepwise layout reads better than a single tall form.
    /// </remarks>
    public sealed class NewPartWizard : EditorWindow
    {
        private const string UXML_PATH = "/Assets/Windows/PartAuthoring/Windows/NewPartWizard.uxml";
        private const string USS_PATH = "/Assets/Windows/PartAuthoring/Windows/NewPartWizard.uss";

        // --- State ---

        /// <summary>The selected archetype the new part is scaffolded from.</summary>
        public IPartArchetype Archetype;

        /// <summary>The selected size key for the new part.</summary>
        public string SizeKey = PartSizeRegistry.DefaultSizeKey;

        /// <summary>The resolved bucket for (family, size) used to seed defaults.</summary>
        public BucketResolution Bucket;

        /// <summary>The part's slug name, used for the prefab, json, and folder name.</summary>
        public string PartName = string.Empty;

        /// <summary>The author string written into the part's data.</summary>
        public string Author = string.Empty;

        /// <summary>Category override applied when the chosen archetype is the Empty archetype.</summary>
        public string CategoryOverride = string.Empty;

        /// <summary>Family override applied when the chosen archetype is the Empty archetype.</summary>
        public string FamilyOverride = string.Empty;

        /// <summary>The resolved parent folder under which the new part folder is created.</summary>
        public string DestinationFolder;

        /// <summary>How the new part's visual mesh is sourced.</summary>
        public SourceMeshChoice MeshChoice = SourceMeshChoice.Skip;

        /// <summary>The existing prefab used as the source mesh, when <see cref="MeshChoice" /> is <see cref="SourceMeshChoice.ExistingPrefab" />.</summary>
        public GameObject SourcePrefab;

        /// <summary>The FBX asset used as the source mesh, when <see cref="MeshChoice" /> is <see cref="SourceMeshChoice.FBX" />.</summary>
        public GameObject SourceFbxAsset;

        /// <summary>When true and the FBX path is chosen, scales the imported instance by 100x.</summary>
        public bool FbxAutoScale = true;

        /// <summary>When true and the FBX path is chosen, rotates the imported instance by -90 degrees on X.</summary>
        public bool FbxAutoRotate = true;

        /// <summary>When true, tags every renderer under the model root with the DragCubeMesh tag.</summary>
        public bool FbxTagDragCubeMesh = true;

        /// <summary>The set of archetype default-module types currently enabled in the wizard's checkboxes.</summary>
        public readonly HashSet<Type> EnabledModules = new();

        /// <summary>Per-stock-field author overrides keyed by field name, applied after the archetype seeds defaults.</summary>
        public readonly Dictionary<string, float> ValueOverrides = new();

        /// <summary>Author-selected interpolation position for missing size buckets. NaN means use the size's natural rank position.</summary>
        public float InterpolationT = float.NaN;

        // Folder picker state - persists across Identity step rebuilds.
        private FolderChoice _folderChoice;
        private bool _folderChoiceInitialized;
        private string _capturedFolder;
        private string _customFolder = "Assets";

        // Step the wizard opens on. Templates set this to skip the archetype picker.
        private int _startStep;

        // --- UI ---
        private int _currentStep;
        private VisualElement _stepSlot;
        private Label _stepCounter;
        private Label _stepTitle;
        private Button _backButton;
        private Button _cancelButton;
        private Button _nextButton;
        private Button _createButton;

        private static readonly (string Title, Func<NewPartWizard, VisualElement> Build, Func<NewPartWizard, bool> CanProceed)[] _steps =
        {
            ("Pick an archetype",   w => w.BuildArchetypeStep(),  w => w.Archetype != null),
            ("Pick a size",         w => w.BuildSizeStep(),        _ => true),
            ("Identity",            w => w.BuildIdentityStep(),    w => IsValidSlug(w.PartName) && !string.IsNullOrEmpty(w.DestinationFolder)),
            ("Source mesh",         w => w.BuildSourceMeshStep(),  w => w.IsSourceMeshValid()),
            ("Default values",      w => w.BuildDefaultsStep(),    _ => true),
            ("Review and create",   w => w.BuildReviewStep(),      _ => true),
        };

        /// <summary>Opens the wizard from the main menu without capturing the Project window selection.</summary>
        [MenuItem(PartAuthoringWindows.MENU_ROOT + "New Part %#n", priority = PartAuthoringWindows.PRIORITY_NEW_PART_WIZARD)]
        public static void ShowWindow() => ShowWindowInternal(captureSelection: false);

        /// <summary>Opens the wizard from the Assets context menu and captures the selected folder as the default destination.</summary>
        [MenuItem("Assets/Redux SDK/Part Authoring/Create New Part", priority = KSP2UnityTools.MenuPriority)]
        public static void ShowWindowFromAssets() => ShowWindowInternal(captureSelection: true);

        // From Template shortcuts. Each opens the wizard with the archetype preselected and
        // jumps directly to Step 2 (size picker), bypassing the archetype picker.
        // Empty + Generic deliberately omitted - those are the "no template" options.
        // Sorted alphabetically by display name to match the picker order.

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Air intake", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateAirIntake() => OpenWithArchetype(typeof(AirIntakeArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Antenna", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateAntenna() => OpenWithArchetype(typeof(AntennaArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Battery", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateBattery() => OpenWithArchetype(typeof(BatteryArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Camera", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateCamera() => OpenWithArchetype(typeof(CameraArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Cargo bay", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateCargoBay() => OpenWithArchetype(typeof(CargoBayArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Command pod", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateCommandPod() => OpenWithArchetype(typeof(CommandPodArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Control surface", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateControlSurface() => OpenWithArchetype(typeof(ControlSurfaceArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Crew cabin", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateCrewCabin() => OpenWithArchetype(typeof(CrewCabinArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Generator", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateGenerator() => OpenWithArchetype(typeof(GeneratorArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Heat shield", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateHeatshield() => OpenWithArchetype(typeof(HeatshieldArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Hydrogen engine", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateHydrogenEngine() => OpenWithArchetype(typeof(HydrogenEngineArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Hydrogen tank", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateHydrogenTank() => OpenWithArchetype(typeof(HydrogenTankArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Inline decoupler", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateInlineDecoupler() => OpenWithArchetype(typeof(InlineDecouplerArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Ion engine", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateIonEngine() => OpenWithArchetype(typeof(IonEngineArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Jet engine", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateJetEngine() => OpenWithArchetype(typeof(JetEngineArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Landing wheel", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateLandingWheel() => OpenWithArchetype(typeof(LandingWheelArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Light", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateLight() => OpenWithArchetype(typeof(LightArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Methalox engine", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateMethaloxEngine() => OpenWithArchetype(typeof(MethaloxEngineArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Methalox tank", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateMethaloxTank() => OpenWithArchetype(typeof(MethaloxTankArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Monoprop engine", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateMonopropEngine() => OpenWithArchetype(typeof(MonopropEngineArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Monoprop tank", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateMonopropTank() => OpenWithArchetype(typeof(MonopropTankArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Parachute", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateParachute() => OpenWithArchetype(typeof(ParachuteArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Probe core", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateProbeCore() => OpenWithArchetype(typeof(ProbeCoreArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/RCS thruster block", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateRCSThrusterBlock() => OpenWithArchetype(typeof(RCSThrusterBlockArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Radial decoupler", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateRadialDecoupler() => OpenWithArchetype(typeof(RadialDecouplerArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Radiator", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateRadiator() => OpenWithArchetype(typeof(RadiatorArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Resource scanner", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateResourceScanner() => OpenWithArchetype(typeof(ResourceScannerArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Science instrument", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateScienceInstrument() => OpenWithArchetype(typeof(ScienceInstrumentArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Solar panel", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateSolarPanel() => OpenWithArchetype(typeof(SolarPanelArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Strut connector", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateStrutConnector() => OpenWithArchetype(typeof(StrutConnectorArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Tail fin", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateTailFin() => OpenWithArchetype(typeof(TailFinArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Universal container", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateUniversalContainer() => OpenWithArchetype(typeof(UniversalContainerArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Wing surface", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateWingSurface() => OpenWithArchetype(typeof(WingSurfaceArchetype));

        [MenuItem("Assets/Redux SDK/Part Authoring/From Template/Xenon tank", priority = KSP2UnityTools.MenuPriority + 10)]
        private static void TemplateXenonTank() => OpenWithArchetype(typeof(XenonTankArchetype));

        /// <summary>
        /// Opens the wizard with a specific archetype preselected, jumping to the Size step.
        /// </summary>
        /// <param name="archetypeType">A concrete <see cref="IPartArchetype" /> implementation type.</param>
        public static void OpenWithArchetype(Type archetypeType)
        {
            string folder = ResolveSelectionFolder();
            var window = CreateInstance<NewPartWizard>();
            window.titleContent = new GUIContent("New Part");
            window.minSize = new Vector2(520, 480);
            window._capturedFolder = folder;
            IPartArchetype archetype = ArchetypeRegistry.Find(archetypeType);
            if (archetype != null)
            {
                window.Archetype = archetype;
                window.SizeKey = archetype.DefaultSizeKey;
                window._startStep = 1;
            }
            window.ShowUtility();
        }

        private static void ShowWindowInternal(bool captureSelection)
        {
            string folder = captureSelection ? ResolveSelectionFolder() : null;
            var window = CreateInstance<NewPartWizard>();
            window.titleContent = new GUIContent("New Part");
            window.minSize = new Vector2(520, 480);
            window._capturedFolder = folder;
            window.ShowUtility();
        }

        private static string ResolveSelectionFolder()
        {
            UnityEngine.Object selected = Selection.activeObject;
            if (selected == null)
            {
                return null;
            }
            string path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }
            return AssetDatabase.IsValidFolder(path)
                ? path
                : Path.GetDirectoryName(path)?.Replace('\\', '/');
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UXML_PATH);
            if (tree == null)
            {
                root.Add(new Label($"Failed to load NewPartWizard.uxml at {SDKConfiguration.BasePath + UXML_PATH}"));
                return;
            }
            tree.CloneTree(root);
            Ksp2UnityToolsStyles.Apply(root, USS_PATH);

            _stepCounter = root.Q<Label>("step-counter");
            _stepTitle = root.Q<Label>("step-title");
            _stepSlot = root.Q<VisualElement>("step-slot");
            _backButton = root.Q<Button>("back-button");
            _cancelButton = root.Q<Button>("cancel-button");
            _nextButton = root.Q<Button>("next-button");
            _createButton = root.Q<Button>("create-button");

            _backButton.clicked += () => GoToStep(_currentStep - 1);
            _cancelButton.clicked += Close;
            _nextButton.clicked += () => GoToStep(_currentStep + 1);
            _createButton.clicked += OnCreate;

            GoToStep(_startStep);
        }

        private void GoToStep(int index)
        {
            _currentStep = Mathf.Clamp(index, 0, _steps.Length - 1);
            var (title, build, _) = _steps[_currentStep];
            _stepCounter.text = $"Step {_currentStep + 1} of {_steps.Length}";
            _stepTitle.text = title;
            _stepSlot.Clear();
            _stepSlot.Add(build(this));
            RefreshFooter();
        }

        /// <summary>Re-evaluates footer button visibility and enablement against current state.</summary>
        /// <remarks>Steps call this when their state changes the <c>CanProceed</c> verdict.</remarks>
        public void RefreshFooter()
        {
            var (_, _, canProceed) = _steps[_currentStep];
            bool isFirst = _currentStep == 0;
            bool isLast = _currentStep == _steps.Length - 1;

            _backButton.style.display = isFirst ? DisplayStyle.None : DisplayStyle.Flex;
            _nextButton.style.display = isLast ? DisplayStyle.None : DisplayStyle.Flex;
            _createButton.style.display = isLast ? DisplayStyle.Flex : DisplayStyle.None;

            bool ok = canProceed(this);
            _nextButton.SetEnabled(ok);
            _createButton.SetEnabled(ok);
        }

        private void OnCreate()
        {
            if (Archetype == null || string.IsNullOrWhiteSpace(PartName) || string.IsNullOrEmpty(DestinationFolder))
            {
                EditorUtility.DisplayDialog("Cannot create part",
                    "Required fields are missing. Go back and complete the previous steps.", "OK");
                return;
            }

            StockStatsLookup lookup = GetLookup();
            string effectiveFamily = string.IsNullOrEmpty(Archetype?.Family) ? FamilyOverride : Archetype.Family;
            BucketResolution bucket = lookup?.ResolveBucket(effectiveFamily ?? string.Empty, SizeKey, InterpolationT)
                                      ?? new BucketResolution(effectiveFamily ?? string.Empty, SizeKey, null,
                                          Array.Empty<StockBucket>(), Array.Empty<StockBucket>());

            HashSet<Type> enabled = EnabledModules != null && EnabledModules.Count > 0 ? EnabledModules : null;
            IReadOnlyDictionary<string, float> overrides = ValueOverrides.Count > 0 ? ValueOverrides : null;
            var scaffold = new PartFolderScaffold(Archetype, DestinationFolder, PartName, SizeKey, bucket, enabled, overrides,
                MeshChoice, SourcePrefab, SourceFbxAsset, FbxTagDragCubeMesh, FbxAutoScale, FbxAutoRotate);

            string prefabPath;
            try
            {
                prefabPath = scaffold.Run();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Part creation failed",
                    $"The scaffold rolled back changes.\n\nError:\n{e.Message}", "OK");
                return;
            }

            // Apply Author + Empty-archetype family/category overrides post-scaffold.
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset != null)
            {
                CorePartData core = prefabAsset.GetComponent<CorePartData>();
                if (core?.Data != null)
                {
                    bool mutated = false;
                    if (!string.IsNullOrEmpty(Author))
                    {
                        core.Data.author = Author;
                        mutated = true;
                    }
                    if (string.IsNullOrEmpty(Archetype?.Family) && !string.IsNullOrEmpty(FamilyOverride))
                    {
                        core.Data.family = FamilyOverride;
                        mutated = true;
                    }
                    if (mutated)
                    {
                        EditorUtility.SetDirty(prefabAsset);
                        AssetDatabase.SaveAssets();
                    }
                }
                AssetDatabase.OpenAsset(prefabAsset);
            }
            Close();
        }

        // ----- Real step builders -----

        private VisualElement BuildArchetypeStep()
        {
            var container = new VisualElement();
            container.AddToClassList("wizard-archetype-step");

            IReadOnlyList<IPartArchetype> all = ArchetypeRegistry.GetAll();
            var byCategory = new Dictionary<string, List<IPartArchetype>>();
            foreach (IPartArchetype a in all)
            {
                string cat = string.IsNullOrEmpty(a.Category) ? "Other" : a.Category;
                if (!byCategory.TryGetValue(cat, out List<IPartArchetype> list))
                {
                    list = new List<IPartArchetype>();
                    byCategory[cat] = list;
                }
                list.Add(a);
            }

            VisualElement currentSelectedRow = null;

            foreach (string cat in byCategory.Keys
                .OrderBy(c => c == "Empty" ? 0 : 1)
                .ThenBy(c => c, StringComparer.OrdinalIgnoreCase))
            {
                var foldout = new Foldout { text = cat, value = true };
                foldout.AddToClassList("wizard-archetype-category");

                foreach (IPartArchetype archetype in byCategory[cat])
                {
                    var row = new VisualElement();
                    row.AddToClassList("wizard-archetype-row");

                    var nameLabel = new Label(archetype.DisplayName);
                    nameLabel.AddToClassList("wizard-archetype-row-name");
                    row.Add(nameLabel);

                    if (!string.IsNullOrEmpty(archetype.Description))
                    {
                        var descLabel = new Label(archetype.Description);
                        descLabel.AddToClassList("wizard-archetype-row-desc");
                        row.Add(descLabel);
                    }

                    IPartArchetype captured = archetype;
                    VisualElement capturedRow = row;
                    row.RegisterCallback<ClickEvent>(_ =>
                    {
                        Archetype = captured;
                        SizeKey = captured.DefaultSizeKey;
                        ResetStockDefaultsState();
                        if (currentSelectedRow != null)
                        {
                            currentSelectedRow.RemoveFromClassList("wizard-archetype-row-selected");
                        }
                        capturedRow.AddToClassList("wizard-archetype-row-selected");
                        currentSelectedRow = capturedRow;
                        RefreshFooter();
                    });

                    if (Archetype == archetype)
                    {
                        row.AddToClassList("wizard-archetype-row-selected");
                        currentSelectedRow = row;
                    }

                    foldout.Add(row);
                }

                container.Add(foldout);
            }

            return container;
        }

        private VisualElement BuildSizeStep()
        {
            var container = new VisualElement();
            VisualTreeAsset tree = LoadStepTree("NewPartWizard_Size.uxml");
            if (tree == null)
            {
                container.Add(new Label("Failed to load NewPartWizard_Size.uxml"));
                return container;
            }
            tree.CloneTree(container);

            if (Archetype == null)
            {
                container.Clear();
                container.Add(new Label("No archetype chosen. Go back to Step 1."));
                return container;
            }

            var archLabel = container.Q<Label>("archetype-label");
            var familyLabel = container.Q<Label>("family-label");
            var sizeSlot = container.Q<VisualElement>("size-key-field-slot");
            var refPanel = container.Q<VisualElement>("reference-panel");

            archLabel.text = $"Archetype: {Archetype.DisplayName}";
            familyLabel.text = $"Family: {Archetype.Family}";

            var sizeField = new AutocompleteField(
                SizeKey,
                "Size key",
                GetSizeKeyChoices,
                newValue =>
                {
                    SizeKey = NormalizeSizeKeyInput(newValue);
                    ResetStockDefaultsState();
                    ResolveAndRenderReferences(refPanel);
                },
                detailSource: PartAuthoringChoiceCatalog.GetKnownSizeKeyDetail,
                preserveSourceOrderForEqualScores: true);
            sizeField.AddToClassList("wizard-size-dropdown");
            sizeSlot?.Add(sizeField);

            ResolveAndRenderReferences(refPanel);

            return container;
        }

        private static VisualTreeAsset LoadStepTree(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                SDKConfiguration.BasePath + "/Assets/Windows/PartAuthoring/Windows/" + fileName);
        }

        private void ResolveAndRenderReferences(VisualElement panel)
        {
            panel.Clear();
            StockStatsLookup lookup = GetLookup();
            if (lookup == null)
            {
                var warn = new Label("Stock stats not baked. Open Modding/Part Authoring/Stock Stats Bake to populate.");
                warn.AddToClassList("wizard-no-bake-warning");
                panel.Add(warn);
                return;
            }
            if (Archetype == null)
            {
                return;
            }
            string effectiveFamily = string.IsNullOrEmpty(Archetype.Family) ? FamilyOverride : Archetype.Family;
            Bucket = lookup.ResolveBucket(effectiveFamily ?? string.Empty, SizeKey, InterpolationT);
            RenderReferencePanel(panel, Bucket);
        }

        private static void RenderReferencePanel(VisualElement panel, BucketResolution bucket)
        {
            if (bucket.InBucket != null)
            {
                int count = bucket.InBucket.ContributingParts?.Count ?? 0;
                var header = new Label($"Reference parts in this bucket ({count}):");
                header.AddToClassList("wizard-reference-section-header");
                panel.Add(header);
                if (bucket.InBucket.ContributingParts != null)
                {
                    foreach (StockPartRef part in bucket.InBucket.ContributingParts)
                    {
                        AddReferenceRow(panel, part.PartName, StockStatsLookup.NormalizeSizeKey(bucket.InBucket.SizeCategory));
                    }
                }
            }
            else
            {
                if (bucket.Interpolated != null && bucket.InterpolationLower != null && bucket.InterpolationUpper != null)
                {
                    int percent = Mathf.RoundToInt(bucket.InterpolationT * 100f);
                    var interpolation = new Label(
                        $"No stock parts at {bucket.SizeKey}; defaults interpolate {percent}% from {StockStatsLookup.NormalizeSizeKey(bucket.InterpolationLower.SizeCategory)} to {StockStatsLookup.NormalizeSizeKey(bucket.InterpolationUpper.SizeCategory)}.");
                    interpolation.AddToClassList("wizard-reference-section-header");
                    panel.Add(interpolation);
                }

                if (bucket.FamilyFallback != null && bucket.FamilyFallback.Count > 0)
                {
                    var header = new Label($"Closest {bucket.Family} parts:");
                    header.AddToClassList("wizard-reference-section-header");
                    panel.Add(header);
                    foreach (StockBucket fallback in bucket.FamilyFallback)
                    {
                        if (fallback?.ContributingParts == null)
                        {
                            continue;
                        }
                        foreach (StockPartRef part in fallback.ContributingParts)
                        {
                            AddReferenceRow(panel, part.PartName, StockStatsLookup.NormalizeSizeKey(fallback.SizeCategory));
                        }
                    }
                }
                else
                {
                    var header = new Label($"No stock data for family '{bucket.Family}'. Values will be hand-authored.");
                    header.AddToClassList("wizard-reference-section-header");
                    panel.Add(header);
                }
            }

            if (bucket.Adjacent != null && bucket.Adjacent.Count > 0)
            {
                var adjFoldout = new Foldout { text = "Adjacent sizes (for trend reference)", value = false };
                adjFoldout.AddToClassList("wizard-reference-adjacent");
                foreach (StockBucket adj in bucket.Adjacent)
                {
                    if (adj?.ContributingParts == null)
                    {
                        continue;
                    }
                    var subHeader = new Label($"{StockStatsLookup.NormalizeSizeKey(adj.SizeCategory)} ({adj.ContributingParts.Count} parts)");
                    subHeader.AddToClassList("wizard-reference-section-header");
                    adjFoldout.Add(subHeader);
                    foreach (StockPartRef part in adj.ContributingParts)
                    {
                        AddReferenceRow(adjFoldout, part.PartName, StockStatsLookup.NormalizeSizeKey(adj.SizeCategory));
                    }
                }
                panel.Add(adjFoldout);
            }
        }

        private static void AddReferenceRow(VisualElement parent, string partName, string sizeKey)
        {
            var row = new Label($"{partName}    [{sizeKey}]");
            row.AddToClassList("wizard-reference-row");
            parent.Add(row);
        }

        private static IEnumerable<string> GetSizeKeyChoices()
        {
            return PartSizeRegistry.Definitions.Select(definition => definition.Key);
        }

        private static string NormalizeSizeKeyInput(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? PartSizeRegistry.DefaultSizeKey : value.Trim();
        }

        private StockStatsLookup GetLookup()
        {
            if (_lookupLoaded)
            {
                return _lookup;
            }
            _lookup = AssetDatabase.LoadAssetAtPath<StockStatsLookup>(
                SDKConfiguration.BasePath + "/Assets/StockStats/StockStatsLookup.asset");
            _lookupLoaded = true;
            return _lookup;
        }

        private StockStatsLookup _lookup;
        private bool _lookupLoaded;

        private VisualElement BuildIdentityStep()
        {
            EnsureFolderChoiceInitialized();
            ResolveDestinationFolder();

            var container = new VisualElement();
            VisualTreeAsset tree = LoadStepTree("NewPartWizard_Identity.uxml");
            if (tree == null)
            {
                container.Add(new Label("Failed to load NewPartWizard_Identity.uxml"));
                return container;
            }
            tree.CloneTree(container);

            var nameField = container.Q<TextField>("part-name-field");
            var slugHint = container.Q<Label>("slug-hint");
            var authorField = container.Q<TextField>("author-field");
            var emptyExtras = container.Q<VisualElement>("empty-extras");
            var categoryField = container.Q<DropdownField>("category-field");
            var familySlot = container.Q<VisualElement>("family-slot");
            var underSelectedRadio = container.Q<RadioButton>("under-selected-radio");
            var underSelectedLabel = container.Q<Label>("under-selected-label");
            var defaultRadio = container.Q<RadioButton>("default-radio");
            var defaultRadioLabel = container.Q<Label>("default-radio-label");
            var customRadio = container.Q<RadioButton>("custom-radio");
            var customField = container.Q<TextField>("custom-field");
            var customBrowseButton = container.Q<Button>("custom-browse-button");
            var customWarningSlot = container.Q<VisualElement>("custom-folder-warning-slot");
            var finalDestLabel = container.Q<Label>("final-dest-label");

            // Initialize from state.
            nameField.SetValueWithoutNotify(PartName);
            authorField.SetValueWithoutNotify(Author);
            customField.SetValueWithoutNotify(_customFolder);

            underSelectedRadio.SetEnabled(!string.IsNullOrEmpty(_capturedFolder));
            underSelectedLabel.text = string.IsNullOrEmpty(_capturedFolder)
                ? "  (no folder captured at open)"
                : $"  {_capturedFolder}";

            authorField.RegisterValueChangedCallback(evt =>
            {
                Author = evt.newValue ?? string.Empty;
            });

#if !REDUX
            defaultRadio.style.display = DisplayStyle.None;
            defaultRadioLabel.style.display = DisplayStyle.None;
#endif

            // Empty archetype: surface Category + Family pickers.
            if (string.IsNullOrEmpty(Archetype?.Family))
            {
                emptyExtras.style.display = DisplayStyle.Flex;
                categoryField.choices = _emptyCategoryChoices.ToList();
                if (string.IsNullOrEmpty(CategoryOverride))
                {
                    CategoryOverride = _emptyCategoryChoices[0];
                }
                categoryField.SetValueWithoutNotify(CategoryOverride);

                var familyField = new AutocompleteField(
                    FamilyOverride,
                    "Family",
                    () => PartAuthoringChoiceCatalog.GetKnownFamilies(),
                    newValue =>
                    {
                        FamilyOverride = newValue ?? string.Empty;
                        ResetStockDefaultsState();
                        ResolveDestinationFolder();
                        defaultRadioLabel.text = "  " + (ResolveDefaultReduxFolder() ?? "(pick category and family)");
                    });
                familyField.AddToClassList("wizard-identity-field");
                familySlot.Add(familyField);

                categoryField.RegisterValueChangedCallback(evt =>
                {
                    CategoryOverride = evt.newValue ?? string.Empty;
                    ResolveDestinationFolder();
                    defaultRadioLabel.text = "  " + (ResolveDefaultReduxFolder() ?? "(pick category and family)");
                });
            }
            else
            {
                emptyExtras.style.display = DisplayStyle.None;
            }

#if REDUX
            defaultRadioLabel.text = "  " + (ResolveDefaultReduxFolder() ?? "(pick category and family)");
#endif

            // Sync initial radio selections.
            underSelectedRadio.SetValueWithoutNotify(_folderChoice == FolderChoice.UnderSelected);
#if REDUX
            defaultRadio.SetValueWithoutNotify(_folderChoice == FolderChoice.DefaultRedux);
#endif
            customRadio.SetValueWithoutNotify(_folderChoice == FolderChoice.Custom);
            customField.SetEnabled(_folderChoice == FolderChoice.Custom);
            customBrowseButton.SetEnabled(_folderChoice == FolderChoice.Custom);
            customBrowseButton.clicked += () => OnCustomFolderBrowse(customField, customWarningSlot);

            void RefreshSlugUi()
            {
                bool valid = string.IsNullOrEmpty(PartName) || IsValidSlug(PartName);
                slugHint.RemoveFromClassList("wizard-slug-hint-error");
                if (!valid)
                {
                    slugHint.AddToClassList("wizard-slug-hint-error");
                }
            }

            void RefreshFinalDestLabel()
            {
                string parent = string.IsNullOrEmpty(DestinationFolder) ? "Assets" : DestinationFolder;
                string name = string.IsNullOrWhiteSpace(PartName) ? "<part-name>" : PartName;
                finalDestLabel.text = $"Final destination: {parent}/{name}";
            }

            void SelectChoice(FolderChoice choice)
            {
                _folderChoice = choice;
                underSelectedRadio.SetValueWithoutNotify(choice == FolderChoice.UnderSelected);
#if REDUX
                defaultRadio.SetValueWithoutNotify(choice == FolderChoice.DefaultRedux);
#endif
                customRadio.SetValueWithoutNotify(choice == FolderChoice.Custom);
                customField.SetEnabled(choice == FolderChoice.Custom);
                customBrowseButton.SetEnabled(choice == FolderChoice.Custom);
                if (choice != FolderChoice.Custom)
                {
                    customWarningSlot.Clear();
                }
                ResolveDestinationFolder();
                RefreshFinalDestLabel();
                RefreshFooter();
            }

            nameField.RegisterValueChangedCallback(evt =>
            {
                PartName = evt.newValue ?? string.Empty;
                RefreshSlugUi();
                RefreshFinalDestLabel();
                RefreshFooter();
            });
            underSelectedRadio.RegisterValueChangedCallback(evt => { if (evt.newValue) SelectChoice(FolderChoice.UnderSelected); });
#if REDUX
            defaultRadio.RegisterValueChangedCallback(evt => { if (evt.newValue) SelectChoice(FolderChoice.DefaultRedux); });
#endif
            customRadio.RegisterValueChangedCallback(evt => { if (evt.newValue) SelectChoice(FolderChoice.Custom); });
            customField.RegisterValueChangedCallback(evt =>
            {
                _customFolder = evt.newValue ?? string.Empty;
                if (_folderChoice == FolderChoice.Custom)
                {
                    ResolveDestinationFolder();
                    RefreshFinalDestLabel();
                    RefreshFooter();
                }
            });

            RefreshSlugUi();
            RefreshFinalDestLabel();
            return container;
        }

        private void OnCustomFolderBrowse(TextField customField, VisualElement warningSlot)
        {
            warningSlot.Clear();

            string assetsAbs = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            string startDir = assetsAbs;
            if (!string.IsNullOrEmpty(_customFolder) && _customFolder.StartsWith("Assets", StringComparison.Ordinal))
            {
                string candidate = Path.GetFullPath(_customFolder).Replace('\\', '/');
                if (Directory.Exists(candidate))
                {
                    startDir = candidate;
                }
            }

            string picked = EditorUtility.OpenFolderPanel("Select destination folder", startDir, string.Empty);
            if (string.IsNullOrEmpty(picked))
            {
                return;
            }

            string normalized = Path.GetFullPath(picked).Replace('\\', '/').TrimEnd('/');
            string assetsRoot = assetsAbs.TrimEnd('/');

            string relative;
            if (string.Equals(normalized, assetsRoot, StringComparison.OrdinalIgnoreCase))
            {
                relative = "Assets";
            }
            else if (normalized.StartsWith(assetsRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                relative = "Assets" + normalized.Substring(assetsRoot.Length);
            }
            else
            {
                warningSlot.Add(new HelpBox(
                    "Folder must be inside the project's Assets/ directory.",
                    HelpBoxMessageType.Warning));
                return;
            }

            _customFolder = relative;
            customField.value = relative;
        }

        private void EnsureFolderChoiceInitialized()
        {
            if (_folderChoiceInitialized)
            {
                return;
            }
            if (!string.IsNullOrEmpty(_capturedFolder))
            {
                _folderChoice = FolderChoice.UnderSelected;
            }
            else
            {
#if REDUX
                _folderChoice = FolderChoice.DefaultRedux;
#else
                _folderChoice = FolderChoice.Custom;
#endif
            }
            _folderChoiceInitialized = true;
        }

        private void ResolveDestinationFolder()
        {
            switch (_folderChoice)
            {
                case FolderChoice.UnderSelected:
                    DestinationFolder = _capturedFolder;
                    break;
#if REDUX
                case FolderChoice.DefaultRedux:
                    DestinationFolder = ResolveDefaultReduxFolder();
                    break;
#endif
                case FolderChoice.Custom:
                    DestinationFolder = _customFolder;
                    break;
                default:
                    DestinationFolder = null;
                    break;
            }
        }

        private string ResolveDefaultReduxFolder()
        {
            string category = string.IsNullOrEmpty(Archetype?.Family) ? CategoryOverride : Archetype?.Category;
            string family = string.IsNullOrEmpty(Archetype?.Family) ? FamilyOverride : Archetype?.Family;
            if (string.IsNullOrEmpty(category))
            {
                return null;
            }
            string root = "Assets/ReduxAssets/Definitions/Parts/Vessel/" + category;
            string stripped = StripFamilyPrefix(family ?? string.Empty);
            return string.IsNullOrEmpty(stripped) ? root : root + "/" + stripped;
        }

        private static readonly string[] _emptyCategoryChoices =
        {
            "Aerodynamics",
            "Communication",
            "Coupling",
            "Electrical",
            "Engines",
            "Fuel Tank",
            "Ground",
            "Payloads",
            "Pods",
            "Science",
            "Structural",
            "Thermal",
            "Utility"
        };

        private static string StripFamilyPrefix(string family)
        {
            if (string.IsNullOrEmpty(family))
            {
                return string.Empty;
            }
            int dashIdx = family.IndexOf('-');
            if (dashIdx >= 0 && dashIdx < family.Length - 1)
            {
                return family.Substring(dashIdx + 1);
            }
            return family;
        }

        private static bool IsValidSlug(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.';
                if (!ok)
                {
                    return false;
                }
            }
            return true;
        }

        private bool IsSourceMeshValid()
        {
            switch (MeshChoice)
            {
                case SourceMeshChoice.Skip: return true;
                case SourceMeshChoice.ExistingPrefab: return SourcePrefab != null;
                case SourceMeshChoice.FBX: return SourceFbxAsset != null;
                default: return false;
            }
        }

        private enum FolderChoice
        {
            UnderSelected,
            DefaultRedux,
            Custom
        }

        private VisualElement BuildSourceMeshStep()
        {
            var container = new VisualElement();
            VisualTreeAsset tree = LoadStepTree("NewPartWizard_SourceMesh.uxml");
            if (tree == null)
            {
                container.Add(new Label("Failed to load NewPartWizard_SourceMesh.uxml"));
                return container;
            }
            tree.CloneTree(container);

            var meshGroup = container.Q<RadioButtonGroup>("mesh-group");
            var prefabBlock = container.Q<VisualElement>("prefab-block");
            var prefabField = container.Q<ObjectField>("prefab-field");
            var fbxBlock = container.Q<VisualElement>("fbx-block");
            var fbxField = container.Q<ObjectField>("fbx-field");
            var autoScale = container.Q<Toggle>("auto-scale-toggle");
            var autoRotate = container.Q<Toggle>("auto-rotate-toggle");
            var dragCube = container.Q<Toggle>("drag-cube-toggle");

            var choices = new List<string> { "Import mesh later", "Use an existing prefab", "Import from FBX" };
            meshGroup.choices = choices;
            meshGroup.value = (int)MeshChoice;

            prefabField.objectType = typeof(GameObject);
            prefabField.allowSceneObjects = false;
            prefabField.SetValueWithoutNotify(SourcePrefab);

            fbxField.objectType = typeof(GameObject);
            fbxField.allowSceneObjects = false;
            fbxField.SetValueWithoutNotify(SourceFbxAsset);

            autoScale.SetValueWithoutNotify(FbxAutoScale);
            autoRotate.SetValueWithoutNotify(FbxAutoRotate);
            dragCube.SetValueWithoutNotify(FbxTagDragCubeMesh);

            void RefreshBlockVisibility()
            {
                prefabBlock.style.display = MeshChoice == SourceMeshChoice.ExistingPrefab ? DisplayStyle.Flex : DisplayStyle.None;
                fbxBlock.style.display = MeshChoice == SourceMeshChoice.FBX ? DisplayStyle.Flex : DisplayStyle.None;
            }
            RefreshBlockVisibility();

            meshGroup.RegisterValueChangedCallback(evt =>
            {
                int idx = evt.newValue;
                if (idx < 0 || idx > 2)
                {
                    return;
                }
                MeshChoice = (SourceMeshChoice)idx;
                RefreshBlockVisibility();
                RefreshFooter();
            });

            prefabField.RegisterValueChangedCallback(evt =>
            {
                SourcePrefab = evt.newValue as GameObject;
                RefreshFooter();
            });

            fbxField.RegisterValueChangedCallback(evt =>
            {
                SourceFbxAsset = evt.newValue as GameObject;
                RefreshFooter();
            });

            autoScale.RegisterValueChangedCallback(evt => FbxAutoScale = evt.newValue);
            autoRotate.RegisterValueChangedCallback(evt => FbxAutoRotate = evt.newValue);
            dragCube.RegisterValueChangedCallback(evt => FbxTagDragCubeMesh = evt.newValue);

            return container;
        }

        private VisualElement BuildDefaultsStep()
        {
            EnsureEnabledModulesInitialized();

            var container = new VisualElement();
            container.AddToClassList("wizard-defaults-step");

            // Modules subsection with checkboxes.
            var modulesHeader = new Label("Modules");
            modulesHeader.AddToClassList("wizard-defaults-section-header");
            container.Add(modulesHeader);

            if (Archetype == null || Archetype.DefaultModules == null || Archetype.DefaultModules.Count == 0)
            {
                container.Add(new Label("(archetype declares no modules)"));
            }
            else
            {
                foreach (Type moduleType in Archetype.DefaultModules)
                {
                    var toggle = new Toggle(moduleType.Name) { value = EnabledModules.Contains(moduleType) };
                    toggle.AddToClassList("wizard-module-toggle");
                    Type capturedType = moduleType;
                    toggle.RegisterValueChangedCallback(evt =>
                    {
                        if (evt.newValue)
                        {
                            EnabledModules.Add(capturedType);
                        }
                        else
                        {
                            EnabledModules.Remove(capturedType);
                        }
                    });
                    container.Add(toggle);
                }
            }

            // Values subsection.
            var valuesHeader = new Label();
            valuesHeader.AddToClassList("wizard-defaults-section-header");
            container.Add(valuesHeader);

            var interpolationHost = new VisualElement();
            container.Add(interpolationHost);

            var valuesHost = new VisualElement();
            container.Add(valuesHost);

            RenderDefaultsValues(valuesHeader, interpolationHost, valuesHost, rebuildInterpolationControls: true);

            return container;
        }

        private void RenderDefaultsValues(
            Label valuesHeader,
            VisualElement interpolationHost,
            VisualElement valuesHost,
            bool rebuildInterpolationControls)
        {
            valuesHost.Clear();

            StockStatsLookup lookup = GetLookup();
            if (lookup == null)
            {
                interpolationHost.Clear();
                valuesHeader.text = "Default values";
                var warn = new Label("Stock stats not baked. Values will be hand-authored.");
                warn.AddToClassList("wizard-no-bake-warning");
                valuesHost.Add(warn);
                return;
            }

            BucketResolution bucket = lookup.ResolveBucket(
                string.IsNullOrEmpty(Archetype?.Family) ? FamilyOverride : Archetype?.Family ?? string.Empty,
                SizeKey,
                InterpolationT);
            Bucket = bucket;

            StockBucket source = bucket?.InBucket ??
                                 bucket?.Interpolated ??
                                 (bucket?.FamilyFallback != null && bucket.FamilyFallback.Count > 0
                                     ? bucket.FamilyFallback[0]
                                     : null);
            if (source == null || source.Fields == null || source.Fields.Count == 0)
            {
                interpolationHost.Clear();
                valuesHeader.text = "Default values";
                valuesHost.Add(new Label("No stock data for this family. Values will be hand-authored."));
                return;
            }

            bool usingInterpolation = bucket?.InBucket == null && bucket?.Interpolated != null && ReferenceEquals(source, bucket.Interpolated);
            if (usingInterpolation)
            {
                valuesHeader.text = $"Default values interpolated for {source.Family} / {StockStatsLookup.NormalizeSizeKey(source.SizeCategory)}";
                if (rebuildInterpolationControls)
                {
                    interpolationHost.Clear();
                    interpolationHost.Add(BuildInterpolationControls(
                        bucket,
                        () => RenderDefaultsValues(valuesHeader, interpolationHost, valuesHost, rebuildInterpolationControls: false),
                        () => RenderDefaultsValues(valuesHeader, interpolationHost, valuesHost, rebuildInterpolationControls: true)));
                }
            }
            else
            {
                interpolationHost.Clear();
                int partCount = source.ContributingParts?.Count ?? 0;
                valuesHeader.text = $"Default values from bucket {source.Family} / {StockStatsLookup.NormalizeSizeKey(source.SizeCategory)} ({partCount} reference parts)";
            }

            // Group fields by module section, preserving encounter order. PartData always sorts first.
            var groups = new Dictionary<string, List<StockField>>();
            foreach (StockField field in source.Fields)
            {
                if (field == null || field.Count == 0)
                {
                    continue;
                }
                string group = GetModuleGroupName(field.Name);
                if (!groups.TryGetValue(group, out List<StockField> list))
                {
                    list = new List<StockField>();
                    groups[group] = list;
                }
                list.Add(field);
            }

            void RenderGroup(string groupName, List<StockField> fields)
            {
                // Skip derived fields entirely - they can't be seeded directly so showing them
                // here as "this will be applied" is misleading.
                var visibleFields = new List<StockField>();
                foreach (StockField f in fields)
                {
                    StockFieldEntry e = StockFieldPaths.Find(f.Name);
                    if (e?.IsDerived == true)
                    {
                        continue;
                    }
                    visibleFields.Add(f);
                }
                if (visibleFields.Count == 0)
                {
                    return;
                }

                var header = new Label(groupName);
                header.AddToClassList("wizard-defaults-group-header");
                valuesHost.Add(header);
                foreach (StockField field in visibleFields)
                {
                    valuesHost.Add(BuildDefaultsRow(field));
                }
            }

            // PartData first, then remaining groups in encounter order.
            if (groups.TryGetValue("PartData", out List<StockField> partDataFields))
            {
                RenderGroup("PartData", partDataFields);
            }
            foreach (var kvp in groups)
            {
                if (kvp.Key == "PartData")
                {
                    continue;
                }
                RenderGroup(kvp.Key, kvp.Value);
            }
        }

        private VisualElement BuildInterpolationControls(BucketResolution bucket, Action onValueChanged, Action onReset)
        {
            var container = new VisualElement();
            container.AddToClassList("wizard-interpolation-box");

            string lowerSize = bucket.InterpolationLower != null ? StockStatsLookup.NormalizeSizeKey(bucket.InterpolationLower.SizeCategory) : "lower";
            string upperSize = bucket.InterpolationUpper != null ? StockStatsLookup.NormalizeSizeKey(bucket.InterpolationUpper.SizeCategory) : "upper";
            int percent = Mathf.RoundToInt(bucket.InterpolationT * 100f);

            var title = new Label($"Interpolate between {lowerSize} and {upperSize}");
            title.AddToClassList("wizard-interpolation-title");
            container.Add(title);

            var row = new VisualElement();
            row.AddToClassList("wizard-interpolation-row");

            var lowerLabel = new Label(lowerSize);
            lowerLabel.AddToClassList("wizard-interpolation-endpoint");
            row.Add(lowerLabel);

            var slider = new Slider(0f, 1f) { value = bucket.InterpolationT };
            slider.AddToClassList("wizard-interpolation-slider");
            row.Add(slider);

            var upperLabel = new Label(upperSize);
            upperLabel.AddToClassList("wizard-interpolation-endpoint");
            row.Add(upperLabel);

            var resetButton = new Button(() =>
            {
                InterpolationT = float.NaN;
                Bucket = null;
                onReset?.Invoke();
            }) { text = "Reset", tooltip = "Reset to the natural size position" };
            resetButton.AddToClassList("wizard-interpolation-reset");
            row.Add(resetButton);

            container.Add(row);

            var positionLabel = new Label($"{percent}% toward {upperSize}");
            positionLabel.AddToClassList("wizard-interpolation-position");
            container.Add(positionLabel);

            slider.RegisterValueChangedCallback(evt =>
            {
                InterpolationT = evt.newValue;
                Bucket = null;
                int currentPercent = Mathf.RoundToInt(evt.newValue * 100f);
                positionLabel.text = $"{currentPercent}% toward {upperSize}";
                onValueChanged?.Invoke();
            });

            return container;
        }

        private VisualElement BuildDefaultsRow(StockField field)
        {
            StockFieldEntry entry = StockFieldPaths.Find(field.Name);
            string display = entry?.DisplayName ?? field.Name;
            string units = entry?.UnitsSuffix ?? string.Empty;
            string format = entry?.Format ?? "{0:0.##}";
            string subKey = entry?.SubKey;
            string label = string.IsNullOrEmpty(subKey) ? display : $"{display} ({subKey})";

            string min = string.Format(System.Globalization.CultureInfo.InvariantCulture, format, field.Min) + units;
            string max = string.Format(System.Globalization.CultureInfo.InvariantCulture, format, field.Max) + units;

            var row = new VisualElement();
            row.AddToClassList("wizard-defaults-row");

            var nameCell = new Label(label);
            nameCell.AddToClassList("wizard-defaults-row-name");
            row.Add(nameCell);

            bool hasOverride = ValueOverrides.TryGetValue(field.Name, out float currentValue);
            float initialValue = hasOverride ? currentValue : field.Median;
            bool canEdit = entry?.IsCopyable == true;

            VisualElement valueElement;
            Button resetBtn = null;

            if (!canEdit)
            {
                string median = string.Format(System.Globalization.CultureInfo.InvariantCulture, format, field.Median) + units;
                var readonlyLabel = new Label(median);
                readonlyLabel.AddToClassList("wizard-defaults-row-median");
                valueElement = readonlyLabel;
            }
            else if (entry.IsInteger)
            {
                var intField = new IntegerField { value = (int)initialValue };
                intField.AddToClassList("wizard-defaults-row-input");
                intField.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue == (int)field.Median)
                    {
                        ValueOverrides.Remove(field.Name);
                    }
                    else
                    {
                        ValueOverrides[field.Name] = evt.newValue;
                    }
                    if (resetBtn != null)
                    {
                        resetBtn.style.visibility = ValueOverrides.ContainsKey(field.Name)
                            ? Visibility.Visible : Visibility.Hidden;
                    }
                });
                valueElement = intField;
                resetBtn = new Button(() =>
                {
                    ValueOverrides.Remove(field.Name);
                    intField.SetValueWithoutNotify((int)field.Median);
                    resetBtn.style.visibility = Visibility.Hidden;
                }) { text = "↺", tooltip = "Reset to median" };
            }
            else
            {
                var floatField = new FloatField { value = initialValue };
                floatField.AddToClassList("wizard-defaults-row-input");
                floatField.RegisterValueChangedCallback(evt =>
                {
                    if (Mathf.Approximately(evt.newValue, field.Median))
                    {
                        ValueOverrides.Remove(field.Name);
                    }
                    else
                    {
                        ValueOverrides[field.Name] = evt.newValue;
                    }
                    if (resetBtn != null)
                    {
                        resetBtn.style.visibility = ValueOverrides.ContainsKey(field.Name)
                            ? Visibility.Visible : Visibility.Hidden;
                    }
                });
                valueElement = floatField;
                resetBtn = new Button(() =>
                {
                    ValueOverrides.Remove(field.Name);
                    floatField.SetValueWithoutNotify(field.Median);
                    resetBtn.style.visibility = Visibility.Hidden;
                }) { text = "↺", tooltip = "Reset to median" };
            }

            row.Add(valueElement);

            string unitsLabel = string.IsNullOrEmpty(units) ? string.Empty : units.TrimStart();
            var unitsCell = new Label(unitsLabel);
            unitsCell.AddToClassList("wizard-defaults-row-units");
            row.Add(unitsCell);

            var rangeCell = new Label($"range {min} - {max}");
            rangeCell.AddToClassList("wizard-defaults-row-range");
            row.Add(rangeCell);

            if (resetBtn != null)
            {
                resetBtn.AddToClassList("wizard-defaults-row-reset");
                resetBtn.style.visibility = hasOverride ? Visibility.Visible : Visibility.Hidden;
                row.Add(resetBtn);
            }

            return row;
        }

        private static string GetModuleGroupName(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName)) return "PartData";
            if (fieldName.StartsWith("engine.gimbalRange", StringComparison.Ordinal) ||
                fieldName.StartsWith("engine.gimbalResponseSpeed", StringComparison.Ordinal))
                return "Gimbal";
            if (fieldName.StartsWith("engine.", StringComparison.Ordinal)) return "Engine";
            if (fieldName.StartsWith("reactionWheel.", StringComparison.Ordinal)) return "Reaction Wheel";
            if (fieldName.StartsWith("decouple.", StringComparison.Ordinal)) return "Decoupler";
            if (fieldName.StartsWith("tank.", StringComparison.Ordinal)) return "Tank";
            if (fieldName.StartsWith("rcs.", StringComparison.Ordinal)) return "RCS";
            if (fieldName.StartsWith("solar.", StringComparison.Ordinal)) return "Solar Panel";
            if (fieldName.StartsWith("antenna.", StringComparison.Ordinal)) return "Antenna";
            if (fieldName.StartsWith("parachute.", StringComparison.Ordinal)) return "Parachute";
            if (fieldName.StartsWith("generator.", StringComparison.Ordinal)) return "Generator";
            if (fieldName.StartsWith("converter.", StringComparison.Ordinal)) return "Converter";
            if (fieldName.StartsWith("wheel.", StringComparison.Ordinal)) return "Wheel";
            if (fieldName.StartsWith("docking.", StringComparison.Ordinal)) return "Docking";
            if (fieldName.StartsWith("controlSurface.", StringComparison.Ordinal)) return "Control Surface";
            if (fieldName.StartsWith("command.", StringComparison.Ordinal)) return "Command";
            if (fieldName.StartsWith("science.experiment.", StringComparison.Ordinal)) return "Science";
            if (fieldName.StartsWith("intake.", StringComparison.Ordinal)) return "Intake";
            if (fieldName.StartsWith("liftSurface.", StringComparison.Ordinal)) return "Lifting Surface";
            if (fieldName.StartsWith("heatshield.", StringComparison.Ordinal)) return "Heatshield";
            if (fieldName.StartsWith("radiator.", StringComparison.Ordinal)) return "Radiator";
            if (fieldName.StartsWith("cargoBay.", StringComparison.Ordinal)) return "Cargo Bay";
            return "PartData";
        }

        private void EnsureEnabledModulesInitialized()
        {
            if (Archetype?.DefaultModules == null)
            {
                return;
            }
            if (EnabledModules.Count == 0)
            {
                foreach (Type t in Archetype.DefaultModules)
                {
                    EnabledModules.Add(t);
                }
            }
        }

        private void ResetStockDefaultsState()
        {
            Bucket = null;
            InterpolationT = float.NaN;
            ValueOverrides.Clear();
        }

        private VisualElement BuildReviewStep()
        {
            var container = new VisualElement();
            container.AddToClassList("wizard-review-step");

            string effectiveFamily = string.IsNullOrEmpty(Archetype?.Family) ? FamilyOverride : Archetype?.Family;
            string effectiveCategory = string.IsNullOrEmpty(Archetype?.Family) ? CategoryOverride : Archetype?.Category;
            string finalFolder = string.IsNullOrEmpty(DestinationFolder)
                ? "(none)"
                : $"{DestinationFolder}/{PartName}";

            AddReviewSection(container, "Summary", new[]
            {
                ("Archetype", Archetype?.DisplayName ?? "(none)"),
                ("Category", effectiveCategory ?? "(none)"),
                ("Family", effectiveFamily ?? "(none)"),
                ("Size", SizeKey),
                ("Part name", string.IsNullOrEmpty(PartName) ? "(empty)" : PartName),
                ("Author", string.IsNullOrEmpty(Author) ? "(empty)" : Author),
            });

            AddReviewSection(container, "Destination", new[]
            {
                ("Folder", finalFolder),
            });

            // Modules: list the enabled set.
            var modulesHeader = new Label("Modules");
            modulesHeader.AddToClassList("wizard-review-section-header");
            container.Add(modulesHeader);
            if (EnabledModules == null || EnabledModules.Count == 0)
            {
                container.Add(new Label("  (none)"));
            }
            else
            {
                foreach (Type t in EnabledModules)
                {
                    container.Add(new Label($"  {t.Name}") { });
                }
            }

            // Attach nodes from the archetype.
            if (Archetype != null)
            {
                var nodes = Archetype.DefaultAttachNodes;
                string nodeList = (nodes == null || nodes.Count == 0)
                    ? "(none)"
                    : string.Join(", ", nodes.Select(n => n.NodeId));
                AddReviewSection(container, "Hierarchy", new[]
                {
                    ("Attach nodes", nodeList),
                });
            }

            // Mesh source.
            string meshSummary = MeshChoice switch
            {
                SourceMeshChoice.Skip => "Skip (author imports later)",
                SourceMeshChoice.ExistingPrefab => SourcePrefab != null ? $"Existing prefab: {SourcePrefab.name}" : "Existing prefab (not selected)",
                SourceMeshChoice.FBX => SourceFbxAsset != null ? $"FBX: {SourceFbxAsset.name}" : "FBX (not selected)",
                _ => "(none)",
            };
            AddReviewSection(container, "Source mesh", new[] { ("", meshSummary) });

            // Files to be created.
            var filesHeader = new Label("Files to be created");
            filesHeader.AddToClassList("wizard-review-section-header");
            container.Add(filesHeader);
            container.Add(new Label($"  + {PartName}.prefab"));
            container.Add(new Label($"  + {PartName}.json"));
            container.Add(new Label("  + model/ + model/col/ + model/lod_reentry/"));

            // Pending bakes hint.
            var bakesHeader = new Label("Run after creation");
            bakesHeader.AddToClassList("wizard-review-section-header");
            container.Add(bakesHeader);
            container.Add(new Label("  - Drag cubes (Module_Drag inspector)"));
            container.Add(new Label("  - Reentry mesh (Quick Tools)"));
            container.Add(new Label("  - Icon (Quick Tools)"));

            return container;
        }

        private static void AddReviewSection(VisualElement parent, string title, IEnumerable<(string Key, string Value)> rows)
        {
            var header = new Label(title);
            header.AddToClassList("wizard-review-section-header");
            parent.Add(header);
            foreach (var (key, value) in rows)
            {
                var row = new VisualElement();
                row.AddToClassList("wizard-review-row");
                if (!string.IsNullOrEmpty(key))
                {
                    var keyLabel = new Label(key);
                    keyLabel.AddToClassList("wizard-review-row-key");
                    row.Add(keyLabel);
                }
                var valueLabel = new Label(value);
                valueLabel.AddToClassList("wizard-review-row-value");
                row.Add(valueLabel);
                parent.Add(row);
            }
        }
    }

    /// <summary>Choice of how the new part's visual mesh is sourced.</summary>
    public enum SourceMeshChoice
    {
        /// <summary>The author will import the mesh later. The scaffold leaves model/ empty.</summary>
        Skip,

        /// <summary>An existing prefab is instantiated under the part's model/ subtree.</summary>
        ExistingPrefab,

        /// <summary>An imported FBX asset is instantiated under model/ with optional auto-scale and auto-rotate.</summary>
        FBX
    }
}
