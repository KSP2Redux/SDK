using UnityEngine;
using UnityEngine.UIElements;

namespace UitkForKsp2.Controls
{
    [UxmlElement]
    public partial class InvertedCornerBox : VisualElement
    {
        private static readonly Color DefaultBorderColor = new Color32(116, 118, 128, 255);
        private static readonly Color DefaultBackgroundColor = new Color32(27, 30, 36, 255);

        private float _borderThickness = 1f;
        private float _notchSize = 5f;
        private Color _borderColor = DefaultBorderColor;
        private Color _backgroundColor = DefaultBackgroundColor;

        public InvertedCornerBox()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            Rect rect = contentRect;
            float width = resolvedStyle.width;
            float height = resolvedStyle.height;

            if (float.IsNaN(width) || width <= 0)
            {
                width = rect.width + resolvedStyle.paddingLeft + resolvedStyle.paddingRight;
            }

            if (float.IsNaN(height) || height <= 0)
            {
                height = rect.height + resolvedStyle.paddingTop + resolvedStyle.paddingBottom;
            }

            float strokeWidth = Mathf.Max(1f, _borderThickness);
            float halfStroke = strokeWidth * 0.5f;
            float xMin = halfStroke;
            float yMin = halfStroke;
            float xMax = width - halfStroke;
            float yMax = height - halfStroke;
            float notchSize = Mathf.Min(_notchSize, (xMax - xMin) * 0.5f, (yMax - yMin) * 0.5f);

            if (xMax <= xMin || yMax <= yMin || notchSize <= 0)
            {
                return;
            }

            var painter = mgc.painter2D;
            painter.strokeColor = _borderColor;
            painter.lineWidth = strokeWidth;
            painter.lineCap = LineCap.Butt;
            painter.lineJoin = LineJoin.Miter;
            painter.fillColor = _backgroundColor;

            painter.BeginPath();
            // Top left notch
            painter.MoveTo(new Vector2(xMin, yMin + notchSize));
            painter.LineTo(new Vector2(xMin + notchSize, yMin + notchSize));
            painter.LineTo(new Vector2(xMin + notchSize, yMin));
            // Top right notch
            painter.LineTo(new Vector2(xMax - notchSize, yMin));
            painter.LineTo(new Vector2(xMax - notchSize, yMin + notchSize));
            painter.LineTo(new Vector2(xMax, yMin + notchSize));
            // Bottom right notch
            painter.LineTo(new Vector2(xMax, yMax - notchSize));
            painter.LineTo(new Vector2(xMax - notchSize, yMax - notchSize));
            painter.LineTo(new Vector2(xMax - notchSize, yMax));
            // Bottom left notch
            painter.LineTo(new Vector2(xMin + notchSize, yMax));
            painter.LineTo(new Vector2(xMin + notchSize, yMax - notchSize));
            painter.LineTo(new Vector2(xMin, yMax - notchSize));
            // Close the path
            painter.LineTo(new Vector2(xMin, yMin + notchSize));
            painter.ClosePath();

            painter.Fill();
            painter.Stroke();
        }

        [UxmlAttribute("border-thickness")]
        public float BorderThickness
        {
            get => _borderThickness;
            set
            {
                _borderThickness = value;
                MarkDirtyRepaint();
            }
        }

        [UxmlAttribute("notch-size")]
        public float NotchSize
        {
            get => _notchSize;
            set
            {
                _notchSize = value;
                MarkDirtyRepaint();
            }
        }

        [UxmlAttribute("border-color")]
        public Color BorderColor
        {
            get => _borderColor;
            set
            {
                _borderColor = value;
                MarkDirtyRepaint();
            }
        }

        [UxmlAttribute("background-color")]
        public Color BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                _backgroundColor = value;
                MarkDirtyRepaint();
            }
        }
    }
}
