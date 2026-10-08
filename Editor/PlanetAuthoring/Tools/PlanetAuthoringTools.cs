using UnityEditor.EditorTools;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>Cross-cutting checks shared by planet-authoring inspectors and tools.</summary>
    internal static class PlanetAuthoringTools
    {
        /// <summary>True when a place/pick EditorTool is currently active. Per-asset SceneView handles should suppress while this is true so they don't steal clicks meant for the tool.</summary>
        public static bool IsExclusiveToolActive()
        {
            var t = ToolManager.activeToolType;
            return t == typeof(PlaceDecalTool)
                || t == typeof(PlaceDiscoverableTool)
                || t == typeof(PlaceSurfaceLandmarkTool)
                || t == typeof(PlaceSurfacePrefabTool)
                || t == typeof(PlanetSurfacePickTool);
        }
    }
}
