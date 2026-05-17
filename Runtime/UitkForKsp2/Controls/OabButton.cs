using System;
using UnityEngine.UIElements;

namespace UitkForKsp2.Controls
{
    [UxmlElement]
    public partial class OabButton : Button
    {
        private readonly VisualElement _innerContainer;
        private readonly Label _textLabel;

        public override string text
        {
            get => _textLabel != null ? _textLabel.text : base.text;
            set
            {
                if (_textLabel != null)
                {
                    _textLabel.text = value;
                }
                else
                {
                    base.text = value;
                }
            }
        }

        public override VisualElement contentContainer => _innerContainer;

        public OabButton()
        {
            _innerContainer = CreateInnerContainer();
            _textLabel = CreateTextLabel();
            BuildVisualTree();
        }

        public OabButton(Action clickEvent) : base(clickEvent)
        {
            _innerContainer = CreateInnerContainer();
            _textLabel = CreateTextLabel();
            BuildVisualTree();
        }

        private static VisualElement CreateInnerContainer()
        {
            var container = new VisualElement();
            container.AddToClassList("oab-button__inner");
            return container;
        }

        /// <summary>
        /// Creates the default Label (for text) and assigns a USS class/name.
        /// </summary>
        private static Label CreateTextLabel()
        {
            var label = new Label();
            label.AddToClassList("oab-button__label");
            return label;
        }

        private void BuildVisualTree()
        {
            Clear();
            AddToClassList("oab-button");
            hierarchy.Add(_innerContainer);
            _innerContainer.Add(_textLabel);
        }
    }
}