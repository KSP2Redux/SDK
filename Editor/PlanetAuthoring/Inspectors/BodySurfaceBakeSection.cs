using System;
using System.Globalization;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Shared wiring for the "Body Surface Baking" inspector section so multiple inspectors can host the same UX.
    /// </summary>
    /// <remarks>
    /// Looks up the section's UI elements by name in the supplied root and binds them to
    /// <see cref="BodySurfaceBakerOperation" />. Both the body-level <c>CelestialBodyEditor</c>
    /// and the PQS-level <c>PQSEditor</c> use this so artists can re-bake from either inspector
    /// without navigating between assets. Persists settings under a single EditorPrefs prefix so
    /// the two inspectors share state.
    /// </remarks>
    internal static class BodySurfaceBakeSection
    {
        // Prefs key prefix kept as the legacy "ScaledSpaceBake." string so users' saved settings
        // survive the C# rename to BodySurfaceBakerOperation.
        private const string PrefsPrefix = "Ksp2UnityTools.ScaledSpaceBake.";
        private static readonly Color DefaultOceanColor = new(0.05f, 0.15f, 0.4f, 1f);
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <summary>
        /// Wires the bake-section widgets inside <paramref name="root" /> against
        /// <see cref="BodySurfaceBakerOperation" />.
        /// </summary>
        /// <param name="root">The inspector root containing the bake-section UXML elements. Returns silently if no bake button is found.</param>
        /// <param name="resolveBody">Called on each bake click to resolve the body the bake should target. Returning null aborts the bake with a status message.</param>
        public static void Wire(VisualElement root, Func<CoreCelestialBodyData> resolveBody)
        {
            var resolution = root.Q<DropdownField>("body-surface-bake-resolution");
            var includeOcean = root.Q<Toggle>("body-surface-bake-include-ocean");
            var oceanColor = root.Q<ColorField>("body-surface-bake-ocean-color");
            var bake = root.Q<Button>("body-surface-bake-button");
            var status = root.Q<Label>("body-surface-bake-status");
            if (bake == null) return;

            int resIndex = EditorPrefs.GetInt(PrefsPrefix + "MeshResIndex", 1);
            if (resolution != null && resIndex >= 0 && resIndex < resolution.choices.Count)
                resolution.SetValueWithoutNotify(resolution.choices[resIndex]);
            includeOcean?.SetValueWithoutNotify(EditorPrefs.GetBool(PrefsPrefix + "IncludeOcean", false));
            oceanColor?.SetValueWithoutNotify(LoadOceanColor());

            bake.clicked += () =>
            {
                int currentResIndex = resolution?.index ?? 1;
                bool currentIncludeOcean = includeOcean?.value ?? false;
                Color currentOceanColor = oceanColor?.value ?? DefaultOceanColor;

                EditorPrefs.SetInt(PrefsPrefix + "MeshResIndex", currentResIndex);
                EditorPrefs.SetBool(PrefsPrefix + "IncludeOcean", currentIncludeOcean);
                StoreOceanColor(currentOceanColor);

                var body = resolveBody?.Invoke();
                if (body == null)
                {
                    if (status != null) status.text = "Bake failed: could not resolve a body for this PQS.";
                    return;
                }

                var result = BodySurfaceBakerOperation.Bake(body, LoadSettings());
                if (status != null)
                    status.text = result.Success ? $"Baked to {result.ScaledFolder}." : $"Bake failed: {result.Error}";
            };
        }

        /// <summary>
        /// Runs a body-surface bake against <paramref name="body" /> using the artist's persisted EditorPrefs settings.
        /// </summary>
        /// <remarks>
        /// Used by the surface-bake-drift validator's re-bake fix so a validator-triggered bake
        /// honors the same settings the inspector's bake button would use. Without this the
        /// validator would silently re-bake with hardcoded defaults, dropping ocean / resolution
        /// choices the artist set in the inspector.
        /// </remarks>
        /// <param name="body">The body to bake.</param>
        /// <returns>The bake result.</returns>
        internal static BodySurfaceBakerOperation.Result BakeWithPersistedSettings(CoreCelestialBodyData body)
        {
            return BodySurfaceBakerOperation.Bake(body, LoadSettings());
        }

        private static BodySurfaceBakerOperation.Settings LoadSettings() => new()
        {
            MeshResolutionIndex = EditorPrefs.GetInt(PrefsPrefix + "MeshResIndex", 1),
            IncludeOcean = EditorPrefs.GetBool(PrefsPrefix + "IncludeOcean", false),
            OceanColor = LoadOceanColor(),
        };

        private static Color LoadOceanColor()
        {
            var packed = EditorPrefs.GetString(PrefsPrefix + "OceanColor", null);
            if (string.IsNullOrEmpty(packed)) return DefaultOceanColor;
            var parts = packed.Split(',');
            if (parts.Length == 4
                && float.TryParse(parts[0], NumberStyles.Float, Invariant, out var r)
                && float.TryParse(parts[1], NumberStyles.Float, Invariant, out var g)
                && float.TryParse(parts[2], NumberStyles.Float, Invariant, out var b)
                && float.TryParse(parts[3], NumberStyles.Float, Invariant, out var a))
            {
                return new Color(r, g, b, a);
            }
            return DefaultOceanColor;
        }

        private static void StoreOceanColor(Color c)
        {
            var packed = string.Format(Invariant, "{0},{1},{2},{3}", c.r, c.g, c.b, c.a);
            EditorPrefs.SetString(PrefsPrefix + "OceanColor", packed);
        }
    }
}
