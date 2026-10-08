using System.Globalization;
using KSP.Modules;
using Ksp2UnityTools.Editor.PartAuthoring.Gizmos;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.DataEditors
{
    /// <summary>
    /// SceneView gizmo renderer for <see cref="Module_Drag" />'s drag cubes. Toggled by the unified "Drag Cubes" pill in the Core inspector's gizmo row (backed by <see cref="PartAuthoringGizmoSettings.ShowDragCubes" />).
    /// </summary>
    public static class DragGizmos
    {
        private const float DRAG_FACE_PANEL_ALPHA = 0.45f;
        private const float DRAG_FACE_LINE_ALPHA = 0.9f;
        private const float DRAG_FACE_OFFSET = 0.015f;

        private static GUIStyle _dragFaceLabelStyle;

        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        private static void DrawGizmosForDrag(Module_Drag moduleDrag, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowDragCubes)
            {
                return;
            }

            var renderRoot = GetDragCubeRenderRoot(moduleDrag.gameObject);
            var mat = renderRoot == null
                ? moduleDrag.gameObject.transform.localToWorldMatrix
                : renderRoot.transform.localToWorldMatrix;
            var dataDrag = GetDragData(moduleDrag);
            if (dataDrag == null)
            {
                return;
            }

            foreach (var cube in dataDrag.cubes)
            {
                DrawDragCubeFaces(mat, cube);
            }
        }

        private static void DrawDragCubeFaces(Matrix4x4 localToWorldMatrix, DragCube cube)
        {
            for (var faceIndex = 0; faceIndex < DragCube.NUM_FACES; faceIndex++)
            {
                GetFaceAxes(faceIndex, cube.Size, out var normal, out var axisA, out var axisB, out var boundA, out var boundB, out var faceDepth);

                var area = Mathf.Max(0f, cube.Area[faceIndex]);
                var faceArea = Mathf.Max(0.0001f, boundA * boundB);
                var areaScale = Mathf.Sqrt(Mathf.Clamp01(area / faceArea));
                if (areaScale <= 0f)
                {
                    continue;
                }

                var drag = Mathf.Clamp01(cube.Drag[faceIndex]);
                var effectiveScale = Mathf.Sqrt(Mathf.Clamp01(areaScale * areaScale * drag));
                if (effectiveScale <= 0f)
                {
                    continue;
                }

                var halfA = boundA * effectiveScale * 0.5f;
                var halfB = boundB * effectiveScale * 0.5f;
                var faceCenter = cube.Center + normal * (faceDepth * 0.5f + DRAG_FACE_OFFSET);
                var corners = new[]
                {
                    faceCenter - axisA * halfA - axisB * halfB,
                    faceCenter - axisA * halfA + axisB * halfB,
                    faceCenter + axisA * halfA + axisB * halfB,
                    faceCenter + axisA * halfA - axisB * halfB,
                };

                for (var i = 0; i < corners.Length; i++)
                {
                    corners[i] = localToWorldMatrix.MultiplyPoint(corners[i]);
                }

                var faceColor = GetDragFaceColor(drag);
                Handles.color = new Color(faceColor.r, faceColor.g, faceColor.b, DRAG_FACE_PANEL_ALPHA);
                Handles.DrawAAConvexPolygon(corners);

                Handles.color = new Color(faceColor.r, faceColor.g, faceColor.b, DRAG_FACE_LINE_ALPHA);
                Handles.DrawAAPolyLine(2.5f, corners[0], corners[1], corners[2], corners[3], corners[0]);

                DrawDragFaceLabel(localToWorldMatrix, faceCenter, normal, faceIndex, drag);

                var depth = Mathf.Clamp(cube.Depth[faceIndex], 0f, faceDepth);
                if (depth > 0f)
                {
                    var worldFaceCenter = localToWorldMatrix.MultiplyPoint(faceCenter);
                    var worldDepthEnd = localToWorldMatrix.MultiplyPoint(faceCenter - normal * depth);
                    Handles.DrawAAPolyLine(2f, worldFaceCenter, worldDepthEnd);
                }
            }
        }

        private static void DrawDragFaceLabel(Matrix4x4 localToWorldMatrix, Vector3 faceCenter, Vector3 normal, int faceIndex, float drag)
        {
            var worldPosition = localToWorldMatrix.MultiplyPoint(faceCenter + normal * DRAG_FACE_OFFSET);
            Handles.Label(
                worldPosition,
                $"{(DragCube.DragFace)faceIndex}\nD {drag.ToString("0.###", CultureInfo.InvariantCulture)}",
                DragFaceLabelStyle);
        }

        private static GUIStyle DragFaceLabelStyle => _dragFaceLabelStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = Color.white },
            alignment = TextAnchor.MiddleCenter,
        };

        private static Color GetDragFaceColor(float drag)
        {
            drag = Mathf.Clamp01(drag);
            return Color.Lerp(new Color(0.1f, 0.75f, 1f), new Color(1f, 0.35f, 0.1f), drag);
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

        private static Data_Drag GetDragData(Module_Drag moduleDrag)
        {
            if (moduleDrag == null)
            {
                return null;
            }
            var field = typeof(Module_Drag).GetField("dataDrag",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return field?.GetValue(moduleDrag) as Data_Drag;
        }

        private static void GetFaceAxes(int faceIndex, Vector3 size, out Vector3 normal, out Vector3 axisA, out Vector3 axisB, out float boundA, out float boundB, out float faceDepth)
        {
            switch ((DragCube.DragFace)faceIndex)
            {
                case DragCube.DragFace.XP:
                    normal = Vector3.right; axisA = Vector3.up; axisB = Vector3.forward;
                    boundA = size.y; boundB = size.z; faceDepth = size.x; break;
                case DragCube.DragFace.XN:
                    normal = Vector3.left; axisA = Vector3.up; axisB = Vector3.forward;
                    boundA = size.y; boundB = size.z; faceDepth = size.x; break;
                case DragCube.DragFace.YP:
                    normal = Vector3.up; axisA = Vector3.right; axisB = Vector3.forward;
                    boundA = size.x; boundB = size.z; faceDepth = size.y; break;
                case DragCube.DragFace.YN:
                    normal = Vector3.down; axisA = Vector3.right; axisB = Vector3.forward;
                    boundA = size.x; boundB = size.z; faceDepth = size.y; break;
                case DragCube.DragFace.ZP:
                    normal = Vector3.forward; axisA = Vector3.right; axisB = Vector3.up;
                    boundA = size.x; boundB = size.y; faceDepth = size.z; break;
                default:
                    normal = Vector3.back; axisA = Vector3.right; axisB = Vector3.up;
                    boundA = size.x; boundB = size.y; faceDepth = size.z; break;
            }
            boundA = Mathf.Abs(boundA);
            boundB = Mathf.Abs(boundB);
            faceDepth = Mathf.Abs(faceDepth);
        }
    }
}
