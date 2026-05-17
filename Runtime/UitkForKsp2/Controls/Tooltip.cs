using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UIElements;

namespace UitkForKsp2.Controls
{
    [PublicAPI]
    [UxmlElement]
    public partial class Tooltip : VisualElement
    {
        private const int FadeStepMilliseconds = 16;
        private const int FadeTimeSteps = 10;

        [UxmlAttribute("delay")]
        public int Delay { get; set; } = 500; // how long to wait before appearing

        [UxmlAttribute("fade-time")]
        public int FadeTime
        {
            get => _fadeTime;
            set
            {
                _fadeTime = Mathf.Max(0, value);
                int duration = _fadeTime * FadeTimeSteps;
                _fadeInDuration = duration;
                _fadeOutDuration = duration;
            }
        }

        [UxmlAttribute("fade-in-duration")]
        public int FadeInDuration
        {
            get => _fadeInDuration;
            set => _fadeInDuration = Mathf.Max(0, value);
        }

        [UxmlAttribute("fade-out-duration")]
        public int FadeOutDuration
        {
            get => _fadeOutDuration;
            set => _fadeOutDuration = Mathf.Max(0, value);
        }

        private Label label;
        private IVisualElementScheduledItem task;
        private VisualElement currentTarget;
        private int transitionVersion;
        private int currentFadeOutDuration = 150;
        private int _fadeTime = 15;
        private int _fadeInDuration = 150;
        private int _fadeOutDuration = 150;

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
            Show(target, Delay, FadeInDuration, FadeOutDuration);
        }

        public virtual void Show(VisualElement target, int delay, int fadeInDuration, int fadeOutDuration)
        {
            ParseTooltip(target, out char hint, out string msg);
            Show(target, msg, hint, delay, fadeInDuration, fadeOutDuration);
        }

        public virtual void Show(
            VisualElement target,
            string msg,
            char hint,
            int delay,
            int fadeInDuration,
            int fadeOutDuration
        )
        {
            task?.Pause();
            currentTarget = target;
            int version = ++transitionVersion;

            label.text = msg;
            currentFadeOutDuration = Mathf.Max(0, fadeOutDuration);
            style.visibility = Visibility.Hidden;
            style.opacity = 0f;

            int clampedDelay = Mathf.Max(0, delay);
            int clampedFadeInDuration = Mathf.Max(0, fadeInDuration);
            if (clampedDelay > 0)
            {
                task = schedule.Execute(() => BeginShow(target, hint, clampedFadeInDuration, version))
                    .StartingIn(clampedDelay);
                return;
            }

            BeginShow(target, hint, clampedFadeInDuration, version);
        }

        public virtual void Close()
        {
            Close(currentFadeOutDuration);
        }

        public virtual void Close(VisualElement target)
        {
            Close(target, currentFadeOutDuration);
        }

        public virtual void Close(VisualElement target, int fadeOutDuration)
        {
            if (!ReferenceEquals(currentTarget, target))
            {
                return;
            }

            Close(fadeOutDuration);
        }

        public virtual void Close(int fadeOutDuration)
        {
            task?.Pause();
            currentTarget = null;
            int version = ++transitionVersion;
            int clampedFadeOutDuration = Mathf.Max(0, fadeOutDuration);

            if (clampedFadeOutDuration > 0 && resolvedStyle.visibility != Visibility.Hidden)
            {
                StartOpacityAnimation(
                    resolvedStyle.opacity,
                    0f,
                    clampedFadeOutDuration,
                    version,
                    hideWhenComplete: true
                );
            }
            else
            {
                style.visibility = Visibility.Hidden;
                style.opacity = 0f;
            }
        }

        public static void ParseTooltip(VisualElement target, out char hint, out string msg)
        {
            ParseTooltip(target.tooltip, out hint, out msg);
        }

        public static void ParseTooltip(string tooltipText, out char hint, out string msg)
        {
            // check if there is position hint in tooltip
            hint = 'B';
            msg = tooltipText ?? string.Empty;
            if (msg.Length > 2 && msg[1] == ':')
            {
                hint = msg[0] switch
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
        }

        private void BeginShow(VisualElement target, char hint, int fadeInDuration, int version)
        {
            if (version != transitionVersion || !IsAttached(target))
            {
                return;
            }

            style.visibility = Visibility.Visible;
            style.opacity = 0f;

            task = schedule.Execute(() =>
            {
                if (version != transitionVersion || !IsAttached(target))
                {
                    return;
                }

                PositionForTarget(target, hint);
                StartOpacityAnimation(0f, 1f, fadeInDuration, version, hideWhenComplete: false);
            });
        }

        private void PositionForTarget(VisualElement target, char hint)
        {
            if (parent == null)
            {
                return;
            }

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

            Rect parentBounds = parent.worldBound;
            if (left < parentBounds.xMin)
            {
                left = parentBounds.xMin;
            }

            if (left + worldBound.width > parentBounds.xMax)
            {
                left = parentBounds.xMax - worldBound.width;
            }

            if (top < parentBounds.yMin)
            {
                top = parentBounds.yMin;
            }

            if (top + worldBound.height > parentBounds.yMax)
            {
                top = parentBounds.yMax - worldBound.height;
            }

            Vector2 localPosition = parent.WorldToLocal(new Vector2(left, top));
            style.left = localPosition.x;
            style.top = localPosition.y;
        }

        private void StartOpacityAnimation(float startOpacity, float endOpacity, int duration, int version, bool hideWhenComplete)
        {
            task?.Pause();

            if (duration <= 0)
            {
                style.opacity = endOpacity;
                if (hideWhenComplete)
                {
                    style.visibility = Visibility.Hidden;
                }

                return;
            }

            float startTime = Time.realtimeSinceStartup;
            style.opacity = Mathf.Clamp01(startOpacity);
            bool isComplete = false;
            task = schedule.Execute(() =>
                {
                    if (version != transitionVersion)
                    {
                        return;
                    }

                    float elapsedMilliseconds = (Time.realtimeSinceStartup - startTime) * 1000f;
                    float t = Mathf.Clamp01(elapsedMilliseconds / duration);
                    style.opacity = Mathf.Lerp(startOpacity, endOpacity, t);
                    if (t >= 1f && hideWhenComplete)
                    {
                        style.visibility = Visibility.Hidden;
                    }

                    isComplete = t >= 1f;
                })
                .Every(FadeStepMilliseconds)
                .Until(() => version != transitionVersion || isComplete);
        }

        private static bool IsAttached(VisualElement target)
        {
            if (target.panel == null)
            {
                return false;
            }

            VisualElement element = target;
            while (element != null)
            {
                if (element.resolvedStyle.display == DisplayStyle.None ||
                    element.resolvedStyle.visibility == Visibility.Hidden)
                {
                    return false;
                }

                element = element.parent;
            }

            return true;
        }

        // ============================================================================================================
    }
}
