using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Bootstraps and resolves the shared "Smoothing Pad" <see cref="PQSDecal" /> template that
    /// every <see cref="Authoring.SurfaceLandmark" /> instance references.
    /// </summary>
    /// <remarks>
    /// The template is deliberately minimal. Every meaningful field is overridden per-instance by
    /// the landmark sync code, so the template's defaults only matter when a landmark hasn't
    /// finished syncing yet. Created on first call at <see cref="AssetPath" /> if missing.
    /// </remarks>
    internal static class SmoothingPadAsset
    {
        public const string AssetPath =
            SDKConfiguration.BasePath + "/Assets/DecalMaps/SmoothingPad.asset";

        private static PQSDecal _cached;

        /// <summary>
        /// Returns the shared SmoothingPad PQSDecal asset, creating it on disk if absent.
        /// </summary>
        public static PQSDecal Get()
        {
            if (_cached != null) return _cached;
            _cached = AssetDatabase.LoadAssetAtPath<PQSDecal>(AssetPath);
            if (_cached != null) return _cached;
            var dir = Path.GetDirectoryName(AssetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            _cached = ScriptableObject.CreateInstance<PQSDecal>();
            _cached.Init();
            // Defaults are immaterial since every landmark overrides per-instance, but pick values
            // that produce a benign no-op fallback if an override slips.
            _cached.HeightScale = 0f;
            _cached.HeightBlendMode = PQSDecalHeightBlendMode.Replace;
            _cached.HeightOffset = 0f;
            _cached.FadeShape = PQSDecalAlphaFadeShape.Circular;
            _cached.FadeStrength = 0.5f;
            _cached.AlbedoOpacity = 0f;
            _cached.NormalOpacity = 0f;
            _cached.GradientOpacity = 0f;
            _cached.UseDecalTexturing = false;
            _cached.UseTextureAlphaMask = false;
            AssetDatabase.CreateAsset(_cached, AssetPath);
            AssetDatabase.SaveAssets();
            return _cached;
        }
    }
}
