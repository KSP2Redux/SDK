using KSP.Modules;
using System;
using System.Collections.Generic;
using System.Globalization;
using KSP;
using KSP.OAB;
using KSP.Sim.Definitions;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.CustomEditors
{
    [CustomEditor(typeof(Module_Fairing))]
    public class FairingModuleEditor : UnityEditor.Editor
    {
        private enum FairingAuthoringMode
        {
            PartShroud,
            VariableShroud,
            Fairing
        }

        private static readonly GUIContent[] ModeLabels =
        {
            new("Part Shroud"),
            new("Variable Shroud"),
            new("Fairing")
        };

        private static bool _shapeFoldout = true;
        private static bool _attachmentFoldout = true;
        private static bool _floatingNodeFoldout = true;
        private static bool _interstageNodeFoldout = true;
        private static bool _deploymentFoldout = true;
        private static bool _generationFoldout = true;
        private static bool _aeroFoldout;
        private static bool _moduleReferencesFoldout;
        private static bool _rawFoldout;

        private SerializedProperty _dataFairing;
        private string _selectedShroudSnapNodeId;

        private Module_Fairing TargetModule => target as Module_Fairing;

        private void OnEnable()
        {
            _dataFairing = serializedObject.FindProperty("_dataFairing");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty script = serializedObject.FindProperty("m_Script");
            using (new EditorGUI.DisabledScope(true))
            {
                if (script != null)
                {
                    EditorGUILayout.PropertyField(script);
                }
            }

            if (_dataFairing == null)
            {
                EditorGUILayout.HelpBox("Fairing data is missing on this module.", MessageType.Error);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            DrawModeSelector();
            EditorGUILayout.Space();

            FairingAuthoringMode mode = GetMode();
            ApplyModeCompatibilityDefaults(mode);
            switch (mode)
            {
                case FairingAuthoringMode.PartShroud:
                    DrawPartShroudInspector();
                    break;
                case FairingAuthoringMode.VariableShroud:
                    DrawVariableShroudInspector();
                    break;
                case FairingAuthoringMode.Fairing:
                    DrawFairingInspector();
                    break;
            }

            bool changedBeforePreview = serializedObject.ApplyModifiedProperties();
            if (changedBeforePreview)
            {
                EditorUtility.SetDirty(TargetModule);
                SceneView.RepaintAll();
                serializedObject.Update();
            }

            ShroudPreviewEditor.DrawInspector(TargetModule);
            DrawRawDataInspector();
            DrawModuleReferencesInspector();

            if (serializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(TargetModule);
                SceneView.RepaintAll();
            }
        }

        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawGizmoForFairing(Module_Fairing module, GizmoType gizmoType)
        {
            ShroudPreviewEditor.DrawGizmo(module);
        }

        private void DrawModeSelector()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Fairing Authoring", EditorStyles.boldLabel);
                FairingAuthoringMode currentMode = GetMode();
                EditorGUI.BeginChangeCheck();
                FairingAuthoringMode newMode = (FairingAuthoringMode)GUILayout.Toolbar((int)currentMode, ModeLabels);
                if (EditorGUI.EndChangeCheck())
                {
                    ApplyMode(newMode);
                }

                string summary = newMode switch
                {
                    FairingAuthoringMode.PartShroud =>
                        "Fixed-length cover for engines, heat shields, and similar parts. Uses a trigger node and covered node.",
                    FairingAuthoringMode.VariableShroud =>
                        "Length-driven shroud for engine mounts, tubes, and similar parts. Uses a generated floating node.",
                    FairingAuthoringMode.Fairing =>
                        "Player-built procedural fairing. Runtime uses the saved cross-section list.",
                    _ => string.Empty
                };
                EditorGUILayout.HelpBox(summary, MessageType.Info);
            }
        }

        private FairingAuthoringMode GetMode()
        {
            if (GetDataField("IsShroud")?.boolValue == true)
            {
                return FairingAuthoringMode.PartShroud;
            }

            SerializedProperty constructionType = GetModuleStoredValue("FairingConstructionType");
            if (GetDataField("DefaultAutoConstruction")?.boolValue == true ||
                constructionType != null && constructionType.enumValueIndex == (int)FairingConstructionType.AUTOMATED)
            {
                return FairingAuthoringMode.VariableShroud;
            }

            return FairingAuthoringMode.Fairing;
        }

        private void ApplyMode(FairingAuthoringMode mode)
        {
            SetDataBool("IsShroud", mode == FairingAuthoringMode.PartShroud);
            SetDataBool("DefaultAutoConstruction", mode != FairingAuthoringMode.Fairing);
            ApplyModeCompatibilityDefaults(mode);

            FairingConstructionType constructionType = mode == FairingAuthoringMode.Fairing
                ? FairingConstructionType.CUSTOM
                : FairingConstructionType.AUTOMATED;
            SetModuleEnum("FairingConstructionType", (int)constructionType);

            if (mode == FairingAuthoringMode.PartShroud)
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
        }

        private void ApplyModeCompatibilityDefaults(FairingAuthoringMode mode)
        {
            if (mode == FairingAuthoringMode.VariableShroud)
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

            if (mode != FairingAuthoringMode.Fairing)
            {
                SetModuleBool("InterstageNodeEnabled", false);
                SetDataBool("AllowInterstageNode", false);
                SetDataBool("DefaultInterstageNodeState", false);
            }

            if (mode != FairingAuthoringMode.Fairing)
            {
                SetDataBool("AllowConstructionTypeChange", false);
                SetDataBool("ShouldCapOnAutoGenerate", false);
            }
        }

        private void DrawPartShroudInspector()
        {
            DrawCommonDefaults(FairingAuthoringMode.PartShroud);

            _shapeFoldout = EditorGUILayout.Foldout(_shapeFoldout, "Shape", true);
            if (_shapeFoldout)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawDataField("FairingStartHeight", "Start Offset");
                    DrawDataField("CrossSectionHeightMax", "Fixed Length");
                    DrawShroudLengthSnapControls();
                    DrawDataField("BaseRadius", "Base Radius");
                    DrawDataField("CapRadius", "Target Min Radius");
                    DrawDataField("MaxRadius", "Target Max Radius");
                    DrawDataField("LocalUpAxis", "Local Axis");
                    DrawDataField("Pivot", "Local Pivot");
                    DrawDataField("FairingSideCount", "Side Count");
                    DrawDataField("FairingThickness", "Wall Thickness");
                    DrawDataField("FairingSmoothingAngle", "Smoothing Angle");
                }
            }

            _attachmentFoldout = EditorGUILayout.Foldout(_attachmentFoldout, "Attachment Detection", true);
            if (_attachmentFoldout)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawDataField("FairingNode", "Trigger Node");
                    DrawDataField("FloatingAttachNodeTag", "Covered Node");
                    DrawDataField("MaxAutoFairingTargetRadius", "Max Target Size");
                    DrawDataField("MinAutoFairingTargetRadius", "Min Target Size");
                }
            }

            DrawGenerationTuning(showSnap: false, showLengthRange: false);
            DrawAeroAndColliders();
        }

        private void DrawShroudLengthSnapControls()
        {
            IReadOnlyList<AttachNodeDefinition> attachNodes = GetAttachNodes();
            SerializedProperty lengthProperty = GetDataField("CrossSectionHeightMax");
            if (lengthProperty == null)
            {
                return;
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Length Snap", EditorStyles.boldLabel);
            if (attachNodes == null || attachNodes.Count == 0)
            {
                EditorGUILayout.HelpBox("No attachment nodes were found on the part data.", MessageType.Info);
                return;
            }

            int selectedIndex = GetSelectedSnapNodeIndex(attachNodes);
            GUIContent[] options = BuildAttachNodeOptions(attachNodes);
            selectedIndex = EditorGUILayout.Popup(new GUIContent("Target Node"), selectedIndex, options);
            selectedIndex = Mathf.Clamp(selectedIndex, 0, attachNodes.Count - 1);

            AttachNodeDefinition selectedNode = attachNodes[selectedIndex];
            _selectedShroudSnapNodeId = selectedNode.nodeID;

            float targetAxisHeight = GetNodeAxisHeight(selectedNode);
            float snappedLength = targetAxisHeight - GetRuntimeStartHeight();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Node Axis Position", FormatMeters(targetAxisHeight));
                EditorGUILayout.TextField("Length To Node", FormatMeters(snappedLength));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Snap Shroud Length"))
                {
                    lengthProperty.floatValue = snappedLength;
                    SceneView.RepaintAll();
                }

                SerializedProperty coveredNode = GetDataField("FloatingAttachNodeTag");
                using (new EditorGUI.DisabledScope(coveredNode == null))
                {
                    if (GUILayout.Button("Use As Covered Node"))
                    {
                        coveredNode.stringValue = selectedNode.nodeID;
                    }
                }
            }
        }

        private void DrawVariableShroudInspector()
        {
            DrawCommonDefaults(FairingAuthoringMode.VariableShroud);

            _shapeFoldout = EditorGUILayout.Foldout(_shapeFoldout, "Generated Shape", true);
            if (_shapeFoldout)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawDataField("FairingNode", "Trigger Node");
                    DrawDataField("FairingStartHeight", "Start Offset");
                    DrawDataField("BaseRadius", "Base Radius");
                    DrawDataField("CapRadius", "Cap Radius");
                    DrawDataField("CloseRadius", "Close Radius");
                    DrawDataField("MaxRadius", "Max Radius");
                    DrawDataField("CrossSectionHeightMin", "Min Section Height");
                    DrawDataField("CrossSectionHeightMax", "Max Section Height");
                    DrawDataField("LengthEditMinimum", "Length Min");
                    DrawDataField("LengthEditMaximum", "Length Max");
                    DrawDataField("LengthEditDefault", "Length Default");
                    DrawDataField("MaxAutoFairingTargetRadius", "Max Target Size");
                    DrawDataField("MinAutoFairingTargetRadius", "Min Target Size");
                }
            }

            DrawFloatingNodeControls();
            DrawDeploymentControls();
            DrawGenerationTuning(showSnap: true, showLengthRange: true);
            DrawAeroAndColliders();
        }

        private void DrawFairingInspector()
        {
            DrawCommonDefaults(FairingAuthoringMode.Fairing);

            _shapeFoldout = EditorGUILayout.Foldout(_shapeFoldout, "Shape", true);
            if (_shapeFoldout)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawDataField("FairingStartHeight", "Start Offset");
                    DrawDataField("BaseRadius", "Base Radius");
                    DrawDataField("CapRadius", "Cap Radius");
                    DrawDataField("CloseRadius", "Close Radius");
                    DrawDataField("MaxRadius", "Max Radius");
                    DrawDataField("CrossSectionHeightMin", "Min Section Height");
                    DrawDataField("CrossSectionHeightMax", "Max Section Height");
                    DrawDataField("CrossSections", "Saved Cross Sections", true);
                }
            }

            DrawInterstageControls();
            DrawDeploymentControls();
            DrawGenerationTuning(showSnap: true, showLengthRange: true);
            DrawAeroAndColliders();
        }

        private void DrawCommonDefaults(FairingAuthoringMode mode)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Runtime Defaults", EditorStyles.boldLabel);
                DrawModuleStoredValue("FairingEnabled", "Enabled By Default");
                DrawDataField("DefaultFairingEnabledToggle", "Default PAM Toggle");
                if (mode == FairingAuthoringMode.Fairing)
                {
                    DrawDataField("AllowConstructionTypeChange", "Allow Construction Mode Change");
                }
            }
        }

        private void DrawFloatingNodeControls()
        {
            _floatingNodeFoldout = EditorGUILayout.Foldout(_floatingNodeFoldout, "Floating End Node", true);
            if (!_floatingNodeFoldout)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                DrawDataField("FloatingAttachNodeTag", "Node Tag");
                DrawDataField("FloatingNodeSize", "Node Size");
                DrawDataField("FloatingNodePosition", "Start Position");
                DrawDataField("FloatingNodeDirection", "Length Direction");
                DrawDataField("FloatingNodeIsMultiJoint", "Multi-Joint");
                if (GetDataField("FloatingNodeIsMultiJoint")?.boolValue == true)
                {
                    DrawDataField("FloatingNodeMultiJointMaxCount", "Joint Count");
                    DrawDataField("FloatingNodeMultiJointOffset", "Joint Offset");
                }
            }
        }

        private void DrawInterstageControls()
        {
            bool allowInterstage = GetDataField("AllowInterstageNode")?.boolValue == true;
            _interstageNodeFoldout = EditorGUILayout.Foldout(_interstageNodeFoldout, "Interstage Node", true);
            if (!_interstageNodeFoldout)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                DrawDataField("AllowInterstageNode", "Has Interstage Node");
                if (!allowInterstage)
                {
                    return;
                }

                DrawModuleStoredValue("InterstageNodeEnabled", "Currently Enabled");
                DrawDataField("DefaultInterstageNodeState", "Enabled By Default");
                DrawDataField("InterstageAttachNodeTag", "Node Tag");
                DrawDataField("InterstageNodeSize", "Node Size");
                DrawDataField("InterstageNodePosition", "Start Position");
                DrawDataField("InterstageNodeDirection", "Length Direction");
                DrawDataField("InterstageLengthMinimum", "Length Min");
                DrawDataField("InterstageLengthMaximum", "Length Max");
                DrawDataField("InterstageLengthDefault", "Length Default");
                DrawDataField("InterstageNodeIsMultiJoint", "Multi-Joint");
                if (GetDataField("InterstageNodeIsMultiJoint")?.boolValue == true)
                {
                    DrawDataField("InterstageNodeMultiJointMaxCount", "Joint Count");
                    DrawDataField("InterstageNodeMultiJointOffset", "Joint Offset");
                }
            }
        }

        private void DrawDeploymentControls()
        {
            _deploymentFoldout = EditorGUILayout.Foldout(_deploymentFoldout, "Deployment", true);
            if (!_deploymentFoldout)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                DrawModuleStoredValue("DeployType", "Current Deploy Type");
                DrawDataField("DefaultDeployType", "Default Deploy Type");
                DrawModuleStoredValue("IsStagingEnabled", "Currently Stageable");
                DrawDataField("StageToggleDefault", "Stageable By Default");
                DrawModuleStoredValue("EjectionForce", "Ejection Force");
                DrawModuleStoredValue("IsDeployed", "Starts Deployed");
            }
        }

        private void DrawGenerationTuning(bool showSnap, bool showLengthRange)
        {
            _generationFoldout = EditorGUILayout.Foldout(_generationFoldout, "Mesh Tuning", true);
            if (!_generationFoldout)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                DrawDataField("FairingSideCount", "Side Count");
                DrawDataField("FairingThickness", "Wall Thickness");
                DrawDataField("FairingSmoothingAngle", "Smoothing Angle");
                DrawDataField("EdgeWarp", "Edge Warp");
                DrawDataField("AberrantNormalLimit", "Normal Limit");
                DrawDataField("MinHeightRadiusRatio", "Min Height/Radius Ratio");
                DrawDataField("MassAreaRatio", "Mass Per Area");
                DrawDataField("NoseTip", "Nose Tip");
                if (showSnap)
                {
                    DrawDataField("SnapThreshold", "Snap Threshold");
                    DrawDataField("FairingLengthSnapIncrement", "Length Snap");
                    DrawDataField("FairingRadiusSnapIncrement", "Radius Snap");
                }

                if (showLengthRange)
                {
                    DrawModuleStoredValue("Length", "Current Length");
                }
            }
        }

        private void DrawAeroAndColliders()
        {
            _aeroFoldout = EditorGUILayout.Foldout(_aeroFoldout, "Aero And Colliders", true);
            if (!_aeroFoldout)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                DrawDataField("AerodynamicallyShieldContents", "Shield Contents");
                DrawDataField("CreateShellColliders", "Create Shell Colliders");
                DrawDataField("NumberOfCollidersPerCrossSection", "Colliders Per Section");
                DrawDataField("ConeSweepRays", "Sweep Rays");
                DrawDataField("ConeSweepPrecision", "Sweep Precision");
            }
        }

        private void DrawRawDataInspector()
        {
            _rawFoldout = EditorGUILayout.Foldout(_rawFoldout, "Raw Fairing Data", true);
            if (!_rawFoldout)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(_dataFairing, true);
            }
        }

        private void DrawModuleReferencesInspector()
        {
            SerializedProperty nodeRings = serializedObject.FindProperty("NodeRings");
            if (nodeRings == null)
            {
                return;
            }

            _moduleReferencesFoldout = EditorGUILayout.Foldout(_moduleReferencesFoldout, "Module References", true);
            if (!_moduleReferencesFoldout)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(nodeRings, true);
            }
        }

        private void DrawModuleStoredValue(string fieldName, string label)
        {
            SerializedProperty property = GetModuleStoredValue(fieldName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label), true);
            }
        }

        private void DrawDataField(string fieldName, string label, bool includeChildren = false)
        {
            SerializedProperty property = GetDataField(fieldName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label), includeChildren);
            }
        }

        private SerializedProperty GetDataField(string fieldName)
        {
            return _dataFairing?.FindPropertyRelative(fieldName);
        }

        private SerializedProperty GetModuleStoredValue(string fieldName)
        {
            SerializedProperty property = GetDataField(fieldName);
            return property?.FindPropertyRelative("storedValue") ?? property;
        }

        private IReadOnlyList<AttachNodeDefinition> GetAttachNodes()
        {
            CorePartData coreData = TargetModule == null
                ? null
                : TargetModule.GetComponent<CorePartData>() ?? TargetModule.GetComponentInParent<CorePartData>();
            return coreData?.Data?.attachNodes;
        }

        private int GetSelectedSnapNodeIndex(IReadOnlyList<AttachNodeDefinition> attachNodes)
        {
            string preferredNodeId = _selectedShroudSnapNodeId;
            if (string.IsNullOrEmpty(preferredNodeId))
            {
                preferredNodeId = GetDataField("FloatingAttachNodeTag")?.stringValue;
            }

            if (string.IsNullOrEmpty(preferredNodeId))
            {
                preferredNodeId = GetDataField("FairingNode")?.stringValue;
            }

            if (!string.IsNullOrEmpty(preferredNodeId))
            {
                for (int i = 0; i < attachNodes.Count; i++)
                {
                    if (string.Equals(attachNodes[i].nodeID, preferredNodeId, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            return 0;
        }

        private GUIContent[] BuildAttachNodeOptions(IReadOnlyList<AttachNodeDefinition> attachNodes)
        {
            var options = new GUIContent[attachNodes.Count];
            for (int i = 0; i < attachNodes.Count; i++)
            {
                AttachNodeDefinition node = attachNodes[i];
                options[i] = new GUIContent(
                    string.IsNullOrEmpty(node.nodeID) ? $"Node {i}" : node.nodeID,
                    $"Axis position: {FormatMeters(GetNodeAxisHeight(node))}"
                );
            }

            return options;
        }

        private float GetNodeAxisHeight(AttachNodeDefinition node)
        {
            Transform moduleTransform = TargetModule == null ? null : TargetModule.gameObject.transform;
            Transform previewTransform = GetPreviewModelTransform(moduleTransform) ?? moduleTransform;
            Vector3 nodeLocalPosition = ToVector3(node.position);
            if (moduleTransform != null && previewTransform != null && previewTransform != moduleTransform)
            {
                nodeLocalPosition = previewTransform.InverseTransformPoint(moduleTransform.TransformPoint(nodeLocalPosition));
            }

            Vector3 axis = GetVector3DataField("LocalUpAxis", Vector3.up);
            axis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.up;
            Vector3 pivot = GetVector3DataField("Pivot", Vector3.zero);
            return Vector3.Dot(nodeLocalPosition - pivot, axis);
        }

        private float GetRuntimeStartHeight()
        {
            Vector3 localUpAxis = GetVector3DataField("LocalUpAxis", Vector3.up);
            float localUpSign = Mathf.Sign(localUpAxis.x) * Mathf.Sign(localUpAxis.y) * Mathf.Sign(localUpAxis.z);
            float startHeight = (GetDataField("FairingStartHeight")?.floatValue ?? 0f) * localUpSign;

            Transform modelTransform = GetPreviewModelTransform(TargetModule == null ? null : TargetModule.gameObject.transform);
            if (modelTransform == null)
            {
                return startHeight;
            }

            float modelTransformSign = Mathf.Sign(modelTransform.localPosition.x) *
                Mathf.Sign(modelTransform.localPosition.y) *
                Mathf.Sign(modelTransform.localPosition.z);
            return startHeight - Vector3.Scale(modelTransform.localPosition, localUpAxis).magnitude * localUpSign *
                modelTransformSign;
        }

        private Vector3 GetVector3DataField(string fieldName, Vector3 fallback)
        {
            SerializedProperty property = GetDataField(fieldName);
            return property is { propertyType: SerializedPropertyType.Vector3 } ? property.vector3Value : fallback;
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
                Transform match = FindChildRecursive(child, childName);
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

        private void SetDataBool(string fieldName, bool value)
        {
            SerializedProperty property = GetDataField(fieldName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private void SetDataEnum(string fieldName, int value)
        {
            SerializedProperty property = GetDataField(fieldName);
            if (property != null)
            {
                property.enumValueIndex = value;
            }
        }

        private void SetModuleBool(string fieldName, bool value)
        {
            SerializedProperty property = GetModuleStoredValue(fieldName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private void SetModuleEnum(string fieldName, int value)
        {
            SerializedProperty property = GetModuleStoredValue(fieldName);
            if (property != null)
            {
                property.enumValueIndex = value;
            }
        }
    }
}
