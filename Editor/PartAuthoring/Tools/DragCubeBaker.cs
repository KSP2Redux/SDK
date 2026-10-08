using System;
using System.Collections.Generic;
using System.Reflection;
using KSP.Modules;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Tools
{
    /// <summary>
    /// Shared editor wrapper around the stock drag-cube renderer.
    /// </summary>
    public static class DragCubeBaker
    {
        private static readonly FieldInfo DataDragField =
            typeof(Module_Drag).GetField("dataDrag", BindingFlags.Instance | BindingFlags.NonPublic);

        public static BakeResult Bake(GameObject partObject)
        {
            if (partObject == null)
            {
                return BakeResult.Skipped("No part object.");
            }

            Module_Drag moduleDrag = partObject.GetComponent<Module_Drag>();
            Data_Drag dataDrag = GetDragData(moduleDrag);
            GameObject renderRoot = GetDragCubeRenderRoot(partObject);
            int rendererCount = CountDragCubeRenderers(renderRoot);
            if (moduleDrag == null || dataDrag == null)
            {
                return BakeResult.Skipped("No Module_Drag/Data_Drag.");
            }

            if (renderRoot == null || rendererCount == 0)
            {
                return BakeResult.Skipped("No tagged DragCubeMesh renderers.");
            }

            if (Shader.Find(DragRendererSettings.DRAG_RENDERER_SHADER_NAME) == null)
            {
                return BakeResult.Failed($"Missing shader '{DragRendererSettings.DRAG_RENDERER_SHADER_NAME}'.");
            }

            List<DragCube> dragCubes = new();
            try
            {
                IEnumerator<object> enumerator = AsGenericEnumerator(
                    DragRenderer.RenderAndCalculateDragCubes(partObject, renderRoot, dragCubes, false, true)
                );
                while (enumerator.MoveNext())
                {
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return BakeResult.Failed("Drag cube calculation failed. See the console for details.");
            }

            if (dragCubes.Count == 0 || !HasRenderedDragCubeArea(dragCubes))
            {
                return BakeResult.Skipped("Drag cube calculation produced no rendered area.");
            }

            dataDrag.cubes ??= new List<DragCube>();
            dataDrag.cubes.Clear();
            dataDrag.cubes.AddRange(dragCubes);
            dataDrag.SetDragWeightsList();
            dataDrag.UpdateExposedArea = true;

            EditorUtility.SetDirty(moduleDrag);
            return BakeResult.Baked(dragCubes.Count);
        }

        private static IEnumerator<object> AsGenericEnumerator(System.Collections.IEnumerator enumerator)
        {
            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
            }
        }

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

            Transform modelTransform = FindChildRecursive(partObject.transform, "model");
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

        public readonly struct BakeResult
        {
            private BakeResult(bool success, bool skipped, int cubeCount, string message)
            {
                Success = success;
                IsSkipped = skipped;
                CubeCount = cubeCount;
                Message = message;
            }

            public bool Success { get; }

            public bool IsSkipped { get; }

            public int CubeCount { get; }

            public string Message { get; }

            public static BakeResult Baked(int cubeCount)
            {
                return new BakeResult(true, false, cubeCount, $"Calculated {cubeCount} drag cube{(cubeCount == 1 ? string.Empty : "s")}.");
            }

            public static BakeResult Skipped(string message)
            {
                return new BakeResult(false, true, 0, message);
            }

            public static BakeResult Failed(string message)
            {
                return new BakeResult(false, false, 0, message);
            }
        }
    }
}
