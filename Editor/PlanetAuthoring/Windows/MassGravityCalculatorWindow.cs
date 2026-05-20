using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Windows
{
    /// <summary>
    /// Editor utility window that recommends a body's gravityASL from its radius using the
    /// stock-game body table.
    /// </summary>
    /// <remarks>
    /// Stock data is taken from the base-game body definitions (gravityASL is in Earth gravities,
    /// where 1.0 = 9.80665 m/s²). Bodies are sorted by radius. For an input radius the calculator
    /// linearly interpolates gravityASL between the two bracketing bodies. Below the smallest stock
    /// body it lerps from zero at radius zero. Above the largest it clamps.
    ///
    /// Stock densities are non-monotonic (Tylo and Kerbin share R=600km with very different g), so
    /// the recommendation is a starting point - the bracketing pair is shown so the artist can pick
    /// or override based on the kind of body they're building. Mass and gravParameter are not
    /// authored values - the runtime derives them from radius and gravityASL on body load.
    /// </remarks>
    public class MassGravityCalculatorWindow : EditorWindow
    {
        private const string UxmlPath = "/Assets/Windows/PlanetAuthoring/Windows/MassGravityCalculator.uxml";
        private const string PrefsPrefix = "Ksp2UnityTools.MassGravityCalculator.";

        // Standard gravity, Earth surface. Mirrors PhysicsSettings.STANDARD_GRAVITY_EARTH.
        private const double G0 = 9.80665;

        private readonly struct StockBody
        {
            public readonly string Name;
            public readonly double Radius;
            public readonly double GravityASL;
            public StockBody(string name, double radius, double gravityASL)
            {
                Name = name;
                Radius = radius;
                GravityASL = gravityASL;
            }
        }

        // Sorted by radius ascending. Sourced from the base-game body definition files.
        private static readonly StockBody[] StockBodies =
        {
            new("Gilly",   13_000,      0.005),
            new("Pol",     44_000,      0.038),
            new("Minmus",  60_000,      0.050),
            new("Bop",     65_000,      0.060),
            new("Ike",     130_000,     0.112),
            new("Dres",    138_000,     0.115),
            new("Bis",     180_000,     0.189),
            new("Mun",     200_000,     0.166),
            new("Eeloo",   210_000,     0.172),
            new("Moho",    250_000,     0.275),
            new("Vall",    300_000,     0.235),
            new("Duna",    320_000,     0.300),
            new("Laythe",  500_000,     0.800),
            new("Verda",   510_000,     1.151),
            new("Tylo",    600_000,     0.800),
            new("Kerbin",  600_000,     1.000),
            new("Eve",     700_000,     1.700),
            new("Jool",    6_000_000,   0.800),
            new("Kerbol",  261_600_000, 1.747),
        };

        /// <summary>
        /// Opens the Mass / Gravity Calculator as a floating utility window.
        /// </summary>
        [MenuItem(PlanetAuthoringWindows.MenuRoot + "Gravity Calculator")]
        public static void Show()
        {
            var win = CreateInstance<MassGravityCalculatorWindow>();
            win.titleContent = new GUIContent("Gravity Calculator");
            win.ShowUtility();
        }

        private DoubleField _radius;
        private DoubleField _gravityAsl;
        private DoubleField _gravityMs2;
        private Label _bracketBelow;
        private Label _bracketAbove;

        private void CreateGUI()
        {
            var root = rootVisualElement;
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                root.Add(new Label("Failed to load MassGravityCalculator.uxml"));
                return;
            }
            tree.CloneTree(root);
            Ksp2UnityToolsStyles.Apply(root);

            _radius = root.Q<DoubleField>("radius-field");
            _gravityAsl = root.Q<DoubleField>("gravity-asl-field");
            _gravityMs2 = root.Q<DoubleField>("gravity-ms2-field");
            _bracketBelow = root.Q<Label>("bracket-below-label");
            _bracketAbove = root.Q<Label>("bracket-above-label");

            LoadPrefs();
            _radius.RegisterValueChangedCallback(_ => Recalculate());
            Recalculate();
        }

        private void Recalculate()
        {
            SavePrefs();
            var radius = _radius.value;
            var gAsl = RecommendGravityASL(radius, out var below, out var above);
            var gMs2 = gAsl * G0;

            _gravityAsl.value = gAsl;
            _gravityMs2.value = gMs2;

            _bracketBelow.text = below.HasValue
                ? $"Below: {below.Value.Name} (R={FormatMeters(below.Value.Radius)}, g={below.Value.GravityASL:F3})"
                : "Below: - (smaller than smallest stock body)";
            _bracketAbove.text = above.HasValue
                ? $"Above: {above.Value.Name} (R={FormatMeters(above.Value.Radius)}, g={above.Value.GravityASL:F3})"
                : "Above: - (larger than largest stock body)";
        }

        private static double RecommendGravityASL(double radius, out StockBody? below, out StockBody? above)
        {
            below = null;
            above = null;
            if (radius <= 0) return 0;

            // Lerp from zero at R=0 if input is below the smallest stock body.
            if (radius < StockBodies[0].Radius)
            {
                above = StockBodies[0];
                return StockBodies[0].GravityASL * (radius / StockBodies[0].Radius);
            }

            // Clamp at the largest stock body.
            var last = StockBodies[StockBodies.Length - 1];
            if (radius >= last.Radius)
            {
                below = last;
                return last.GravityASL;
            }

            for (int i = 0; i < StockBodies.Length - 1; i++)
            {
                var lo = StockBodies[i];
                var hi = StockBodies[i + 1];
                if (radius >= lo.Radius && radius <= hi.Radius)
                {
                    below = lo;
                    above = hi;
                    if (hi.Radius == lo.Radius) return 0.5 * (lo.GravityASL + hi.GravityASL);
                    var t = (radius - lo.Radius) / (hi.Radius - lo.Radius);
                    return lo.GravityASL + t * (hi.GravityASL - lo.GravityASL);
                }
            }

            return 0;
        }

        private static string FormatMeters(double m)
        {
            if (m >= 1_000_000) return (m / 1000).ToString("N0", CultureInfo.InvariantCulture) + " km";
            if (m >= 1_000) return (m / 1000).ToString("0.###", CultureInfo.InvariantCulture) + " km";
            return m.ToString("N0", CultureInfo.InvariantCulture) + " m";
        }

        private void LoadPrefs()
        {
            _radius.SetValueWithoutNotify(GetDoublePref("Radius"));
        }

        private void SavePrefs()
        {
            SetDoublePref("Radius", _radius.value);
        }

        private static double GetDoublePref(string suffix) =>
            double.TryParse(EditorPrefs.GetString(PrefsPrefix + suffix, "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

        private static void SetDoublePref(string suffix, double v) =>
            EditorPrefs.SetString(PrefsPrefix + suffix, v.ToString("R", CultureInfo.InvariantCulture));
    }
}
