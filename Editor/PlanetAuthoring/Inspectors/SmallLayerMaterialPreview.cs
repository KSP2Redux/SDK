using System.Collections.Generic;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Renders a lit sphere preview of a <see cref="SmallLayerMaterial" /> using a single shared <see cref="PreviewRenderUtility" />, with a per-SO <see cref="Texture2D" /> cache.
    /// </summary>
    /// <remarks>
    /// Surfaces in the SO inspector preview pane, the project window thumbnail, and the planet
    /// authoring matrix cell. All three render at a single fixed view angle and a fixed resolution
    /// so a single cache entry per SO covers every consumer. Cache entries are dropped by
    /// <c>SmallLayerMaterialPostProcessor</c> when the SO is saved, and on
    /// <see cref="AssemblyReloadEvents.beforeAssemblyReload" /> the utility and material are
    /// disposed so domain reload doesn't leak native handles.
    /// </remarks>
    [InitializeOnLoad]
    internal static class SmallLayerMaterialPreview
    {
        private const string PreviewShaderName = "Hidden/Ksp2UnityTools/SmallLayerMaterialPreview";
        private const int PreviewSize = 256;
        private static readonly Quaternion PreviewRotation = Quaternion.Euler(-20f, 120f, 0f);

        private static PreviewRenderUtility _utility;
        private static Mesh _sphereMesh;
        private static Material _previewMaterial;
        private static readonly Dictionary<SmallLayerMaterial, Texture2D> _cache = new();

        static SmallLayerMaterialPreview()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
        }

        /// <summary>
        /// Returns the cached sphere preview for <paramref name="so" />, rendering it on first request and on cache miss.
        /// </summary>
        /// <remarks>
        /// One cached <see cref="Texture2D" /> per SO at a fixed <c>PreviewSize x PreviewSize</c> resolution and a
        /// fixed view angle. Callers (inspector preview pane, project window thumbnail, matrix cell) all stretch
        /// this same texture into their own rect via <see cref="GUI.DrawTexture(Rect, Texture, ScaleMode, bool)" /> /
        /// <c>StyleBackground</c>.
        /// </remarks>
        public static Texture2D Render(SmallLayerMaterial so)
        {
            if (so == null) return null;
            if (_cache.TryGetValue(so, out var cached) && cached != null)
                return cached;

            if (!EnsureInitialized()) return null;

            ApplyFieldsToMaterial(so);

            var rect = new Rect(0, 0, PreviewSize, PreviewSize);
            _utility.BeginPreview(rect, GUIStyle.none);

            _utility.camera.transform.position = new Vector3(0f, 0f, -3f);
            _utility.camera.transform.rotation = Quaternion.identity;
            _utility.camera.nearClipPlane = 0.1f;
            _utility.camera.farClipPlane = 50f;
            _utility.camera.fieldOfView = 30f;
            _utility.ambientColor = new Color(0.25f, 0.25f, 0.28f, 1f);

            if (_utility.lights != null && _utility.lights.Length > 0)
            {
                _utility.lights[0].intensity = 1.2f;
                _utility.lights[0].color = Color.white;
                _utility.lights[0].transform.rotation = Quaternion.Euler(30f, -30f, 0f);
            }
            if (_utility.lights != null && _utility.lights.Length > 1)
            {
                _utility.lights[1].intensity = 0.4f;
                _utility.lights[1].color = new Color(0.7f, 0.75f, 0.85f, 1f);
                _utility.lights[1].transform.rotation = Quaternion.Euler(-15f, 150f, 0f);
            }

            _utility.DrawMesh(_sphereMesh, Matrix4x4.TRS(Vector3.zero, PreviewRotation, Vector3.one), _previewMaterial, 0);
            _utility.Render(allowScriptableRenderPipeline: false);

            var rt = _utility.EndPreview() as RenderTexture;
            var tex = CopyToTexture2D(rt, PreviewSize, PreviewSize);
            _cache[so] = tex;
            return tex;
        }

        /// <summary>
        /// Returns the cached preview <see cref="Texture2D" /> for <paramref name="so" />, or null when no entry is cached.
        /// </summary>
        public static Texture2D GetCached(SmallLayerMaterial so) =>
            so != null && _cache.TryGetValue(so, out var t) ? t : null;

        /// <summary>
        /// Drops the cached preview for <paramref name="so" />, if any. The next <see cref="Render" /> call regenerates.
        /// </summary>
        public static void Invalidate(SmallLayerMaterial so)
        {
            if (so == null) return;
            DropCacheEntry(so);
        }

        private static bool EnsureInitialized()
        {
            if (_utility == null)
                _utility = new PreviewRenderUtility();
            if (_sphereMesh == null)
                _sphereMesh = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx") ??
                              Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            if (_previewMaterial == null)
            {
                var shader = Shader.Find(PreviewShaderName);
                if (shader == null)
                {
                    Debug.LogWarning($"[SmallLayerMaterialPreview] Shader '{PreviewShaderName}' not found. Preview will be skipped.");
                    return false;
                }
                _previewMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            return _sphereMesh != null && _previewMaterial != null;
        }

        private static void ApplyFieldsToMaterial(SmallLayerMaterial so)
        {
            _previewMaterial.SetTexture("_Albedo", so.AlbedoTexture);
            _previewMaterial.SetTexture("_Normal", so.NormalTexture);
            _previewMaterial.SetTexture("_Metal", so.MetallicTexture);

            _previewMaterial.SetFloat("_UVScale", so.UVScale);
            _previewMaterial.SetFloat("_UVOffset", so.UVOffset);

            _previewMaterial.SetColor("_Tint", so.Tint);
            _previewMaterial.SetFloat("_Brightness", so.Brightness);
            _previewMaterial.SetFloat("_Contrast", so.Contrast);
            _previewMaterial.SetFloat("_Saturation", so.Saturation);

            _previewMaterial.SetFloat("_NormalStrength", so.NormalStrength);
            _previewMaterial.SetFloat("_GlossStrength", so.GlossStrength);
            _previewMaterial.SetFloat("_MetallicStrength", so.MetallicStrength);
            _previewMaterial.SetFloat("_AOStrength", so.AOStrength);

            _previewMaterial.SetFloat("_EmissionStrength", so.EmissionStrength);
            _previewMaterial.SetColor("_EmissionColor", so.EmissionColor);
        }

        private static Texture2D CopyToTexture2D(RenderTexture rt, int width, int height)
        {
            if (rt == null) return null;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false, linear: false)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0, recalculateMipMaps: false);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            RenderTexture.active = prev;
            return tex;
        }

        private static void DropCacheEntry(SmallLayerMaterial so)
        {
            if (_cache.TryGetValue(so, out var tex))
            {
                if (tex != null) Object.DestroyImmediate(tex);
                _cache.Remove(so);
            }
        }

        private static void Dispose()
        {
            foreach (var tex in _cache.Values)
            {
                if (tex != null) Object.DestroyImmediate(tex);
            }
            _cache.Clear();

            if (_previewMaterial != null)
            {
                Object.DestroyImmediate(_previewMaterial);
                _previewMaterial = null;
            }
            _sphereMesh = null;

            _utility?.Cleanup();
            _utility = null;
        }
    }
}
