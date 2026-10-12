using System;
using KSP.VFX;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Redux.VFX.Plume.Volumetric;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace KSP.Editor
{
    /// <summary>
    /// Inspector for <see cref="VolumetricPlumeSource" /> with profile tools and the parent engine effects preview.
    /// </summary>
    [CustomEditor(typeof(VolumetricPlumeSource))]
    public class VolumetricPlumeSourceEditor : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            var tools = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
            tools.Add(new Button(FillFromPreset)
            {
                text = "Fill From Preset",
                tooltip = "Replace the authored profile with the preset for the preview engine type."
            });
            tools.Add(new Button(CopyOverrideJson)
            {
                text = "Copy Override JSON",
                tooltip = "Copy the authored profile as a JSON override for a plugin's assets/volumetric-plumes folder."
            });
            tools.Add(new Button(PasteProfileJson)
            {
                text = "Paste Profile JSON",
                tooltip = "Apply profile values from JSON on the clipboard to the authored profile."
            });
            root.Add(tools);

            var source = (VolumetricPlumeSource)target;
            var manager = source.GetComponentInParent<ThrottleVFXManager>();
            root.Add(new Label("Preview (Parent Manager)")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10 }
            });
            if (manager == null)
            {
                root.Add(new HelpBox(
                    "No parent ThrottleVFXManager found. Place this component under an engine's ThrottleVFXManager " +
                    "and press Gather Effects From Children to preview the exhaust.",
                    HelpBoxMessageType.Info));
                return root;
            }

            var managerRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            managerRow.Add(new Button(() => Selection.activeObject = manager) { text = "Select Manager" });
            managerRow.Add(new Button(() => ThrottleVFXPreviewBridge.ActivateAndRefresh(manager))
            {
                text = "Focus Preview on Manager"
            });
            root.Add(managerRow);
            // The shared engine effects preview is an IMGUI panel. Hosting it
            // keeps one set of throttle and atmosphere controls for every effect.
            root.Add(new IMGUIContainer(() => ThrottleVFXPreviewBridge.DrawPreviewControls(manager, false)));
            return root;
        }

        private void FillFromPreset()
        {
            var source = (VolumetricPlumeSource)target;
            Undo.RecordObject(source, "Fill Volumetric Plume Profile From Preset");
            source.FillAuthoredProfileFromPreset();
            EditorUtility.SetDirty(source);
            serializedObject.Update();
        }

        private void CopyOverrideJson()
        {
            var source = (VolumetricPlumeSource)target;
            JObject json = JObject.FromObject(source.AuthoredProfile,
                JsonSerializer.Create(VolumetricPlumeJson.SerializerSettings));
            json.AddFirst(new JProperty("PartName", source.transform.root.name));
            EditorGUIUtility.systemCopyBuffer = json.ToString(Formatting.Indented);
        }

        private void PasteProfileJson()
        {
            var source = (VolumetricPlumeSource)target;
            Undo.RecordObject(source, "Paste Volumetric Plume Profile");
            try
            {
                JsonConvert.PopulateObject(EditorGUIUtility.systemCopyBuffer, source.AuthoredProfile,
                    VolumetricPlumeJson.SerializerSettings);
            }
            catch (Exception exception) when (exception is JsonException or ArgumentException)
            {
                UnityEngine.Debug.LogWarning($"The clipboard does not contain a volumetric plume profile: {exception.Message}");
                return;
            }

            source.AuthoredProfile.Clamp();
            source.UseAuthoredProfile = true;
            EditorUtility.SetDirty(source);
            serializedObject.Update();
        }
    }
}
