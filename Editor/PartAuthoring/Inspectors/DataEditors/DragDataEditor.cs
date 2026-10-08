using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using KSP.Modules;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.DataEditors
{
    /// <summary>
    /// Custom editor for <see cref="Data_Drag" />. Replaces the generic field rendering of the
    /// nested <c>DragCube</c> list with a card-style UI and surfaces the drag-cube authoring tools
    /// (tag paintable renderers, calculate drag cubes, deployable capture) that lived on the
    /// legacy IMGUI editor.
    /// </summary>
    [DataEditor(typeof(Data_Drag))]
    public sealed class DragDataEditor : IDataEditor
    {
        private const string PAINTABLE_SHADER_NAME = "KSP2/Parts/Paintable";
        private const string USS_PATH = "/Assets/Windows/DataEditors.uss";

        private static readonly string[] FACE_LABELS = { "XP", "XN", "YP", "YN", "ZP", "ZN" };

        private static readonly FieldInfo DataDragField =
            typeof(Module_Drag).GetField("dataDrag", BindingFlags.Instance | BindingFlags.NonPublic);

        private PartBehaviourModule _module;
        private SerializedProperty _cubesProp;
        private VisualElement _cubesContainer;
        private Label _cubeCountLabel;
        private Label _rendererCountLabel;
        private Label _paintableCountLabel;
        private Label _renderRootLabel;
        private HelpBox _statusBox;
        private VisualElement _deployableSection;

        private bool _advancedDeployableCapture;
        private int _deployableClipIndex;
        private float _deployableRetractedTime;
        private float _deployableExtendedTime = 1f;

        /// <inheritdoc />
        public VisualElement Build(SerializedProperty dataProp, PartBehaviourModule module)
        {
            _module = module;
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;

            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(SDKConfiguration.BasePath + USS_PATH);
            if (sheet != null)
            {
                root.styleSheets.Add(sheet);
            }

            AddPropertyField(root, dataProp, "BodyLiftEnabled");
            AddPropertyField(root, dataProp, "DragEnabled");
            AddPropertyField(root, dataProp, "bodyLiftMultiplier");

            _cubesProp = dataProp.FindPropertyRelative("cubes");
            root.Add(BuildCubesList());
            root.Add(BuildDragCubeTools());

            return root;
        }

        private static void AddPropertyField(VisualElement parent, SerializedProperty dataProp, string fieldName)
        {
            var prop = dataProp.FindPropertyRelative(fieldName);
            if (prop == null)
            {
                return;
            }
            var field = new PropertyField(prop);
            field.AddToClassList("unity-base-field__aligned");
            parent.Add(field);
        }

        // -------------------- Cubes list --------------------

        private VisualElement BuildCubesList()
        {
            var outer = new VisualElement();
            outer.style.marginTop = 6f;

            var header = new Label("Drag Cubes");
            header.AddToClassList("data-editor-section-header");
            header.style.marginBottom = 2f;
            outer.Add(header);

            _cubesContainer = new VisualElement();
            _cubesContainer.style.flexDirection = FlexDirection.Column;
            outer.Add(_cubesContainer);

            var addButton = new Button(AddCube) { text = "+ Add Cube" };
            addButton.style.alignSelf = Align.FlexStart;
            addButton.style.marginTop = 4f;
            outer.Add(addButton);

            RefreshCubesList();
            return outer;
        }

        private void RefreshCubesList()
        {
            if (_cubesContainer == null || _cubesProp == null)
            {
                return;
            }
            _cubesContainer.Clear();
            for (var i = 0; i < _cubesProp.arraySize; i++)
            {
                _cubesContainer.Add(BuildCubeCard(i));
            }
            UpdateCounters();
        }

        private VisualElement BuildCubeCard(int index)
        {
            var elementProp = _cubesProp.GetArrayElementAtIndex(index);

            var card = new VisualElement();
            card.AddToClassList("data-editor-card");

            // ---- Header row ----
            var headerRow = new VisualElement();
            headerRow.AddToClassList("data-editor-card-header");

            var disclosure = new Button { text = "▼" };
            disclosure.AddToClassList("data-editor-card-disclosure");
            headerRow.Add(disclosure);

            var nameProp = elementProp.FindPropertyRelative("name");
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
            headerRow.Add(nameField);

            var weightLabel = new Label("Weight");
            weightLabel.AddToClassList("data-editor-card-mini-label");
            headerRow.Add(weightLabel);

            var weightProp = elementProp.FindPropertyRelative("weight");
            var weightField = new FloatField { value = weightProp?.floatValue ?? 1f, isDelayed = true };
            weightField.style.width = 60f;
            weightField.style.marginRight = 6f;
            if (weightProp != null)
            {
                weightField.RegisterValueChangedCallback(evt =>
                {
                    weightProp.serializedObject.Update();
                    weightProp.floatValue = evt.newValue;
                    weightProp.serializedObject.ApplyModifiedProperties();
                });
            }
            headerRow.Add(weightField);

            var capturedIndex = index;
            var removeBtn = new Button(() => RemoveCube(capturedIndex)) { text = "X" };
            removeBtn.AddToClassList("data-editor-card-remove-btn");
            headerRow.Add(removeBtn);

            card.Add(headerRow);

            // ---- Body ----
            var body = new VisualElement();
            body.AddToClassList("data-editor-card-body");

            body.Add(BuildFaceTable(elementProp));

            var centerProp = elementProp.FindPropertyRelative("center");
            var sizeProp = elementProp.FindPropertyRelative("size");
            if (centerProp != null)
            {
                body.Add(BuildVector3Row("Center", centerProp));
            }
            if (sizeProp != null)
            {
                body.Add(BuildVector3Row("Size", sizeProp));
            }
            card.Add(body);

            var expanded = false;
            body.style.display = DisplayStyle.None;
            disclosure.text = "▶";
            disclosure.clicked += () =>
            {
                expanded = !expanded;
                body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                disclosure.text = expanded ? "▼" : "▶";
            };

            return card;
        }

        private const float FACE_COL_WIDTH = 40f;
        private const float CELL_MARGIN_RIGHT = 4f;

        private static VisualElement BuildVector3Row(string label, SerializedProperty prop)
        {
            var row = new VisualElement();
            row.AddToClassList("data-editor-vector-row");

            var caption = new Label(label);
            caption.AddToClassList("data-editor-vector-row__label");
            row.Add(caption);

            var field = new Vector3Field { value = prop.vector3Value };
            field.AddToClassList("data-editor-vector-row__field");
            field.RegisterValueChangedCallback(evt =>
            {
                prop.serializedObject.Update();
                prop.vector3Value = evt.newValue;
                prop.serializedObject.ApplyModifiedProperties();
            });
            field.TrackPropertyValue(prop, p =>
            {
                if (field.value != p.vector3Value)
                {
                    field.SetValueWithoutNotify(p.vector3Value);
                }
            });
            row.Add(field);
            return row;
        }

        private VisualElement BuildFaceTable(SerializedProperty cubeProp)
        {
            var areaProp = cubeProp.FindPropertyRelative("area");
            var dragProp = cubeProp.FindPropertyRelative("drag");
            var depthProp = cubeProp.FindPropertyRelative("depth");
            var multProp = cubeProp.FindPropertyRelative("dragMultiplier");

            var table = new VisualElement();
            table.style.flexDirection = FlexDirection.Column;
            table.style.marginBottom = 6f;

            // Header
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.backgroundColor = new Color(70f / 255f, 100f / 255f, 140f / 255f, 0.25f);
            headerRow.style.paddingTop = 3f;
            headerRow.style.paddingBottom = 3f;
            headerRow.Add(MakeFaceColumnCell(NewHeaderLabel("Face"), isLast: false));
            headerRow.Add(MakeFlexColumnCell(NewHeaderLabel("Area"), isLast: false));
            headerRow.Add(MakeFlexColumnCell(NewHeaderLabel("Drag"), isLast: false));
            headerRow.Add(MakeFlexColumnCell(NewHeaderLabel("Depth"), isLast: false));
            headerRow.Add(MakeFlexColumnCell(NewHeaderLabel("Mult"), isLast: true));
            table.Add(headerRow);

            // Body rows
            for (var i = 0; i < 6; i++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.paddingTop = 1f;
                row.style.paddingBottom = 1f;

                row.Add(MakeFaceColumnCell(NewFaceLabel(FACE_LABELS[i]), isLast: false));
                row.Add(MakeFlexColumnCell(NewValueField(areaProp?.GetArrayElementAtIndex(i)), isLast: false));
                row.Add(MakeFlexColumnCell(NewValueField(dragProp?.GetArrayElementAtIndex(i)), isLast: false));
                row.Add(MakeFlexColumnCell(NewValueField(depthProp?.GetArrayElementAtIndex(i)), isLast: false));
                row.Add(MakeFlexColumnCell(NewValueField(multProp?.GetArrayElementAtIndex(i)), isLast: true));

                table.Add(row);
            }

            return table;
        }

        private static VisualElement MakeFaceColumnCell(VisualElement content, bool isLast)
        {
            return WrapInSizedCell(content, fixedWidth: FACE_COL_WIDTH, isLast: isLast);
        }

        private static VisualElement MakeFlexColumnCell(VisualElement content, bool isLast)
        {
            return WrapInSizedCell(content, fixedWidth: -1f, isLast: isLast);
        }

        private static VisualElement WrapInSizedCell(VisualElement content, float fixedWidth, bool isLast)
        {
            var wrapper = new VisualElement();
            wrapper.style.flexDirection = FlexDirection.Row;
            wrapper.style.alignItems = Align.Center;
            wrapper.style.overflow = Overflow.Hidden;
            wrapper.style.marginRight = isLast ? 0 : CELL_MARGIN_RIGHT;
            if (fixedWidth >= 0f)
            {
                wrapper.style.width = fixedWidth;
                wrapper.style.flexGrow = 0;
                wrapper.style.flexShrink = 0;
            }
            else
            {
                wrapper.style.flexGrow = 1f;
                wrapper.style.flexShrink = 1f;
                wrapper.style.minWidth = 0;
                wrapper.style.flexBasis = 0;
            }

            content.style.width = new Length(100f, LengthUnit.Percent);
            content.style.flexGrow = 0;
            content.style.flexShrink = 1f;
            content.style.marginLeft = 0;
            content.style.marginRight = 0;
            content.style.marginTop = 0;
            content.style.marginBottom = 0;
            wrapper.Add(content);
            return wrapper;
        }

        private static Label NewHeaderLabel(string text)
        {
            var label = new Label(text);
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.paddingLeft = 6f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = new Color(220f / 255f, 230f / 255f, 245f / 255f);
            return label;
        }

        private static Label NewFaceLabel(string text)
        {
            var label = new Label(text);
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.paddingLeft = 6f;
            label.style.color = new Color(200f / 255f, 215f / 255f, 235f / 255f);
            return label;
        }

        private static VisualElement NewValueField(SerializedProperty prop)
        {
            if (prop == null)
            {
                var placeholder = new Label("-");
                placeholder.style.unityTextAlign = TextAnchor.MiddleLeft;
                placeholder.style.paddingLeft = 6f;
                return placeholder;
            }
            var field = new FloatField { value = prop.floatValue, isDelayed = true };
            field.RegisterValueChangedCallback(evt =>
            {
                prop.serializedObject.Update();
                prop.floatValue = evt.newValue;
                prop.serializedObject.ApplyModifiedProperties();
            });
            return field;
        }

        private void AddCube()
        {
            if (_cubesProp == null)
            {
                return;
            }
            _cubesProp.serializedObject.Update();
            var index = _cubesProp.arraySize;
            _cubesProp.arraySize++;
            _cubesProp.serializedObject.ApplyModifiedProperties();
            _cubesProp.serializedObject.Update();
            var entry = _cubesProp.GetArrayElementAtIndex(index);
            var nameProp = entry.FindPropertyRelative("name");
            if (nameProp != null && string.IsNullOrEmpty(nameProp.stringValue))
            {
                nameProp.stringValue = "NEW";
            }
            var weightProp = entry.FindPropertyRelative("weight");
            if (weightProp != null && weightProp.floatValue == 0f)
            {
                weightProp.floatValue = 1f;
            }
            _cubesProp.serializedObject.ApplyModifiedProperties();
            RefreshCubesList();
        }

        private void RemoveCube(int index)
        {
            if (_cubesProp == null || index < 0 || index >= _cubesProp.arraySize)
            {
                return;
            }
            _cubesProp.serializedObject.Update();
            _cubesProp.DeleteArrayElementAtIndex(index);
            _cubesProp.serializedObject.ApplyModifiedProperties();
            RefreshCubesList();
        }

        // -------------------- Drag Cube Tools --------------------

        private VisualElement BuildDragCubeTools()
        {
            var section = new VisualElement();
            section.AddToClassList("drag-tools-box");

            var sectionHeader = new Label("Drag Cube Tools");
            sectionHeader.AddToClassList("data-editor-section-header");
            sectionHeader.style.marginBottom = 4f;
            section.Add(sectionHeader);

            // Counters
            _cubeCountLabel = AddInfoRow(section, "Current Cubes", "0");
            _rendererCountLabel = AddInfoRow(section, "DragCubeMesh Renderers", "0");
            _paintableCountLabel = AddInfoRow(section, "Paintable Renderers", "0");
            _renderRootLabel = AddInfoRow(section, "Render Root", "-");

            // Action buttons
            var tagBtn = new Button(OnClickTagPaintable) { text = "Tag Paintable Renderers" };
            tagBtn.style.marginTop = 4f;
            section.Add(tagBtn);

            var calcBtn = new Button(OnClickCalculate) { text = "Calculate Drag Cubes" };
            calcBtn.style.marginTop = 2f;
            section.Add(calcBtn);

            // Deployable capture
            _deployableSection = BuildDeployableSection();
            section.Add(_deployableSection);

            _statusBox = new HelpBox(string.Empty, HelpBoxMessageType.Info);
            _statusBox.style.display = DisplayStyle.None;
            _statusBox.style.marginTop = 4f;
            section.Add(_statusBox);

            UpdateCounters();
            return section;
        }

        private VisualElement BuildDeployableSection()
        {
            var deployable = _module == null ? null : _module.GetComponent<Module_Deployable>();
            var container = new VisualElement();
            container.style.marginTop = 6f;
            if (deployable == null)
            {
                container.style.display = DisplayStyle.None;
                return container;
            }

            var header = new Label("Deployable Module Detected");
            header.AddToClassList("data-editor-subsection-header");
            container.Add(header);

            var advancedToggle = new Toggle("Advanced Deployable Capture") { value = _advancedDeployableCapture };
            advancedToggle.AddToClassList("unity-base-field__aligned");
            var captureContent = new VisualElement();
            captureContent.style.display = _advancedDeployableCapture ? DisplayStyle.Flex : DisplayStyle.None;
            advancedToggle.RegisterValueChangedCallback(evt =>
            {
                _advancedDeployableCapture = evt.newValue;
                captureContent.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });
            container.Add(advancedToggle);
            container.Add(captureContent);

            var clips = GetDeployableAnimationClips(deployable);
            if (clips.Length == 0)
            {
                captureContent.Add(new HelpBox("No animation clips were found on this deployable's animator.", HelpBoxMessageType.Warning));
                return container;
            }

            var clipNames = new List<string>();
            for (var i = 0; i < clips.Length; i++)
            {
                clipNames.Add(clips[i] == null ? "(missing clip)" : clips[i].name);
            }

            var clipDropdown = new PopupField<string>("Animation Clip", clipNames, Mathf.Clamp(_deployableClipIndex, 0, clipNames.Count - 1));
            clipDropdown.AddToClassList("unity-base-field__aligned");
            clipDropdown.RegisterValueChangedCallback(evt => _deployableClipIndex = clipNames.IndexOf(evt.newValue));
            captureContent.Add(clipDropdown);

            var retractedSlider = new Slider("Retracted Time", 0f, 1f) { value = _deployableRetractedTime, showInputField = true };
            retractedSlider.AddToClassList("unity-base-field__aligned");
            retractedSlider.RegisterValueChangedCallback(evt => _deployableRetractedTime = evt.newValue);
            captureContent.Add(retractedSlider);

            var extendedSlider = new Slider("Extended Time", 0f, 1f) { value = _deployableExtendedTime, showInputField = true };
            extendedSlider.AddToClassList("unity-base-field__aligned");
            extendedSlider.RegisterValueChangedCallback(evt => _deployableExtendedTime = evt.newValue);
            captureContent.Add(extendedSlider);

            var buttonRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4f } };
            var captureRetracted = new Button(() => OnClickCaptureDeployable(clips, isRetracted: true, isExtended: false)) { text = "Capture Retracted" };
            captureRetracted.style.flexGrow = 1f;
            captureRetracted.style.marginRight = 4f;
            buttonRow.Add(captureRetracted);
            var captureExtended = new Button(() => OnClickCaptureDeployable(clips, isRetracted: false, isExtended: true)) { text = "Capture Extended" };
            captureExtended.style.flexGrow = 1f;
            buttonRow.Add(captureExtended);
            captureContent.Add(buttonRow);

            var captureBoth = new Button(() => OnClickCaptureDeployable(clips, isRetracted: true, isExtended: true)) { text = "Capture Retracted + Extended" };
            captureBoth.style.marginTop = 2f;
            captureContent.Add(captureBoth);

            return container;
        }

        private Label AddInfoRow(VisualElement parent, string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("drag-info-row");

            var l = new Label(label);
            l.AddToClassList("drag-info-row__label");
            row.Add(l);

            var v = new Label(value);
            v.AddToClassList("drag-info-row__value");
            row.Add(v);

            parent.Add(row);
            return v;
        }

        private void UpdateCounters()
        {
            var moduleDrag = _module as Module_Drag;
            var partObject = moduleDrag == null ? null : moduleDrag.gameObject;
            var dataDrag = GetDragData(moduleDrag);
            var renderRoot = GetDragCubeRenderRoot(partObject);
            var rendererCount = CountDragCubeRenderers(renderRoot);
            var paintableCount = CollectPaintableRendererObjects(partObject, false).Count;

            if (_cubeCountLabel != null)
            {
                _cubeCountLabel.text = (dataDrag?.cubes?.Count ?? 0).ToString(CultureInfo.InvariantCulture);
            }
            if (_rendererCountLabel != null)
            {
                _rendererCountLabel.text = rendererCount.ToString(CultureInfo.InvariantCulture);
            }
            if (_paintableCountLabel != null)
            {
                _paintableCountLabel.text = paintableCount.ToString(CultureInfo.InvariantCulture);
            }
            if (_renderRootLabel != null)
            {
                _renderRootLabel.text = renderRoot == null ? "-" : renderRoot.name;
            }
        }

        private void OnClickTagPaintable()
        {
            var moduleDrag = _module as Module_Drag;
            if (moduleDrag == null)
            {
                return;
            }
            var partObject = moduleDrag.gameObject;
            var prefabPath = PathUtils.GetPrefabOrAssetPath(moduleDrag, partObject);
            if (string.IsNullOrEmpty(prefabPath))
            {
                SetStatus("Open or select a prefab-backed part to save drag cube changes.", HelpBoxMessageType.Info);
                return;
            }

            var untagged = CollectPaintableRendererObjects(partObject, onlyUntagged: true);
            if (untagged.Count == 0)
            {
                SetStatus($"All renderers using {PAINTABLE_SHADER_NAME} are already tagged {DragRendererSettings.DRAG_CUBE_TAG}.", HelpBoxMessageType.Info);
                return;
            }

            Undo.RecordObjects(untagged.ToArray(), $"Tag {DragRendererSettings.DRAG_CUBE_TAG} Renderers");
            try
            {
                foreach (var obj in untagged)
                {
                    obj.tag = DragRendererSettings.DRAG_CUBE_TAG;
                    EditorUtility.SetDirty(obj);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
                }
            }
            catch (UnityException ex)
            {
                Debug.LogException(ex);
                SetStatus($"Could not assign tag '{DragRendererSettings.DRAG_CUBE_TAG}'. Verify it exists in TagManager.", HelpBoxMessageType.Error);
                return;
            }

            SavePrefabChanges(partObject, prefabPath);
            SetStatus($"Tagged {untagged.Count} Paintable renderer object{(untagged.Count == 1 ? string.Empty : "s")} as {DragRendererSettings.DRAG_CUBE_TAG}.", HelpBoxMessageType.Info);
            UpdateCounters();
        }

        private void OnClickCalculate()
        {
            var moduleDrag = _module as Module_Drag;
            if (moduleDrag == null)
            {
                return;
            }
            var partObject = moduleDrag.gameObject;
            var dataDrag = GetDragData(moduleDrag);
            var prefabPath = PathUtils.GetPrefabOrAssetPath(moduleDrag, partObject);
            var renderRoot = GetDragCubeRenderRoot(partObject);
            var rendererCount = CountDragCubeRenderers(renderRoot);

            if (dataDrag == null || renderRoot == null || rendererCount == 0 || string.IsNullOrEmpty(prefabPath))
            {
                SetStatus("Calculate requires a tagged DragCubeMesh renderer set and a prefab-backed part.", HelpBoxMessageType.Warning);
                return;
            }

            if (Shader.Find(DragRendererSettings.DRAG_RENDERER_SHADER_NAME) == null)
            {
                SetStatus($"Could not find shader '{DragRendererSettings.DRAG_RENDERER_SHADER_NAME}'.", HelpBoxMessageType.Error);
                return;
            }

            var dragCubes = new List<DragCube>();
            try
            {
                var enumerator = DragRenderer.RenderAndCalculateDragCubes(moduleDrag.gameObject, renderRoot, dragCubes, false, true);
                while (enumerator.MoveNext())
                {
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SetStatus("Drag cube calculation failed. See the console for details.", HelpBoxMessageType.Error);
                return;
            }

            if (dragCubes.Count == 0 || !HasRenderedDragCubeArea(dragCubes))
            {
                SetStatus("Drag cube calculation produced no rendered area.", HelpBoxMessageType.Warning);
                return;
            }

            Undo.RecordObject(moduleDrag, "Calculate Drag Cubes");
            dataDrag.cubes ??= new List<DragCube>();
            dataDrag.cubes.Clear();
            dataDrag.cubes.AddRange(dragCubes);
            dataDrag.SetDragWeightsList();
            dataDrag.UpdateExposedArea = true;

            EditorUtility.SetDirty(moduleDrag);
            PrefabUtility.RecordPrefabInstancePropertyModifications(moduleDrag);
            SavePrefabChanges(moduleDrag.gameObject, prefabPath);

            SetStatus($"Calculated {dragCubes.Count} drag cube{(dragCubes.Count == 1 ? string.Empty : "s")}.", HelpBoxMessageType.Info);
            _cubesProp.serializedObject.Update();
            RefreshCubesList();
        }

        private void OnClickCaptureDeployable(AnimationClip[] clips, bool isRetracted, bool isExtended)
        {
            var moduleDrag = _module as Module_Drag;
            if (moduleDrag == null || clips == null || clips.Length == 0)
            {
                return;
            }
            var clipIndex = Mathf.Clamp(_deployableClipIndex, 0, clips.Length - 1);
            var clip = clips[clipIndex];
            if (clip == null)
            {
                SetStatus("Selected animation clip is missing.", HelpBoxMessageType.Warning);
                return;
            }

            var dataDrag = GetDragData(moduleDrag);
            var partObject = moduleDrag.gameObject;
            var renderRoot = GetDragCubeRenderRoot(partObject);
            var prefabPath = PathUtils.GetPrefabOrAssetPath(moduleDrag, partObject);
            if (dataDrag == null || renderRoot == null || string.IsNullOrEmpty(prefabPath))
            {
                SetStatus("Capture requires a tagged renderer set, a Data_Drag, and a prefab-backed part.", HelpBoxMessageType.Warning);
                return;
            }
            if (Shader.Find(DragRendererSettings.DRAG_RENDERER_SHADER_NAME) == null)
            {
                SetStatus($"Could not find shader '{DragRendererSettings.DRAG_RENDERER_SHADER_NAME}'.", HelpBoxMessageType.Error);
                return;
            }

            var captures = new List<(string name, float time, float weight)>();
            if (isRetracted)
            {
                captures.Add((Module_Deployable.DRAGCUBE_RETRACTED_NAME, _deployableRetractedTime, 1f));
            }
            if (isExtended)
            {
                captures.Add((Module_Deployable.DRAGCUBE_EXTENDED_NAME, _deployableExtendedTime, 0f));
            }
            if (captures.Count == 0)
            {
                return;
            }

            var captured = new List<DragCube>();
            try
            {
                var sampleRoot = GetDeployableAnimationRoot(partObject);
                foreach (var capture in captures)
                {
                    var cube = CaptureDeployableDragCube(sampleRoot, renderRoot, clip, capture.name, capture.time, capture.weight);
                    if (cube == null)
                    {
                        SetStatus($"Capture for {capture.name} produced no rendered area.", HelpBoxMessageType.Warning);
                        return;
                    }
                    captured.Add(cube);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SetStatus("Deployable drag cube capture failed. See the console for details.", HelpBoxMessageType.Error);
                return;
            }

            Undo.RecordObject(moduleDrag, "Capture Deployable Drag Cubes");
            dataDrag.cubes ??= new List<DragCube>();
            foreach (var cube in captured)
            {
                UpsertDragCube(dataDrag.cubes, cube);
            }
            dataDrag.SetDragWeightsList();
            dataDrag.UpdateExposedArea = true;

            EditorUtility.SetDirty(moduleDrag);
            PrefabUtility.RecordPrefabInstancePropertyModifications(moduleDrag);
            SavePrefabChanges(moduleDrag.gameObject, prefabPath);

            SetStatus($"Captured {captured.Count} deployable cube{(captured.Count == 1 ? string.Empty : "s")}: {string.Join(", ", captured.ConvertAll(c => c.Name))}.", HelpBoxMessageType.Info);
            _cubesProp.serializedObject.Update();
            RefreshCubesList();
        }

        private void SetStatus(string message, HelpBoxMessageType type)
        {
            if (_statusBox == null)
            {
                return;
            }
            _statusBox.text = message;
            _statusBox.messageType = type;
            _statusBox.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // -------------------- Helpers (lifted from legacy editor) --------------------

        private static Data_Drag GetDragData(Module_Drag moduleDrag)
        {
            return moduleDrag == null ? null : DataDragField?.GetValue(moduleDrag) as Data_Drag;
        }

        private static GameObject GetDragCubeRenderRoot(GameObject partObject)
        {
            if (partObject == null)
            {
                return null;
            }
            var modelTransform = FindChildRecursive(partObject.transform, "model");
            return modelTransform == null ? partObject : modelTransform.gameObject;
        }

        private static Transform FindChildRecursive(Transform parentTransform, string childName)
        {
            if (parentTransform.name == childName)
            {
                return parentTransform;
            }
            foreach (Transform child in parentTransform)
            {
                var match = FindChildRecursive(child, childName);
                if (match != null)
                {
                    return match;
                }
            }
            return null;
        }

        private static int CountDragCubeRenderers(GameObject renderRoot)
        {
            if (renderRoot == null)
            {
                return 0;
            }
            var count = 0;
            foreach (var renderer in renderRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != null && renderer.CompareTag(DragRendererSettings.DRAG_CUBE_TAG))
                {
                    count++;
                }
            }
            return count;
        }

        private static List<GameObject> CollectPaintableRendererObjects(GameObject renderRoot, bool onlyUntagged)
        {
            var objects = new List<GameObject>();
            if (renderRoot == null)
            {
                return objects;
            }
            var seen = new HashSet<GameObject>();
            foreach (var renderer in renderRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !UsesPaintableShader(renderer)
                    || (onlyUntagged && renderer.CompareTag(DragRendererSettings.DRAG_CUBE_TAG))
                    || !seen.Add(renderer.gameObject))
                {
                    continue;
                }
                objects.Add(renderer.gameObject);
            }
            return objects;
        }

        private static bool UsesPaintableShader(Renderer renderer)
        {
            foreach (var material in renderer.sharedMaterials)
            {
                if (material?.shader == null)
                {
                    continue;
                }
                var shaderName = material.shader.name;
                if (shaderName == PAINTABLE_SHADER_NAME || shaderName.EndsWith("/Paintable", StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasRenderedDragCubeArea(List<DragCube> dragCubes)
        {
            foreach (var dragCube in dragCubes)
            {
                foreach (var area in dragCube.Area)
                {
                    if (area > 0f)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static void UpsertDragCube(List<DragCube> dragCubes, DragCube cube)
        {
            for (var i = 0; i < dragCubes.Count; i++)
            {
                if (dragCubes[i].Name == cube.Name)
                {
                    dragCubes[i] = cube;
                    return;
                }
            }
            dragCubes.Add(cube);
        }

        private static AnimationClip[] GetDeployableAnimationClips(Module_Deployable deployable)
        {
            var animator = GetDeployableAnimator(deployable);
            var controller = animator == null ? null : animator.runtimeAnimatorController;
            return controller == null ? Array.Empty<AnimationClip>() : controller.animationClips;
        }

        private static Animator GetDeployableAnimator(Module_Deployable deployable)
        {
            if (deployable == null)
            {
                return null;
            }
            if (deployable.animator != null)
            {
                return deployable.animator;
            }
            return deployable.GetComponentInChildren<Animator>(true);
        }

        private static GameObject GetDeployableAnimationRoot(GameObject partObject)
        {
            var deployable = partObject == null ? null : partObject.GetComponent<Module_Deployable>();
            var animator = GetDeployableAnimator(deployable);
            return animator == null ? partObject : animator.gameObject;
        }

        private static DragCube CaptureDeployableDragCube(GameObject sampleRoot, GameObject renderRoot, AnimationClip clip, string name, float normalizedTime, float weight)
        {
            var startedAnimationMode = !AnimationMode.InAnimationMode();
            if (startedAnimationMode)
            {
                AnimationMode.StartAnimationMode();
            }
            try
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(sampleRoot, clip, Mathf.Clamp01(normalizedTime) * clip.length);
                AnimationMode.EndSampling();

                var dragCubes = new List<DragCube>
                {
                    new(name) { Weight = weight },
                };
                var dragRenderer = new DragRenderer();
                var context = new DragRenderContext();
                var enumerator = dragRenderer.RenderPartDragCubes(renderRoot, dragCubes, context, false, true);
                while (enumerator.MoveNext())
                {
                }
                if (dragCubes.Count == 0 || !HasRenderedDragCubeArea(dragCubes))
                {
                    return null;
                }
                dragCubes[0].Name = name;
                dragCubes[0].Weight = weight;
                return dragCubes[0];
            }
            finally
            {
                if (startedAnimationMode && AnimationMode.InAnimationMode())
                {
                    AnimationMode.StopAnimationMode();
                }
            }
        }

        private static void SavePrefabChanges(GameObject targetObject, string prefabPath)
        {
            EditorUtility.SetDirty(targetObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(targetObject);
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot != null && targetObject.transform.root == stage.prefabContentsRoot.transform)
            {
                PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, prefabPath);
                return;
            }
            if (PrefabUtility.IsPartOfPrefabAsset(targetObject))
            {
                PrefabUtility.SavePrefabAsset(targetObject.transform.root.gameObject);
            }
            AssetDatabase.SaveAssets();
        }

    }
}
