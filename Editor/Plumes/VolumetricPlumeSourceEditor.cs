using System;
using KSP.VFX;
using KSP.Modules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ksp2UnityTools.Editor;
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
            var source = (VolumetricPlumeSource)target;
            var manager = source.GetComponentInParent<ThrottleVFXManager>(true);
            if (manager != null && manager.gameObject == source.gameObject)
            {
                root.Add(new HelpBox("Preview and appearance are available in the Throttle VFX Manager on this object.", HelpBoxMessageType.Info));
                return root;
            }
            root.Add(ThrottleVFXPreviewBridge.CreatePreviewControls(manager));
            if (manager != null)
                root.Add(new Button(() => Selection.activeObject = manager) { text = "Edit Low effects and engine bindings" });
            root.Add(CreateProfileControls(serializedObject));
            return root;
        }

        internal static VisualElement CreateProfileControls(SerializedObject settings)
        {
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                SDKConfiguration.BasePath + "/Assets/Windows/Plumes/VolumetricPlumeAuthoring.uxml").CloneTree();
            var source = (VolumetricPlumeSource)settings.targetObject;
            var authored = root.Q<VisualElement>("authored-profile");
            var fillPreset = root.Q<Button>("fill-preset");
            void UpdateProfileState(SerializedProperty property)
            {
                authored.SetEnabled(property.boolValue);
                fillPreset.text = property.boolValue
                    ? "Reset shared appearance to engine preset"
                    : "Customize from engine preset";
            }
            SerializedProperty custom = settings.FindProperty("_useAuthoredProfile");
            UpdateProfileState(custom);
            root.TrackPropertyValue(custom, UpdateProfileState);
            fillPreset.clicked += () => FillFromPreset(source, settings);
            root.Q<Button>("copy-profile").clicked += () => CopyOverrideJson(source);
            root.Q<Button>("paste-profile").clicked += () => PasteProfileJson(source, settings);
            root.RegisterCallback<SerializedPropertyChangeEvent>(_ => ThrottleVFXPreviewBridge.RefreshIfActive(source));
            // A manager inspector embeds this tree but binds a different component.
            root.RegisterCallback<AttachToPanelEvent>(_ => root.Bind(settings));
            var origin = root.Q<HelpBox>("profile-origin");
            void RefreshOrigin()
            {
                if (source == null)
                    return;
                var manager = source.GetComponentInParent<ThrottleVFXManager>(true);
                if (!ThrottleVFXPreviewBridge.IsPreviewing(manager))
                {
                    origin.text = "Medium and High share this appearance. Enable preview to inspect the active profile.";
                    return;
                }

                int mode = ThrottleVFXPreviewBridge.PreviewEngineMode;
                bool comparing = ThrottleVFXPreviewBridge.ComparingPreset;
                string basis = $"{source.EngineType} preset";
                if (source.UseAuthoredProfile && !comparing)
                {
                    settings.UpdateIfRequiredOrScript();
                    basis = mode < settings.FindProperty("_authoredModeProfiles").arraySize
                        ? $"operating-mode profile {mode}"
                        : "shared authored profile";
                }
                string overlay = VolumetricPlumeProfiles.HasOverride(GetPartName(source))
                    ? "Part JSON override applied last. It can override authored values."
                    : "No part JSON override registered in this editor.";
                origin.text = $"Medium / High base: {basis} (mode {mode}).\n{overlay}";
                if (comparing)
                    origin.text += "\nComparing the preset. Custom values are preserved.";
                if (source.UsesAuthoredJetAppearance)
                    origin.text += "\nNative jet layers also drive the appearance.";
            }
            RefreshOrigin();
            root.schedule.Execute(RefreshOrigin).Every(200);
            return root;
        }

        private static void FillFromPreset(VolumetricPlumeSource source, SerializedObject settings)
        {
            Undo.RecordObject(source, "Fill Volumetric Plume Profile From Preset");
            source.FillAuthoredProfileFromPreset();
            EditorUtility.SetDirty(source);
            PrefabUtility.RecordPrefabInstancePropertyModifications(source);
            settings.Update();
            ThrottleVFXPreviewBridge.RefreshIfActive(source);
        }

        private static string GetPartName(VolumetricPlumeSource source)
        {
            var module = source.GetComponentInParent<Module_Engine>(true);
            return module != null ? module.gameObject.name : source.transform.root.name;
        }

        private static void CopyOverrideJson(VolumetricPlumeSource source)
        {
            JObject json = JObject.FromObject(source.AuthoredProfile,
                JsonSerializer.Create(VolumetricPlumeJson.SerializerSettings));
            json.AddFirst(new JProperty("PartName", GetPartName(source)));
            EditorGUIUtility.systemCopyBuffer = json.ToString(Formatting.Indented);
        }

        private static void PasteProfileJson(VolumetricPlumeSource source, SerializedObject settings)
        {
            // Parse into a copy so an invalid field cannot leave a partially edited profile.
            VolumetricPlumeProfile profile = source.AuthoredProfile.Clone();
            try
            {
                JsonConvert.PopulateObject(EditorGUIUtility.systemCopyBuffer, profile,
                    VolumetricPlumeJson.SerializerSettings);
            }
            catch (Exception exception) when (exception is JsonException or ArgumentException)
            {
                UnityEngine.Debug.LogWarning($"The clipboard does not contain a volumetric plume profile: {exception.Message}");
                return;
            }

            profile.Clamp();
            settings.Update();
            Undo.SetCurrentGroupName("Paste Volumetric Plume Profile");
            settings.FindProperty("_authoredProfile").boxedValue = profile;
            settings.FindProperty("_useAuthoredProfile").boolValue = true;
            settings.ApplyModifiedProperties();
            ThrottleVFXPreviewBridge.RefreshIfActive(source);
        }
    }
}
