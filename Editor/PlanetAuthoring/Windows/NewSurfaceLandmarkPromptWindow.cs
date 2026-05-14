using System;
using System.IO;
using KSP;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Windows
{
    /// <summary>
    /// Modal popup for staging a new <see cref="SurfaceLandmark" />'s settings before activating
    /// the click-to-place tool.
    /// </summary>
    /// <remarks>
    /// The artist tweaks radii, fade strength, prefab, and the three Enable toggles in the dialog,
    /// then clicks Place to switch to the scene-view tool. The tool reads these staged values when
    /// the next left-click drops a fresh landmark.
    /// </remarks>
    public class NewSurfaceLandmarkPromptWindow : EditorWindow
    {
        private const string UxmlPath = "/Assets/Windows/PlanetAuthoring/Prompts/NewSurfaceLandmarkPromptWindow.uxml";

        /// <summary>
        /// The form payload returned to the caller's place callback.
        /// </summary>
        public class Result
        {
            /// <summary>Radius of the flat smoothing pad in meters.</summary>
            public float SmoothingRadius = 100f;
            /// <summary>Edge falloff strength of the smoothing pad. Range 0.25 to 2.</summary>
            public float SmoothingFadeStrength = 0.5f;
            /// <summary>Discoverable detection radius in meters.</summary>
            public float DiscoverableRadius = 100f;
            /// <summary>Whether the decal child is created on placement.</summary>
            public bool EnableDecal = true;
            /// <summary>Whether the decal applies smoothing-pad height behavior at placement.</summary>
            public bool EnableSmoothing = true;
            /// <summary>Optional PQSDecal template the placed landmark adopts. Null falls back to the SDK default smoothing pad.</summary>
            public PQSDecal SmoothingDecal;
            /// <summary>Whether the prefab spawner child is created on placement.</summary>
            public bool EnablePrefab = true;
            /// <summary>Whether the discoverable entry is added on placement.</summary>
            public bool EnableDiscoverable = true;
            /// <summary>Prefab the runtime PrefabSpawner will load. Must be addressable. Ignored when <see cref="UseRawAddressableKey" /> is on.</summary>
            public GameObject Prefab;
            /// <summary>Raw addressable key used in place of <see cref="Prefab" /> when <see cref="UseRawAddressableKey" /> is on.</summary>
            public string PrefabAddressableKey;
            /// <summary>When true, the placed landmark is configured to author the prefab as a raw addressable key string.</summary>
            public bool UseRawAddressableKey;
            /// <summary>Prefab footprint width in meters along the surface plane.</summary>
            public float PrefabWidth = 10f;
        }

        private readonly Result _result = new();
        private Action<Result> _onPlace;

        private Toggle _prefabRawKeyToggle;
        private VisualElement _prefabModeGroup;
        private VisualElement _prefabRawKeyModeGroup;
        private ObjectField _prefabField;
        private TextField _prefabRawKeyField;
        private FloatField _prefabWidthField;

        /// <summary>
        /// Opens the prompt window and invokes <paramref name="onPlace" /> when the user confirms.
        /// </summary>
        /// <param name="onPlace">Callback fired with the populated <see cref="Result" /> on confirm.</param>
        public static void Show(Action<Result> onPlace)
        {
            var win = CreateInstance<NewSurfaceLandmarkPromptWindow>();
            win.titleContent = new GUIContent("New Surface Landmark");
            win._onPlace = onPlace;
            win.minSize = new Vector2(380f, 320f);
            win.maxSize = new Vector2(520f, 420f);
            win.ShowUtility();
        }

        /// <inheritdoc />
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                root.Add(new Label("Failed to load NewSurfaceLandmarkPromptWindow.uxml"));
                return;
            }
            tree.CloneTree(root);

            Ksp2UnityToolsStyles.Apply(root);

            var smoothingRadius = root.Q<FloatField>("smoothing-radius-field");
            smoothingRadius.SetValueWithoutNotify(_result.SmoothingRadius);
            smoothingRadius.RegisterValueChangedCallback(evt => _result.SmoothingRadius = evt.newValue);

            var smoothingFade = root.Q<Slider>("smoothing-fade-field");
            smoothingFade.SetValueWithoutNotify(_result.SmoothingFadeStrength);
            smoothingFade.RegisterValueChangedCallback(evt => _result.SmoothingFadeStrength = evt.newValue);

            var discoverableRadius = root.Q<FloatField>("discoverable-radius-field");
            discoverableRadius.SetValueWithoutNotify(_result.DiscoverableRadius);
            discoverableRadius.RegisterValueChangedCallback(evt => _result.DiscoverableRadius = evt.newValue);

            var enableDecal = root.Q<Toggle>("enable-decal-toggle");
            var enableSmoothing = root.Q<Toggle>("enable-smoothing-toggle");
            var decalTemplate = root.Q<ObjectField>("decal-template-field");
            var createDecalButton = root.Q<Button>("create-decal-button");
            enableDecal.SetValueWithoutNotify(_result.EnableDecal);
            enableDecal.RegisterValueChangedCallback(evt =>
            {
                _result.EnableDecal = evt.newValue;
                enableSmoothing.SetEnabled(evt.newValue);
                decalTemplate.SetEnabled(evt.newValue);
                createDecalButton.SetEnabled(evt.newValue);
            });
            enableSmoothing.SetValueWithoutNotify(_result.EnableSmoothing);
            enableSmoothing.RegisterValueChangedCallback(evt => _result.EnableSmoothing = evt.newValue);
            enableSmoothing.SetEnabled(_result.EnableDecal);
            decalTemplate.SetValueWithoutNotify(_result.SmoothingDecal);
            decalTemplate.RegisterValueChangedCallback(evt => _result.SmoothingDecal = evt.newValue as PQSDecal);
            decalTemplate.SetEnabled(_result.EnableDecal);
            createDecalButton.SetEnabled(_result.EnableDecal);
            createDecalButton.clicked += () => OnCreateDecalClicked(decalTemplate);

            var enablePrefab = root.Q<Toggle>("enable-prefab-toggle");
            enablePrefab.SetValueWithoutNotify(_result.EnablePrefab);
            enablePrefab.RegisterValueChangedCallback(evt =>
            {
                _result.EnablePrefab = evt.newValue;
                SyncPrefabRowEnabled(evt.newValue);
            });

            var enableDiscoverable = root.Q<Toggle>("enable-discoverable-toggle");
            enableDiscoverable.SetValueWithoutNotify(_result.EnableDiscoverable);
            enableDiscoverable.RegisterValueChangedCallback(evt => _result.EnableDiscoverable = evt.newValue);

            _prefabRawKeyToggle = root.Q<Toggle>("prefab-raw-key-toggle");
            _prefabModeGroup = root.Q<VisualElement>("prefab-mode-group");
            _prefabRawKeyModeGroup = root.Q<VisualElement>("prefab-raw-key-mode-group");
            _prefabRawKeyToggle.SetValueWithoutNotify(_result.UseRawAddressableKey);
            ApplyPrefabAuthoringMode(_result.UseRawAddressableKey);
            _prefabRawKeyToggle.RegisterValueChangedCallback(evt =>
            {
                _result.UseRawAddressableKey = evt.newValue;
                ApplyPrefabAuthoringMode(evt.newValue);
            });

            _prefabField = root.Q<ObjectField>("prefab-field");
            _prefabField.SetValueWithoutNotify(_result.Prefab);
            _prefabField.RegisterValueChangedCallback(evt => _result.Prefab = evt.newValue as GameObject);

            _prefabRawKeyField = root.Q<TextField>("prefab-raw-key-field");
            _prefabRawKeyField.SetValueWithoutNotify(_result.PrefabAddressableKey ?? string.Empty);
            _prefabRawKeyField.RegisterValueChangedCallback(evt => _result.PrefabAddressableKey = evt.newValue ?? string.Empty);

            _prefabWidthField = root.Q<FloatField>("prefab-width-field");
            _prefabWidthField.SetValueWithoutNotify(_result.PrefabWidth);
            _prefabWidthField.RegisterValueChangedCallback(evt => _result.PrefabWidth = evt.newValue);

            SyncPrefabRowEnabled(_result.EnablePrefab);

            root.Q<Button>("cancel-button").clicked += Close;
            root.Q<Button>("place-button").clicked += () =>
            {
                _onPlace?.Invoke(_result);
                Close();
            };
        }

        private void SyncPrefabRowEnabled(bool enabled)
        {
            _prefabRawKeyToggle?.SetEnabled(enabled);
            _prefabField?.SetEnabled(enabled);
            _prefabRawKeyField?.SetEnabled(enabled);
            _prefabWidthField?.SetEnabled(enabled);
        }

        private void ApplyPrefabAuthoringMode(bool rawKey)
        {
            if (_prefabModeGroup != null)
            {
                _prefabModeGroup.style.display = rawKey ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (_prefabRawKeyModeGroup != null)
            {
                _prefabRawKeyModeGroup.style.display = rawKey ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void OnCreateDecalClicked(ObjectField decalTemplateField)
        {
            var session = PlanetAuthoringSession.Active;
            var body = BodyResolver.FindBody(session?.Pqs);
            var folder = ResolveBodyFolder(body);
            var defaultName = (body?.name ?? "Body") + "_Landmark_Decal";
            NewDecalPromptWindow.Show(defaultName, decalResult =>
            {
                var template = decalResult.ExistingTemplate != null
                    ? decalResult.ExistingTemplate
                    : CreatePqsDecalAsset.CreateConfigured(folder, decalResult);
                if (template == null) return;
                _result.SmoothingDecal = template;
                decalTemplateField.SetValueWithoutNotify(template);
            });
        }

        private static string ResolveBodyFolder(CoreCelestialBodyData body)
        {
            if (body == null) return "Assets";
            var scenePath = body.gameObject.scene.path;
            if (string.IsNullOrEmpty(scenePath)) return "Assets";
            var dir = Path.GetDirectoryName(scenePath)?.Replace('\\', '/');
            return !string.IsNullOrEmpty(dir) && AssetDatabase.IsValidFolder(dir) ? dir : "Assets";
        }
    }
}
