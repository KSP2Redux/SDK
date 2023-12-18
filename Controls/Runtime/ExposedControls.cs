using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace UitkForKsp2.Unity
{
    internal class BoxControl : Box
    {
        public new class UxmlFactory : UxmlFactory<BoxControl, UxmlTraits>
        {
        }

        public new class UxmlTraits : Box.UxmlTraits
        {
        }
    }

    internal class HelpBoxControl : HelpBox
    {
        public new class UxmlFactory : UxmlFactory<HelpBoxControl, UxmlTraits>
        {
        }

        public new class UxmlTraits : HelpBox.UxmlTraits
        {
        }
    }

    internal class RepeatButtonControl : RepeatButton
    {
        public new class UxmlFactory : UxmlFactory<RepeatButtonControl, UxmlTraits>
        {
        }

        public new class UxmlTraits : RepeatButton.UxmlTraits
        {
        }
    }

    internal class TwoPaneSplitViewControl : TwoPaneSplitView
    {
        public new class UxmlFactory : UxmlFactory<TwoPaneSplitViewControl, UxmlTraits>
        {
        }

        public new class UxmlTraits : TwoPaneSplitView.UxmlTraits
        {
        }
    }

    namespace zExperimental
    {
        internal class PopupWindowControl : PopupWindow
        {
            public new class UxmlFactory : UxmlFactory<PopupWindowControl, UxmlTraits>
            {
            }

            public new class UxmlTraits : PopupWindow.UxmlTraits
            {
            }
        }

        internal class ImageControl : Image
        {
            public new class UxmlFactory : UxmlFactory<ImageControl, UxmlTraits>
            {
            }

            public new class UxmlTraits : Image.UxmlTraits
            {
            }
        }
    }
}