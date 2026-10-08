using Ksp2UnityTools.Editor.PlanetAuthoring.Ocean;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Builds the authoring UI for an <see cref="OceanWaveSpectrum" />, shown on the asset and inline in the PQS
    /// inspector's Ocean section.
    /// </summary>
    /// <remarks>
    /// Layout lives in <c>Assets/Windows/PlanetAuthoring/Inspectors/OceanWaveSpectrumInspector.uxml</c>. The octave
    /// powers, mutes, chop and per-octave speeds are hidden from Unity's default inspector, so this is the only place they
    /// can be edited.
    /// </remarks>
    public static class OceanWaveSpectrumInspector
    {
        private const string UXML_PATH = "/Assets/Windows/PlanetAuthoring/Inspectors/OceanWaveSpectrumInspector.uxml";
        private const float GRAPH_HEIGHT = 120f;

        /// <summary>
        /// Builds the spectrum's authoring UI bound to <paramref name="serializedSpectrum" />.
        /// </summary>
        /// <param name="serializedSpectrum">A serialized view of the spectrum to bind the fields to.</param>
        /// <returns>The inspector root.</returns>
        public static VisualElement Build(SerializedObject serializedSpectrum)
        {
            var root = new VisualElement();
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UXML_PATH);
            if (tree == null)
            {
                root.Add(new Label("Failed to load OceanWaveSpectrumInspector.uxml"));
                return root;
            }

            tree.CloneTree(root);
            Ksp2UnityToolsStyles.Apply(root);
            var spectrum = (OceanWaveSpectrum)serializedSpectrum.targetObject;
            BuildGraph(root.Q("spectrum-octave-graph"), serializedSpectrum);
            WireOctaveDetail(root, serializedSpectrum);
            WireModels(root, spectrum, serializedSpectrum);
            WirePreset(root, spectrum, serializedSpectrum);
            root.Bind(serializedSpectrum);
            return root;
        }

        private static void WirePreset(VisualElement root, OceanWaveSpectrum spectrum, SerializedObject serializedSpectrum)
        {
            var dropdown = root.Q<DropdownField>("spectrum-preset");
            var apply = root.Q<Button>("spectrum-apply-preset");
            var status = root.Q<Label>("spectrum-preset-status");
            if (dropdown == null || apply == null)
                return;

            dropdown.choices = new System.Collections.Generic.List<string>(OceanPresets.SPECTRUM_BODIES);
            dropdown.index = 0;
            SetStatus(status, string.Empty);
            apply.clicked += () =>
            {
                serializedSpectrum.ApplyModifiedProperties();
                OceanPresets.TryApplySpectrum(spectrum, dropdown.value, out string message);
                serializedSpectrum.Update();
                SetStatus(status, message);
            };
        }

        private static void SetStatus(Label status, string message)
        {
            if (status == null)
                return;

            status.text = message;
            status.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // One column per octave: its power as a vertical slider over a toggle that silences it.
        private static void BuildGraph(VisualElement graph, SerializedObject serializedSpectrum)
        {
            if (graph == null)
                return;

            graph.style.flexDirection = FlexDirection.Row;
            graph.style.justifyContent = Justify.SpaceBetween;
            SerializedProperty powers = serializedSpectrum.FindProperty("_powerLog");
            SerializedProperty disabled = serializedSpectrum.FindProperty("_powerDisabled");
            for (int octave = 0; octave < OceanWaveSpectrum.NUM_OCTAVES; octave++)
            {
                var column = new VisualElement { style = { flexGrow = 1f, alignItems = Align.Center } };
                string wavelength = DescribeWavelength(OceanWaveSpectrum.SmallWavelength(octave));
                var power = new Slider(OceanWaveSpectrum.MIN_POWER_LOG, OceanWaveSpectrum.MAX_POWER_LOG, SliderDirection.Vertical)
                {
                    style = { height = GRAPH_HEIGHT },
                    tooltip = $"Power of waves from {wavelength}, as a power of ten.",
                };
                if (powers != null && octave < powers.arraySize)
                {
                    power.BindProperty(powers.GetArrayElementAtIndex(octave));
                }

                var silenced = new Toggle { tooltip = "Silence this octave." };
                if (disabled != null && octave < disabled.arraySize)
                {
                    silenced.BindProperty(disabled.GetArrayElementAtIndex(octave));
                }

                column.Add(power);
                column.Add(silenced);
                column.Add(new Label(wavelength) { style = { fontSize = 8, unityTextAlign = TextAnchor.MiddleCenter } });
                graph.Add(column);
            }
        }

        private static void WireOctaveDetail(VisualElement root, SerializedObject serializedSpectrum)
        {
            var octave = root.Q<SliderInt>("spectrum-octave");
            var wavelength = root.Q<Label>("spectrum-octave-wavelength");
            var detail = root.Q("spectrum-octave-detail");
            if (octave == null || detail == null)
                return;

            void Show(int index)
            {
                detail.Unbind();
                detail.Clear();
                float shortest = OceanWaveSpectrum.SmallWavelength(index);
                if (wavelength != null)
                {
                    wavelength.text = $"Waves from {DescribeWavelength(shortest)} to {DescribeWavelength(shortest * 2f)}.";
                }

                AddArrayField(detail, serializedSpectrum, "_chopScales", index, "Chop",
                    "How far this octave's waves push the surface sideways into sharper crests.");
                AddArrayField(detail, serializedSpectrum, "_gravityScales", index, "Speed",
                    "Multiplier on how fast this octave's waves travel.");
            }

            octave.RegisterValueChangedCallback(evt => Show(evt.newValue));
            Show(octave.value);
        }

        private static void AddArrayField(VisualElement parent, SerializedObject serializedSpectrum, string arrayPath, int index, string label, string tooltip)
        {
            SerializedProperty array = serializedSpectrum.FindProperty(arrayPath);
            if (array == null || index >= array.arraySize)
                return;

            var field = new FloatField(label) { tooltip = tooltip };
            field.AddToClassList("unity-base-field__aligned");
            field.BindProperty(array.GetArrayElementAtIndex(index));
            parent.Add(field);
        }

        private static void WireModels(VisualElement root, OceanWaveSpectrum spectrum, SerializedObject serializedSpectrum)
        {
            void Apply(string undoName, System.Action<OceanWaveSpectrum> model)
            {
                serializedSpectrum.ApplyModifiedProperties();
                Undo.RecordObject(spectrum, undoName);
                model(spectrum);
                EditorUtility.SetDirty(spectrum);
                serializedSpectrum.Update();
            }

            root.Q<Button>("spectrum-apply-phillips")?.RegisterCallback<ClickEvent>(_ =>
                Apply("Apply Phillips Spectrum", s => s.ApplyPhillipsSpectrum(s._windSpeed, s._smallWavelengthMultiplier)));
            root.Q<Button>("spectrum-apply-pierson-moskowitz")?.RegisterCallback<ClickEvent>(_ =>
                Apply("Apply Pierson-Moskowitz Spectrum", s => s.ApplyPiersonMoskowitzSpectrum(s._windSpeed, s._smallWavelengthMultiplier)));
            root.Q<Button>("spectrum-apply-jonswap")?.RegisterCallback<ClickEvent>(_ =>
                Apply("Apply JONSWAP Spectrum", s => s.ApplyJONSWAPSpectrum(s._windSpeed, s._fetch, s._smallWavelengthMultiplier)));
        }

        private static string DescribeWavelength(float meters) =>
            meters < 1f ? $"{meters * 100f:0.#} cm" : $"{meters:0.#} m";
    }

    /// <summary>
    /// Shows <see cref="OceanWaveSpectrumInspector" /> on an <see cref="OceanWaveSpectrum" /> asset.
    /// </summary>
    [CustomEditor(typeof(OceanWaveSpectrum))]
    public class OceanWaveSpectrumEditor : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI() => OceanWaveSpectrumInspector.Build(serializedObject);
    }
}
