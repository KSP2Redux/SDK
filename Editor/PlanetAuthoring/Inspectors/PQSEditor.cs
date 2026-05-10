using KSP.Rendering.Planets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Custom inspector for <see cref="PQS" />.
    /// </summary>
    /// <remarks>
    /// Surfaces the artist-facing authoring fields and the hand-coded surface authoring sections
    /// rendered against the bound <see cref="PQSData" /> and surface material. Layout lives in
    /// <c>Assets/Windows/PQSInspector.uxml</c> with styling in <c>PQSInspector.uss</c>. Field
    /// routing:
    /// <list type="bullet">
    ///   <item>Authoring: data, generatePhysics, UseFixedLevel/FixedLevel, scaled-space tex
    ///         generator settings, maxRaycastDistance.</item>
    ///   <item>Hidden: settings, PQSRenderer, isAlive, isActive, isStarted,
    ///         primaryTargetDistance, primaryTargetAltitude, isSubdivisionEnabled, trackStats.
    ///         These are runtime or auto-managed and belong in the Preview Controls or debug
    ///         window, not in the authoring inspector.</item>
    /// </list>
    /// </remarks>
    [CustomEditor(typeof(PQS))]
    public class PQSEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "/Assets/Windows/PQSInspector.uxml";
        private const string UssPath = "/Assets/Windows/PQSInspector.uss";

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                root.Add(new Label("Failed to load PQSInspector.uxml"));
                return root;
            }
            tree.CloneTree(root);

            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(SDKConfiguration.BasePath + UssPath);
            if (styles != null)
                root.styleSheets.Add(styles);
            else
                Debug.LogWarning($"[PQSEditor] Could not load stylesheet at '{SDKConfiguration.BasePath + UssPath}'. Inspector will render unstyled.");

            var surfaceSlot = root.Q<VisualElement>("surface-authoring-slot");
            if (surfaceSlot != null)
                SurfaceAuthoringBuilder.Populate(surfaceSlot, target as PQS);

            root.Bind(serializedObject);
            return root;
        }
    }
}
