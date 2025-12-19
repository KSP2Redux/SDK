using System;
using UnityEngine.UIElements;

namespace UitkForKsp2.Controls
{
    public class OabButton : Button
    {
        private readonly VisualElement _innerContainer;
        private readonly Label _textLabel;

        public override string text
        {
            get => _textLabel.text;
            set => _textLabel.text = value;
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

        public new class UxmlFactory : UxmlFactory<OabButton, UxmlTraits>
        {
        }

        public new class UxmlTraits : Button.UxmlTraits
        {
            private readonly UxmlStringAttributeDescription _uxmlText = new() { name = "text", defaultValue = "" };

            public override void Init(VisualElement element, IUxmlAttributes bag, CreationContext context)
            {
                base.Init(element, bag, context);

                if (element is OabButton btn)
                {
                    string parsed = _uxmlText.GetValueFromBag(bag, context);
                    if (!string.IsNullOrEmpty(parsed))
                    {
                        btn.text = parsed;
                    }
                }
            }
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