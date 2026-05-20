using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Windows
{
    /// <summary>
    /// Editor utility window that converts Blender Displace-modifier midlevel and strength values into a KSP2 body radius and TerrainHeightScale.
    /// </summary>
    /// <remarks>
    /// Inputs are the Mid-Level and Strength values from Blender's Displace modifier on the source
    /// sphere, or the equivalent values reported by the Heightmap Baker after baking the mesh in
    /// Unity. The calculator scales them to a target body so the mesh's relative shape is preserved
    /// at the new scale, and writes the resulting radius and TerrainHeightScale to drive
    /// CoreCelestialBodyData. Last-used inputs persist in EditorPrefs.
    ///
    /// Radius mode: radius = target, TerrainHeightScale = (target / mid) * strength.
    ///
    /// Max Height mode: target = radius + TerrainHeightScale, distributed by the mid/strength ratio.
    /// scale = target / (mid + strength), radius = mid * scale, TerrainHeightScale = strength * scale.
    /// </remarks>
    public class PlanetCalculatorsWindow : EditorWindow
    {
        private const string UxmlPath = "/Assets/Windows/PlanetAuthoring/Windows/PlanetCalculators.uxml";
        private const string PrefsPrefix = "Ksp2UnityTools.PlanetCalculators.";

        /// <summary>
        /// Opens the Height Calculator as a floating utility window.
        /// </summary>
        [MenuItem(PlanetAuthoringWindows.MenuRoot + "Height Calculator")]
        public static void Show()
        {
            var win = CreateInstance<PlanetCalculatorsWindow>();
            win.titleContent = new GUIContent("Height Calculator");
            win.ShowUtility();
        }

        private DoubleField _mid;
        private DoubleField _strength;
        private DropdownField _targetType;
        private DoubleField _target;
        private DoubleField _terrainHeight;
        private DoubleField _radius;

        private void CreateGUI()
        {
            var root = rootVisualElement;
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                root.Add(new Label("Failed to load PlanetCalculators.uxml"));
                return;
            }
            tree.CloneTree(root);
            Ksp2UnityToolsStyles.Apply(root);
            _mid = root.Q<DoubleField>("midlevel-field");
            _strength = root.Q<DoubleField>("strength-field");
            _targetType = root.Q<DropdownField>("target-type-field");
            _target = root.Q<DoubleField>("target-field");
            _radius = root.Q<DoubleField>("radius-field");
            _terrainHeight = root.Q<DoubleField>("terrain-height-field");

            LoadPrefs();

            _mid.RegisterValueChangedCallback(_ => Recalculate());
            _strength.RegisterValueChangedCallback(_ => Recalculate());
            _target.RegisterValueChangedCallback(_ => Recalculate());
            _targetType.RegisterValueChangedCallback(_ => Recalculate());

            Recalculate();
        }

        private void Recalculate()
        {
            SavePrefs();
            var mid = _mid.value;
            var str = _strength.value;
            var target = _target.value;
            var targetIsMaxHeight = _targetType.index == 1;
            double terrainHeight;
            double radius;
            if (targetIsMaxHeight)
            {
                var scale = target / (mid + str);
                terrainHeight = str * scale;
                radius = mid * scale;
            }
            else
            {
                radius = target;
                terrainHeight = (radius / mid) * str;
            }
            _radius.value = radius;
            _terrainHeight.value = terrainHeight;
        }

        private void LoadPrefs()
        {
            _mid.SetValueWithoutNotify(GetDoublePref("MidLevel"));
            _strength.SetValueWithoutNotify(GetDoublePref("Strength"));
            _target.SetValueWithoutNotify(GetDoublePref("Target"));
            var ttIndex = EditorPrefs.GetInt(PrefsPrefix + "TargetTypeIndex", 0);
            var choices = _targetType.choices;
            if (ttIndex >= 0 && ttIndex < choices.Count)
                _targetType.SetValueWithoutNotify(choices[ttIndex]);
        }

        private void SavePrefs()
        {
            SetDoublePref("MidLevel", _mid.value);
            SetDoublePref("Strength", _strength.value);
            SetDoublePref("Target", _target.value);
            EditorPrefs.SetInt(PrefsPrefix + "TargetTypeIndex", _targetType.index);
        }

        private static double GetDoublePref(string suffix) =>
            double.TryParse(EditorPrefs.GetString(PrefsPrefix + suffix, "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

        private static void SetDoublePref(string suffix, double v) =>
            EditorPrefs.SetString(PrefsPrefix + suffix, v.ToString("R", CultureInfo.InvariantCulture));
    }
}
