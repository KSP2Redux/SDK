using KSP.VFX;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace KSP.Editor
{
    /// <summary>
    /// Provides an all-nozzle preview for RCS effects in the inspector.
    /// </summary>
    [CustomEditor(typeof(RCSVFXManager))]
    public class RCSVFXManagerEditor : UnityEditor.Editor
    {
        private readonly RCSVFXPreviewBridge _preview = new();
        private Toggle _previewToggle;

        private void OnEnable()
        {
            _preview.Initialize((RCSVFXManager)target);
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            var preview = new VisualElement();
            preview.style.marginTop = 8;

            var heading = new Label("Editor Preview");
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            preview.Add(heading);

            _previewToggle = new Toggle("Enable Preview");
            _previewToggle.SetValueWithoutNotify(_preview.IsPreviewEnabled);
            _previewToggle.SetEnabled(_preview.CanPreview);
            _previewToggle.RegisterValueChangedCallback(change =>
            {
                _preview.SetPreview(change.newValue);
            });
            preview.Add(_previewToggle);

            root.Add(preview);
            return root;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            _preview.Release();
            _previewToggle = null;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                _preview.Initialize((RCSVFXManager)target);

            if (_previewToggle == null)
                return;

            _previewToggle.SetValueWithoutNotify(
                state != PlayModeStateChange.ExitingEditMode && _preview.IsPreviewEnabled
            );
            _previewToggle.SetEnabled(_preview.CanPreview);
        }
    }
}
