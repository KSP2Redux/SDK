using UnityEngine;
using UnityEngine.UIElements;

namespace UitkForKsp2.Controls
{
    public class InvertedCornerBox : VisualElement
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
            float width = rect.width;
            float height = rect.height;
            float paddingX = resolvedStyle.paddingLeft + resolvedStyle.paddingRight;
            float paddingY = resolvedStyle.paddingTop + resolvedStyle.paddingBottom;

            if (width <= 0 || height <= 0 || width + paddingX < 2 * _notchSize || height + paddingY < 2 * _notchSize)
            {
                return;
            }

            var painter = mgc.painter2D;
            painter.strokeColor = _borderColor;
            painter.lineWidth = 1 + _borderThickness;
            painter.lineCap = LineCap.Butt;
            painter.lineJoin = LineJoin.Miter;
            painter.fillColor = _backgroundColor;

            painter.BeginPath();
            // Top left notch
            painter.MoveTo(new Vector2(0, _notchSize));
            painter.LineTo(new Vector2(_notchSize, _notchSize));
            painter.LineTo(new Vector2(_notchSize, 0));
            // Top right notch
            painter.LineTo(new Vector2(width + paddingX - _notchSize, 0));
            painter.LineTo(new Vector2(width + paddingX - _notchSize, _notchSize));
            painter.LineTo(new Vector2(width + paddingX, _notchSize));
            // Bottom right notch
            painter.LineTo(new Vector2(width + paddingX, height + paddingY - _notchSize));
            painter.LineTo(new Vector2(width + paddingX - _notchSize, height + paddingY - _notchSize));
            painter.LineTo(new Vector2(width + paddingX - _notchSize, height + paddingY));
            // Bottom left notch
            painter.LineTo(new Vector2(_notchSize, height + paddingY));
            painter.LineTo(new Vector2(_notchSize, height + paddingY - _notchSize));
            painter.LineTo(new Vector2(0, height + paddingY - _notchSize));
            // Close the path
            painter.LineTo(new Vector2(0, _notchSize));
            painter.ClosePath();

            painter.Stroke();
            painter.Fill();
        }

        public float BorderThickness
        {
            get => _borderThickness;
            set
            {
                _borderThickness = value;
                MarkDirtyRepaint();
            }
        }

        public float NotchSize
        {
            get => _notchSize;
            set
            {
                _notchSize = value;
                MarkDirtyRepaint();
            }
        }

        public Color BorderColor
        {
            get => _borderColor;
            set
            {
                _borderColor = value;
                MarkDirtyRepaint();
            }
        }

        public Color BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                _backgroundColor = value;
                MarkDirtyRepaint();
            }
        }

        public new class UxmlFactory : UxmlFactory<InvertedCornerBox, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlFloatAttributeDescription _borderThickness = new()
            {
                name = "border-thickness",
                defaultValue = 1f
            };

            private readonly UxmlFloatAttributeDescription _notchSize = new()
            {
                name = "notch-size",
                defaultValue = 5f
            };

            private readonly UxmlColorAttributeDescription _borderColor = new()
            {
                name = "border-color",
                defaultValue = DefaultBorderColor
            };

            private readonly UxmlColorAttributeDescription _backgroundColor = new()
            {
                name = "background-color",
                defaultValue = DefaultBackgroundColor
            };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var element = (InvertedCornerBox)ve;
                element.BorderThickness = _borderThickness.GetValueFromBag(bag, cc);
                element.NotchSize = _notchSize.GetValueFromBag(bag, cc);
                element.BorderColor = _borderColor.GetValueFromBag(bag, cc);
                element.BackgroundColor = _backgroundColor.GetValueFromBag(bag, cc);
            }
        }
    }
}