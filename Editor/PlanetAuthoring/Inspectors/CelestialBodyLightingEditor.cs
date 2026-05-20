using KSP.Rendering;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Custom inspector for <see cref="CelestialBodyLighting" />.
    /// </summary>
    /// <remarks>
    /// Surfaces the inline <see cref="CelestialBodyLightingData" /> fields under domain foldouts so
    /// artists can edit them without drilling into a serialized struct. Field routing:
    /// <list type="bullet">
    ///   <item>Surfaced under Time of Day: horizonOffset, dayBlendRange, nightBlendRange.</item>
    ///   <item>Surfaced under Ambient: useAmbient, ambientInnerAltitude, ambientOuterAltitude,
    ///         ambientDay, ambientNight, ambientScaled.</item>
    ///   <item>Surfaced under Directional: directionalInnerAltitude, directionalOuterAltitude,
    ///         lightingOverrides.</item>
    ///   <item>Surfaced under Bounce Light: enabled, sphericalGaussianSettings, color,
    ///         intensityAtPeriapsis, intensityAtApoapsis, lightFalloffDistance, lightFalloffCurve.</item>
    ///   <item>Surfaced under Skybox Visibility: skyboxVisibilityInnerAltitude,
    ///         skyboxVisibilityOuterAltitude, dayVisibility, nightVisibility,
    ///         dayNightBlendDistanceScale.</item>
    ///   <item>Hidden: lightingOverridesDict. Runtime-only lookup table rebuilt from
    ///         lightingOverrides.</item>
    /// </list>
    /// Layout lives in <c>Assets/Windows/PlanetAuthoring/Inspectors/CelestialBodyLightingInspector.uxml</c>.
    /// </remarks>
    [CustomEditor(typeof(CelestialBodyLighting))]
    public class CelestialBodyLightingEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "/Assets/Windows/PlanetAuthoring/Inspectors/CelestialBodyLightingInspector.uxml";

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                root.Add(new Label("Failed to load CelestialBodyLightingInspector.uxml"));
                return root;
            }
            tree.CloneTree(root);

            Ksp2UnityToolsStyles.Apply(root);

            root.Bind(serializedObject);
            return root;
        }
    }
}
