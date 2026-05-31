using Ksp2UnityTools.Editor.PartAuthoring.Windows;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring
{
    public sealed class Ksp1ImportReportWindow : EditorWindow
    {
        private const string USS_PATH = "/Assets/Windows/PartAuthoring/Windows/Ksp1ModConverterWindow.uss";

        private string _reportText = "";
        private TextField _reportField;

        public static void Open(string reportText, string title)
        {
            Ksp1ImportReportWindow window = GetWindow<Ksp1ImportReportWindow>();
            window.titleContent = new GUIContent(string.IsNullOrWhiteSpace(title) ? "KSP1 Import Report" : title);
            window.minSize = new Vector2(520f, 360f);
            window._reportText = reportText ?? "";
            window.Show();
            window.RefreshReportText();
        }

        private void CreateGUI()
        {
            Ksp2UnityToolsStyles.Apply(
                rootVisualElement,
                "/Assets/Windows/PartAuthoring/Inspectors/CorePartDataEditor.uss",
                USS_PATH
            );
            Refresh();
        }

        private void Refresh()
        {
            if (rootVisualElement == null)
            {
                return;
            }

            rootVisualElement.Clear();

            VisualElement root = new();
            root.AddToClassList("ksp1-report-window-root");
            rootVisualElement.Add(root);

            Label title = new("KSP1 Import Report");
            title.AddToClassList("part-inspector-section-label");
            title.AddToClassList("ksp1-converter-section-title");
            root.Add(title);

            _reportField = new TextField
            {
                multiline = true,
                isReadOnly = true,
                value = GetDisplayText()
            };
            _reportField.AddToClassList("ksp1-converter-full-report-field");
            _reportField.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

            ScrollView textScrollView = _reportField.Q<ScrollView>();
            if (textScrollView != null)
            {
                textScrollView.mode = ScrollViewMode.VerticalAndHorizontal;
                textScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
                textScrollView.horizontalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            }

            root.Add(_reportField);
        }

        private void RefreshReportText()
        {
            _reportField?.SetValueWithoutNotify(GetDisplayText());
        }

        private string GetDisplayText()
        {
            return string.IsNullOrWhiteSpace(_reportText) ? "No report text is available." : _reportText;
        }
    }
}
