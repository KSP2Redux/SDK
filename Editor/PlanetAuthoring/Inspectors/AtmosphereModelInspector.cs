using System.Collections.Generic;
using System.IO;
using KSP.Rendering;
using Ksp2UnityTools.Editor.PlanetAuthoring.Atmosphere;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Builds the authoring UI for a Bruneton <see cref="AtmosphereModel" />.
    /// </summary>
    /// <remarks>
    /// Shared by the model asset's own inspector and the scaled body's atmosphere component, which
    /// shows its model inline. Layout lives in
    /// <c>Assets/Windows/PlanetAuthoring/Inspectors/AtmosphereModelInspector.uxml</c>. A running
    /// preview picks edits up by itself, rebaking its tables once a table input settles.
    /// </remarks>
    public static class AtmosphereModelInspector
    {
        private const string UXML_PATH = "/Assets/Windows/PlanetAuthoring/Inspectors/AtmosphereModelInspector.uxml";
        private const long STATUS_REFRESH_MS = 500;

        /// <summary>
        /// Builds the model's authoring UI bound to <paramref name="serializedModel" />.
        /// </summary>
        /// <param name="model">The model being edited.</param>
        /// <param name="serializedModel">A serialized view of <paramref name="model" /> to bind the fields to.</param>
        /// <returns>The inspector root.</returns>
        public static VisualElement Build(AtmosphereModel model, SerializedObject serializedModel)
        {
            var root = new VisualElement();
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UXML_PATH);
            if (tree == null)
            {
                root.Add(new Label("Failed to load AtmosphereModelInspector.uxml"));
                return root;
            }

            tree.CloneTree(root);
            Ksp2UnityToolsStyles.Apply(root);

            // Set by Add or Refit Atmosphere from the body, and the tables only change by baking.
            foreach (string readOnly in new[]
                     {
                         "atmosphere-bottom-radius",
                         "atmosphere-lut-transmittance",
                         "atmosphere-lut-irradiance",
                         "atmosphere-lut-scattering",
                     })
            {
                root.Q(readOnly)?.SetEnabled(false);
            }

            WirePresets(root, model, serializedModel);
            WireBake(root, model);
            BindColor(root, "atmosphere-rayleigh-color", serializedModel, "RayleighScattering");
            BindColor(root, "atmosphere-mie-color", serializedModel, "MieScattering");
            BindColor(root, "atmosphere-absorption-color", serializedModel, "Absorption");
            BindColor(root, "atmosphere-sunlight-color", serializedModel, "SolarIrradiance");

            var status = root.Q<Label>("atmosphere-lut-status");
            RefreshTableStatus(status, model);
            root.schedule.Execute(() => RefreshTableStatus(status, model)).Every(STATUS_REFRESH_MS);

            root.Bind(serializedModel);
            return root;
        }

        private static void WirePresets(VisualElement root, AtmosphereModel model, SerializedObject serializedModel)
        {
            var dropdown = root.Q<DropdownField>("atmosphere-preset");
            var apply = root.Q<Button>("atmosphere-apply-preset");
            if (dropdown == null || apply == null)
                return;

            var names = new List<string>();
            foreach (AtmospherePreset preset in AtmospherePreset.Stock)
            {
                names.Add($"{preset.Name} ({preset.BottomRadius:0} km radius)");
            }

            dropdown.choices = names;
            dropdown.index = 0;
            apply.clicked += () =>
            {
                if (dropdown.index < 0 || dropdown.index >= AtmospherePreset.Stock.Count)
                    return;

                Undo.RecordObject(model, "Apply Atmosphere Preset");
                AtmospherePreset.Stock[dropdown.index].ApplyTo(model);
                EditorUtility.SetDirty(model);
                serializedModel.Update();
            };
        }

        // The model keeps these per-channel ratios as Vector3, which a ColorField cannot bind to
        // directly. Edits go through the SerializedObject so they record undo, and the tracker keeps
        // the picker in step with presets, undo and edits made elsewhere.
        private static void BindColor(VisualElement root, string fieldName, SerializedObject serializedModel, string propertyPath)
        {
            var field = root.Q<ColorField>(fieldName);
            SerializedProperty property = serializedModel.FindProperty(propertyPath);
            if (field == null || property == null)
                return;

            field.SetValueWithoutNotify(ToColor(property.vector3Value));
            field.RegisterValueChangedCallback(evt =>
            {
                serializedModel.Update();
                property.vector3Value = new Vector3(evt.newValue.r, evt.newValue.g, evt.newValue.b);
                serializedModel.ApplyModifiedProperties();
            });
            field.TrackPropertyValue(property, changed => field.SetValueWithoutNotify(ToColor(changed.vector3Value)));
        }

        private static Color ToColor(Vector3 ratios) => new(ratios.x, ratios.y, ratios.z, 1f);

        private static void WireBake(VisualElement root, AtmosphereModel model)
        {
            var bake = root.Q<Button>("atmosphere-bake-luts");
            if (bake == null)
                return;

            bake.clicked += () =>
            {
                string modelPath = AssetDatabase.GetAssetPath(model);
                string folder = Path.GetDirectoryName(modelPath)?.Replace('\\', '/');
                var status = root.Q<Label>("atmosphere-lut-status");
                if (string.IsNullOrEmpty(folder))
                {
                    SetStatus(status, "Save the model as an asset before baking.");
                    return;
                }

                if (!BrunetonLutBaker.Bake(model, folder, out string error))
                {
                    SetStatus(status, $"Bake failed: {error}");
                    return;
                }

                RefreshTableStatus(status, model);
            };
        }

        private static void RefreshTableStatus(Label status, AtmosphereModel model)
        {
            if (status == null || model == null)
                return;

            SetStatus(status, AtmosphereSetup.GetTableState(model) switch
            {
                AtmosphereTableState.Missing => "No baked tables. The game renders nothing until Bake LUTs runs.",
                AtmosphereTableState.Stale => "Baked tables are out of date with the settings above. Bake LUTs before shipping.",
                _ => "Baked tables match the settings above.",
            });
        }

        private static void SetStatus(Label status, string text)
        {
            if (status != null)
            {
                status.text = text;
            }
        }
    }
}
