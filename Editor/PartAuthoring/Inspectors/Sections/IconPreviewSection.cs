using System;
using KSP;
using Ksp2UnityTools.Editor.PartAuthoring.Tools;
using Redux.Assets.PartIconRendering;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections
{
    /// <summary>
    /// UI Toolkit section that renders an interactive preview of a part's icon and lets the author
    /// tune the render settings before baking the PNG sidecar.
    /// </summary>
    /// <remarks>
    /// Owns a <see cref="PartIconRenderSettings" /> instance plus a preview <see cref="Texture2D" />.
    /// Every control updates the settings through a small bind helper and queues a debounced preview
    /// refresh. Save invokes <see cref="PartIconBaker" /> with the current settings; Reset rebuilds
    /// the settings from <see cref="PartIconRenderSettings.CreateDefault" />.
    /// </remarks>
    public sealed class IconPreviewSection : VisualElement
    {
        private const string UXML_PATH = "/Assets/Windows/PartAuthoring/Inspectors/Sections/IconPreviewSection.uxml";
        private const string USS_PATH = "/Assets/Windows/PartAuthoring/Inspectors/Sections/IconPreviewSection.uss";

        private readonly CorePartData _target;
        private PartIconRenderSettings _settings;
        private Texture2D _previewTexture;
        private VisualElement _previewImage;
        private bool _refreshQueued;

        /// <summary>
        /// Creates an icon preview section bound to <paramref name="target" />.
        /// </summary>
        /// <param name="target">The part whose icon is being previewed.</param>
        public IconPreviewSection(CorePartData target)
        {
            _target = target;
            _settings = PartIconRenderSettings.CreateDefault(target?.Core);

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UXML_PATH);
            if (tree == null)
            {
                Add(new Label("Failed to load IconPreviewSection.uxml"));
                return;
            }
            tree.CloneTree(this);

            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(SDKConfiguration.BasePath + USS_PATH);
            if (sheet != null)
            {
                styleSheets.Add(sheet);
            }

            _previewImage = this.Q<VisualElement>("icon-preview-image");

            WireControls();
            QueueRefresh();
            RegisterCallback<DetachFromPanelEvent>(_ => Cleanup());
        }

        private void WireControls()
        {
            var saveButton = this.Q<Button>("icon-save-button");
            if (saveButton != null)
            {
                saveButton.clicked += () => PartIconBaker.Bake(_target, _settings);
            }

            var resetButton = this.Q<Button>("icon-reset-button");
            if (resetButton != null)
            {
                resetButton.clicked += OnReset;
            }

            var presetField = this.Q<EnumField>("icon-preset-field");
            if (presetField != null)
            {
                presetField.Init(_settings.CameraPreset);
                presetField.RegisterValueChangedCallback(evt =>
                {
                    _settings.CameraPreset = (PartIconCameraPreset)evt.newValue;
                    SyncControlsFromSettings();
                    QueueRefresh();
                });
            }

            Bind<Slider,        float>  ("icon-padding-field",  () => _settings.cameraPadding,        v => _settings.cameraPadding = v);
            Bind<Vector3Field,  Vector3>("icon-rotation-field", () => _settings.partTransformRotation, v => _settings.partTransformRotation = v);

            Bind<Slider, float>("icon-yaw-field",                   () => _settings.cameraYawDegrees,        v => _settings.cameraYawDegrees = v);
            Bind<Slider, float>("icon-pitch-field",                 () => _settings.cameraPitchDegrees,      v => _settings.cameraPitchDegrees = v);
            Bind<Slider, float>("icon-roll-field",                  () => _settings.cameraOrbitDegrees,      v => _settings.cameraOrbitDegrees = v);
            Bind<Toggle, bool> ("icon-orthographic-override-field", () => _settings.overrideOrthographicSize, v => _settings.overrideOrthographicSize = v);
            Bind<Slider, float>("icon-orthographic-size-field",     () => _settings.cameraOrthographicSize,  v => _settings.cameraOrthographicSize = v);

            Bind<Slider,       float>  ("icon-front-key-intensity", () => _settings.frontKeyIntensity,     v => _settings.frontKeyIntensity = v);
            Bind<Vector3Field, Vector3>("icon-front-key-direction", () => _settings.frontKeyDirection,     v => _settings.frontKeyDirection = v);
            Bind<Slider,       float>  ("icon-top-key-intensity",   () => _settings.topKeyIntensity,       v => _settings.topKeyIntensity = v);
            Bind<Vector3Field, Vector3>("icon-top-key-direction",   () => _settings.topKeyDirection,       v => _settings.topKeyDirection = v);
            Bind<Slider,       float>  ("icon-rim-intensity",       () => _settings.rimIntensity,          v => _settings.rimIntensity = v);
            Bind<Vector3Field, Vector3>("icon-rim-direction",       () => _settings.rimDirection,          v => _settings.rimDirection = v);
            Bind<Slider,       float>  ("icon-fill-intensity",      () => _settings.fillIntensity,         v => _settings.fillIntensity = v);
            Bind<Slider,       float>  ("icon-key-spread",          () => _settings.keyLightSpreadDegrees, v => _settings.keyLightSpreadDegrees = v);

            Bind<Toggle,     bool> ("icon-palette-toggle", () => _settings.applyModuleColorPalette, v => _settings.applyModuleColorPalette = v);
            Bind<ColorField, Color>("icon-base-color",     () => _settings.moduleColorBase,         v => _settings.moduleColorBase = v);
            Bind<ColorField, Color>("icon-accent-color",   () => _settings.moduleColorAccent,       v => _settings.moduleColorAccent = v);

            Bind<Toggle,    bool>("icon-outline-toggle", () => _settings.addOutline,    v => _settings.addOutline = v);
            Bind<SliderInt, int> ("icon-outline-radius", () => _settings.outlineRadius, v => _settings.outlineRadius = v);
        }

        private void SyncControlsFromSettings()
        {
            SetField<Slider, float>      ("icon-padding-field",                _settings.cameraPadding);
            SetField<Vector3Field, Vector3>("icon-rotation-field",            _settings.partTransformRotation);
            SetField<Slider, float>      ("icon-yaw-field",                    _settings.cameraYawDegrees);
            SetField<Slider, float>      ("icon-pitch-field",                  _settings.cameraPitchDegrees);
            SetField<Slider, float>      ("icon-roll-field",                   _settings.cameraOrbitDegrees);
            SetField<Toggle, bool>       ("icon-orthographic-override-field",  _settings.overrideOrthographicSize);
            SetField<Slider, float>      ("icon-orthographic-size-field",      _settings.cameraOrthographicSize);
            SetField<Slider, float>      ("icon-front-key-intensity",          _settings.frontKeyIntensity);
            SetField<Vector3Field, Vector3>("icon-front-key-direction",        _settings.frontKeyDirection);
            SetField<Slider, float>      ("icon-top-key-intensity",            _settings.topKeyIntensity);
            SetField<Vector3Field, Vector3>("icon-top-key-direction",          _settings.topKeyDirection);
            SetField<Slider, float>      ("icon-rim-intensity",                _settings.rimIntensity);
            SetField<Vector3Field, Vector3>("icon-rim-direction",              _settings.rimDirection);
            SetField<Slider, float>      ("icon-fill-intensity",               _settings.fillIntensity);
            SetField<Slider, float>      ("icon-key-spread",                   _settings.keyLightSpreadDegrees);
            SetField<Toggle, bool>       ("icon-palette-toggle",               _settings.applyModuleColorPalette);
            SetField<ColorField, Color>  ("icon-base-color",                   _settings.moduleColorBase);
            SetField<ColorField, Color>  ("icon-accent-color",                 _settings.moduleColorAccent);
            SetField<Toggle, bool>       ("icon-outline-toggle",               _settings.addOutline);
            SetField<SliderInt, int>     ("icon-outline-radius",               _settings.outlineRadius);
        }

        private void SetField<TField, TValue>(string name, TValue value) where TField : BaseField<TValue>
        {
            var field = this.Q<TField>(name);
            field?.SetValueWithoutNotify(value);
        }

        private void Bind<TField, TValue>(string name, Func<TValue> getter, Action<TValue> setter) where TField : BaseField<TValue>
        {
            var field = this.Q<TField>(name);
            if (field == null) return;
            field.SetValueWithoutNotify(getter());
            field.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                QueueRefresh();
            });
        }

        private void OnReset()
        {
            DestroyPreview();
            _settings = PartIconRenderSettings.CreateDefault(_target?.Core);
            var presetField = this.Q<EnumField>("icon-preset-field");
            presetField?.SetValueWithoutNotify(_settings.CameraPreset);
            SyncControlsFromSettings();
            QueueRefresh();
        }

        private void QueueRefresh()
        {
            if (_refreshQueued)
            {
                return;
            }
            _refreshQueued = true;
            EditorApplication.delayCall -= ProcessRefresh;
            EditorApplication.delayCall += ProcessRefresh;
        }

        private void ProcessRefresh()
        {
            _refreshQueued = false;
            if (_target == null)
            {
                return;
            }
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            DestroyPreview();
            if (_target == null || _target.Core == null)
            {
                return;
            }

            string partName = !string.IsNullOrWhiteSpace(_target.Core.data?.partName)
                ? _target.Core.data.partName
                : _target.gameObject.name;
            var settings = _settings.Clone();
            settings.backgroundColor = new Color(0x1c / 255f, 0x1f / 255f, 0x27 / 255f, 1f);
            settings.supersampleScale = 2;
            _previewTexture = PartIconRenderer.RenderTexture2D(
                partName + "-editor-preview",
                _target.Core,
                _target.gameObject,
                settings
            );
            if (_previewTexture != null && _previewImage != null)
            {
                _previewImage.style.backgroundImage = new StyleBackground(_previewTexture);
            }
        }

        private void DestroyPreview()
        {
            if (_previewTexture == null)
            {
                return;
            }
            UnityEngine.Object.DestroyImmediate(_previewTexture);
            _previewTexture = null;
            if (_previewImage != null)
            {
                _previewImage.style.backgroundImage = new StyleBackground();
            }
        }

        private void Cleanup()
        {
            EditorApplication.delayCall -= ProcessRefresh;
            DestroyPreview();
        }
    }
}
