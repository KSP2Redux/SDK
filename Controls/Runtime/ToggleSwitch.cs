using UnityEngine;
using UnityEngine.UIElements;

namespace UitkForKsp2.Controls
{
    [UxmlElement]
    public partial class ToggleSwitch : Toggle
    {
        private VisualElement _backgroundOuter;
        private VisualElement _borderOuter;
        private VisualElement _backgroundInner;
        private VisualElement _backgroundPuck;
        private VisualElement _borderPuck;
        
        public ToggleSwitch()
        {
            _backgroundOuter = CreateVisualElement("toggle-switch-background-outer");
            _borderOuter = CreateVisualElement("toggle-switch-border-outer");
            _backgroundInner = CreateVisualElement("toggle-switch-background-inner");
            _backgroundPuck = CreateVisualElement("toggle-switch-background-puck");
            _borderPuck = CreateVisualElement("toggle-switch-border-puck");
            BuildVisualTree();
        }
        
        private static VisualElement CreateVisualElement(string className)
        {
            var container = new VisualElement();
            container.AddToClassList(className);
            return container;
        }

        private void BuildVisualTree()
        {
            Clear();
            AddToClassList("switch");
            hierarchy.Add(_backgroundOuter);
            _backgroundOuter.Add(_borderOuter);
            _borderOuter.Add(_backgroundInner);
            _borderOuter.Add(_backgroundPuck);
            _backgroundPuck.Add(_borderPuck);
        }
    }
}
