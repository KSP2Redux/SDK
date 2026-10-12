using System;
using System.Collections.Generic;
using Ksp2UnityTools.Editor.PlanetAuthoring.Ocean;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Builds an inline editor for an ocean material, grouped by the sections its shader declares.
    /// </summary>
    /// <remarks>
    /// Built from the shader's own property list, since the ocean shader has over a hundred properties and the stand-in
    /// carries stock's headers. Ranges become sliders, vectors named as colours become colour pickers, and textures become
    /// object fields. Edits go through undo, and the fields follow undo and edits made elsewhere.
    /// </remarks>
    public static class OceanMaterialInspector
    {
        private const string HEADER_PREFIX = "Header(";

        /// <summary>
        /// Builds the editor for <paramref name="material" />.
        /// </summary>
        /// <param name="material">The material to edit.</param>
        /// <returns>The editor root.</returns>
        public static VisualElement Build(Material material)
        {
            var root = new VisualElement();
            Shader shader = material != null ? material.shader : null;
            if (shader == null)
            {
                root.Add(new Label("The material has no shader."));
                return root;
            }

            var refreshers = new List<Action>();
            root.Add(BuildPreset(material));
            VisualElement section = root;
            for (int index = 0; index < shader.GetPropertyCount(); index++)
            {
                if ((shader.GetPropertyFlags(index) & ShaderPropertyFlags.HideInInspector) != 0)
                    continue;

                string header = FindHeader(shader.GetPropertyAttributes(index));
                if (header != null)
                {
                    var foldout = new Foldout { text = header, value = false };
                    foldout.AddToClassList("sdk-subsection-foldout");
                    root.Add(foldout);
                    section = foldout;
                }

                VisualElement field = BuildField(material, shader, index, refreshers);
                if (field != null)
                {
                    field.AddToClassList("unity-base-field__aligned");
                    section.Add(field);
                }
            }

            // Undo and edits made in the material's own inspector land in its serialized data, which this tracks.
            root.TrackSerializedObjectValue(new SerializedObject(material), _ =>
            {
                foreach (Action refresh in refreshers)
                {
                    refresh();
                }
            });
            return root;
        }

        // The fields follow the material's serialized data, so they pick the copied values up on their own.
        private static VisualElement BuildPreset(Material material)
        {
            var foldout = new Foldout { text = "Start From", value = false };
            foldout.AddToClassList("sdk-subsection-foldout");
            var dropdown = new DropdownField("Stock Preset", new List<string>(OceanPresets.MATERIAL_BODIES), 0)
            {
                tooltip = "A stock body's water to copy. Applying it overwrites every value and keyword, but keeps this material's textures and shader, since stock's stay in the game. Undoable.",
            };
            dropdown.AddToClassList("unity-base-field__aligned");
            var status = new Label { style = { display = DisplayStyle.None } };
            status.AddToClassList("sdk-hint");
            var apply = new Button { text = "Apply Preset", tooltip = "Copy the selected stock body's water into this material. Undoable." };
            apply.AddToClassList("sdk-action-button");
            apply.clicked += () =>
            {
                OceanPresets.TryApplyMaterial(material, dropdown.value, out string message);
                status.text = message;
                status.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
                SceneView.RepaintAll();
            };
            foldout.Add(dropdown);
            foldout.Add(apply);
            foldout.Add(status);
            return foldout;
        }

        private static string FindHeader(string[] attributes)
        {
            foreach (string attribute in attributes)
            {
                if (attribute.StartsWith(HEADER_PREFIX) && attribute.EndsWith(")"))
                    return attribute.Substring(HEADER_PREFIX.Length, attribute.Length - HEADER_PREFIX.Length - 1);
            }

            return null;
        }

        private static VisualElement BuildField(Material material, Shader shader, int index, List<Action> refreshers)
        {
            string name = shader.GetPropertyName(index);
            string label = shader.GetPropertyDescription(index);
            switch (shader.GetPropertyType(index))
            {
                case ShaderPropertyType.Range:
                {
                    Vector2 limits = shader.GetPropertyRangeLimits(index);
                    var slider = new Slider(label, limits.x, limits.y) { showInputField = true };
                    return Wire(slider, material, name, () => material.GetFloat(name), value => material.SetFloat(name, value), refreshers);
                }
                case ShaderPropertyType.Float:
                    return Wire(new FloatField(label), material, name, () => material.GetFloat(name), value => material.SetFloat(name, value), refreshers);
                case ShaderPropertyType.Int:
                    return Wire(new IntegerField(label), material, name, () => material.GetInteger(name), value => material.SetInteger(name, value), refreshers);
                case ShaderPropertyType.Color:
                    return Wire(new ColorField(label) { hdr = true }, material, name, () => material.GetColor(name), value => material.SetColor(name, value), refreshers);
                case ShaderPropertyType.Vector:
                    // Stock declares its colours as vectors, so a name is the only sign of which ones are colours.
                    if (name.IndexOf("Color", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return Wire(new ColorField(label) { hdr = true }, material, name,
                            () => (Color)material.GetVector(name), value => material.SetVector(name, value), refreshers);
                    }

                    return Wire(new Vector4Field(label), material, name, () => material.GetVector(name), value => material.SetVector(name, value), refreshers);
                case ShaderPropertyType.Texture:
                    return Wire(new ObjectField(label) { objectType = typeof(Texture), allowSceneObjects = false }, material, name,
                        () => material.GetTexture(name), value => material.SetTexture(name, value as Texture), refreshers);
                default:
                    return null;
            }
        }

        private static VisualElement Wire<T>(
            BaseField<T> field,
            Material material,
            string propertyName,
            Func<T> read,
            Action<T> write,
            List<Action> refreshers
        )
        {
            field.tooltip = propertyName;
            field.SetValueWithoutNotify(read());
            field.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(material, $"Edit {field.label}");
                write(evt.newValue);
                EditorUtility.SetDirty(material);
                SceneView.RepaintAll();
            });
            refreshers.Add(() => field.SetValueWithoutNotify(read()));
            return field;
        }
    }
}
