using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Validation;
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

        private ValidationSectionBuilder.Handle _validationHandle;

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

            Ksp2UnityToolsStyles.Apply(root, UssPath);

            var validationSlot = root.Q<VisualElement>("validation-slot");
            if (validationSlot != null)
            {
                _validationHandle = ValidationSectionBuilder.Mount(validationSlot);
                RefreshValidation();
                root.schedule.Execute(RefreshValidation).Every(500);
            }

            var surfaceSlot = root.Q<VisualElement>("surface-authoring-slot");
            if (surfaceSlot != null)
                SurfaceAuthoringBuilder.Populate(surfaceSlot, target as PQS);

            root.Bind(serializedObject);
            return root;
        }

        private void RefreshValidation()
        {
            if (!_validationHandle.IsValid || target == null)
                return;
            var pqs = target as PQS;
            if (pqs == null)
                return;
            CoreCelestialBodyData body = pqs.GetComponentInParent<CoreCelestialBodyData>();
            ValidationSectionBuilder.Refresh(_validationHandle, body);
        }
    }
}
