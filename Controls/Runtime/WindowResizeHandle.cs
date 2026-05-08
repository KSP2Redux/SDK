using System;
using System.Reflection;
using UnityEngine.UIElements;

namespace UitkForKsp2.Controls
{
    [UxmlElement]
    public partial class WindowResizeHandle : VisualElement
    {
        private bool _isSetup;

        [UxmlAttribute("check-screen-bounds")]
        public bool CheckScreenBounds { get; set; } = true;

        [UxmlAttribute("min-width")]
        public float MinWidth { get; set; }

        [UxmlAttribute("min-height")]
        public float MinHeight { get; set; }

        [UxmlAttribute("target-name")]
        public string TargetName { get; set; } = string.Empty;

        public WindowResizeHandle()
        {
            name = "window-resize-handle";
            AddToClassList("window-resize-handle");
            var icon = new VisualElement
            {
                name = "window-resize-handle-icon",
                pickingMode = PickingMode.Ignore
            };
            icon.AddToClassList("window-resize-handle-icon");
            Add(icon);
            RegisterCallback<AttachToPanelEvent>(_ => schedule.Execute(Setup));
        }

        private void Setup()
        {
            if (_isSetup)
            {
                return;
            }

            VisualElement? window = FindTargetWindow();
            IManipulator? manipulator = CreateManipulator();
            if (window == null || manipulator == null)
            {
                return;
            }

            window.AddManipulator(manipulator);
            _isSetup = true;
        }

        private VisualElement? FindTargetWindow()
        {
            if (!string.IsNullOrWhiteSpace(TargetName))
            {
                return panel?.visualTree?.Q<VisualElement>(TargetName);
            }

            VisualElement? visualTree = panel?.visualTree;
            VisualElement? current = parent;
            VisualElement? candidate = null;
            while (current != null && current != visualTree)
            {
                candidate = current;
                current = current.parent;
            }

            return candidate ?? parent;
        }

        private IManipulator? CreateManipulator()
        {
            Type? optionsType = FindType("UitkForKsp2.API.ResizeOptions");
            Type? manipulatorType = FindType("UitkForKsp2.API.Manipulator.ResizeManipulator");
            if (optionsType == null || manipulatorType == null)
            {
                return null;
            }

            object options = Activator.CreateInstance(optionsType);
            SetProperty(options, "IsResizingEnabled", true);
            SetProperty(options, "CheckScreenBounds", CheckScreenBounds);
            SetProperty(options, "MinWidth", MinWidth);
            SetProperty(options, "MinHeight", MinHeight);

            return Activator.CreateInstance(manipulatorType, options, this) as IManipulator;
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            target.GetType()
                .GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                ?.SetValue(target, value);
        }

        private static Type? FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
