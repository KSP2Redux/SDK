using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using KSP;
using KSP.Modules;
using KSP.OAB;
using KSP.Sim;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.CustomEditors;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.DataEditors
{
    /// <summary>
    /// Custom editor for <see cref="Data_Fairing" />. Surfaces three first-class authoring modes
    /// (Part Shroud / Variable Shroud / Fairing) with mode-switching that applies compatibility
    /// defaults, per-mode field subsets with renamed labels, a length-snap attach-node picker,
    /// the shroud-preview integration, a node-rings inline list, the cross-sections viewer, and
    /// a raw-data escape-hatch foldout.
    /// </summary>
    [DataEditor(typeof(Data_Fairing))]
    public sealed class FairingDataEditor : IDataEditor
    {
        private const string USS_PATH = "/Assets/Windows/PartAuthoring/Inspectors/DataEditors/DataEditors.uss";

        private enum AuthoringMode { PartShroud, VariableShroud, Fairing }

        private static readonly Dictionary<string, FieldInfo> FieldInfoCache = new();

        private PartBehaviourModule _module;
        private Transform _partRoot;
        private SerializedProperty _dataProp;

        private VisualElement _root;
        private VisualElement _modeContent;
        private Button _btnPartShroud;
        private Button _btnVariableShroud;
        private Button _btnFairing;
        private HelpBox _modeDescription;

        private string _selectedSnapNodeId;

        /// <inheritdoc />
        public VisualElement Build(SerializedProperty dataProp, PartBehaviourModule module)
        {
            _module = module;
            _partRoot = module == null ? null : module.gameObject.transform;
            _dataProp = dataProp;

            _root = new VisualElement();
            _root.style.flexDirection = FlexDirection.Column;

            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(SDKConfiguration.BasePath + USS_PATH);
            if (sheet != null)
            {
                _root.styleSheets.Add(sheet);
            }

            _root.Add(BuildModeBar());

            _modeContent = new VisualElement();
            _root.Add(_modeContent);

            _root.Add(BuildPreviewSection());
            _root.Add(BuildNodeRingsSection());

            RefreshModeContent();
            UpdateModeBarVisuals();
            return _root;
        }

        // -------------------- Mode bar --------------------

        private VisualElement BuildModeBar()
        {
            var outer = new VisualElement();
            outer.AddToClassList("data-editor-section");

            var label = new Label("Authoring Mode");
            label.AddToClassList("data-editor-section-header");
            outer.Add(label);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 4f;

            _btnPartShroud = new Button(() => SetMode(AuthoringMode.PartShroud)) { text = "Part Shroud" };
            _btnPartShroud.style.flexGrow = 1f;
            row.Add(_btnPartShroud);

            _btnVariableShroud = new Button(() => SetMode(AuthoringMode.VariableShroud)) { text = "Variable Shroud" };
            _btnVariableShroud.style.flexGrow = 1f;
            row.Add(_btnVariableShroud);

            _btnFairing = new Button(() => SetMode(AuthoringMode.Fairing)) { text = "Fairing" };
            _btnFairing.style.flexGrow = 1f;
            row.Add(_btnFairing);

            outer.Add(row);

            _modeDescription = new HelpBox(string.Empty, HelpBoxMessageType.Info);
            outer.Add(_modeDescription);

            return outer;
        }

        private void UpdateModeBarVisuals()
        {
            var current = GetCurrentMode();
            ApplyModeButtonStyle(_btnPartShroud, current == AuthoringMode.PartShroud);
            ApplyModeButtonStyle(_btnVariableShroud, current == AuthoringMode.VariableShroud);
            ApplyModeButtonStyle(_btnFairing, current == AuthoringMode.Fairing);

            _modeDescription.text = current switch
            {
                AuthoringMode.PartShroud =>
                    "Fixed-length cover for engines, heat shields, and similar parts. Uses a trigger node and covered node.",
                AuthoringMode.VariableShroud =>
                    "Length-driven shroud for engine mounts, tubes, and similar parts. Uses a generated floating node.",
                AuthoringMode.Fairing =>
                    "Player-built procedural fairing. Runtime uses the saved cross-section list.",
                _ => string.Empty,
            };
        }

        private static void ApplyModeButtonStyle(Button btn, bool selected)
        {
            if (selected)
            {
                btn.style.backgroundColor = new Color(70f / 255f, 100f / 255f, 140f / 255f, 0.55f);
                btn.style.color = new Color(245f / 255f, 250f / 255f, 1f);
                btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            else
            {
                btn.style.backgroundColor = StyleKeyword.Null;
                btn.style.color = StyleKeyword.Null;
                btn.style.unityFontStyleAndWeight = FontStyle.Normal;
            }
        }

        private AuthoringMode GetCurrentMode()
        {
            if (GetDataProp("IsShroud")?.boolValue == true)
            {
                return AuthoringMode.PartShroud;
            }
            var constructionType = GetModuleStoredValue("FairingConstructionType");
            if (GetDataProp("DefaultAutoConstruction")?.boolValue == true ||
                (constructionType != null && constructionType.enumValueIndex == (int)FairingConstructionType.AUTOMATED))
            {
                return AuthoringMode.VariableShroud;
            }
            return AuthoringMode.Fairing;
        }

        private void SetMode(AuthoringMode mode)
        {
            ApplyModeDefaults(mode);
            RefreshModeContent();
            UpdateModeBarVisuals();
        }

        private void ApplyModeDefaults(AuthoringMode mode)
        {
            _dataProp.serializedObject.Update();

            SetDataBool("IsShroud", mode == AuthoringMode.PartShroud);
            SetDataBool("DefaultAutoConstruction", mode != AuthoringMode.Fairing);

            var constructionType = mode == AuthoringMode.Fairing
                ? FairingConstructionType.CUSTOM
                : FairingConstructionType.AUTOMATED;
            SetModuleEnum("FairingConstructionType", (int)constructionType);

            if (mode == AuthoringMode.PartShroud)
            {
                SetModuleEnum("DeployType", (int)FairingDeployType.Shroud);
                SetDataEnum("DefaultDeployType", (int)FairingDeployType.Shroud);
                SetDataBool("ShouldCapOnAutoGenerate", false);
                SetDataBool("StageToggleDefault", false);
            }
            else
            {
                SetDataEnum("DefaultDeployType", (int)FairingDeployType.Clamshellx4);
                if (GetModuleStoredValue("DeployType")?.enumValueIndex == (int)FairingDeployType.Shroud)
                {
                    SetModuleEnum("DeployType", (int)FairingDeployType.Clamshellx4);
                }
            }

            if (mode == AuthoringMode.VariableShroud)
            {
                SetModuleBool("FloatingNodeEnabled", true);
                SetDataBool("AllowFloatingNodeChange", true);
                SetDataBool("DefaultFloatingNodeState", true);
            }
            else
            {
                SetModuleBool("FloatingNodeEnabled", false);
                SetDataBool("AllowFloatingNodeChange", false);
                SetDataBool("DefaultFloatingNodeState", false);
            }

            if (mode != AuthoringMode.Fairing)
            {
                SetModuleBool("InterstageNodeEnabled", false);
                SetDataBool("AllowInterstageNode", false);
                SetDataBool("DefaultInterstageNodeState", false);
                SetDataBool("AllowConstructionTypeChange", false);
                SetDataBool("ShouldCapOnAutoGenerate", false);
            }

            _dataProp.serializedObject.ApplyModifiedProperties();
        }

        // -------------------- Mode-specific content --------------------

        private void RefreshModeContent()
        {
            _modeContent.Clear();
            var mode = GetCurrentMode();
            _modeContent.Add(BuildRuntimeDefaultsBlock(mode));
            switch (mode)
            {
                case AuthoringMode.PartShroud:
                    _modeContent.Add(BuildPartShroudShapeFoldout());
                    _modeContent.Add(BuildAttachmentDetectionFoldout());
                    _modeContent.Add(BuildMeshTuningFoldout(showSnap: false, showLengthRange: false));
                    _modeContent.Add(BuildAeroAndCollidersFoldout());
                    break;
                case AuthoringMode.VariableShroud:
                    _modeContent.Add(BuildVariableShroudShapeFoldout());
                    _modeContent.Add(BuildFloatingNodeFoldout());
                    _modeContent.Add(BuildDeploymentFoldout());
                    _modeContent.Add(BuildMeshTuningFoldout(showSnap: true, showLengthRange: true));
                    _modeContent.Add(BuildAeroAndCollidersFoldout());
                    break;
                case AuthoringMode.Fairing:
                    _modeContent.Add(BuildFairingShapeFoldout());
                    _modeContent.Add(BuildInterstageNodeFoldout());
                    _modeContent.Add(BuildDeploymentFoldout());
                    _modeContent.Add(BuildMeshTuningFoldout(showSnap: true, showLengthRange: true));
                    _modeContent.Add(BuildAeroAndCollidersFoldout());
                    _modeContent.Add(BuildCrossSectionsViewer());
                    break;
            }
        }

        // -------------------- Section builders --------------------

        private VisualElement BuildRuntimeDefaultsBlock(AuthoringMode mode)
        {
            var section = new VisualElement();
            section.AddToClassList("data-editor-section");

            var header = new Label("Runtime Defaults");
            header.AddToClassList("data-editor-section-header");
            section.Add(header);

            AddModulePropertyRow(section, "FairingEnabled", "Enabled By Default");
            AddDataFieldRow(section, "DefaultFairingEnabledToggle", "Default PAM Toggle");
            if (mode == AuthoringMode.Fairing)
            {
                AddDataFieldRow(section, "AllowConstructionTypeChange", "Allow Construction Mode Change");
            }
            return section;
        }

        private VisualElement BuildPartShroudShapeFoldout()
        {
            var foldout = NewSubsectionFoldout("Shape");
            AddDataFieldRow(foldout, "FairingStartHeight", "Start Offset");
            AddDataFieldRow(foldout, "CrossSectionHeightMax", "Fixed Length");
            foldout.Add(BuildLengthSnapTool());
            AddDataFieldRow(foldout, "BaseRadius", "Base Radius");
            AddDataFieldRow(foldout, "CapRadius", "Target Min Radius");
            AddDataFieldRow(foldout, "MaxRadius", "Target Max Radius");
            AddDataFieldRow(foldout, "LocalUpAxis", "Local Axis");
            AddDataFieldRow(foldout, "Pivot", "Local Pivot");
            AddDataFieldRow(foldout, "FairingSideCount", "Side Count");
            AddDataFieldRow(foldout, "FairingThickness", "Wall Thickness");
            AddDataFieldRow(foldout, "FairingSmoothingAngle", "Smoothing Angle");
            return foldout;
        }

        private VisualElement BuildAttachmentDetectionFoldout()
        {
            var foldout = NewSubsectionFoldout("Attachment Detection");
            AddDataFieldRow(foldout, "FairingNode", "Trigger Node");
            AddDataFieldRow(foldout, "FloatingAttachNodeTag", "Covered Node");
            AddDataFieldRow(foldout, "MaxAutoFairingTargetRadius", "Max Target Size");
            AddDataFieldRow(foldout, "MinAutoFairingTargetRadius", "Min Target Size");
            return foldout;
        }

        private VisualElement BuildVariableShroudShapeFoldout()
        {
            var foldout = NewSubsectionFoldout("Generated Shape");
            AddDataFieldRow(foldout, "FairingNode", "Trigger Node");
            AddDataFieldRow(foldout, "FairingStartHeight", "Start Offset");
            AddDataFieldRow(foldout, "BaseRadius", "Base Radius");
            AddDataFieldRow(foldout, "CapRadius", "Cap Radius");
            AddDataFieldRow(foldout, "CloseRadius", "Close Radius");
            AddDataFieldRow(foldout, "MaxRadius", "Max Radius");
            AddDataFieldRow(foldout, "CrossSectionHeightMin", "Min Section Height");
            AddDataFieldRow(foldout, "CrossSectionHeightMax", "Max Section Height");
            AddDataFieldRow(foldout, "LengthEditMinimum", "Length Min");
            AddDataFieldRow(foldout, "LengthEditMaximum", "Length Max");
            AddDataFieldRow(foldout, "LengthEditDefault", "Length Default");
            AddDataFieldRow(foldout, "MaxAutoFairingTargetRadius", "Max Target Size");
            AddDataFieldRow(foldout, "MinAutoFairingTargetRadius", "Min Target Size");
            return foldout;
        }

        private VisualElement BuildFairingShapeFoldout()
        {
            var foldout = NewSubsectionFoldout("Shape");
            AddDataFieldRow(foldout, "FairingStartHeight", "Start Offset");
            AddDataFieldRow(foldout, "BaseRadius", "Base Radius");
            AddDataFieldRow(foldout, "CapRadius", "Cap Radius");
            AddDataFieldRow(foldout, "CloseRadius", "Close Radius");
            AddDataFieldRow(foldout, "MaxRadius", "Max Radius");
            AddDataFieldRow(foldout, "CrossSectionHeightMin", "Min Section Height");
            AddDataFieldRow(foldout, "CrossSectionHeightMax", "Max Section Height");
            return foldout;
        }

        private VisualElement BuildFloatingNodeFoldout()
        {
            var foldout = NewSubsectionFoldout("Floating End Node");
            AddDataFieldRow(foldout, "FloatingAttachNodeTag", "Node Tag");
            AddDataFieldRow(foldout, "FloatingNodeSize", "Node Size");
            AddDataFieldRow(foldout, "FloatingNodePosition", "Start Position");
            AddDataFieldRow(foldout, "FloatingNodeDirection", "Length Direction");
            AddDataFieldRow(foldout, "FloatingNodeIsMultiJoint", "Multi-Joint");

            var multiJointContent = new VisualElement();
            AddDataFieldRow(multiJointContent, "FloatingNodeMultiJointMaxCount", "Joint Count");
            AddDataFieldRow(multiJointContent, "FloatingNodeMultiJointOffset", "Joint Offset");
            foldout.Add(multiJointContent);
            GateOnBool(multiJointContent, "FloatingNodeIsMultiJoint");
            return foldout;
        }

        private VisualElement BuildInterstageNodeFoldout()
        {
            var foldout = NewSubsectionFoldout("Interstage Node");
            AddDataFieldRow(foldout, "AllowInterstageNode", "Has Interstage Node");

            var enabledContent = new VisualElement();
            AddModulePropertyRow(enabledContent, "InterstageNodeEnabled", "Currently Enabled");
            AddDataFieldRow(enabledContent, "DefaultInterstageNodeState", "Enabled By Default");
            AddDataFieldRow(enabledContent, "InterstageAttachNodeTag", "Node Tag");
            AddDataFieldRow(enabledContent, "InterstageNodeSize", "Node Size");
            AddDataFieldRow(enabledContent, "InterstageNodePosition", "Start Position");
            AddDataFieldRow(enabledContent, "InterstageNodeDirection", "Length Direction");
            AddDataFieldRow(enabledContent, "InterstageLengthMinimum", "Length Min");
            AddDataFieldRow(enabledContent, "InterstageLengthMaximum", "Length Max");
            AddDataFieldRow(enabledContent, "InterstageLengthDefault", "Length Default");
            AddDataFieldRow(enabledContent, "InterstageNodeIsMultiJoint", "Multi-Joint");

            var multiJointContent = new VisualElement();
            AddDataFieldRow(multiJointContent, "InterstageNodeMultiJointMaxCount", "Joint Count");
            AddDataFieldRow(multiJointContent, "InterstageNodeMultiJointOffset", "Joint Offset");
            enabledContent.Add(multiJointContent);
            GateOnBool(multiJointContent, "InterstageNodeIsMultiJoint");

            foldout.Add(enabledContent);
            GateOnBool(enabledContent, "AllowInterstageNode");
            return foldout;
        }

        private VisualElement BuildDeploymentFoldout()
        {
            var foldout = NewSubsectionFoldout("Deployment");
            AddModulePropertyRow(foldout, "DeployType", "Current Deploy Type");
            AddDataFieldRow(foldout, "DefaultDeployType", "Default Deploy Type");
            AddModulePropertyRow(foldout, "IsStagingEnabled", "Currently Stageable");
            AddDataFieldRow(foldout, "StageToggleDefault", "Stageable By Default");
            AddModulePropertyRow(foldout, "EjectionForce", "Ejection Force");
            AddModulePropertyRow(foldout, "IsDeployed", "Starts Deployed");
            return foldout;
        }

        private VisualElement BuildMeshTuningFoldout(bool showSnap, bool showLengthRange)
        {
            var foldout = NewSubsectionFoldout("Mesh Tuning");
            AddDataFieldRow(foldout, "FairingSideCount", "Side Count");
            AddDataFieldRow(foldout, "FairingThickness", "Wall Thickness");
            AddDataFieldRow(foldout, "FairingSmoothingAngle", "Smoothing Angle");
            AddDataFieldRow(foldout, "EdgeWarp", "Edge Warp");
            AddDataFieldRow(foldout, "AberrantNormalLimit", "Normal Limit");
            AddDataFieldRow(foldout, "MinHeightRadiusRatio", "Min Height/Radius Ratio");
            AddDataFieldRow(foldout, "MassAreaRatio", "Mass Per Area");
            AddDataFieldRow(foldout, "NoseTip", "Nose Tip");
            if (showSnap)
            {
                AddDataFieldRow(foldout, "SnapThreshold", "Snap Threshold");
                AddDataFieldRow(foldout, "FairingLengthSnapIncrement", "Length Snap");
                AddDataFieldRow(foldout, "FairingRadiusSnapIncrement", "Radius Snap");
            }
            if (showLengthRange)
            {
                AddModulePropertyRow(foldout, "Length", "Current Length");
            }
            return foldout;
        }

        private VisualElement BuildAeroAndCollidersFoldout()
        {
            var foldout = NewSubsectionFoldout("Aero And Colliders");
            AddDataFieldRow(foldout, "AerodynamicallyShieldContents", "Shield Contents");
            AddDataFieldRow(foldout, "CreateShellColliders", "Create Shell Colliders");
            AddDataFieldRow(foldout, "NumberOfCollidersPerCrossSection", "Colliders Per Section");
            AddDataFieldRow(foldout, "ConeSweepRays", "Sweep Rays");
            AddDataFieldRow(foldout, "ConeSweepPrecision", "Sweep Precision");
            return foldout;
        }

        private VisualElement BuildCrossSectionsViewer()
        {
            var foldout = NewSubsectionFoldout("Saved Cross Sections");
            foldout.value = false;
            var prop = _dataProp.FindPropertyRelative("CrossSections");
            if (prop != null)
            {
                var pf = new PropertyField(prop, "Cross Sections");
                pf.AddToClassList("unity-base-field__aligned");
                foldout.Add(pf);
            }
            return foldout;
        }

        // -------------------- Length Snap tool --------------------

        private VisualElement BuildLengthSnapTool()
        {
            var outer = new VisualElement();
            outer.AddToClassList("data-editor-inline-block");

            var header = new Label("Length Snap");
            header.AddToClassList("data-editor-subsection-header");
            outer.Add(header);

            var attachNodes = GetAttachNodes();
            if (attachNodes == null || attachNodes.Count == 0)
            {
                outer.Add(new HelpBox("No attachment nodes were found on the part data.", HelpBoxMessageType.Info));
                return outer;
            }

            var options = new List<string>(attachNodes.Count);
            for (var i = 0; i < attachNodes.Count; i++)
            {
                var node = attachNodes[i];
                options.Add(string.IsNullOrEmpty(node.nodeID) ? $"Node {i}" : node.nodeID);
            }

            var initialIndex = Mathf.Clamp(GetSelectedSnapNodeIndex(attachNodes), 0, options.Count - 1);
            var popup = new PopupField<string>("Target Node", options, initialIndex);
            popup.AddToClassList("unity-base-field__aligned");
            outer.Add(popup);

            var axisField = new TextField("Node Axis Position");
            axisField.AddToClassList("unity-base-field__aligned");
            axisField.SetEnabled(false);
            outer.Add(axisField);

            var lengthField = new TextField("Length To Node");
            lengthField.AddToClassList("unity-base-field__aligned");
            lengthField.SetEnabled(false);
            outer.Add(lengthField);

            void UpdateForIndex(int idx)
            {
                if (idx < 0 || idx >= attachNodes.Count)
                {
                    return;
                }
                var node = attachNodes[idx];
                _selectedSnapNodeId = node.nodeID;
                var axis = GetNodeAxisHeight(node);
                var snapped = axis - GetRuntimeStartHeight();
                axisField.SetValueWithoutNotify(FormatMeters(axis));
                lengthField.SetValueWithoutNotify(FormatMeters(snapped));
            }

            UpdateForIndex(initialIndex);

            popup.RegisterValueChangedCallback(evt =>
            {
                var idx = options.IndexOf(evt.newValue);
                UpdateForIndex(idx);
            });

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.marginTop = 4f;

            var snapBtn = new Button(() =>
            {
                var idx = options.IndexOf(popup.value);
                if (idx < 0 || idx >= attachNodes.Count)
                {
                    return;
                }
                var snapped = GetNodeAxisHeight(attachNodes[idx]) - GetRuntimeStartHeight();
                var lengthProp = GetDataProp("CrossSectionHeightMax");
                if (lengthProp != null)
                {
                    lengthProp.serializedObject.Update();
                    lengthProp.floatValue = snapped;
                    lengthProp.serializedObject.ApplyModifiedProperties();
                    SceneView.RepaintAll();
                }
            }) { text = "Snap Shroud Length" };
            snapBtn.style.flexGrow = 1f;
            snapBtn.style.marginRight = 4f;
            btnRow.Add(snapBtn);

            var useCoveredBtn = new Button(() =>
            {
                var idx = options.IndexOf(popup.value);
                if (idx < 0 || idx >= attachNodes.Count)
                {
                    return;
                }
                var coveredProp = GetDataProp("FloatingAttachNodeTag");
                if (coveredProp != null)
                {
                    coveredProp.serializedObject.Update();
                    coveredProp.stringValue = attachNodes[idx].nodeID;
                    coveredProp.serializedObject.ApplyModifiedProperties();
                }
            }) { text = "Use As Covered Node" };
            useCoveredBtn.style.flexGrow = 1f;
            btnRow.Add(useCoveredBtn);
            outer.Add(btnRow);

            return outer;
        }

        // -------------------- Preview section --------------------

        private VisualElement BuildPreviewSection()
        {
            var fairingModule = _module as Module_Fairing;
            if (fairingModule == null)
            {
                return new VisualElement();
            }

            var foldout = NewSubsectionFoldout("Generated Shape Preview");

            var fairing = ShroudPreviewEditor.GetFairingData(fairingModule);
            if (fairing == null)
            {
                foldout.Add(new HelpBox("Fairing data is missing on this module.", HelpBoxMessageType.Warning));
                return foldout;
            }

            var settings = ShroudPreviewEditor.GetOrCreateSettings(fairingModule, fairing);

            var showToggle = new Toggle("Show Scene Preview") { value = settings.Enabled };
            showToggle.AddToClassList("unity-base-field__aligned");
            showToggle.RegisterValueChangedCallback(evt =>
            {
                settings.Enabled = evt.newValue;
                SceneView.RepaintAll();
            });
            foldout.Add(showToggle);

            // Target Part Size popup. Index-based so display label and stored key stay linked.
            var sizeDefs = PartSizeRegistry.Definitions;
            var sizeOptions = new List<string>(sizeDefs.Count);
            var initialSizeIndex = 0;
            for (var i = 0; i < sizeDefs.Count; i++)
            {
                var def = sizeDefs[i];
                sizeOptions.Add(def.DisplayName + " (" + def.Diameter.ToString("0.####", CultureInfo.InvariantCulture) + " m)");
                if (string.Equals(def.Key, settings.TargetSizeKey, StringComparison.OrdinalIgnoreCase))
                {
                    initialSizeIndex = i;
                }
            }
            var sizePopup = new PopupField<string>("Target Part Size", sizeOptions, Mathf.Clamp(initialSizeIndex, 0, sizeOptions.Count - 1));
            sizePopup.AddToClassList("unity-base-field__aligned");
            foldout.Add(sizePopup);

            // Subsection header making the calculated-vs-editable split visually explicit.
            var metricsHeader = new Label("Calculated Metrics");
            metricsHeader.AddToClassList("data-editor-subsection-header");
            metricsHeader.style.marginTop = 4f;
            foldout.Add(metricsHeader);

            var hostDiameterField = NewMetricField("Host Diameter", "Diameter of the part this fairing sits on (BaseRadius * 2).");
            var targetDiameterField = NewMetricField("Target Diameter", "Diameter of the part the fairing would cover, from the selected Target Part Size.");
            var previewDiameterField = NewMetricField("Preview Diameter", "Resolved diameter after applying MinAuto/MaxAuto target-radius clamps and snapping to a known size.");
            var generatedLengthField = NewMetricField("Generated Length", "Distance the fairing extends along its local up-axis from the start height.");
            var heightRangeField = NewMetricField("Height Range", "Start and end positions along the local up-axis where the fairing's geometry occupies.");

            foldout.Add(hostDiameterField);
            foldout.Add(targetDiameterField);
            foldout.Add(previewDiameterField);
            foldout.Add(generatedLengthField);
            foldout.Add(heightRangeField);

            void RefreshMetrics()
            {
                var data = ShroudPreviewEditor.GetFairingData(fairingModule);
                if (data == null)
                {
                    return;
                }
                var modelTransform = ShroudPreviewEditor.GetPreviewModelTransform(_partRoot);
                var metrics = ShroudPreviewEditor.CalculateMetrics(data, settings.TargetSizeKey, modelTransform);
                hostDiameterField.SetValueWithoutNotify(ShroudPreviewEditor.FormatMeters(metrics.HostDiameter));
                targetDiameterField.SetValueWithoutNotify(ShroudPreviewEditor.FormatMeters(metrics.TargetDiameter));
                previewDiameterField.SetValueWithoutNotify(ShroudPreviewEditor.FormatMeters(metrics.ResolvedDiameter));
                generatedLengthField.SetValueWithoutNotify(ShroudPreviewEditor.FormatMeters(Mathf.Abs(metrics.GeneratedHeight)));
                heightRangeField.SetValueWithoutNotify(
                    ShroudPreviewEditor.FormatMeters(metrics.StartHeight) + " to " + ShroudPreviewEditor.FormatMeters(metrics.EndHeight));
            }

            sizePopup.RegisterValueChangedCallback(evt =>
            {
                var idx = sizeOptions.IndexOf(evt.newValue);
                if (idx >= 0 && idx < sizeDefs.Count)
                {
                    settings.TargetSizeKey = sizeDefs[idx].Key;
                    RefreshMetrics();
                    SceneView.RepaintAll();
                }
            });

            foldout.TrackSerializedObjectValue(_dataProp.serializedObject, _ => RefreshMetrics());

            RefreshMetrics();
            return foldout;
        }

        private static TextField NewMetricField(string label, string tooltip)
        {
            var field = new TextField(label) { tooltip = tooltip };
            field.AddToClassList("unity-base-field__aligned");
            field.SetEnabled(false);
            return field;
        }

        // -------------------- NodeRings inline list --------------------

        private VisualElement BuildNodeRingsSection()
        {
            var fairingModule = _module as Module_Fairing;
            if (fairingModule == null)
            {
                return new VisualElement();
            }

            var moduleSo = new SerializedObject(fairingModule);
            var nodeRingsProp = moduleSo.FindProperty("NodeRings");
            if (nodeRingsProp == null)
            {
                return new VisualElement();
            }

            var section = new VisualElement();
            section.AddToClassList("data-editor-section");

            var header = new Label("Node Rings");
            header.AddToClassList("data-editor-section-header");
            section.Add(header);

            section.Add(InlineListBlock.Build(
                nodeRingsProp,
                titleFormat: "Rings ({0})",
                addButtonText: "+ Add Ring",
                emptyHint: "(none)",
                rowBuilder: BuildAttachNodeRingCard,
                onAdd: entry =>
                {
                    var nameProp = entry.FindPropertyRelative("RingName");
                    if (nameProp != null)
                    {
                        nameProp.stringValue = "ring";
                    }
                }));
            section.Bind(moduleSo);
            return section;
        }

        private VisualElement BuildAttachNodeRingCard(SerializedProperty entry, int index, Action onDelete)
        {
            var card = new VisualElement();
            card.AddToClassList("data-editor-card");

            var header = new VisualElement();
            header.AddToClassList("data-editor-card-header");

            var nameProp = entry.FindPropertyRelative("RingName");
            var nameField = new TextField { value = nameProp?.stringValue ?? string.Empty, isDelayed = true };
            nameField.AddToClassList("data-editor-card-name-field");
            if (nameProp != null)
            {
                nameField.RegisterValueChangedCallback(evt =>
                {
                    nameProp.serializedObject.Update();
                    nameProp.stringValue = evt.newValue ?? string.Empty;
                    nameProp.serializedObject.ApplyModifiedProperties();
                });
            }
            header.Add(nameField);

            var removeBtn = new Button(onDelete) { text = "X" };
            removeBtn.AddToClassList("data-editor-card-remove-btn");
            header.Add(removeBtn);
            card.Add(header);

            var body = new VisualElement();
            body.AddToClassList("data-editor-card-body");
            body.Add(BuildTripletRow("Count", entry, "RingNodeCountMin", "RingNodeCountDefault", "RingNodeCountMax", isInt: true));
            body.Add(BuildTripletRow("Distance", entry, "RingNodeDistanceMin", "RingNodeDistanceDefault", "RingNodeDistanceMax", isInt: false));
            body.Add(BuildTripletRow("Size", entry, "RingNodeSizeMin", "RingNodeSizeDefault", "RingNodeSizeMax", isInt: true));
            card.Add(body);

            return card;
        }

        private static VisualElement BuildTripletRow(string label, SerializedProperty entry, string minName, string defaultName, string maxName, bool isInt)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2f;

            var caption = new Label(label);
            caption.style.width = 70f;
            caption.style.flexShrink = 0;
            caption.style.color = new Color(195f / 255f, 205f / 255f, 220f / 255f);
            row.Add(caption);

            AddTripletField(row, entry, minName, "Min", isInt);
            AddTripletField(row, entry, defaultName, "Default", isInt);
            AddTripletField(row, entry, maxName, "Max", isInt);
            return row;
        }

        private static void AddTripletField(VisualElement row, SerializedProperty entry, string fieldName, string label, bool isInt)
        {
            var wrapper = new VisualElement();
            wrapper.style.flexDirection = FlexDirection.Row;
            wrapper.style.alignItems = Align.Center;
            wrapper.style.flexGrow = 1f;
            wrapper.style.flexShrink = 1f;
            wrapper.style.minWidth = 0;
            wrapper.style.flexBasis = 0;
            wrapper.style.marginRight = 4f;

            var miniLabel = new Label(label);
            miniLabel.style.color = new Color(180f / 255f, 195f / 255f, 215f / 255f);
            miniLabel.style.marginRight = 2f;
            miniLabel.style.flexShrink = 0;
            wrapper.Add(miniLabel);

            var prop = entry.FindPropertyRelative(fieldName);
            if (prop == null)
            {
                wrapper.Add(new Label("-"));
                row.Add(wrapper);
                return;
            }

            if (isInt)
            {
                var f = new IntegerField { value = prop.intValue, isDelayed = true };
                f.style.flexGrow = 1f;
                f.style.minWidth = 0;
                f.RegisterValueChangedCallback(evt =>
                {
                    prop.serializedObject.Update();
                    prop.intValue = evt.newValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
                wrapper.Add(f);
            }
            else
            {
                var f = new FloatField { value = prop.floatValue, isDelayed = true };
                f.style.flexGrow = 1f;
                f.style.minWidth = 0;
                f.RegisterValueChangedCallback(evt =>
                {
                    prop.serializedObject.Update();
                    prop.floatValue = evt.newValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
                wrapper.Add(f);
            }
            row.Add(wrapper);
        }

        // -------------------- Helpers --------------------

        private static Foldout NewSubsectionFoldout(string title)
        {
            var foldout = new Foldout { text = title, value = true };
            foldout.AddToClassList("data-editor-subsection-foldout");
            return foldout;
        }

        private static FieldInfo ResolveFieldInfo(string fieldName)
        {
            if (FieldInfoCache.TryGetValue(fieldName, out var cached))
            {
                return cached;
            }
            var fi = typeof(Data_Fairing).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            FieldInfoCache[fieldName] = fi;
            return fi;
        }

        private SerializedProperty GetDataProp(string fieldName)
        {
            return _dataProp?.FindPropertyRelative(fieldName);
        }

        private SerializedProperty GetModuleStoredValue(string fieldName)
        {
            var prop = GetDataProp(fieldName);
            return prop?.FindPropertyRelative("storedValue") ?? prop;
        }

        private void AddDataFieldRow(VisualElement parent, string fieldName, string label = null)
        {
            var prop = GetDataProp(fieldName);
            if (prop == null)
            {
                return;
            }
            var field = ResolveFieldInfo(fieldName);
            if (field == null)
            {
                return;
            }
            var row = ReflectionModuleEditor.BuildFieldRowForCustomEditor(prop, field, _partRoot, label);
            if (row != null)
            {
                parent.Add(row);
            }
        }

        private void AddModulePropertyRow(VisualElement parent, string fieldName, string label = null)
        {
            var prop = GetDataProp(fieldName);
            if (prop == null)
            {
                return;
            }
            var stored = prop.FindPropertyRelative("storedValue");
            if (stored == null)
            {
                return;
            }
            var pf = new PropertyField(stored, label ?? prop.displayName);
            pf.AddToClassList("unity-base-field__aligned");
            parent.Add(pf);
        }

        private void GateOnBool(VisualElement content, string boolFieldName)
        {
            var prop = GetDataProp(boolFieldName);
            if (prop == null)
            {
                return;
            }
            content.style.display = prop.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            content.TrackPropertyValue(prop, p =>
            {
                content.style.display = p.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            });
        }

        private void SetDataBool(string fieldName, bool value)
        {
            var prop = GetDataProp(fieldName);
            if (prop != null)
            {
                prop.boolValue = value;
            }
        }

        private void SetDataEnum(string fieldName, int value)
        {
            var prop = GetDataProp(fieldName);
            if (prop != null)
            {
                prop.enumValueIndex = value;
            }
        }

        private void SetModuleBool(string fieldName, bool value)
        {
            var prop = GetModuleStoredValue(fieldName);
            if (prop != null)
            {
                prop.boolValue = value;
            }
        }

        private void SetModuleEnum(string fieldName, int value)
        {
            var prop = GetModuleStoredValue(fieldName);
            if (prop != null)
            {
                prop.enumValueIndex = value;
            }
        }

        // -------------------- Length-snap geometry helpers (lifted from legacy) --------------------

        private IReadOnlyList<AttachNodeDefinition> GetAttachNodes()
        {
            if (_module == null)
            {
                return null;
            }
            var coreData = _module.GetComponent<CorePartData>() ?? _module.GetComponentInParent<CorePartData>();
            return coreData?.Data?.attachNodes;
        }

        private int GetSelectedSnapNodeIndex(IReadOnlyList<AttachNodeDefinition> attachNodes)
        {
            var preferredNodeId = _selectedSnapNodeId;
            if (string.IsNullOrEmpty(preferredNodeId))
            {
                preferredNodeId = GetDataProp("FloatingAttachNodeTag")?.stringValue;
            }
            if (string.IsNullOrEmpty(preferredNodeId))
            {
                preferredNodeId = GetDataProp("FairingNode")?.stringValue;
            }
            if (!string.IsNullOrEmpty(preferredNodeId))
            {
                for (var i = 0; i < attachNodes.Count; i++)
                {
                    if (string.Equals(attachNodes[i].nodeID, preferredNodeId, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }
            return 0;
        }

        private float GetNodeAxisHeight(AttachNodeDefinition node)
        {
            var moduleTransform = _partRoot;
            var previewTransform = GetPreviewModelTransform(moduleTransform) ?? moduleTransform;
            var nodeLocalPosition = ToVector3(node.position);
            if (moduleTransform != null && previewTransform != null && previewTransform != moduleTransform)
            {
                nodeLocalPosition = previewTransform.InverseTransformPoint(moduleTransform.TransformPoint(nodeLocalPosition));
            }
            var axis = GetVector3DataField("LocalUpAxis", Vector3.up);
            axis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.up;
            var pivot = GetVector3DataField("Pivot", Vector3.zero);
            return Vector3.Dot(nodeLocalPosition - pivot, axis);
        }

        private float GetRuntimeStartHeight()
        {
            var localUpAxis = GetVector3DataField("LocalUpAxis", Vector3.up);
            var localUpSign = Mathf.Sign(localUpAxis.x) * Mathf.Sign(localUpAxis.y) * Mathf.Sign(localUpAxis.z);
            var startHeight = (GetDataProp("FairingStartHeight")?.floatValue ?? 0f) * localUpSign;
            var modelTransform = GetPreviewModelTransform(_partRoot);
            if (modelTransform == null)
            {
                return startHeight;
            }
            var modelTransformSign = Mathf.Sign(modelTransform.localPosition.x) *
                Mathf.Sign(modelTransform.localPosition.y) *
                Mathf.Sign(modelTransform.localPosition.z);
            return startHeight - Vector3.Scale(modelTransform.localPosition, localUpAxis).magnitude * localUpSign * modelTransformSign;
        }

        private Vector3 GetVector3DataField(string fieldName, Vector3 fallback)
        {
            var prop = GetDataProp(fieldName);
            return prop is { propertyType: SerializedPropertyType.Vector3 } ? prop.vector3Value : fallback;
        }

        private static Transform GetPreviewModelTransform(Transform partTransform)
        {
            return partTransform == null ? null : FindChildRecursive(partTransform, "model");
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent.name == childName)
            {
                return parent;
            }
            foreach (Transform child in parent)
            {
                var match = FindChildRecursive(child, childName);
                if (match != null)
                {
                    return match;
                }
            }
            return null;
        }

        private static Vector3 ToVector3(Vector3d value)
        {
            return new Vector3((float)value.x, (float)value.y, (float)value.z);
        }

        private static string FormatMeters(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture) + " m";
        }
    }
}
