using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KSP.VFX;
using UnityEditor;
using UnityEngine;

namespace KSP.Editor
{
    [CustomEditor(typeof(ThrottleBlendshapeData))]
    public class ThrottleBlendshapeDataEditor : UnityEditor.Editor
    {
        private const float UpperAtmoThresholdDefault = 0.0092f;
        private const string PresetAssetPath =
            "Assets/Modules/KSP2UnityTools/Assets/Editor/ThrottleBlendshapePresetLibrary.asset";
        private const string ParamLockPrefix = "ThrottleBlendshapeDataEditor.ParamLock.";
        private static string _clipboardJson;

        private enum InspectorMode
        {
            Basic,
            Advanced
        }

        private readonly struct FloatParamDescriptor
        {
            public readonly string ShaderParam;
            public readonly string Label;

            public FloatParamDescriptor(string shaderParam, string label)
            {
                ShaderParam = shaderParam;
                Label = label;
            }
        }

        private readonly struct ColorParamDescriptor
        {
            public readonly string ShaderParam;
            public readonly string Label;

            public ColorParamDescriptor(string shaderParam, string label)
            {
                ShaderParam = shaderParam;
                Label = label;
            }
        }

        [Serializable]
        private class FloatParamSnapshot
        {
            public string ShaderParamName;
            public Keyframe[] CurveKeys;
        }

        [Serializable]
        private class ColorParamSnapshot
        {
            public string ShaderParamName;
            public GradientColorKey[] ColorKeys;
            public GradientAlphaKey[] AlphaKeys;
        }

        [Serializable]
        private class BlendshapeSnapshot
        {
            public float UpperAtmoNormalizedPressureThreshold;
            public int ZeroThrottleBlendshapeIndex;
            public int UpperAtmosphereBlendshapeIndex;
            public int VacuumBlendshapeIndex;
            public float bendRotationOffset;
            public Keyframe[] ThrottleScaleForAtmoBlendCurve;
            public Keyframe[] AtmoToUpperBlendCurve;
            public Keyframe[] UpperAtmoToVacuumBlendCurve;
            public Keyframe[] VacuumFromUpperAtmoBlendCurve;
            public List<FloatParamSnapshot> AtmoVacFloat;
            public List<ColorParamSnapshot> AtmoVacColor;
            public List<FloatParamSnapshot> ThrottleFloat;
            public List<ColorParamSnapshot> ThrottleColor;
        }

        [Serializable]
        private class UserPreset
        {
            public string Name;
            public string SnapshotJson;
        }

        [Serializable]
        private class UserPresetCollection
        {
            public List<UserPreset> Items = new();
        }

        private class LockedParamState
        {
            public List<FloatParamSnapshot> AtmoVacFloat = new();
            public List<ColorParamSnapshot> AtmoVacColor = new();
            public List<FloatParamSnapshot> ThrottleFloat = new();
            public List<ColorParamSnapshot> ThrottleColor = new();
        }

        private static readonly FloatParamDescriptor[] ShapeFloatParams =
        {
            new("_VertexDispScale", "Displacement Scale"),
            new("_VertexDispPosOffset", "Displacement Offset"),
            new("_VertexDispFalloffGradient", "Displacement Falloff"),
            new("_ErosionAmount", "Erosion Amount"),
            new("_ErosionPosOffset", "Erosion Offset"),
            new("_ErosionFalloffGradient", "Erosion Falloff")
        };

        private static readonly FloatParamDescriptor[] BrightnessFloatParams =
        {
            new("_Alpha", "Alpha"),
            new("_ColorTintBoost", "Tint Boost"),
            new("_ColorTintFalloff", "Tint Falloff"),
            new("_ColorTintOffset", "Tint Offset")
        };

        private static readonly FloatParamDescriptor[] DistortionFloatParams =
        {
            new("_NoiseAmount", "Noise Amount"),
            new("_NoiseStrength", "Noise Strength"),
            new("_ScrollSpeedX", "Scroll Speed X"),
            new("_ScrollSpeedY", "Scroll Speed Y")
        };

        private static readonly ColorParamDescriptor[] ColorParams =
        {
            new("_ColorTintStart", "Tint Start"),
            new("_ColorTintMiddle", "Tint Middle"),
            new("_ColorTintEnd", "Tint End")
        };

        private InspectorMode _mode;
        private bool _showPreview = true;
        private bool _showPresetTools = true;
        private bool _showValidation = true;
        private bool _showAdvancedAtmo = true;
        private bool _showAdvancedThrottle = true;
        private string _previewStatus;
        private string _presetName = "";
        private int _selectedUserPreset;
        private SerializedProperty _upperAtmoThreshold;
        private SerializedProperty _zeroThrottleBlendshapeIndex;
        private SerializedProperty _upperAtmosphereBlendshapeIndex;
        private SerializedProperty _vacuumBlendshapeIndex;
        private SerializedProperty _throttleScaleForAtmoBlendCurve;
        private SerializedProperty _atmoToUpperBlendCurve;
        private SerializedProperty _upperAtmoToVacuumBlendCurve;
        private SerializedProperty _vacuumFromUpperAtmoBlendCurve;
        private SerializedProperty _bendRotationOffset;
        private SerializedProperty _atmoVacFloatParams;
        private SerializedProperty _atmoVacColorParams;
        private SerializedProperty _throttleFloatMultipliers;
        private SerializedProperty _throttleColorMultipliers;

        private void OnEnable()
        {
            _mode = (InspectorMode)EditorPrefs.GetInt(GetPrefKey("Mode"), (int)InspectorMode.Basic);
            _upperAtmoThreshold = serializedObject.FindProperty("UpperAtmoNormalizedPressureThreshold");
            _zeroThrottleBlendshapeIndex = serializedObject.FindProperty("ZeroThrottleBlendshapeIndex");
            _upperAtmosphereBlendshapeIndex = serializedObject.FindProperty("UpperAtmosphereBlendshapeIndex");
            _vacuumBlendshapeIndex = serializedObject.FindProperty("VacuumBlendshapeIndex");
            _throttleScaleForAtmoBlendCurve = serializedObject.FindProperty("ThrottleScaleForAtmoBlendCurve");
            _atmoToUpperBlendCurve = serializedObject.FindProperty("AtmoToUpperBlendCurve");
            _upperAtmoToVacuumBlendCurve = serializedObject.FindProperty("UpperAtmoToVacuumBlendCurve");
            _vacuumFromUpperAtmoBlendCurve = serializedObject.FindProperty("VacuumFromUpperAtmoBlendCurve");
            _bendRotationOffset = serializedObject.FindProperty("bendRotationOffset");
            _atmoVacFloatParams = serializedObject.FindProperty("AtmoVac_Float_ShaderParameters");
            _atmoVacColorParams = serializedObject.FindProperty("AtmoVac_Color_ShaderParameters");
            _throttleFloatMultipliers = serializedObject.FindProperty("Throttle_Float_ShaderMultipliers");
            _throttleColorMultipliers = serializedObject.FindProperty("Throttle_Color_ShaderMultipliers");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawModeToolbar();
            DrawPreviewSection();
            EditorGUILayout.Space();
            if (_mode == InspectorMode.Basic)
            {
                DrawBasicInspector();
            }
            else
            {
                DrawAdvancedInspector();
            }

            EditorGUILayout.Space();
            DrawPresetToolsSection();
            EditorGUILayout.Space();
            DrawValidationSection();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawModeToolbar()
        {
            EditorGUILayout.LabelField("Editor Mode", EditorStyles.boldLabel);
            var newMode = (InspectorMode)GUILayout.Toolbar((int)_mode, new[] { "Basic", "Advanced" });
            if (newMode == _mode)
            {
                return;
            }

            _mode = newMode;
            EditorPrefs.SetInt(GetPrefKey("Mode"), (int)_mode);
        }

        private void DrawPreviewSection()
        {
            _showPreview = EditorGUILayout.BeginFoldoutHeaderGroup(_showPreview, "Preview (Parent Manager)");
            if (_showPreview)
            {
                var data = (ThrottleBlendshapeData)target;
                var manager = data.GetComponentInParent<ThrottleVFXManager>();
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField("Parent Manager", manager, typeof(ThrottleVFXManager), true);
                EditorGUI.EndDisabledGroup();

                if (manager == null)
                {
                    EditorGUILayout.HelpBox(
                        "No parent ThrottleVFXManager found. Add one to the root engine object to use unified preview.",
                        MessageType.Info
                    );
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Select Manager"))
                    {
                        Selection.activeObject = manager;
                        EditorGUIUtility.PingObject(manager);
                    }

                    if (GUILayout.Button("Focus Preview on Manager"))
                    {
                        ThrottleVFXPreviewBridge.ActivateAndRefresh(manager);
                        _previewStatus = "Updated parent manager preview.";
                    }

                    EditorGUILayout.EndHorizontal();
                    ThrottleVFXPreviewBridge.DrawPreviewControls(manager, showHeader: false);
                }

                if (!string.IsNullOrEmpty(_previewStatus))
                {
                    EditorGUILayout.HelpBox(_previewStatus, MessageType.Info);
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawBasicInspector()
        {
            var data = (ThrottleBlendshapeData)target;
            EditorGUILayout.LabelField("Blendshape Setup", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_upperAtmoThreshold, new GUIContent("Upper Atmo Threshold"));
            EditorGUILayout.PropertyField(_zeroThrottleBlendshapeIndex, new GUIContent("Zero Throttle Shape"));
            EditorGUILayout.PropertyField(_upperAtmosphereBlendshapeIndex, new GUIContent("Upper Atmosphere Shape"));
            EditorGUILayout.PropertyField(_vacuumBlendshapeIndex, new GUIContent("Vacuum Shape"));
            EditorGUILayout.PropertyField(_bendRotationOffset, new GUIContent("Bend Rotation Offset"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Atmosphere Transition", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_throttleScaleForAtmoBlendCurve, new GUIContent("Throttle Scale"));
            EditorGUILayout.PropertyField(_atmoToUpperBlendCurve, new GUIContent("Atmo -> Upper"));
            EditorGUILayout.PropertyField(_upperAtmoToVacuumBlendCurve, new GUIContent("Upper -> Vacuum"));
            EditorGUILayout.PropertyField(_vacuumFromUpperAtmoBlendCurve, new GUIContent("Vacuum Fill"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Appearance", EditorStyles.boldLabel);
            DrawFloatGroup(
                "Shape",
                data,
                "AtmoVacFloat",
                _atmoVacFloatParams,
                ShapeFloatParams,
                shader => AddMissingFloatParam(data, data.AtmoVac_Float_ShaderParameters, shader)
            );
            DrawFloatGroup(
                "Brightness",
                data,
                "AtmoVacFloat",
                _atmoVacFloatParams,
                BrightnessFloatParams,
                shader => AddMissingFloatParam(data, data.AtmoVac_Float_ShaderParameters, shader)
            );
            DrawColorGroup(
                "Color",
                data,
                "AtmoVacColor",
                _atmoVacColorParams,
                ColorParams,
                shader => AddMissingColorParam(data, data.AtmoVac_Color_ShaderParameters, shader)
            );
            DrawFloatGroup(
                "Distortion",
                data,
                "AtmoVacFloat",
                _atmoVacFloatParams,
                DistortionFloatParams,
                shader => AddMissingFloatParam(data, data.AtmoVac_Float_ShaderParameters, shader)
            );
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Throttle Multipliers", EditorStyles.boldLabel);
            DrawFloatGroup(
                "Float Multipliers",
                data,
                "ThrottleFloat",
                _throttleFloatMultipliers,
                BrightnessFloatParams,
                shader => AddMissingFloatParam(data, data.Throttle_Float_ShaderMultipliers, shader)
            );
            DrawColorGroup(
                "Color Multipliers",
                data,
                "ThrottleColor",
                _throttleColorMultipliers,
                ColorParams,
                shader => AddMissingColorParam(data, data.Throttle_Color_ShaderMultipliers, shader)
            );
        }

        private void DrawAdvancedInspector()
        {
            EditorGUILayout.PropertyField(_upperAtmoThreshold);
            EditorGUILayout.PropertyField(_zeroThrottleBlendshapeIndex);
            EditorGUILayout.PropertyField(_upperAtmosphereBlendshapeIndex);
            EditorGUILayout.PropertyField(_vacuumBlendshapeIndex);
            EditorGUILayout.PropertyField(_throttleScaleForAtmoBlendCurve);
            EditorGUILayout.PropertyField(_atmoToUpperBlendCurve);
            EditorGUILayout.PropertyField(_upperAtmoToVacuumBlendCurve);
            EditorGUILayout.PropertyField(_vacuumFromUpperAtmoBlendCurve);
            EditorGUILayout.PropertyField(_bendRotationOffset);
            _showAdvancedAtmo = EditorGUILayout.Foldout(_showAdvancedAtmo, "Atmosphere/Vacuum Shader Params", true);
            if (_showAdvancedAtmo)
            {
                EditorGUILayout.PropertyField(_atmoVacFloatParams, includeChildren: true);
                EditorGUILayout.PropertyField(_atmoVacColorParams, includeChildren: true);
            }

            _showAdvancedThrottle = EditorGUILayout.Foldout(_showAdvancedThrottle, "Throttle Shader Multipliers", true);
            if (_showAdvancedThrottle)
            {
                EditorGUILayout.PropertyField(_throttleFloatMultipliers, includeChildren: true);
                EditorGUILayout.PropertyField(_throttleColorMultipliers, includeChildren: true);
            }
        }

        private void DrawPresetToolsSection()
        {
            _showPresetTools = EditorGUILayout.BeginFoldoutHeaderGroup(_showPresetTools, "Presets, Copy/Paste, Batch");
            if (_showPresetTools)
            {
                var data = (ThrottleBlendshapeData)target;
                UserPresetCollection presets = LoadUserPresets();
                EditorGUILayout.LabelField("User Presets", EditorStyles.miniBoldLabel);
                _presetName = EditorGUILayout.TextField("Preset Name", _presetName);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Save User Preset"))
                {
                    SaveUserPreset(data, _presetName);
                    presets = LoadUserPresets();
                }

                GUI.enabled = presets.Items.Count > 0;
                string[] presetNames = presets.Items.Select(p => p.Name).ToArray();
                _selectedUserPreset = Mathf.Clamp(_selectedUserPreset, 0, Math.Max(0, presetNames.Length - 1));
                _selectedUserPreset = EditorGUILayout.Popup(
                    _selectedUserPreset,
                    presetNames.Length == 0 ? new[] { "(none)" } : presetNames
                );
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                GUI.enabled = presets.Items.Count > 0;
                if (GUILayout.Button("Apply User Preset"))
                {
                    if (TryApplyUserPreset(data, presets.Items[_selectedUserPreset], "Apply User Preset"))
                    {
                        serializedObject.Update();
                    }
                }

                if (GUILayout.Button("Apply + Preview"))
                {
                    if (TryApplyUserPreset(data, presets.Items[_selectedUserPreset], "Apply User Preset"))
                    {
                        serializedObject.Update();
                        ApplyPreview();
                    }
                }

                if (GUILayout.Button("Delete User Preset"))
                {
                    string presetToDelete = presets.Items[_selectedUserPreset].Name;
                    presets.Items.RemoveAt(_selectedUserPreset);
                    SaveUserPresets(presets);
                    _selectedUserPreset = 0;
                    _previewStatus = "Deleted preset: " + presetToDelete;
                }

                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Copy/Paste", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Copy Settings"))
                {
                    _clipboardJson = JsonUtility.ToJson(CaptureSnapshot(data));
                    _previewStatus = "Copied settings to editor clipboard.";
                }

                GUI.enabled = !string.IsNullOrEmpty(_clipboardJson);
                if (GUILayout.Button("Paste Settings"))
                {
                    PasteFromClipboard(data);
                    serializedObject.Update();
                }

                if (GUILayout.Button("Paste + Preview"))
                {
                    PasteFromClipboard(data);
                    serializedObject.Update();
                    ApplyPreview();
                }

                if (GUILayout.Button("Paste To Selected"))
                {
                    PasteClipboardToSelected(data);
                    serializedObject.Update();
                }

                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Batch", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("Apply Current To Selected"))
                {
                    ApplyCurrentToSelected(data);
                    serializedObject.Update();
                }

                EditorGUILayout.LabelField(
                    "Locked parameters are preserved during preset/paste/batch apply.",
                    EditorStyles.miniLabel
                );
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawValidationSection()
        {
            _showValidation = EditorGUILayout.BeginFoldoutHeaderGroup(_showValidation, "Validation");
            if (_showValidation)
            {
                var data = (ThrottleBlendshapeData)target;
                ValidationReport report = Validate(data);
                if (!report.Issues.Any())
                {
                    EditorGUILayout.HelpBox("No validation issues detected.", MessageType.Info);
                }
                else
                {
                    foreach (string? issue in report.Issues)
                    {
                        EditorGUILayout.HelpBox(issue, MessageType.Warning);
                    }
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Repair Known Defaults"))
                {
                    RepairKnownDefaults(data);
                    serializedObject.Update();
                }

                if (GUILayout.Button("Auto-Assign Blendshape Indices"))
                {
                    AutoAssignBlendshapeIndices(data);
                    serializedObject.Update();
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawFloatGroup(
            string title,
            ThrottleBlendshapeData data,
            string paramGroup,
            SerializedProperty listProp,
            IEnumerable<FloatParamDescriptor> descriptors,
            Action<string> onAddMissing
        )
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            foreach (FloatParamDescriptor descriptor in descriptors)
            {
                SerializedProperty paramProp = FindParamByName(listProp, descriptor.ShaderParam);
                DrawFloatParamRow(data, paramGroup, paramProp, descriptor.Label, descriptor.ShaderParam, onAddMissing);
            }
        }

        private void DrawColorGroup(
            string title,
            ThrottleBlendshapeData data,
            string paramGroup,
            SerializedProperty listProp,
            IEnumerable<ColorParamDescriptor> descriptors,
            Action<string> onAddMissing
        )
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            foreach (ColorParamDescriptor descriptor in descriptors)
            {
                SerializedProperty paramProp = FindParamByName(listProp, descriptor.ShaderParam);
                DrawColorParamRow(data, paramGroup, paramProp, descriptor.Label, descriptor.ShaderParam, onAddMissing);
            }
        }

        private static SerializedProperty FindParamByName(SerializedProperty listProp, string shaderParamName)
        {
            if (listProp == null || !listProp.isArray)
            {
                return null;
            }

            for (int i = 0; i < listProp.arraySize; i++)
            {
                SerializedProperty? element = listProp.GetArrayElementAtIndex(i);
                SerializedProperty? nameProp = element.FindPropertyRelative("ShaderParamName");
                if (nameProp != null && nameProp.stringValue == shaderParamName)
                {
                    return element;
                }
            }

            return null;
        }

        private void DrawFloatParamRow(
            ThrottleBlendshapeData data,
            string paramGroup,
            SerializedProperty paramProp,
            string label,
            string shaderParam,
            Action<string> onAddMissing
        )
        {
            bool isLocked = IsParameterLocked(data, paramGroup, shaderParam);
            if (paramProp == null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox(label + " (" + shaderParam + ") is not configured.", MessageType.None);
                if (GUILayout.Button("Add", GUILayout.Width(56f)))
                {
                    onAddMissing?.Invoke(shaderParam);
                }

                if (DrawLockIconButton(isLocked))
                {
                    SetParameterLocked(data, paramGroup, shaderParam, !isLocked);
                }

                EditorGUILayout.EndHorizontal();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(paramProp.FindPropertyRelative("MinMaxCurve"), new GUIContent(label));
            if (DrawLockIconButton(isLocked))
            {
                SetParameterLocked(data, paramGroup, shaderParam, !isLocked);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawColorParamRow(
            ThrottleBlendshapeData data,
            string paramGroup,
            SerializedProperty paramProp,
            string label,
            string shaderParam,
            Action<string> onAddMissing
        )
        {
            bool isLocked = IsParameterLocked(data, paramGroup, shaderParam);
            if (paramProp == null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox(label + " (" + shaderParam + ") is not configured.", MessageType.None);
                if (GUILayout.Button("Add", GUILayout.Width(56f)))
                {
                    onAddMissing?.Invoke(shaderParam);
                }

                if (DrawLockIconButton(isLocked))
                {
                    SetParameterLocked(data, paramGroup, shaderParam, !isLocked);
                }

                EditorGUILayout.EndHorizontal();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(paramProp.FindPropertyRelative("MinMaxGradient"), new GUIContent(label));
            if (DrawLockIconButton(isLocked))
            {
                SetParameterLocked(data, paramGroup, shaderParam, !isLocked);
            }

            EditorGUILayout.EndHorizontal();
        }

        private static bool DrawLockIconButton(bool isLocked)
        {
            GUIContent content = GetLockButtonContent(isLocked);
            Color previousBackground = GUI.backgroundColor;
            if (isLocked)
            {
                GUI.backgroundColor = new Color(0.60f, 0.80f, 1f, 1f);
            }

            bool clicked = GUILayout.Button(content, GUILayout.Width(24f), GUILayout.Height(18f));
            GUI.backgroundColor = previousBackground;
            return clicked;
        }

        private static GUIContent GetLockButtonContent(bool isLocked)
        {
            string iconName = isLocked ? "LockIcon-On" : "LockIcon";
            GUIContent? icon = EditorGUIUtility.IconContent(iconName);
            if (icon != null && icon.image != null)
            {
                icon.tooltip = isLocked ? "Locked (click to unlock)" : "Unlocked (click to lock)";
                return icon;
            }

            return new GUIContent(
                isLocked ? "On" : "Off",
                isLocked ? "Locked (click to unlock)" : "Unlocked (click to lock)"
            );
        }

        private void ApplyPreview()
        {
            var data = (ThrottleBlendshapeData)target;
            var manager = data.GetComponentInParent<ThrottleVFXManager>();
            if (manager != null)
            {
                ThrottleVFXPreviewBridge.ActivateAndRefresh(manager);
                _previewStatus = "Updated parent manager preview.";
                return;
            }

            _previewStatus = "No parent ThrottleVFXManager found. Preview is controlled from the manager.";
        }

        private static ValidationReport Validate(ThrottleBlendshapeData data)
        {
            var issues = new List<string>();
            var renderer = data.GetComponent<SkinnedMeshRenderer>();
            if (renderer == null)
            {
                issues.Add("Missing SkinnedMeshRenderer on the same GameObject.");
                return new ValidationReport(issues);
            }

            if (renderer.sharedMesh == null)
            {
                issues.Add("SkinnedMeshRenderer has no mesh assigned.");
            }

            if (renderer.sharedMaterial == null)
            {
                issues.Add("SkinnedMeshRenderer has no material assigned.");
            }

            if (renderer.sharedMesh != null)
            {
                int count = renderer.sharedMesh.blendShapeCount;
                ValidateBlendshapeIndex(data.ZeroThrottleBlendshapeIndex, "ZeroThrottleBlendshapeIndex", count, issues);
                ValidateBlendshapeIndex(
                    data.UpperAtmosphereBlendshapeIndex,
                    "UpperAtmosphereBlendshapeIndex",
                    count,
                    issues
                );
                ValidateBlendshapeIndex(data.VacuumBlendshapeIndex, "VacuumBlendshapeIndex", count, issues);
            }

            if (renderer.sharedMaterial == null)
            {
                return new ValidationReport(issues);
            }

            ValidateShaderParams(
                data.AtmoVac_Float_ShaderParameters.Select(p => p.ShaderParamName),
                renderer.sharedMaterial,
                "AtmoVac float",
                issues
            );
            ValidateShaderParams(
                data.AtmoVac_Color_ShaderParameters.Select(p => p.ShaderParamName),
                renderer.sharedMaterial,
                "AtmoVac color",
                issues
            );
            ValidateShaderParams(
                data.Throttle_Float_ShaderMultipliers.Select(p => p.ShaderParamName),
                renderer.sharedMaterial,
                "Throttle float",
                issues
            );
            ValidateShaderParams(
                data.Throttle_Color_ShaderMultipliers.Select(p => p.ShaderParamName),
                renderer.sharedMaterial,
                "Throttle color",
                issues
            );

            return new ValidationReport(issues);
        }

        private static void ValidateBlendshapeIndex(int index, string label, int count, ICollection<string> issues)
        {
            if (index < 0 || index >= count)
            {
                issues.Add(label + " is out of range. Expected 0.." + (count - 1) + ", current " + index + ".");
            }
        }

        private static void ValidateShaderParams(
            IEnumerable<string> names,
            Material material,
            string group,
            ICollection<string> issues
        )
        {
            List<string> list = names.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            foreach (IGrouping<string, string>? dup in list.GroupBy(n => n).Where(g => g.Count() > 1))
            {
                issues.Add(group + " has duplicate shader parameter: " + dup.Key);
            }

            foreach (string? shaderParam in list.Distinct())
            {
                if (!material.HasProperty(shaderParam))
                {
                    issues.Add(group + " references missing shader parameter: " + shaderParam);
                }
            }
        }

        private static void AddMissingFloatParam(
            ThrottleBlendshapeData data,
            List<ThrottleBlendshapeData.BlendshapeShaderFloatParam> list,
            string shaderParam
        )
        {
            if (list.Any(p => p.ShaderParamName == shaderParam))
            {
                return;
            }

            Undo.RecordObject(data, "Add Missing Float Param");
            list.Add(
                new ThrottleBlendshapeData.BlendshapeShaderFloatParam(
                    shaderParam,
                    AnimationCurve.Linear(0f, 1f, 1f, 1f)
                )
            );
            EditorUtility.SetDirty(data);
        }

        private static void AddMissingColorParam(
            ThrottleBlendshapeData data,
            List<ThrottleBlendshapeData.BlendshapeShaderColorParam> list,
            string shaderParam
        )
        {
            if (list.Any(p => p.ShaderParamName == shaderParam))
            {
                return;
            }

            Undo.RecordObject(data, "Add Missing Color Param");
            list.Add(new ThrottleBlendshapeData.BlendshapeShaderColorParam(shaderParam, WhiteGradient()));
            EditorUtility.SetDirty(data);
        }

        private static void RepairKnownDefaults(ThrottleBlendshapeData data)
        {
            Undo.RecordObject(data, "Repair Throttle Blendshape Defaults");
            AddDefaultsFromPrivateList(
                "defaultAtmoVacFloatParameters",
                data.AtmoVac_Float_ShaderParameters,
                floatParam => new ThrottleBlendshapeData.BlendshapeShaderFloatParam(
                    floatParam.ShaderParamName,
                    new AnimationCurve(floatParam.MinMaxCurve.keys)
                )
            );
            AddDefaultsFromPrivateList(
                "defaultAtmoVacColorParameters",
                data.AtmoVac_Color_ShaderParameters,
                colorParam => new ThrottleBlendshapeData.BlendshapeShaderColorParam(
                    colorParam.ShaderParamName,
                    CopyGradient(colorParam.MinMaxGradient)
                )
            );
            AddDefaultsFromPrivateList(
                "defaultThrottleFloatMultipliers",
                data.Throttle_Float_ShaderMultipliers,
                floatParam => new ThrottleBlendshapeData.BlendshapeShaderFloatParam(
                    floatParam.ShaderParamName,
                    new AnimationCurve(floatParam.MinMaxCurve.keys)
                )
            );
            AddDefaultsFromPrivateList(
                "defaultThrottleColorMultipliers",
                data.Throttle_Color_ShaderMultipliers,
                colorParam => new ThrottleBlendshapeData.BlendshapeShaderColorParam(
                    colorParam.ShaderParamName,
                    CopyGradient(colorParam.MinMaxGradient)
                )
            );
            EditorUtility.SetDirty(data);
        }

        private static void AddDefaultsFromPrivateList(
            string fieldName,
            List<ThrottleBlendshapeData.BlendshapeShaderFloatParam> targetList,
            Func<ThrottleBlendshapeData.BlendshapeShaderFloatParam, ThrottleBlendshapeData.BlendshapeShaderFloatParam>
                clone
        )
        {
            FieldInfo? field = typeof(ThrottleBlendshapeData).GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Static
            );
            if (field?.GetValue(null) is not List<ThrottleBlendshapeData.BlendshapeShaderFloatParam> defaults)
            {
                return;
            }

            var existing = new HashSet<string>(targetList.Select(p => p.ShaderParamName));
            foreach (ThrottleBlendshapeData.BlendshapeShaderFloatParam? source in defaults)
            {
                if (existing.Contains(source.ShaderParamName))
                {
                    continue;
                }

                targetList.Add(clone(source));
            }
        }

        private static void AddDefaultsFromPrivateList(
            string fieldName,
            List<ThrottleBlendshapeData.BlendshapeShaderColorParam> targetList,
            Func<ThrottleBlendshapeData.BlendshapeShaderColorParam, ThrottleBlendshapeData.BlendshapeShaderColorParam>
                clone
        )
        {
            FieldInfo? field = typeof(ThrottleBlendshapeData).GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Static
            );
            if (field?.GetValue(null) is not List<ThrottleBlendshapeData.BlendshapeShaderColorParam> defaults)
            {
                return;
            }

            var existing = new HashSet<string>(targetList.Select(p => p.ShaderParamName));
            foreach (ThrottleBlendshapeData.BlendshapeShaderColorParam? source in defaults)
            {
                if (existing.Contains(source.ShaderParamName))
                {
                    continue;
                }

                targetList.Add(clone(source));
            }
        }

        private static Gradient CopyGradient(Gradient source)
        {
            var gradient = new Gradient();
            gradient.SetKeys(source.colorKeys, source.alphaKeys);
            return gradient;
        }

        private static Gradient WhiteGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            return gradient;
        }

        private static void AutoAssignBlendshapeIndices(ThrottleBlendshapeData data)
        {
            var renderer = data.GetComponent<SkinnedMeshRenderer>();
            if (renderer == null || renderer.sharedMesh == null)
            {
                return;
            }

            Undo.RecordObject(data, "Auto-Assign Blendshape Indices");
            int zero = FindBlendshapeIndex(renderer.sharedMesh, "zero", "idle", "off");
            int upper = FindBlendshapeIndex(renderer.sharedMesh, "upper", "atmo", "sea");
            int vacuum = FindBlendshapeIndex(renderer.sharedMesh, "vac", "space", "vacuum");
            if (zero >= 0)
            {
                data.ZeroThrottleBlendshapeIndex = zero;
            }

            if (upper >= 0)
            {
                data.UpperAtmosphereBlendshapeIndex = upper;
            }

            if (vacuum >= 0)
            {
                data.VacuumBlendshapeIndex = vacuum;
            }

            EditorUtility.SetDirty(data);
        }

        private static int FindBlendshapeIndex(Mesh mesh, params string[] tokens)
        {
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string name = mesh.GetBlendShapeName(i).ToLowerInvariant();
                if (tokens.Any(name.Contains))
                {
                    return i;
                }
            }

            return -1;
        }

        private void SaveUserPreset(ThrottleBlendshapeData data, string presetName)
        {
            string trimmed = (presetName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                _previewStatus = "Preset name is required.";
                return;
            }

            UserPresetCollection collection = LoadUserPresets();
            BlendshapeSnapshot snapshot = CaptureSnapshot(data);
            string? snapshotJson = JsonUtility.ToJson(snapshot);
            UserPreset? existing = collection.Items.FirstOrDefault(p => p.Name.Equals(
                    trimmed,
                    StringComparison.OrdinalIgnoreCase
                )
            );
            if (existing != null)
            {
                existing.Name = trimmed;
                existing.SnapshotJson = snapshotJson;
            }
            else
            {
                collection.Items.Add(new UserPreset { Name = trimmed, SnapshotJson = snapshotJson });
            }

            collection.Items = collection.Items.OrderBy(p => p.Name).ToList();
            bool createdLibrary = SaveUserPresets(collection);
            _previewStatus = createdLibrary
                ? "Created preset library and saved preset: " + trimmed + " to " + PresetAssetPath
                : "Saved preset: " + trimmed + " to " + PresetAssetPath;
        }

        private static UserPresetCollection LoadUserPresets()
        {
            ThrottleBlendshapePresetLibrary? library = LoadPresetLibrary(createIfMissing: false);
            if (library == null || library.Presets == null || library.Presets.Count == 0)
            {
                return new UserPresetCollection();
            }

            var collection = new UserPresetCollection();
            foreach (ThrottleBlendshapeUserPresetEntry entry in library.Presets)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Name))
                {
                    continue;
                }

                collection.Items.Add(new UserPreset
                {
                    Name = entry.Name,
                    SnapshotJson = entry.SnapshotJson
                });
            }

            return collection;
        }

        private static bool SaveUserPresets(UserPresetCollection collection)
        {
            ThrottleBlendshapePresetLibrary? library = LoadPresetLibrary(createIfMissing: true, out bool createdLibrary);
            if (library == null)
            {
                return false;
            }

            library.Presets = collection.Items.Select(item => new ThrottleBlendshapeUserPresetEntry
            {
                Name = item.Name,
                SnapshotJson = item.SnapshotJson
            }).ToList();

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return createdLibrary;
        }

        private bool TryApplyUserPreset(ThrottleBlendshapeData data, UserPreset preset, string undoLabel)
        {
            if (preset == null || string.IsNullOrWhiteSpace(preset.SnapshotJson))
            {
                _previewStatus = "Preset data is empty or invalid.";
                return false;
            }

            if (!TryDeserializeSnapshot(preset.SnapshotJson, out BlendshapeSnapshot snapshot))
            {
                _previewStatus = "Preset JSON could not be parsed.";
                return false;
            }

            ApplySnapshotWithUndo(data, snapshot, undoLabel);
            return true;
        }

        private static bool TryDeserializeSnapshot(string json, out BlendshapeSnapshot snapshot)
        {
            snapshot = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                snapshot = JsonUtility.FromJson<BlendshapeSnapshot>(json);
            }
            catch
            {
                snapshot = null;
            }

            return snapshot != null;
        }

        private static ThrottleBlendshapePresetLibrary LoadPresetLibrary(bool createIfMissing)
        {
            return LoadPresetLibrary(createIfMissing, out _);
        }

        private static ThrottleBlendshapePresetLibrary LoadPresetLibrary(bool createIfMissing, out bool created)
        {
            created = false;
            var library = AssetDatabase.LoadAssetAtPath<ThrottleBlendshapePresetLibrary>(PresetAssetPath);
            if (library != null || !createIfMissing)
            {
                return library;
            }

            EnsureDirectoryForAsset(PresetAssetPath);
            var newLibrary = CreateInstance<ThrottleBlendshapePresetLibrary>();
            AssetDatabase.CreateAsset(newLibrary, PresetAssetPath);
            AssetDatabase.SaveAssets();
            created = true;
            return newLibrary;
        }

        private static void EnsureDirectoryForAsset(string assetPath)
        {
            string directory = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            if (string.IsNullOrEmpty(directory))
            {
                return;
            }

            if (AssetDatabase.IsValidFolder(directory))
            {
                return;
            }

            string[] segments = directory.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        private void PasteFromClipboard(ThrottleBlendshapeData data)
        {
            if (string.IsNullOrEmpty(_clipboardJson))
            {
                _previewStatus = "Clipboard is empty.";
                return;
            }

            var snapshot = JsonUtility.FromJson<BlendshapeSnapshot>(_clipboardJson);
            if (snapshot == null)
            {
                _previewStatus = "Clipboard contents are invalid.";
                return;
            }

            ApplySnapshotWithUndo(data, snapshot, "Paste Blendshape Settings");
            _previewStatus = "Pasted settings from clipboard.";
        }

        private void PasteClipboardToSelected(ThrottleBlendshapeData source)
        {
            if (string.IsNullOrEmpty(_clipboardJson))
            {
                _previewStatus = "Clipboard is empty.";
                return;
            }

            var snapshot = JsonUtility.FromJson<BlendshapeSnapshot>(_clipboardJson);
            if (snapshot == null)
            {
                _previewStatus = "Clipboard contents are invalid.";
                return;
            }

            ThrottleBlendshapeData[]? selected =
                Selection.GetFiltered<ThrottleBlendshapeData>(SelectionMode.Editable | SelectionMode.ExcludePrefab);
            int count = 0;
            foreach (var item in selected)
            {
                if (item == null)
                {
                    continue;
                }

                ApplySnapshotWithUndo(item, snapshot, "Paste Blendshape Settings To Selected");
                count++;
            }

            _previewStatus = "Pasted clipboard to " + count + " component(s).";
        }

        private void ApplyCurrentToSelected(ThrottleBlendshapeData source)
        {
            BlendshapeSnapshot snapshot = CaptureSnapshot(source);
            ThrottleBlendshapeData[]? selected =
                Selection.GetFiltered<ThrottleBlendshapeData>(SelectionMode.Editable | SelectionMode.ExcludePrefab);
            int count = 0;
            foreach (var item in selected)
            {
                if (item == null || item == source)
                {
                    continue;
                }

                ApplySnapshotWithUndo(item, snapshot, "Batch Apply Blendshape Settings");
                count++;
            }

            _previewStatus = "Applied current settings to " + count + " selected component(s).";
        }

        private static void SetFloatCurve(
            List<ThrottleBlendshapeData.BlendshapeShaderFloatParam> list,
            string paramName,
            float start,
            float end
        )
        {
            ThrottleBlendshapeData.BlendshapeShaderFloatParam? param =
                list.FirstOrDefault(p => p.ShaderParamName == paramName);
            if (param == null)
            {
                return;
            }

            param.MinMaxCurve = AnimationCurve.Linear(0f, start, 1f, end);
        }

        private static BlendshapeSnapshot CaptureSnapshot(ThrottleBlendshapeData data)
        {
            return new BlendshapeSnapshot
            {
                UpperAtmoNormalizedPressureThreshold = data.UpperAtmoNormalizedPressureThreshold,
                ZeroThrottleBlendshapeIndex = data.ZeroThrottleBlendshapeIndex,
                UpperAtmosphereBlendshapeIndex = data.UpperAtmosphereBlendshapeIndex,
                VacuumBlendshapeIndex = data.VacuumBlendshapeIndex,
                bendRotationOffset = data.bendRotationOffset,
                ThrottleScaleForAtmoBlendCurve = CopyKeys(data.ThrottleScaleForAtmoBlendCurve),
                AtmoToUpperBlendCurve = CopyKeys(data.AtmoToUpperBlendCurve),
                UpperAtmoToVacuumBlendCurve = CopyKeys(data.UpperAtmoToVacuumBlendCurve),
                VacuumFromUpperAtmoBlendCurve = CopyKeys(data.VacuumFromUpperAtmoBlendCurve),
                AtmoVacFloat = SnapshotFloatParams(data.AtmoVac_Float_ShaderParameters),
                AtmoVacColor = SnapshotColorParams(data.AtmoVac_Color_ShaderParameters),
                ThrottleFloat = SnapshotFloatParams(data.Throttle_Float_ShaderMultipliers),
                ThrottleColor = SnapshotColorParams(data.Throttle_Color_ShaderMultipliers)
            };
        }

        private void ApplySnapshotWithUndo(ThrottleBlendshapeData data, BlendshapeSnapshot snapshot, string undoLabel)
        {
            LockedParamState locked = CaptureLockedParams(data);
            Undo.RecordObject(data, undoLabel);
            ApplySnapshot(data, snapshot);
            RestoreLockedParams(data, locked);
            EditorUtility.SetDirty(data);
        }

        private static void ApplySnapshot(ThrottleBlendshapeData data, BlendshapeSnapshot snapshot)
        {
            data.UpperAtmoNormalizedPressureThreshold = snapshot.UpperAtmoNormalizedPressureThreshold;
            data.ZeroThrottleBlendshapeIndex = snapshot.ZeroThrottleBlendshapeIndex;
            data.UpperAtmosphereBlendshapeIndex = snapshot.UpperAtmosphereBlendshapeIndex;
            data.VacuumBlendshapeIndex = snapshot.VacuumBlendshapeIndex;
            data.bendRotationOffset = snapshot.bendRotationOffset;
            data.ThrottleScaleForAtmoBlendCurve =
                new AnimationCurve(snapshot.ThrottleScaleForAtmoBlendCurve ?? Array.Empty<Keyframe>());
            data.AtmoToUpperBlendCurve = new AnimationCurve(snapshot.AtmoToUpperBlendCurve ?? Array.Empty<Keyframe>());
            data.UpperAtmoToVacuumBlendCurve =
                new AnimationCurve(snapshot.UpperAtmoToVacuumBlendCurve ?? Array.Empty<Keyframe>());
            data.VacuumFromUpperAtmoBlendCurve =
                new AnimationCurve(snapshot.VacuumFromUpperAtmoBlendCurve ?? Array.Empty<Keyframe>());
            data.AtmoVac_Float_ShaderParameters = RestoreFloatParams(snapshot.AtmoVacFloat);
            data.AtmoVac_Color_ShaderParameters = RestoreColorParams(snapshot.AtmoVacColor);
            data.Throttle_Float_ShaderMultipliers = RestoreFloatParams(snapshot.ThrottleFloat);
            data.Throttle_Color_ShaderMultipliers = RestoreColorParams(snapshot.ThrottleColor);
        }

        private static Keyframe[] CopyKeys(AnimationCurve curve)
        {
            return curve == null ? Array.Empty<Keyframe>() : curve.keys.ToArray();
        }

        private static List<FloatParamSnapshot> SnapshotFloatParams(
            List<ThrottleBlendshapeData.BlendshapeShaderFloatParam> source
        )
        {
            return source.Select(param => new FloatParamSnapshot
                    { ShaderParamName = param.ShaderParamName, CurveKeys = CopyKeys(param.MinMaxCurve) }
                )
                .ToList();
        }

        private static List<ColorParamSnapshot> SnapshotColorParams(
            List<ThrottleBlendshapeData.BlendshapeShaderColorParam> source
        )
        {
            return source.Select(param => new ColorParamSnapshot
                    {
                        ShaderParamName = param.ShaderParamName,
                        ColorKeys = param.MinMaxGradient != null
                            ? param.MinMaxGradient.colorKeys.ToArray()
                            : Array.Empty<GradientColorKey>(),
                        AlphaKeys = param.MinMaxGradient != null
                            ? param.MinMaxGradient.alphaKeys.ToArray()
                            : Array.Empty<GradientAlphaKey>()
                    }
                )
                .ToList();
        }

        private static List<ThrottleBlendshapeData.BlendshapeShaderFloatParam> RestoreFloatParams(
            List<FloatParamSnapshot> snapshot
        )
        {
            var list = new List<ThrottleBlendshapeData.BlendshapeShaderFloatParam>();
            if (snapshot == null)
            {
                return list;
            }

            foreach (FloatParamSnapshot? param in snapshot)
            {
                var curve = new AnimationCurve(param.CurveKeys ?? Array.Empty<Keyframe>());
                list.Add(new ThrottleBlendshapeData.BlendshapeShaderFloatParam(param.ShaderParamName, curve));
            }

            return list;
        }

        private static List<ThrottleBlendshapeData.BlendshapeShaderColorParam> RestoreColorParams(
            List<ColorParamSnapshot> snapshot
        )
        {
            var list = new List<ThrottleBlendshapeData.BlendshapeShaderColorParam>();
            if (snapshot == null)
            {
                return list;
            }

            foreach (ColorParamSnapshot? param in snapshot)
            {
                var gradient = new Gradient();
                gradient.SetKeys(
                    param.ColorKeys ?? Array.Empty<GradientColorKey>(),
                    param.AlphaKeys ?? Array.Empty<GradientAlphaKey>()
                );
                list.Add(new ThrottleBlendshapeData.BlendshapeShaderColorParam(param.ShaderParamName, gradient));
            }

            return list;
        }

        private LockedParamState CaptureLockedParams(ThrottleBlendshapeData data)
        {
            var state = new LockedParamState();
            CaptureLockedFloatGroup(data, "AtmoVacFloat", data.AtmoVac_Float_ShaderParameters, state.AtmoVacFloat);
            CaptureLockedColorGroup(data, "AtmoVacColor", data.AtmoVac_Color_ShaderParameters, state.AtmoVacColor);
            CaptureLockedFloatGroup(data, "ThrottleFloat", data.Throttle_Float_ShaderMultipliers, state.ThrottleFloat);
            CaptureLockedColorGroup(data, "ThrottleColor", data.Throttle_Color_ShaderMultipliers, state.ThrottleColor);
            return state;
        }

        private void RestoreLockedParams(ThrottleBlendshapeData data, LockedParamState state)
        {
            RestoreLockedFloatGroup(data.AtmoVac_Float_ShaderParameters, state.AtmoVacFloat);
            RestoreLockedColorGroup(data.AtmoVac_Color_ShaderParameters, state.AtmoVacColor);
            RestoreLockedFloatGroup(data.Throttle_Float_ShaderMultipliers, state.ThrottleFloat);
            RestoreLockedColorGroup(data.Throttle_Color_ShaderMultipliers, state.ThrottleColor);
        }

        private void CaptureLockedFloatGroup(
            ThrottleBlendshapeData data,
            string group,
            List<ThrottleBlendshapeData.BlendshapeShaderFloatParam> source,
            List<FloatParamSnapshot> destination
        )
        {
            foreach (ThrottleBlendshapeData.BlendshapeShaderFloatParam? item in source)
            {
                if (!IsParameterLocked(data, group, item.ShaderParamName))
                {
                    continue;
                }

                destination.Add(
                    new FloatParamSnapshot
                    {
                        ShaderParamName = item.ShaderParamName,
                        CurveKeys = CopyKeys(item.MinMaxCurve)
                    }
                );
            }
        }

        private void CaptureLockedColorGroup(
            ThrottleBlendshapeData data,
            string group,
            List<ThrottleBlendshapeData.BlendshapeShaderColorParam> source,
            List<ColorParamSnapshot> destination
        )
        {
            foreach (ThrottleBlendshapeData.BlendshapeShaderColorParam? item in source)
            {
                if (!IsParameterLocked(data, group, item.ShaderParamName))
                {
                    continue;
                }

                destination.Add(
                    new ColorParamSnapshot
                    {
                        ShaderParamName = item.ShaderParamName,
                        ColorKeys = item.MinMaxGradient != null
                            ? item.MinMaxGradient.colorKeys.ToArray()
                            : Array.Empty<GradientColorKey>(),
                        AlphaKeys = item.MinMaxGradient != null
                            ? item.MinMaxGradient.alphaKeys.ToArray()
                            : Array.Empty<GradientAlphaKey>()
                    }
                );
            }
        }

        private static void RestoreLockedFloatGroup(
            List<ThrottleBlendshapeData.BlendshapeShaderFloatParam> target,
            List<FloatParamSnapshot> locked
        )
        {
            foreach (FloatParamSnapshot? item in locked)
            {
                ThrottleBlendshapeData.BlendshapeShaderFloatParam? existing =
                    target.FirstOrDefault(p => p.ShaderParamName == item.ShaderParamName);
                if (existing == null)
                {
                    target.Add(
                        new ThrottleBlendshapeData.BlendshapeShaderFloatParam(
                            item.ShaderParamName,
                            new AnimationCurve(item.CurveKeys ?? Array.Empty<Keyframe>())
                        )
                    );
                    continue;
                }

                existing.MinMaxCurve = new AnimationCurve(item.CurveKeys ?? Array.Empty<Keyframe>());
            }
        }

        private static void RestoreLockedColorGroup(
            List<ThrottleBlendshapeData.BlendshapeShaderColorParam> target,
            List<ColorParamSnapshot> locked
        )
        {
            foreach (ColorParamSnapshot? item in locked)
            {
                ThrottleBlendshapeData.BlendshapeShaderColorParam? existing =
                    target.FirstOrDefault(p => p.ShaderParamName == item.ShaderParamName);
                var gradient = new Gradient();
                gradient.SetKeys(
                    item.ColorKeys ?? Array.Empty<GradientColorKey>(),
                    item.AlphaKeys ?? Array.Empty<GradientAlphaKey>()
                );
                if (existing == null)
                {
                    target.Add(new ThrottleBlendshapeData.BlendshapeShaderColorParam(item.ShaderParamName, gradient));
                    continue;
                }

                existing.MinMaxGradient = gradient;
            }
        }

        private bool IsParameterLocked(ThrottleBlendshapeData data, string paramGroup, string shaderParam)
        {
            return EditorPrefs.GetBool(GetParamLockKey(data, paramGroup, shaderParam), false);
        }

        private void SetParameterLocked(
            ThrottleBlendshapeData data,
            string paramGroup,
            string shaderParam,
            bool isLocked
        )
        {
            EditorPrefs.SetBool(GetParamLockKey(data, paramGroup, shaderParam), isLocked);
        }

        private static string SanitizeKeyFragment(string value)
        {
            return string.IsNullOrEmpty(value)
                ? "_"
                : value.Replace(" ", "_").Replace(".", "_").Replace("/", "_");
        }

        private static string GetParamLockKey(ThrottleBlendshapeData data, string paramGroup, string shaderParam)
        {
            return ParamLockPrefix + data.GetInstanceID() + "." + SanitizeKeyFragment(paramGroup) + "." +
                SanitizeKeyFragment(shaderParam);
        }

        private string GetPrefKey(string suffix)
        {
            return "ThrottleBlendshapeDataEditor." + target.GetInstanceID() + "." + suffix;
        }

        private readonly struct ValidationReport
        {
            public readonly IReadOnlyList<string> Issues;

            public ValidationReport(IReadOnlyList<string> issues)
            {
                Issues = issues;
            }
        }
    }
}
