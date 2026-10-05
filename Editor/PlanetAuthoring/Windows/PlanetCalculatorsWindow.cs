using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Windows
{
    /// <summary>
    /// Editor utility window that converts Blender Displace-modifier midlevel and strength values into a KSP2 body radius, TerrainHeightScale and oceanAltitude.
    /// </summary>
    /// <remarks>
    /// Inputs are the Mid-Level and Strength values from Blender's Displace modifier on the source
    /// sphere, or the equivalent values reported by the Heightmap Baker after baking the mesh in
    /// Unity, plus how far the sea sits above the lowest point in the same units. The calculator
    /// scales them to a target body so the mesh's relative shape is preserved at the new scale, and
    /// writes the results to drive CoreCelestialBodyData. Last-used inputs persist in EditorPrefs.
    /// <para>
    /// The body radius is sea level. PQS builds terrain up from a base sphere of radius minus
    /// oceanAltitude, which is the mesh's lowest point, so oceanAltitude is the scaled sea level.
    /// Stock Kerbin is 600000 m with oceanAltitude 4270.
    /// </para>
    /// </remarks>
    public class PlanetCalculatorsWindow : EditorWindow
    {
        /// <summary>
        /// The body values that reproduce a displaced source mesh at a target scale.
        /// </summary>
        public readonly struct HeightSolution
        {
            /// <summary>
            /// Gets the body radius in meters, which is sea level.
            /// </summary>
            public double Radius { get; }

            /// <summary>
            /// Gets the TerrainHeightScale in meters spanned by the heightmap's full range.
            /// </summary>
            public double TerrainHeightScale { get; }

            /// <summary>
            /// Gets the oceanAltitude in meters, the height of sea level above the lowest point.
            /// </summary>
            public double OceanAltitude { get; }

            /// <summary>
            /// Initializes a solution from its three body values.
            /// </summary>
            /// <param name="radius">The body radius in meters.</param>
            /// <param name="terrainHeightScale">The TerrainHeightScale in meters.</param>
            /// <param name="oceanAltitude">The oceanAltitude in meters.</param>
            public HeightSolution(double radius, double terrainHeightScale, double oceanAltitude)
            {
                Radius = radius;
                TerrainHeightScale = terrainHeightScale;
                OceanAltitude = oceanAltitude;
            }
        }

        /// <summary>
        /// Scales a displaced source mesh to a target body.
        /// </summary>
        /// <remarks>
        /// Radius mode puts sea level at the target: scale = target / (mid + seaLevel). Max Height
        /// mode puts the highest peak at the target distance from the center: scale =
        /// target / (mid + strength). A negative sea level is treated as zero, since a sea below the
        /// lowest point covers nothing.
        /// </remarks>
        /// <param name="midLevel">The mesh's lowest point, in mesh units.</param>
        /// <param name="strength">The mesh's highest point minus its lowest, in mesh units.</param>
        /// <param name="seaLevel">The sea's height above the lowest point, in mesh units.</param>
        /// <param name="target">The target sea-level radius or peak distance from the center, in meters.</param>
        /// <param name="targetIsMaxHeight">True if <paramref name="target" /> is the peak distance, false if it is the radius.</param>
        /// <returns>The body values, or all zeros when the inputs leave the scale undefined.</returns>
        public static HeightSolution Solve(
            double midLevel,
            double strength,
            double seaLevel,
            double target,
            bool targetIsMaxHeight
        )
        {
            seaLevel = Math.Max(0.0, seaLevel);
            double denominator = targetIsMaxHeight ? midLevel + strength : midLevel + seaLevel;
            if (denominator <= 0.0)
                return default;

            double scale = target / denominator;
            return new HeightSolution((midLevel + seaLevel) * scale, strength * scale, seaLevel * scale);
        }

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
        private DoubleField _seaLevel;
        private DropdownField _targetType;
        private DoubleField _target;
        private DoubleField _terrainHeight;
        private DoubleField _radius;
        private DoubleField _oceanAltitude;

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
            _seaLevel = root.Q<DoubleField>("sea-level-field");
            _targetType = root.Q<DropdownField>("target-type-field");
            _target = root.Q<DoubleField>("target-field");
            _radius = root.Q<DoubleField>("radius-field");
            _terrainHeight = root.Q<DoubleField>("terrain-height-field");
            _oceanAltitude = root.Q<DoubleField>("ocean-altitude-field");

            LoadPrefs();

            _mid.RegisterValueChangedCallback(_ => Recalculate());
            _strength.RegisterValueChangedCallback(_ => Recalculate());
            _seaLevel.RegisterValueChangedCallback(_ => Recalculate());
            _target.RegisterValueChangedCallback(_ => Recalculate());
            _targetType.RegisterValueChangedCallback(_ => Recalculate());

            Recalculate();
        }

        private void Recalculate()
        {
            SavePrefs();
            HeightSolution solution = Solve(
                _mid.value,
                _strength.value,
                _seaLevel.value,
                _target.value,
                _targetType.index == 1
            );
            _radius.value = solution.Radius;
            _terrainHeight.value = solution.TerrainHeightScale;
            _oceanAltitude.value = solution.OceanAltitude;
        }

        private void LoadPrefs()
        {
            _mid.SetValueWithoutNotify(GetDoublePref("MidLevel"));
            _strength.SetValueWithoutNotify(GetDoublePref("Strength"));
            _seaLevel.SetValueWithoutNotify(GetDoublePref("SeaLevel"));
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
            SetDoublePref("SeaLevel", _seaLevel.value);
            SetDoublePref("Target", _target.value);
            EditorPrefs.SetInt(PrefsPrefix + "TargetTypeIndex", _targetType.index);
        }

        private static double GetDoublePref(string suffix) =>
            double.TryParse(EditorPrefs.GetString(PrefsPrefix + suffix, "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

        private static void SetDoublePref(string suffix, double v) =>
            EditorPrefs.SetString(PrefsPrefix + suffix, v.ToString("R", CultureInfo.InvariantCulture));
    }
}
