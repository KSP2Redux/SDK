using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UIElements;
using UnityObject = UnityEngine.Object;

namespace UitkForKsp2.Controls
{
    [UxmlElement]
    public partial class ColorPicker : BaseField<Color>
    {
        private const string ColorPickerUxmlAddress = "Assets/Modules/KSP2UnityTools/Assets/UITK for KSP2/Templates/ColorPicker/ColorPickerUI.uxml";
        private const string SbSquareShaderAddress = "Assets/Modules/KSP2UnityTools/Assets/UITK for KSP2/Templates/ColorPicker/SBSquare.shader";
        private const string AlphaGradientShaderAddress = "Assets/Modules/KSP2UnityTools/Assets/UITK for KSP2/Templates/ColorPicker/AlphaGradient.shader";

        private static readonly int Hue = Shader.PropertyToID("_Hue");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private Slider _hueSlider;
        private VisualElement _hueSliderBackground;
        private VisualElement _sbSquareBackground;
        private VisualElement _sbSquare;
        private VisualElement _sbHandle;
        private Slider _alphaSlider;
        private VisualElement _alphaSliderBackground;

        private Texture2D? _hueSliderTexture;
        private Material? _sbMaterial;
        private RenderTexture? _sbRenderTexture;
        private Material? _alphaMaterial;
        private RenderTexture? _alphaRenderTexture;

        private bool _isUpdatingFromUI;

        private AsyncOperationHandle<VisualTreeAsset> _uxmlLoadHandle;
        private AsyncOperationHandle<Shader> _sbShaderLoadHandle;
        private AsyncOperationHandle<Shader> _alphaShaderLoadHandle;

        public ColorPicker() : base(null, null)
        {
            LoadAssetsAsync();
        }

        private async Task LoadAssetsAsync()
        {
            _uxmlLoadHandle = Addressables.LoadAssetAsync<VisualTreeAsset>(ColorPickerUxmlAddress);
            _sbShaderLoadHandle = Addressables.LoadAssetAsync<Shader>(SbSquareShaderAddress);
            _alphaShaderLoadHandle = Addressables.LoadAssetAsync<Shader>(AlphaGradientShaderAddress);

            try
            {
                await Task.WhenAll(
                    _uxmlLoadHandle.Task,
                    _sbShaderLoadHandle.Task,
                    _alphaShaderLoadHandle.Task
                );

                if (_uxmlLoadHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError(
                        $"Failed to load ColorPicker UXML from address '{ColorPickerUxmlAddress}': " +
                        $"{_uxmlLoadHandle.OperationException}"
                    );
                    Cleanup();
                    return;
                }

                if (_sbShaderLoadHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError(
                        $"Failed to load SB Square Shader from address '{SbSquareShaderAddress}': " +
                        $"{_sbShaderLoadHandle.OperationException}"
                    );
                    Cleanup();
                    return;
                }

                if (_alphaShaderLoadHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError(
                        $"Failed to load Alpha Gradient Shader from address '{AlphaGradientShaderAddress}': " +
                        $"{_alphaShaderLoadHandle.OperationException}"
                    );
                    Cleanup();
                    return;
                }

                InitializeUI();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"An error occurred during ColorPicker asset loading: {e.Message}");
                Cleanup();
            }
        }

        private void InitializeUI()
        {
            _uxmlLoadHandle.Result.CloneTree(this);
            Remove(Children().First());

            _hueSlider = this.Q<Slider>("HueSlider");
            _hueSliderBackground = _hueSlider.hierarchy[0];
            _sbSquareBackground = this.Q<VisualElement>("SBSquareBackground");
            _sbSquare = this.Q<VisualElement>("SBSquare");
            _sbHandle = this.Q<VisualElement>("SBHandle");
            _alphaSlider = this.Q<Slider>("AlphaSlider");
            _alphaSliderBackground = _alphaSlider.hierarchy[0];

            SetupTextures(_sbShaderLoadHandle.Result, _alphaShaderLoadHandle.Result);
            UpdateSbSquareTexture();
            UpdateAlphaSliderTexture();

            SetupHueSlider();
            SetupSbSquare();
            SetupAlphaSlider();

            // The value might be changed before the UI is initialized
            UpdateUIFromColor(value);

            this.RegisterValueChangedCallback(OnValueChanged);
            RegisterCallback<DetachFromPanelEvent>(_ => Cleanup());
        }

        private void OnValueChanged(ChangeEvent<Color> evt)
        {
            // Skip if the update is from internal UI changes
            if (_isUpdatingFromUI)
            {
                return;
            }

            // Update UI for external changes
            UpdateUIFromColor(evt.newValue);
        }

        private void SetupTextures(Shader sbShader, Shader alphaShader)
        {
            _sbMaterial = new Material(sbShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _sbRenderTexture = new RenderTexture(512, 512, 0)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _sbSquareBackground.style.backgroundImage = Background.FromRenderTexture(_sbRenderTexture);

            _alphaMaterial = new Material(alphaShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _alphaRenderTexture = new RenderTexture(1, 256, 0)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _alphaSliderBackground.style.backgroundImage = Background.FromRenderTexture(_alphaRenderTexture);

            _hueSliderTexture = CreateHueSliderTexture();
            _hueSliderBackground.style.backgroundImage = Background.FromTexture2D(_hueSliderTexture);
        }

        private void UpdateSbSquareTexture()
        {
            if (_sbMaterial == null || _sbRenderTexture == null)
            {
                return;
            }

            _sbMaterial.SetFloat(Hue, _hueSlider.value / 255f);
            Graphics.Blit(null, _sbRenderTexture, _sbMaterial);

            _sbSquare.MarkDirtyRepaint();
        }

        private void UpdateAlphaSliderTexture()
        {
            if (_alphaMaterial == null || _alphaRenderTexture == null)
            {
                return;
            }

            _alphaMaterial.SetColor(BaseColor, new Color(value.r, value.g, value.b));
            Graphics.Blit(null, _alphaRenderTexture, _alphaMaterial);

            _alphaSlider.MarkDirtyRepaint();
        }

        private void SetupHueSlider()
        {
            _hueSlider.RegisterCallback<PointerDownEvent>(evt =>
                {
                    Vector2 localPosition = evt.localPosition;
                    float clickValue = Mathf.Clamp(localPosition.x / _hueSlider.resolvedStyle.width * 255f, 0f, 255f);
                    if (Mathf.Abs(_hueSlider.value - clickValue) <= 0.01f)
                    {
                        return;
                    }

                    _hueSlider.value = clickValue;
                    UpdateColorFromUI();
                }
            );

            _hueSlider.RegisterValueChangedCallback(evt =>
                {
                    if (Mathf.Abs(evt.newValue - GetHue(value) * 255f) <= 0.01f)
                    {
                        return;
                    }

                    UpdateColorFromUI();
                    UpdateSbSquareTexture();
                    UpdateAlphaSliderTexture();
                }
            );
        }

        private void SetupSbSquare()
        {
            _sbSquare.RegisterCallback<PointerDownEvent>(evt =>
                {
                    _sbSquare.CapturePointer(evt.pointerId);
                    UpdateSbHandleWithDrag(
                        evt.localPosition,
                        _sbSquare.resolvedStyle.width,
                        _sbSquare.resolvedStyle.height
                    );
                    evt.StopPropagation();
                }
            );

            _sbSquare.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (!_sbSquare.HasPointerCapture(evt.pointerId))
                    {
                        return;
                    }

                    Vector2 localPosition = _sbSquare.WorldToLocal(evt.position);
                    UpdateSbHandleWithDrag(
                        localPosition,
                        _sbSquare.resolvedStyle.width,
                        _sbSquare.resolvedStyle.height
                    );
                }
            );

            _sbSquare.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (_sbSquare.HasPointerCapture(evt.pointerId))
                    {
                        _sbSquare.ReleasePointer(evt.pointerId);
                    }
                }
            );

            _sbHandle.style.left = _sbSquare.resolvedStyle.width * 0.5f - _sbHandle.resolvedStyle.width * 0.5f;
            _sbHandle.style.top = _sbSquare.resolvedStyle.height * 0.5f - _sbHandle.resolvedStyle.height * 0.5f;
        }

        private void UpdateSbHandleWithDrag(Vector2 position, float squareWidth, float squareHeight)
        {
            float handleWidth = _sbHandle.resolvedStyle.width;
            float handleHeight = _sbHandle.resolvedStyle.height;

            // Make sure the handle can't go outside the square
            float minCenterX = handleWidth * 0.5f;
            float maxCenterX = squareWidth - handleWidth * 0.5f;
            float minCenterY = handleHeight * 0.5f;
            float maxCenterY = squareHeight - handleHeight * 0.5f;

            float clampedCenterX = Mathf.Clamp(position.x, minCenterX, maxCenterX);
            float clampedCenterY = Mathf.Clamp(position.y, minCenterY, maxCenterY);

            _sbHandle.style.left = clampedCenterX - handleWidth * 0.5f;
            _sbHandle.style.top = clampedCenterY - handleHeight * 0.5f;

            UpdateColorFromUI();
            UpdateAlphaSliderTexture();
        }

        private void SetupAlphaSlider()
        {
            _alphaSlider.lowValue = 0f;
            _alphaSlider.highValue = 255f;
            _alphaSlider.value = 255f;
            _alphaSlider.RegisterValueChangedCallback(_ =>
                {
                    if (Mathf.Abs(_alphaSlider.value - value.a * 255f) > 0.01f)
                    {
                        UpdateColorFromUI();
                    }
                }
            );
        }

        private void UpdateColorFromUI()
        {
            // Prevent recursive updates from the UI
            if (_isUpdatingFromUI)
            {
                return;
            }

            _isUpdatingFromUI = true;

            float squareWidth = _sbSquare.resolvedStyle.width;
            float squareHeight = _sbSquare.resolvedStyle.height;
            float handleWidth = _sbHandle.resolvedStyle.width;
            float handleHeight = _sbHandle.resolvedStyle.height;

            float travelWidth = squareWidth - handleWidth;
            float travelHeight = squareHeight - handleHeight;

            float handleLeft = _sbHandle.style.left.value.value;
            float handleTop = _sbHandle.style.top.value.value;

            float h = _hueSlider.value / 255f;
            float s = travelWidth > 0 ? Mathf.Clamp01(handleLeft / travelWidth) : 0f;
            float v = travelHeight > 0 ? Mathf.Clamp01(1.0f - handleTop / travelHeight) : 0f;
            float a = _alphaSlider.value / 255f;

            Color newColor = Color.HSVToRGB(h, s, v);
            newColor.a = a;

            value = newColor;

            _isUpdatingFromUI = false;
        }

        private void UpdateUIFromColor(Color color)
        {
            if (_isUpdatingFromUI)
            {
                return;
            }

            value = color;

            Color.RGBToHSV(color, out float h, out float s, out float v);

            // Prevents hue slider from wrapping around to 0 when the slider is at the right edge
            if (h < 0.001f && Mathf.Approximately(_hueSlider.value, _hueSlider.highValue))
            {
                h = 1.0f;
            }

            // Prevents the hue slider from jumping to 0 when the color is grayscale
            if (Mathf.Approximately(color.r, color.g) && Mathf.Approximately(color.g, color.b))
            {
                h = _hueSlider.value / 255f;
            }

            float handleWidth = _sbHandle.resolvedStyle.width;
            float handleHeight = _sbHandle.resolvedStyle.height;
            float squareWidth = _sbSquare.resolvedStyle.width;
            float squareHeight = _sbSquare.resolvedStyle.height;
            float travelWidth = squareWidth - handleWidth;
            float travelHeight = squareHeight - handleHeight;

            // Prevents the SB handle from jumping to the left edge when the color is black
            if (v < 0.001f)
            {
                float handleLeft = _sbHandle.style.left.value.value;
                s = travelWidth > 0 ? Mathf.Clamp01(handleLeft / travelWidth) : 0f;
            }

            _hueSlider.SetValueWithoutNotify(h * 255f);
            _alphaSlider.SetValueWithoutNotify(color.a * 255f);

            _sbHandle.style.left = travelWidth > 0 ? s * travelWidth : 0;
            _sbHandle.style.top = travelHeight > 0 ? (1 - v) * travelHeight : 0;

            UpdateSbSquareTexture();
            UpdateAlphaSliderTexture();
        }

        private static Texture2D CreateHueSliderTexture()
        {
            var texture = new Texture2D(512, 1, TextureFormat.RGB24, false)
            {
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int i = 0; i < texture.width; i++)
            {
                texture.SetPixel(i, 0, Color.HSVToRGB((float)i / (texture.width - 1), 1f, 1f));
            }

            texture.Apply();

            return texture;
        }

        private void Cleanup()
        {
            if (_sbRenderTexture != null)
            {
                _sbRenderTexture.Release();
                Destroy(_sbRenderTexture);
                _sbRenderTexture = null;
            }

            if (_alphaRenderTexture != null)
            {
                _alphaRenderTexture.Release();
                Destroy(_alphaRenderTexture);
                _alphaRenderTexture = null;
            }

            if (_sbMaterial != null)
            {
                Destroy(_sbMaterial);
                _sbMaterial = null;
            }

            if (_alphaMaterial != null)
            {
                Destroy(_alphaMaterial);
                _alphaMaterial = null;
            }

            if (_hueSliderTexture != null)
            {
                Destroy(_hueSliderTexture);
                _hueSliderTexture = null;
            }

            if (_uxmlLoadHandle.IsValid())
            {
                Addressables.Release(_uxmlLoadHandle);
            }

            if (_sbShaderLoadHandle.IsValid())
            {
                Addressables.Release(_sbShaderLoadHandle);
            }

            if (_alphaShaderLoadHandle.IsValid())
            {
                Addressables.Release(_alphaShaderLoadHandle);
            }
        }

        private static void Destroy(UnityObject obj)
        {
            if (Application.isPlaying)
            {
                UnityObject.Destroy(obj);
            }
            else
            {
                UnityObject.DestroyImmediate(obj);
            }
        }

        private static float GetHue(Color color)
        {
            Color.RGBToHSV(color, out float h, out _, out _);
            return h;
        }
    }
}
