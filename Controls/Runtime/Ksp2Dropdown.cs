using UnityEngine;
using UnityEngine.UIElements;

namespace UitkForKsp2.Controls
{
    [UxmlElement]
    public partial class Ksp2Dropdown : DropdownField
    {
        private VisualElement _dropdownBackground;
        private VisualElement _dropdownBorder;
        private TextElement _popUpTextElement;
        private VisualElement _dropdownArrows;
        
        public Ksp2Dropdown()
        {
            _dropdownBackground = CreateVisualElement("ksp2-dropdown-background");
            _dropdownBorder = CreateVisualElement("ksp2-dropdown-border");
            _popUpTextElement = textElement;
            _dropdownArrows = this.Q<VisualElement>(className: "unity-base-popup-field__arrow");
            BuildVisualTree();
        }
        
        private static VisualElement CreateVisualElement(params string[] classNames)
        {
            var container = new VisualElement();
            AddClasses(container, classNames);
            return container;
        }

        private static void AddClasses(VisualElement element, string[] classNames)
        {
            foreach (string className in classNames)
            {
                element.AddToClassList(className);
            }
        }

        private void BuildVisualTree()
        {
            Clear();
            AddToClassList("ksp2-dropdown");
            _dropdownBackground.AddToClassList("unity-base-field__input");
            _dropdownBackground.AddToClassList("unity-popup-field__input");
            hierarchy.Add(_dropdownBackground);
            _dropdownBackground.Add(_dropdownBorder);
            _dropdownBorder.Add(_popUpTextElement);
            if (_dropdownArrows != null)
            {
                _dropdownBorder.Add(_dropdownArrows);
            }
        }
    }
}
