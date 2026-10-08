using System;
using KSP.Rendering.Planets;
using KSP.VolumeCloud;
using Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Planet;
using Ksp2UnityTools.Editor.Widgets;
using Uber.Scatter;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Hosts the planet inspector on a body's Local prefab, the GameObject carrying <see cref="PQS" />.
    /// </summary>
    /// <remarks>
    /// The components the planet inspector presents are hidden from the Inspector while it shows, as the part
    /// inspector hides a part's modules.
    /// </remarks>
    [CustomEditor(typeof(PQS))]
    public class PQSEditor : UnityEditor.Editor
    {
        private static readonly Type[] COVERED_COMPONENTS =
        {
            typeof(PQSRenderer),
            typeof(PqsTerrain),
            typeof(PQSDecalController),
            typeof(CloudRenderHelper),
        };

        private readonly InspectorComponentHider _hider = new();

        private void OnEnable()
        {
            if (target is PQS pqs)
            {
                _hider.Hide(pqs.gameObject, COVERED_COMPONENTS);
            }
        }

        private void OnDisable() => _hider.Restore();

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            PlanetInspectorContext context = PlanetInspectorContext.FromPqs((PQS)target, serializedObject);
            if (context != null)
                return PlanetInspectorView.Build(context);

            var root = new VisualElement();
            root.Add(new HelpBox(
                "No celestial body was found for this PQS. Keep the Local prefab beside its Scaled prefab, or open the authoring scene that holds both.",
                HelpBoxMessageType.Warning));
            return root;
        }
    }
}
