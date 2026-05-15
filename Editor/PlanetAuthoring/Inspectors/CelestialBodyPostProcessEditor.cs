using KSP.Rendering;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Custom inspector for <see cref="CelestialBodyPostProcess" />.
    /// </summary>
    /// <remarks>
    /// Surfaces the bound <see cref="PostProcessData" /> ScriptableObject reference and inlines the
    /// asset's fields directly under it so artists can edit altitude bounds, the profile, and
    /// per-time-of-day auto-exposure without opening the asset. Mirrors the PQS inspector's
    /// data-asset inlining pattern. Field routing:
    /// <list type="bullet">
    ///   <item>Component-level: data (the PostProcessData reference).</item>
    ///   <item>Inlined from PostProcessData under Altitudes + Profile: innerAltitude,
    ///         outerAltitude, profile.</item>
    ///   <item>Inlined from PostProcessData under Auto Exposure: autoExposureEnabled,
    ///         autoExposureBlendMode, autoExposurePropertiesDay, autoExposurePropertiesSunset,
    ///         autoExposurePropertiesNight.</item>
    ///   <item>Hidden: none. The inlined section is rebuilt whenever the data reference changes.</item>
    /// </list>
    /// Layout lives in <c>Assets/Windows/PlanetAuthoring/Inspectors/CelestialBodyPostProcessInspector.uxml</c>.
    /// </remarks>
    [CustomEditor(typeof(CelestialBodyPostProcess))]
    public class CelestialBodyPostProcessEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "/Assets/Windows/PlanetAuthoring/Inspectors/CelestialBodyPostProcessInspector.uxml";

        private VisualElement _dataSlot;
        private PostProcessData _boundData;

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                root.Add(new Label("Failed to load CelestialBodyPostProcessInspector.uxml"));
                return root;
            }
            tree.CloneTree(root);

            Ksp2UnityToolsStyles.Apply(root);

            _dataSlot = root.Q<VisualElement>("post-process-data-slot");
            RebuildDataSlot();

            // Rebuild the inlined section only when the user drops a different PostProcessData into
            // the field above; cheaper than polling every 500ms.
            var dataProp = serializedObject.FindProperty("data");
            if (dataProp != null && _dataSlot != null)
            {
                var tracker = new VisualElement();
                tracker.TrackPropertyValue(dataProp, _ => RebuildDataSlot());
                _dataSlot.Add(tracker);
            }

            root.Bind(serializedObject);
            return root;
        }

        private void RebuildDataSlot()
        {
            if (_dataSlot == null)
                return;

            var component = (CelestialBodyPostProcess)target;
            var data = component != null ? component.Data : null;
            if (data == _boundData)
                return;

            _boundData = data;
            _dataSlot.Clear();

            if (data == null)
            {
                _dataSlot.Add(new HelpBox(
                    "No PostProcessData assigned. Assign an asset above to edit altitude bounds and auto-exposure here.",
                    HelpBoxMessageType.Info
                ));
                return;
            }

            var dataSO = new SerializedObject(data);

            var altitudesAndProfile = new Foldout { text = "Altitudes + Profile", value = true };
            altitudesAndProfile.AddToClassList("body-inspector-section");
            altitudesAndProfile.Add(BindField(dataSO, "innerAltitude", "Inner Altitude (m)",
                "Altitude (m) at which the post-process volume is fully active. Below this the profile blends to 1."));
            altitudesAndProfile.Add(BindField(dataSO, "outerAltitude", "Outer Altitude (m)",
                "Altitude (m) at which the post-process volume fades out. Above this the body's profile no longer contributes."));
            altitudesAndProfile.Add(BindField(dataSO, "profile", "Post-Process Profile",
                "Post-processing profile blended into the camera stack while the camera is between Inner and Outer altitudes."));
            _dataSlot.Add(altitudesAndProfile);

            var autoExposure = new Foldout { text = "Auto Exposure", value = true };
            autoExposure.AddToClassList("body-inspector-section");
            autoExposure.Add(BindField(dataSO, "autoExposureEnabled", "Enabled",
                "Drive auto-exposure with the per-time-of-day properties below."));
            autoExposure.Add(BindField(dataSO, "autoExposureBlendMode", "Blend Mode",
                "How Day/Sunset/Night auto-exposure properties blend across the day/night terminator."));
            autoExposure.Add(BindField(dataSO, "autoExposurePropertiesDay", "Day",
                "Auto-exposure properties applied when the sun is well above the horizon."));
            autoExposure.Add(BindField(dataSO, "autoExposurePropertiesSunset", "Sunset",
                "Auto-exposure properties applied near the day/night terminator. Used only when Blend Mode is set to a three-point blend."));
            autoExposure.Add(BindField(dataSO, "autoExposurePropertiesNight", "Night",
                "Auto-exposure properties applied when the sun is well below the horizon."));
            _dataSlot.Add(autoExposure);
        }

        private static PropertyField BindField(SerializedObject so, string path, string label, string tooltip)
        {
            var prop = so.FindProperty(path);
            var field = new PropertyField(prop, label) { tooltip = tooltip };
            if (prop != null)
                field.BindProperty(prop);
            return field;
        }
    }
}
