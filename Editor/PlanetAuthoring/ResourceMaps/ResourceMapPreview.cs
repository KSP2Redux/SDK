using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.ResourceMaps
{
    /// <summary>
    /// Drives one preview image: debounces requests, renders off the main thread, and shows the
    /// previous result dimmed while a new one is in flight.
    /// </summary>
    /// <remarks>
    /// Rendering a preview takes long enough that doing it inline would stall the window on every
    /// slider movement. Debouncing means a drag produces one render when it ends rather than one per
    /// frame, and running on a worker thread keeps the window responsive while that render happens.
    /// </remarks>
    internal sealed class ResourceMapPreview : IDisposable
    {
        // Long enough that a drag settles first, short enough that it still feels like a response.
        private const double DEBOUNCE_SECONDS = 0.15;

        private const float STALE_OPACITY = 0.45f;

        private readonly Image _target;

        private Func<float[]> _pendingRender;
        private double _pendingRequestedAt;
        private int _pendingSize;
        private Color _pendingTint;

        private Task<float[]> _running;
        private int _runningSize;
        private Color _runningTint;

        private Texture2D _texture;

        /// <summary>
        /// Binds a preview to the image element it updates.
        /// </summary>
        /// <param name="target">The image element to render into.</param>
        public ResourceMapPreview(Image target)
        {
            _target = target;
        }

        /// <summary>
        /// Queues a render, replacing any request not yet started.
        /// </summary>
        /// <param name="render">Produces the field to display. Runs on a worker thread, so it must not touch the Unity API.</param>
        /// <param name="size">Side length of the field <paramref name="render" /> returns.</param>
        /// <param name="tint">Colour the density is multiplied by. White renders it as grey.</param>
        public void Request(Func<float[]> render, int size, Color tint)
        {
            _pendingRender = render;
            _pendingSize = size;
            _pendingTint = tint;
            _pendingRequestedAt = EditorApplication.timeSinceStartup;
            SetStale(true);
        }

        /// <summary>
        /// Advances the preview: applies a finished render and starts a debounced one.
        /// </summary>
        /// <remarks>
        /// Called from the window's scheduler, so everything here runs on the main thread.
        /// </remarks>
        public void Tick()
        {
            if (_running != null && _running.IsCompleted)
            {
                Task<float[]> finished = _running;
                _running = null;

                if (finished.IsFaulted)
                {
                    Debug.LogError($"[ResourceMaps] Preview render failed: {finished.Exception?.GetBaseException().Message}");
                }
                else if (finished.Result != null)
                {
                    Apply(finished.Result, _runningSize, _runningTint);
                }

                SetStale(_pendingRender != null);
            }

            if (_pendingRender == null || _running != null)
                return;
            if (EditorApplication.timeSinceStartup - _pendingRequestedAt < DEBOUNCE_SECONDS)
                return;

            Func<float[]> render = _pendingRender;
            _runningSize = _pendingSize;
            _runningTint = _pendingTint;
            _pendingRender = null;
            _running = Task.Run(render);
        }

        private void Apply(float[] field, int size, Color tint)
        {
            if (_target == null || field == null || field.Length != size * size)
                return;

            if (_texture == null || _texture.width != size)
            {
                if (_texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(_texture);
                }
                _texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
                {
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }

            var pixels = new Color32[field.Length];
            for (var index = 0; index < field.Length; index++)
            {
                float density = Mathf.Clamp01(field[index]);
                pixels[index] = new Color32(
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(tint.r * density) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(tint.g * density) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(tint.b * density) * 255f),
                    255
                );
            }

            _texture.SetPixels32(pixels);
            _texture.Apply(false, false);
            _target.image = _texture;
        }

        private void SetStale(bool stale)
        {
            if (_target != null)
            {
                _target.style.opacity = stale ? STALE_OPACITY : 1f;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _pendingRender = null;
            if (_texture != null)
            {
                UnityEngine.Object.DestroyImmediate(_texture);
                _texture = null;
            }
        }
    }
}
