using KSP.Rendering.Planets;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Overlays
{
    /// <summary>
    /// Base class for editor-only IPQSOverlay implementations driven by the Preview Controls window.
    /// </summary>
    /// <remarks>
    /// Each preview overlay owns one Material instance built from a shader looked up by name.
    /// Subclasses override <see cref="RefreshBindings" /> to push per-body inputs into the material
    /// (mask textures, planet radius, alpha map, etc.) when the active session changes or the
    /// manager triggers a refresh. The Material is fed buffer bindings by PQSRenderer when
    /// added via <see cref="PQSRenderer.AddOverlay" />.
    /// </remarks>
    internal abstract class PreviewOverlay : IPQSOverlay
    {
        private readonly string _shaderName;
        private Material _material;

        /// <summary>
        /// Initializes a new overlay that will look up its shader by the given name.
        /// </summary>
        /// <param name="shaderName">The shader name passed to <see cref="Shader.Find(string)" /> when the material is created.</param>
        protected PreviewOverlay(string shaderName)
        {
            _shaderName = shaderName;
        }

        /// <summary>
        /// Gets the overlay material, building it on first access from the configured shader.
        /// </summary>
        public Material OverlayMaterial => _material ??= CreateMaterial();

        /// <summary>
        /// Pulls per-body inputs from the session and applies them to the overlay material.
        /// </summary>
        /// <remarks>
        /// Called when the overlay is enabled and whenever the manager refreshes. Inputs include
        /// mask textures, planet radius, and any prepass handles required by the shader.
        /// </remarks>
        /// <param name="pqs">The PQS for the currently previewed body.</param>
        public abstract void RefreshBindings(PQS pqs);

        /// <summary>
        /// Releases the underlying material.
        /// </summary>
        /// <remarks>
        /// Called when the overlay is disabled or the editor reloads. The next call to
        /// <see cref="OverlayMaterial" /> rebuilds it on demand.
        /// </remarks>
        public virtual void Dispose()
        {
            if (_material != null)
            {
                Object.DestroyImmediate(_material);
                _material = null;
            }
        }

        private Material CreateMaterial()
        {
            var shader = Shader.Find(_shaderName);
            if (shader == null)
            {
                Debug.LogError($"[PreviewOverlay] Shader '{_shaderName}' not found. Overlay will fall back to InternalErrorShader (pink) and not render correctly.");
                return new Material(Shader.Find("Hidden/InternalErrorShader"));
            }
            if (!shader.isSupported)
            {
                Debug.LogWarning($"[PreviewOverlay] Shader '{_shaderName}' is not supported on this platform / has compile errors. Overlay will not render.");
            }
            var mat = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = $"PreviewOverlay_{shader.name}",
            };
            // Sync both the keyword and the [Toggle]-bound float so PQSRenderer's SetBuffer
            // calls land in the variant that actually reads QuadMeshDataBuffer.
            mat.EnableKeyword("_USE_PQS_BUFFER");
            if (mat.HasProperty("_NoComputeBuffer"))
            {
                mat.SetFloat("_NoComputeBuffer", 1f);
            }
            return mat;
        }
    }
}
