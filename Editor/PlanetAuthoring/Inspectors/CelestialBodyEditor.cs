using System;
using KSP;
using KSP.Rendering;
using Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Planet;
using Ksp2UnityTools.Editor.Widgets;
using Redux.CelestialBody;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Hosts the planet inspector on a body's Scaled prefab, the GameObject carrying <see cref="CoreCelestialBodyData" />.
    /// </summary>
    /// <remarks>
    /// The components the planet inspector presents are hidden from the Inspector while it shows, as the part
    /// inspector hides a part's modules.
    /// </remarks>
    [CustomEditor(typeof(CoreCelestialBodyData))]
    public class CelestialBodyEditor : UnityEditor.Editor
    {
        private static readonly Type[] COVERED_COMPONENTS =
        {
            typeof(CelestialBodyLighting),
            typeof(CelestialBodyPostProcess),
            typeof(AtmosphereDataModelComponent),
            typeof(ScaledCloudDataModelComponent),
            typeof(CelestialScaledMaterialReplacer),
        };

        private readonly InspectorComponentHider _hider = new();

        private void OnEnable()
        {
            if (target is CoreCelestialBodyData body)
            {
                _hider.Hide(body.gameObject, COVERED_COMPONENTS);
            }
        }

        private void OnDisable() => _hider.Restore();

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI() =>
            PlanetInspectorView.Build(PlanetInspectorContext.FromBody((CoreCelestialBodyData)target, serializedObject));
    }
}
