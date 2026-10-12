using System;
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

        /// <summary>
        /// Wires the bake-section widgets inside <paramref name="root" /> against
        /// <see cref="BodySurfaceBakerOperation" />.
        /// </summary>
        /// <param name="root">The inspector root containing the bake-section UXML elements. Returns silently if no bake button is found.</param>
        /// <param name="resolveBody">Called on each bake click to resolve the body the bake should target. Returning null aborts the bake with a status message.</param>
        public static void Wire(VisualElement root, Func<CoreCelestialBodyData> resolveBody)
        {
            var gradience = root.Q<Toggle>("body-surface-bake-gradience");
            var textures = root.Q<Toggle>("body-surface-bake-textures");
            var mesh = root.Q<Toggle>("body-surface-bake-mesh");
            var resolution = root.Q<DropdownField>("body-surface-bake-resolution");
            var bake = root.Q<Button>("body-surface-bake-button");
            var status = root.Q<Label>("body-surface-bake-status");
            if (bake == null) return;

            int resIndex = EditorPrefs.GetInt(PrefsPrefix + "MeshResIndex", 1);
            if (resolution != null && resIndex >= 0 && resIndex < resolution.choices.Count)
                resolution.SetValueWithoutNotify(resolution.choices[resIndex]);
            gradience?.SetValueWithoutNotify(EditorPrefs.GetBool(PrefsPrefix + "BakeGradience", true));
            textures?.SetValueWithoutNotify(EditorPrefs.GetBool(PrefsPrefix + "BakeTextures", true));
            mesh?.SetValueWithoutNotify(EditorPrefs.GetBool(PrefsPrefix + "BakeMesh", true));

            bake.clicked += () =>
            {
                int currentResIndex = resolution?.index ?? 1;

                EditorPrefs.SetInt(PrefsPrefix + "MeshResIndex", currentResIndex);
                EditorPrefs.SetBool(PrefsPrefix + "BakeGradience", gradience?.value ?? true);
                EditorPrefs.SetBool(PrefsPrefix + "BakeTextures", textures?.value ?? true);
                EditorPrefs.SetBool(PrefsPrefix + "BakeMesh", mesh?.value ?? true);

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
        /// Used by the Quick Tools bake button, so it bakes what the section has ticked.
        /// </remarks>
        /// <param name="body">The body to bake.</param>
        /// <returns>The bake result.</returns>
        internal static BodySurfaceBakerOperation.Result BakeWithPersistedSettings(CoreCelestialBodyData body)
        {
            return BodySurfaceBakerOperation.Bake(body, LoadSettings());
        }

        /// <summary>
        /// Runs a full body-surface bake against <paramref name="body" />, every output, at the artist's persisted
        /// resolution.
        /// </summary>
        /// <remarks>
        /// Used by the surface-bake-drift validator's re-bake fix. It honors the resolution the artist set in the
        /// inspector rather than a hardcoded default, but bakes every output whatever is ticked, since only a full bake
        /// clears the drift.
        /// </remarks>
        /// <param name="body">The body to bake.</param>
        /// <returns>The bake result.</returns>
        internal static BodySurfaceBakerOperation.Result RebakeEverything(CoreCelestialBodyData body)
        {
            return BodySurfaceBakerOperation.Bake(
                body,
                BodySurfaceBakerOperation.Settings.Everything(EditorPrefs.GetInt(PrefsPrefix + "MeshResIndex", 1))
            );
        }

        private static BodySurfaceBakerOperation.Settings LoadSettings() => new()
        {
            MeshResolutionIndex = EditorPrefs.GetInt(PrefsPrefix + "MeshResIndex", 1),
            BakeGradience = EditorPrefs.GetBool(PrefsPrefix + "BakeGradience", true),
            BakeTextures = EditorPrefs.GetBool(PrefsPrefix + "BakeTextures", true),
            BakeMesh = EditorPrefs.GetBool(PrefsPrefix + "BakeMesh", true),
        };
    }
}
