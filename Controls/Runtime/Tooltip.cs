using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UIElements;

namespace UitkForKsp2.Controls
{
    [PublicAPI]
    [UxmlElement]
    public partial class Tooltip : VisualElement
    {
        [UxmlAttribute("delay")]
        public int Delay { get; set; } = 500; // how long to wait before appearing

        [UxmlAttribute("fade-time")]
        public int FadeTime { get; set; } = 15; // speed at which it fades in and out (lower = faster)

        private Label label;
        private IVisualElementScheduledItem task;

        private const string ussClassName = "tooltip";
        private const string ussLabel = ussClassName + "__label";

        // ------------------------------------------------------------------------------------------------------------

        public Tooltip()
        {
            AddToClassList(ussClassName);
            pickingMode = PickingMode.Ignore;

            // label
            label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList(ussLabel);
            Add(label);

            style.position = Position.Absolute;
            style.visibility = Visibility.Hidden;
            style.opacity = 0f;
        }

        public virtual void Show(VisualElement target)
        {
            if (FadeTime > 0.0f)
            {
                task?.Pause();
                style.visibility = Visibility.Visible;
                style.opacity = 0f;
                task = schedule
                    .Execute(() => style.opacity = Mathf.Clamp01(resolvedStyle.opacity + 0.1f))
                    .Every(FadeTime) // ms
                    .Until(() => resolvedStyle.opacity >= 1.0f)
                    .StartingIn(Delay);
            }
            else
            {
                style.visibility = Visibility.Visible;
                style.opacity = 1f;
            }


            // check if there is position hint in tooltip
            char hint = 'B';
            string msg = target.tooltip;
            if (msg.Length > 2 && msg[1] == ':')
            {
                hint = target.tooltip[0] switch
                {
                    'B' => 'B',
                    'b' => 'B',
                    'T' => 'T',
                    't' => 'T',
                    'L' => 'L',
                    'l' => 'L',
                    'R' => 'R',
                    'r' => 'R',
                    _ => 'B'
                };

                msg = msg[2..];
            }

            label.text = msg;

            float top = 0f;
            float left = 0f;
            switch (hint)
            {
                case 'L': // left
                    left = target.worldBound.xMin - worldBound.width - 5;
                    top = target.worldBound.center.y - worldBound.height * 0.5f;
                    break;
                case 'R': // right
                    left = target.worldBound.xMax + 5;
                    top = target.worldBound.center.y - worldBound.height * 0.5f;
                    break;
                case 'T': //top
                    left = target.worldBound.center.x - worldBound.width * 0.5f;
                    top = target.worldBound.yMin - worldBound.height - 5;
                    break;
                default: // bottom
                    left = target.worldBound.center.x - worldBound.width * 0.5f;
                    top = target.worldBound.yMax + 5;
                    break;
            }

            if (left < parent.worldBound.xMin)
            {
                left = parent.worldBound.xMin;
            }

            if (left + worldBound.width > parent.worldBound.xMax)
            {
                left = parent.worldBound.xMax - worldBound.width;
            }

            if (top < parent.worldBound.yMin)
            {
                top = parent.worldBound.yMin;
            }

            if (top + worldBound.height > parent.worldBound.yMax)
            {
                top = parent.worldBound.yMax - worldBound.height;
            }

            style.left = left;
            style.top = top;
        }

        public virtual void Close()
        {
            if (FadeTime > 0.0f && resolvedStyle.visibility != Visibility.Hidden)
            {
                task?.Pause();
                task = schedule
                    .Execute(() =>
                        {
                            float o = Mathf.Clamp01(resolvedStyle.opacity - 0.1f);
                            style.opacity = o;
                            if (o <= 0.0f)
                            {
                                style.visibility = Visibility.Hidden;
                            }
                        }
                    )
                    .Every(FadeTime) // ms
                    .Until(() => resolvedStyle.opacity <= 0.0f);
            }
            else
            {
                style.visibility = Visibility.Hidden;
                style.opacity = 0f;
            }
        }

        // ============================================================================================================
    }
}