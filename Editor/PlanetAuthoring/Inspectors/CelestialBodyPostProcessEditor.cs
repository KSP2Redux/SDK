//// TODO: Post-processing
// using System.IO;
// using KSP.Rendering;
// using UnityEditor;
// using UnityEditor.UIElements;
// using UnityEngine;
// using UnityEngine.Rendering.PostProcessing;
// using UnityEngine.UIElements;
//
// namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
// {
//     /// <summary>
//     /// Custom inspector for <see cref="CelestialBodyPostProcess" />.
//     /// </summary>
//     /// <remarks>
//     /// Surfaces the bound <see cref="PostProcessData" /> ScriptableObject reference and inlines the
//     /// asset's fields directly under it so artists can edit altitude bounds, the profile, and
//     /// per-time-of-day auto-exposure without opening the asset. When the profile slot is empty a
//     /// Create button drops a new <see cref="PostProcessProfile" /> next to the data asset. When the
//     /// slot is filled the profile's stock editor is embedded under the field so effects can be
//     /// added and tuned in place. Field routing:
//     /// <list type="bullet">
//     ///   <item>Component-level: data (the PostProcessData reference).</item>
//     ///   <item>Inlined from PostProcessData under Altitudes + Profile: innerAltitude,
//     ///         outerAltitude, profile, plus the inline profile editor or Create button.</item>
//     ///   <item>Inlined from PostProcessData under Auto Exposure: autoExposureEnabled,
//     ///         autoExposureBlendMode, autoExposurePropertiesDay, autoExposurePropertiesSunset,
//     ///         autoExposurePropertiesNight.</item>
//     ///   <item>Hidden: none. The inlined section is rebuilt whenever the data reference changes
//     ///         and the inline profile section is rebuilt when the profile reference changes.</item>
//     /// </list>
//     /// Layout lives in <c>Assets/Windows/PlanetAuthoring/Inspectors/CelestialBodyPostProcessInspector.uxml</c>.
//     /// </remarks>
//     [CustomEditor(typeof(CelestialBodyPostProcess))]
//     public class CelestialBodyPostProcessEditor : UnityEditor.Editor
//     {
//         private const string UxmlPath = "/Assets/Windows/PlanetAuthoring/Inspectors/CelestialBodyPostProcessInspector.uxml";
//
//         private VisualElement _dataSlot;
//         private VisualElement _profileInlineSlot;
//         private PostProcessData _boundData;
//         private PostProcessProfile _boundProfile;
//         private UnityEditor.Editor _embeddedProfileEditor;
//         private SerializedObject _dataSO;
//
//         /// <inheritdoc />
//         public override VisualElement CreateInspectorGUI()
//         {
//             var root = new VisualElement();
//
//             var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
//             if (tree == null)
//             {
//                 root.Add(new Label("Failed to load CelestialBodyPostProcessInspector.uxml"));
//                 return root;
//             }
//             tree.CloneTree(root);
//
//             Ksp2UnityToolsStyles.Apply(root);
//
//             _dataSlot = root.Q<VisualElement>("post-process-data-slot");
//             RebuildDataSlot();
//
//             // Rebuild the inlined section only when the user drops a different PostProcessData into
//             // the field above, cheaper than polling every 500ms.
//             var dataProp = serializedObject.FindProperty("data");
//             if (dataProp != null && _dataSlot != null)
//             {
//                 var tracker = new VisualElement();
//                 tracker.TrackPropertyValue(dataProp, _ => RebuildDataSlot());
//                 _dataSlot.Add(tracker);
//             }
//
//             root.Bind(serializedObject);
//             return root;
//         }
//
//         private void OnDisable()
//         {
//             DisposeEmbeddedProfileEditor();
//         }
//
//         private void RebuildDataSlot()
//         {
//             if (_dataSlot == null)
//                 return;
//
//             var component = (CelestialBodyPostProcess)target;
//             var data = component != null ? component.Data : null;
//             if (data == _boundData)
//                 return;
//
//             _boundData = data;
//             _dataSlot.Clear();
//             _profileInlineSlot = null;
//             DisposeEmbeddedProfileEditor();
//             _boundProfile = null;
//             _dataSO = null;
//
//             if (data == null)
//             {
//                 _dataSlot.Add(new HelpBox(
//                     "No PostProcessData assigned. Assign an asset above to edit altitude bounds and auto-exposure here.",
//                     HelpBoxMessageType.Info
//                 ));
//                 return;
//             }
//
//             _dataSO = new SerializedObject(data);
//
//             var altitudesAndProfile = new Foldout { text = "Altitudes + Profile", value = true };
//             altitudesAndProfile.AddToClassList("body-inspector-section");
//             altitudesAndProfile.Add(BindField(_dataSO, "innerAltitude", "Inner Altitude (m)",
//                 "Altitude (m) at which the post-process volume is fully active. Below this the profile blends to 1."));
//             altitudesAndProfile.Add(BindField(_dataSO, "outerAltitude", "Outer Altitude (m)",
//                 "Altitude (m) at which the post-process volume fades out. Above this the body's profile no longer contributes."));
//             altitudesAndProfile.Add(BindField(_dataSO, "profile", "Post-Process Profile",
//                 "Post-processing profile blended into the camera stack while the camera is between Inner and Outer altitudes."));
//
//             _profileInlineSlot = new VisualElement();
//             altitudesAndProfile.Add(_profileInlineSlot);
//             RebuildProfileInline();
//
//             // Tracker lives on the foldout, NOT the slot we clear inside RebuildProfileInline.
//             var profileProp = _dataSO.FindProperty("profile");
//             if (profileProp != null)
//             {
//                 var tracker = new VisualElement();
//                 tracker.TrackPropertyValue(profileProp, _ => RebuildProfileInline());
//                 altitudesAndProfile.Add(tracker);
//             }
//
//             _dataSlot.Add(altitudesAndProfile);
//
//             var autoExposure = new Foldout { text = "Auto Exposure", value = true };
//             autoExposure.AddToClassList("body-inspector-section");
//             autoExposure.Add(BindField(_dataSO, "autoExposureEnabled", "Enabled",
//                 "Drive auto-exposure with the per-time-of-day properties below."));
//             autoExposure.Add(BindField(_dataSO, "autoExposureBlendMode", "Blend Mode",
//                 "How Day/Sunset/Night auto-exposure properties blend across the day/night terminator."));
//             autoExposure.Add(BindField(_dataSO, "autoExposurePropertiesDay", "Day",
//                 "Auto-exposure properties applied when the sun is well above the horizon."));
//             autoExposure.Add(BindField(_dataSO, "autoExposurePropertiesSunset", "Sunset",
//                 "Auto-exposure properties applied near the day/night terminator. Used only when Blend Mode is set to a three-point blend."));
//             autoExposure.Add(BindField(_dataSO, "autoExposurePropertiesNight", "Night",
//                 "Auto-exposure properties applied when the sun is well below the horizon."));
//             _dataSlot.Add(autoExposure);
//         }
//
//         private void RebuildProfileInline()
//         {
//             if (_profileInlineSlot == null || _boundData == null)
//                 return;
//
//             _profileInlineSlot.Clear();
//             DisposeEmbeddedProfileEditor();
//             _boundProfile = _boundData.profile;
//
//             if (_boundProfile == null)
//             {
//                 _profileInlineSlot.Add(new HelpBox(
//                     "No Post-Process Profile assigned. Drop one into the field above or click Create to make a new asset next to this PostProcessData.",
//                     HelpBoxMessageType.Info
//                 ));
//                 _profileInlineSlot.Add(new Button(CreateProfileAsset) { text = "Create Post-Process Profile" });
//                 return;
//             }
//
//             _embeddedProfileEditor = UnityEditor.Editor.CreateEditor(_boundProfile);
//             if (_embeddedProfileEditor == null)
//                 return;
//
//             var inlineFoldout = new Foldout { text = "Profile Contents", value = true };
//             inlineFoldout.AddToClassList("body-inspector-section");
//             inlineFoldout.Add(new IMGUIContainer(DrawEmbeddedProfileEditor));
//             _profileInlineSlot.Add(inlineFoldout);
//         }
//
//         private void DrawEmbeddedProfileEditor()
//         {
//             if (_embeddedProfileEditor == null || _embeddedProfileEditor.target == null)
//                 return;
//             _embeddedProfileEditor.OnInspectorGUI();
//         }
//
//         private void DisposeEmbeddedProfileEditor()
//         {
//             if (_embeddedProfileEditor != null)
//             {
//                 Object.DestroyImmediate(_embeddedProfileEditor);
//                 _embeddedProfileEditor = null;
//             }
//         }
//
//         private void CreateProfileAsset()
//         {
//             if (_boundData == null || _dataSO == null)
//                 return;
//
//             var dataAssetPath = AssetDatabase.GetAssetPath(_boundData);
//             if (string.IsNullOrEmpty(dataAssetPath))
//             {
//                 Debug.LogError("[CelestialBodyPostProcess] Cannot create profile next to an unsaved PostProcessData asset. Save the data asset first.");
//                 return;
//             }
//
//             var directory = Path.GetDirectoryName(dataAssetPath)?.Replace('\\', '/');
//             if (string.IsNullOrEmpty(directory))
//                 return;
//
//             var profile = ScriptableObject.CreateInstance<PostProcessProfile>();
//             var path = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{_boundData.name}_Profile.asset");
//             AssetDatabase.CreateAsset(profile, path);
//             AssetDatabase.SaveAssets();
//
//             // Route the assignment through the SerializedObject so TrackPropertyValue notices and
//             // RebuildProfileInline runs. Direct field assignment would not trigger the tracker.
//             _dataSO.Update();
//             _dataSO.FindProperty("profile").objectReferenceValue = profile;
//             _dataSO.ApplyModifiedProperties();
//         }
//
//         private static PropertyField BindField(SerializedObject so, string path, string label, string tooltip)
//         {
//             var prop = so.FindProperty(path);
//             var field = new PropertyField(prop, label) { tooltip = tooltip };
//             if (prop != null)
//                 field.BindProperty(prop);
//             return field;
//         }
//     }
// }
