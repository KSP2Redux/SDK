using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using KSP.Modules;
using Ksp2UnityTools.Editor.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ksp2UnityTools.Editor.CustomEditors
{
    [CustomEditor(typeof(Module_Drag))]
    public class ModuleDragEditor : UnityEditor.Editor
    {
        private const string PaintableShaderName = "KSP2/Parts/Paintable";
        private const float DragFacePanelAlpha = 0.45f;
        private const float DragFaceLineAlpha = 0.9f;
        private const float DragFaceOffset = 0.015f;

        private static GUIStyle _dragFaceLabelStyle;

        private static readonly FieldInfo DataDragField =
            typeof(Module_Drag).GetField("dataDrag", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo DeployableDataField =
            typeof(Module_Deployable).GetField("dataDeployable", BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool _dragCubeGizmos;

        private bool _advancedDeployableCapture;
        private int _deployableClipIndex;
        private float _deployableRetractedTime;
        private float _deployableExtendedTime = 1f;
        private string _dragCubeStatus;
        private MessageType _dragCubeStatusType = MessageType.Info;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DrawDragCubeTools();
        }

        private void DrawDragCubeTools()
        {
            GUILayout.Space(8f);
            GUILayout.Label("Drag Cube Tools", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Module_Drag moduleDrag = target as Module_Drag;
                GameObject partObject = moduleDrag == null ? null : moduleDrag.gameObject;
                Module_Deployable deployable = partObject == null ? null : partObject.GetComponent<Module_Deployable>();
                Data_Drag dataDrag = GetDragData(moduleDrag);
                GameObject renderRoot = GetDragCubeRenderRoot(partObject);
                int rendererCount = CountDragCubeRenderers(renderRoot);
                int paintableRendererCount = CollectPaintableRendererObjects(partObject, false).Count;
                string prefabPath = PathUtils.GetPrefabOrAssetPath(moduleDrag, partObject);

                EditorGUI.BeginChangeCheck();
                _dragCubeGizmos = EditorGUILayout.Toggle("Visualize Drag Cubes", _dragCubeGizmos);
                if (EditorGUI.EndChangeCheck())
                {
                    SceneView.RepaintAll();
                }

                EditorGUILayout.LabelField(
                    "Current Cubes",
                    (dataDrag?.cubes?.Count ?? 0).ToString(CultureInfo.InvariantCulture)
                );
                EditorGUILayout.LabelField(
                    "DragCubeMesh Renderers",
                    rendererCount.ToString(CultureInfo.InvariantCulture)
                );
                EditorGUILayout.LabelField(
                    "Paintable Renderers",
                    paintableRendererCount.ToString(CultureInfo.InvariantCulture)
                );
                if (renderRoot != null)
                {
                    EditorGUILayout.LabelField("Render Root", renderRoot.name);
                }

                bool canTagPaintableRenderers = moduleDrag != null &&
                    partObject != null &&
                    paintableRendererCount > 0 &&
                    !string.IsNullOrEmpty(prefabPath);
                using (new EditorGUI.DisabledScope(!canTagPaintableRenderers))
                {
                    if (GUILayout.Button("Tag Paintable Renderers"))
                    {
                        TagPaintableRenderers(moduleDrag.gameObject, partObject, prefabPath);
                    }
                }

                bool canCalculate = moduleDrag != null &&
                    deployable == null &&
                    dataDrag != null &&
                    renderRoot != null &&
                    rendererCount > 0 &&
                    !string.IsNullOrEmpty(prefabPath);
                using (new EditorGUI.DisabledScope(!canCalculate))
                {
                    if (GUILayout.Button("Calculate Drag Cubes"))
                    {
                        CalculateDragCubes(moduleDrag, dataDrag, renderRoot, rendererCount, prefabPath);
                    }
                }

                DrawDeployableCaptureTools(
                    moduleDrag,
                    deployable,
                    dataDrag,
                    renderRoot,
                    rendererCount,
                    prefabPath
                );

                if (moduleDrag == null)
                {
                    EditorGUILayout.HelpBox("Select a Module_Drag component to calculate drag cubes.", MessageType.Info);
                }
                else if (dataDrag == null)
                {
                    EditorGUILayout.HelpBox("Module_Drag has no serialized Data_Drag instance.", MessageType.Warning);
                }
                else if (string.IsNullOrEmpty(prefabPath))
                {
                    EditorGUILayout.HelpBox("Open or select a prefab-backed part to save drag cube changes.", MessageType.Info);
                }
                else if (deployable != null && !_advancedDeployableCapture)
                {
                    EditorGUILayout.HelpBox(
                        "This part has a deployable module. Enable Advanced Deployable Capture to generate named state cubes.",
                        MessageType.Info
                    );
                }
                else if (paintableRendererCount == 0)
                {
                    EditorGUILayout.HelpBox(
                        $"No renderers using {PaintableShaderName} were found under the part hierarchy.",
                        MessageType.Info
                    );
                }
                else if (rendererCount == 0)
                {
                    EditorGUILayout.HelpBox(
                        $"No {DragRendererSettings.DRAG_CUBE_TAG} renderers were found under the render root.",
                        MessageType.Warning
                    );
                }
                else if (!string.IsNullOrWhiteSpace(_dragCubeStatus))
                {
                    EditorGUILayout.HelpBox(_dragCubeStatus, _dragCubeStatusType);
                }
            }
        }

        private void TagPaintableRenderers(GameObject targetObject, GameObject hierarchyRoot, string prefabPath)
        {
            List<GameObject> untaggedPaintableObjects = CollectPaintableRendererObjects(hierarchyRoot, true);
            if (untaggedPaintableObjects.Count == 0)
            {
                SetDragCubeStatus(
                    $"All renderers using {PaintableShaderName} are already tagged {DragRendererSettings.DRAG_CUBE_TAG}.",
                    MessageType.Info
                );
                return;
            }

            Undo.RecordObjects(untaggedPaintableObjects.ToArray(), $"Tag {DragRendererSettings.DRAG_CUBE_TAG} Renderers");
            try
            {
                foreach (GameObject paintableObject in untaggedPaintableObjects)
                {
                    paintableObject.tag = DragRendererSettings.DRAG_CUBE_TAG;
                    EditorUtility.SetDirty(paintableObject);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(paintableObject);
                }
            }
            catch (UnityException ex)
            {
                Debug.LogException(ex);
                SetDragCubeStatus(
                    $"Could not assign tag '{DragRendererSettings.DRAG_CUBE_TAG}'. Verify it exists in TagManager.",
                    MessageType.Error
                );
                return;
            }

            SavePrefabChanges(targetObject, prefabPath);
            SetDragCubeStatus(
                $"Tagged {untaggedPaintableObjects.Count} Paintable renderer object" +
                $"{(untaggedPaintableObjects.Count == 1 ? string.Empty : "s")} as {DragRendererSettings.DRAG_CUBE_TAG}.",
                MessageType.Info
            );
        }

        private void DrawDeployableCaptureTools(
            Module_Drag moduleDrag,
            Module_Deployable deployable,
            Data_Drag dataDrag,
            GameObject renderRoot,
            int rendererCount,
            string prefabPath
        )
        {
            if (deployable == null)
            {
                return;
            }

            GUILayout.Space(4f);
            EditorGUILayout.LabelField("Deployable Module Detected", EditorStyles.miniBoldLabel);
            _advancedDeployableCapture = EditorGUILayout.Toggle(
                "Advanced Deployable Capture",
                _advancedDeployableCapture
            );
            if (!_advancedDeployableCapture)
            {
                return;
            }

            AnimationClip[] clips = GetDeployableAnimationClips(deployable);
            if (clips.Length == 0)
            {
                EditorGUILayout.HelpBox("No animation clips were found on this deployable's animator.", MessageType.Warning);
                return;
            }

            _deployableClipIndex = Mathf.Clamp(_deployableClipIndex, 0, clips.Length - 1);
            string[] clipNames = new string[clips.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                clipNames[i] = clips[i] == null ? "(missing clip)" : clips[i].name;
            }

            _deployableClipIndex = EditorGUILayout.Popup("Animation Clip", _deployableClipIndex, clipNames);
            _deployableRetractedTime = EditorGUILayout.Slider("Retracted Time", _deployableRetractedTime, 0f, 1f);
            _deployableExtendedTime = EditorGUILayout.Slider("Extended Time", _deployableExtendedTime, 0f, 1f);

            AnimationClip clip = clips[_deployableClipIndex];
            bool canCapture = moduleDrag != null &&
                dataDrag != null &&
                renderRoot != null &&
                rendererCount > 0 &&
                !string.IsNullOrEmpty(prefabPath) &&
                clip != null;

            using (new EditorGUI.DisabledScope(!canCapture))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Capture Retracted"))
                    {
                        CaptureDeployableDragCubes(
                            moduleDrag,
                            dataDrag,
                            renderRoot,
                            prefabPath,
                            clip,
                            new DeployableDragCubeCapture(
                                Module_Deployable.DRAGCUBE_RETRACTED_NAME,
                                _deployableRetractedTime,
                                1f
                            )
                        );
                    }

                    if (GUILayout.Button("Capture Extended"))
                    {
                        CaptureDeployableDragCubes(
                            moduleDrag,
                            dataDrag,
                            renderRoot,
                            prefabPath,
                            clip,
                            new DeployableDragCubeCapture(
                                Module_Deployable.DRAGCUBE_EXTENDED_NAME,
                                _deployableExtendedTime,
                                0f
                            )
                        );
                    }
                }

                if (GUILayout.Button("Capture Retracted + Extended"))
                {
                    CaptureDeployableDragCubes(
                        moduleDrag,
                        dataDrag,
                        renderRoot,
                        prefabPath,
                        clip,
                        new DeployableDragCubeCapture(
                            Module_Deployable.DRAGCUBE_RETRACTED_NAME,
                            _deployableRetractedTime,
                            1f
                        ),
                        new DeployableDragCubeCapture(
                            Module_Deployable.DRAGCUBE_EXTENDED_NAME,
                            _deployableExtendedTime,
                            0f
                        )
                    );
                }
            }
        }

        private void CaptureDeployableDragCubes(
            Module_Drag moduleDrag,
            Data_Drag dataDrag,
            GameObject renderRoot,
            string prefabPath,
            AnimationClip clip,
            params DeployableDragCubeCapture[] captures
        )
        {
            if (Shader.Find(DragRendererSettings.DRAG_RENDERER_SHADER_NAME) == null)
            {
                SetDragCubeStatus(
                    $"Could not find shader '{DragRendererSettings.DRAG_RENDERER_SHADER_NAME}'.",
                    MessageType.Error
                );
                return;
            }

            var capturedCubes = new List<DragCube>();
            try
            {
                GameObject sampleRoot = GetDeployableAnimationRoot(moduleDrag.gameObject);
                foreach (DeployableDragCubeCapture capture in captures)
                {
                    DragCube cube = CaptureDeployableDragCube(sampleRoot, renderRoot, clip, capture);
                    if (cube == null)
                    {
                        SetDragCubeStatus(
                            $"Capture for {capture.Name} produced no rendered area.",
                            MessageType.Warning
                        );
                        return;
                    }

                    capturedCubes.Add(cube);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SetDragCubeStatus("Deployable drag cube capture failed. See the console for details.", MessageType.Error);
                return;
            }

            Undo.RecordObject(moduleDrag, "Capture Deployable Drag Cubes");
            dataDrag.cubes ??= new List<DragCube>();
            foreach (DragCube cube in capturedCubes)
            {
                UpsertDragCube(dataDrag.cubes, cube);
            }

            dataDrag.SetDragWeightsList();
            dataDrag.UpdateExposedArea = true;

            EditorUtility.SetDirty(moduleDrag);
            PrefabUtility.RecordPrefabInstancePropertyModifications(moduleDrag);
            SavePrefabChanges(moduleDrag.gameObject, prefabPath);

            SetDragCubeStatus(
                $"Captured {capturedCubes.Count} deployable drag cube" +
                $"{(capturedCubes.Count == 1 ? string.Empty : "s")}: {string.Join(", ", capturedCubes.ConvertAll(cube => cube.Name))}.",
                MessageType.Info
            );
        }

        private static DragCube CaptureDeployableDragCube(
            GameObject sampleRoot,
            GameObject renderRoot,
            AnimationClip clip,
            DeployableDragCubeCapture capture
        )
        {
            bool startedAnimationMode = !AnimationMode.InAnimationMode();
            if (startedAnimationMode)
            {
                AnimationMode.StartAnimationMode();
            }

            try
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(sampleRoot, clip, Mathf.Clamp01(capture.NormalizedTime) * clip.length);
                AnimationMode.EndSampling();

                var dragCubes = new List<DragCube>
                {
                    new(capture.Name)
                    {
                        Weight = capture.Weight
                    }
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

                dragCubes[0].Name = capture.Name;
                dragCubes[0].Weight = capture.Weight;
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

        private void CalculateDragCubes(
            Module_Drag moduleDrag,
            Data_Drag dataDrag,
            GameObject renderRoot,
            int rendererCount,
            string prefabPath
        )
        {
            if (Shader.Find(DragRendererSettings.DRAG_RENDERER_SHADER_NAME) == null)
            {
                SetDragCubeStatus(
                    $"Could not find shader '{DragRendererSettings.DRAG_RENDERER_SHADER_NAME}'.",
                    MessageType.Error
                );
                return;
            }

            var dragCubes = new List<DragCube>();
            try
            {
                var enumerator = DragRenderer.RenderAndCalculateDragCubes(
                    moduleDrag.gameObject,
                    renderRoot,
                    dragCubes,
                    false,
                    true
                );
                while (enumerator.MoveNext())
                {
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SetDragCubeStatus("Drag cube calculation failed. See the console for details.", MessageType.Error);
                return;
            }

            if (dragCubes.Count == 0)
            {
                SetDragCubeStatus("Drag cube calculation produced no cubes.", MessageType.Warning);
                return;
            }

            if (!HasRenderedDragCubeArea(dragCubes))
            {
                SetDragCubeStatus(
                    "Drag cube calculation produced no rendered area. Check the DragCubeMesh renderers and materials.",
                    MessageType.Warning
                );
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

            SetDragCubeStatus(
                $"Calculated {dragCubes.Count} drag cube{(dragCubes.Count == 1 ? string.Empty : "s")} from " +
                $"{rendererCount} {DragRendererSettings.DRAG_CUBE_TAG} renderer{(rendererCount == 1 ? string.Empty : "s")}.",
                MessageType.Info
            );
        }

        private static Data_Drag GetDragData(Module_Drag moduleDrag)
        {
            return moduleDrag == null ? null : DataDragField?.GetValue(moduleDrag) as Data_Drag;
        }

        private static Data_Deployable GetDeployableData(Module_Deployable deployable)
        {
            return deployable == null ? null : DeployableDataField?.GetValue(deployable) as Data_Deployable;
        }

        private static AnimationClip[] GetDeployableAnimationClips(Module_Deployable deployable)
        {
            Animator animator = GetDeployableAnimator(deployable);
            RuntimeAnimatorController controller = animator == null ? null : animator.runtimeAnimatorController;
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

            Data_Deployable dataDeployable = GetDeployableData(deployable);
            if (!string.IsNullOrWhiteSpace(dataDeployable?.animationName))
            {
                Transform animationRoot = FindChildRecursive(deployable.gameObject.transform, dataDeployable.animationName);
                Animator animator = animationRoot == null ? null : animationRoot.GetComponentInChildren<Animator>(true);
                if (animator != null)
                {
                    return animator;
                }
            }

            return deployable.GetComponentInChildren<Animator>(true);
        }

        private static GameObject GetDeployableAnimationRoot(GameObject partObject)
        {
            Module_Deployable deployable = partObject == null ? null : partObject.GetComponent<Module_Deployable>();
            Animator animator = GetDeployableAnimator(deployable);
            return animator == null ? partObject : animator.gameObject;
        }

        private static GameObject GetDragCubeRenderRoot(GameObject partObject)
        {
            if (partObject == null)
            {
                return null;
            }

            Transform modelTransform = FindChildRecursive(partObject.transform, "model");
            return modelTransform == null ? partObject : modelTransform.gameObject;
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

        private static int CountDragCubeRenderers(GameObject renderRoot)
        {
            if (renderRoot == null)
            {
                return 0;
            }

            int count = 0;
            foreach (Renderer renderer in renderRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != null && renderer.CompareTag(DragRendererSettings.DRAG_CUBE_TAG))
                {
                    count++;
                }
            }

            return count;
        }

        private static void UpsertDragCube(List<DragCube> dragCubes, DragCube cube)
        {
            for (int i = 0; i < dragCubes.Count; i++)
            {
                if (dragCubes[i].Name == cube.Name)
                {
                    dragCubes[i] = cube;
                    return;
                }
            }

            dragCubes.Add(cube);
        }

        private static List<GameObject> CollectPaintableRendererObjects(GameObject renderRoot, bool onlyUntagged)
        {
            var objects = new List<GameObject>();
            if (renderRoot == null)
            {
                return objects;
            }

            var seen = new HashSet<GameObject>();
            foreach (Renderer renderer in renderRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null ||
                    !UsesPaintableShader(renderer) ||
                    (onlyUntagged && renderer.CompareTag(DragRendererSettings.DRAG_CUBE_TAG)) ||
                    !seen.Add(renderer.gameObject))
                {
                    continue;
                }

                objects.Add(renderer.gameObject);
            }

            return objects;
        }

        private static bool UsesPaintableShader(Renderer renderer)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material?.shader == null)
                {
                    continue;
                }

                string shaderName = material.shader.name;
                if (shaderName == PaintableShaderName ||
                    shaderName.EndsWith("/Paintable", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasRenderedDragCubeArea(List<DragCube> dragCubes)
        {
            foreach (DragCube dragCube in dragCubes)
            {
                foreach (float area in dragCube.Area)
                {
                    if (area > 0f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private struct DeployableDragCubeCapture
        {
            public readonly string Name;
            public readonly float NormalizedTime;
            public readonly float Weight;

            public DeployableDragCubeCapture(string name, float normalizedTime, float weight)
            {
                Name = name;
                NormalizedTime = normalizedTime;
                Weight = weight;
            }
        }

        private static void SavePrefabChanges(GameObject targetObject, string prefabPath)
        {
            EditorUtility.SetDirty(targetObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(targetObject);
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot != null &&
                targetObject.transform.root == stage.prefabContentsRoot.transform)
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

        private void SetDragCubeStatus(string status, MessageType type)
        {
            _dragCubeStatus = status;
            _dragCubeStatusType = type;
        }

        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawGizmosForDrag(Module_Drag moduleDrag, GizmoType gizmoType)
        {
            if (!_dragCubeGizmos)
            {
                return;
            }

            GameObject renderRoot = GetDragCubeRenderRoot(moduleDrag.gameObject);
            Matrix4x4 mat = renderRoot == null
                ? moduleDrag.gameObject.transform.localToWorldMatrix
                : renderRoot.transform.localToWorldMatrix;
            Data_Drag dataDrag = GetDragData(moduleDrag);
            if (dataDrag == null)
            {
                return;
            }

            foreach (DragCube cube in dataDrag.cubes)
            {
                DrawDragCubeFaces(mat, cube);
            }
        }

        private static void DrawDragCubeFaces(Matrix4x4 localToWorldMatrix, DragCube cube)
        {
            for (int faceIndex = 0; faceIndex < DragCube.NUM_FACES; faceIndex++)
            {
                GetFaceAxes(
                    faceIndex,
                    cube.Size,
                    out Vector3 normal,
                    out Vector3 axisA,
                    out Vector3 axisB,
                    out float boundA,
                    out float boundB,
                    out float faceDepth
                );

                float area = Mathf.Max(0f, cube.Area[faceIndex]);
                float faceArea = Mathf.Max(0.0001f, boundA * boundB);
                float areaScale = Mathf.Sqrt(Mathf.Clamp01(area / faceArea));
                if (areaScale <= 0f)
                {
                    continue;
                }

                float drag = Mathf.Clamp01(cube.Drag[faceIndex]);
                float effectiveScale = Mathf.Sqrt(Mathf.Clamp01(areaScale * areaScale * drag));
                if (effectiveScale <= 0f)
                {
                    continue;
                }

                float halfA = boundA * effectiveScale * 0.5f;
                float halfB = boundB * effectiveScale * 0.5f;
                Vector3 faceCenter = cube.Center + normal * (faceDepth * 0.5f + DragFaceOffset);
                Vector3[] corners =
                {
                    faceCenter - axisA * halfA - axisB * halfB,
                    faceCenter - axisA * halfA + axisB * halfB,
                    faceCenter + axisA * halfA + axisB * halfB,
                    faceCenter + axisA * halfA - axisB * halfB
                };

                for (int i = 0; i < corners.Length; i++)
                {
                    corners[i] = localToWorldMatrix.MultiplyPoint(corners[i]);
                }

                Color faceColor = GetDragFaceColor(drag);
                Handles.color = new Color(faceColor.r, faceColor.g, faceColor.b, DragFacePanelAlpha);
                Handles.DrawAAConvexPolygon(corners);

                Handles.color = new Color(faceColor.r, faceColor.g, faceColor.b, DragFaceLineAlpha);
                Handles.DrawAAPolyLine(2.5f, corners[0], corners[1], corners[2], corners[3], corners[0]);

                DrawDragFaceLabel(localToWorldMatrix, faceCenter, normal, faceIndex, drag);

                float depth = Mathf.Clamp(cube.Depth[faceIndex], 0f, faceDepth);
                if (depth > 0f)
                {
                    Vector3 worldFaceCenter = localToWorldMatrix.MultiplyPoint(faceCenter);
                    Vector3 worldDepthEnd = localToWorldMatrix.MultiplyPoint(faceCenter - normal * depth);
                    Handles.DrawAAPolyLine(2f, worldFaceCenter, worldDepthEnd);
                }
            }
        }

        private static void DrawDragFaceLabel(
            Matrix4x4 localToWorldMatrix,
            Vector3 faceCenter,
            Vector3 normal,
            int faceIndex,
            float drag
        )
        {
            Vector3 worldPosition = localToWorldMatrix.MultiplyPoint(faceCenter + normal * DragFaceOffset);
            Handles.Label(
                worldPosition,
                $"{(DragCube.DragFace)faceIndex}\nD {drag.ToString("0.###", CultureInfo.InvariantCulture)}",
                DragFaceLabelStyle
            );
        }

        private static GUIStyle DragFaceLabelStyle => _dragFaceLabelStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            normal =
            {
                textColor = Color.white
            },
            alignment = TextAnchor.MiddleCenter
        };

        private static Color GetDragFaceColor(float drag)
        {
            drag = Mathf.Clamp01(drag);
            return Color.Lerp(new Color(0.1f, 0.75f, 1f), new Color(1f, 0.35f, 0.1f), drag);
        }

        private static void GetFaceAxes(
            int faceIndex,
            Vector3 size,
            out Vector3 normal,
            out Vector3 axisA,
            out Vector3 axisB,
            out float boundA,
            out float boundB,
            out float faceDepth
        )
        {
            switch ((DragCube.DragFace)faceIndex)
            {
                case DragCube.DragFace.XP:
                    normal = Vector3.right;
                    axisA = Vector3.up;
                    axisB = Vector3.forward;
                    boundA = size.y;
                    boundB = size.z;
                    faceDepth = size.x;
                    break;
                case DragCube.DragFace.XN:
                    normal = Vector3.left;
                    axisA = Vector3.up;
                    axisB = Vector3.forward;
                    boundA = size.y;
                    boundB = size.z;
                    faceDepth = size.x;
                    break;
                case DragCube.DragFace.YP:
                    normal = Vector3.up;
                    axisA = Vector3.right;
                    axisB = Vector3.forward;
                    boundA = size.x;
                    boundB = size.z;
                    faceDepth = size.y;
                    break;
                case DragCube.DragFace.YN:
                    normal = Vector3.down;
                    axisA = Vector3.right;
                    axisB = Vector3.forward;
                    boundA = size.x;
                    boundB = size.z;
                    faceDepth = size.y;
                    break;
                case DragCube.DragFace.ZP:
                    normal = Vector3.forward;
                    axisA = Vector3.right;
                    axisB = Vector3.up;
                    boundA = size.x;
                    boundB = size.y;
                    faceDepth = size.z;
                    break;
                default:
                    normal = Vector3.back;
                    axisA = Vector3.right;
                    axisB = Vector3.up;
                    boundA = size.x;
                    boundB = size.y;
                    faceDepth = size.z;
                    break;
            }

            boundA = Mathf.Abs(boundA);
            boundB = Mathf.Abs(boundB);
            faceDepth = Mathf.Abs(faceDepth);
        }
    }
}
