using UnityEngine;
using UnityEngine.UIElements;

namespace UitkForKsp2.Controls
{
    // This allows the element to be used directly in UXML
    [UxmlElement]
    public partial class PlaceholderTextField : TextField
    {
        private Label _placeholderLabel;
        private string _placeholderText;

        [UxmlAttribute("placeholder")]
        public string Placeholder
        {
            get => _placeholderText;
            set
            {
                _placeholderText = value;
                UpdatePlaceholder();
            }
        }

        public PlaceholderTextField()
        {
            // We need to wait until the hierarchy is built to find the input container
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<ChangeEvent<string>>(OnValueChanged);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            EnsurePlaceholderVisual();
            UpdatePlaceholderVisibility();
        }

        private void EnsurePlaceholderVisual()
        {
            if (_placeholderLabel != null) return;

            // Find the actual text input area inside the TextField
            VisualElement inputContainer = this.Q(className: "unity-base-text-field__input");
            if (inputContainer == null) return;

            _placeholderLabel = new Label(_placeholderText)
            {
                pickingMode = PickingMode.Ignore, // Let clicks pass through to the input
                style =
                {
                    position = Position.Absolute,
                    left = 5,
                    top = 2,
                    marginLeft = 0,
                    marginTop = 0,
                    paddingLeft = 0,
                    paddingTop = 0,
                    color = new Color(0.5f, 0.5f, 0.5f, 0.5f),
                    unityTextAlign = multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft,
                    whiteSpace = WhiteSpace.Normal
                }
            };

            inputContainer.Add(_placeholderLabel);
            inputContainer.RegisterCallback<GeometryChangedEvent>(_ => AlignPlaceholder(inputContainer));
        }

        /// <summary>
        /// Puts the placeholder where the typed text starts. The input's padding differs between themes and dense
        /// windows, and an absolute child ignores it, so the offsets are read from the resolved padding. A single line
        /// field centres the placeholder on its height, a multiline one keeps it at the top.
        /// </summary>
        private void AlignPlaceholder(VisualElement inputContainer)
        {
            IResolvedStyle padding = inputContainer.resolvedStyle;
            _placeholderLabel.style.left = padding.paddingLeft;
            _placeholderLabel.style.right = padding.paddingRight;
            _placeholderLabel.style.top = multiline ? padding.paddingTop : 0;
            _placeholderLabel.style.bottom = multiline ? StyleKeyword.Auto : 0;
            _placeholderLabel.style.unityTextAlign = multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
        }

        private void OnValueChanged(ChangeEvent<string> evt)
        {
            UpdatePlaceholderVisibility();
        }

        private void UpdatePlaceholder()
        {
            if (_placeholderLabel != null) _placeholderLabel.text = _placeholderText;
            UpdatePlaceholderVisibility();
        }

        private void UpdatePlaceholderVisibility()
        {
            if (_placeholderLabel == null) return;

            // Show if placeholder has text AND field is empty
            bool show = !string.IsNullOrEmpty(_placeholderText) && string.IsNullOrEmpty(value);
            _placeholderLabel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}