using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Ocean;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Wires the PQS inspector's Ocean section: the renderer's ocean assets and the material's generated textures.
    /// </summary>
    /// <remarks>
    /// The section's fields live on the PQS renderer and the material's sidecar rather than the PQS, so it binds them
    /// itself, after the inspector has bound the PQS.
    /// </remarks>
    public static class OceanSection
    {
        private static readonly (OceanTextureKind Kind, string Path, string Label)[] TEXTURES =
        {
            (OceanTextureKind.Caustics, "Caustics", "Caustics"),
            (OceanTextureKind.Foam, "Foam", "Foam"),
            (OceanTextureKind.FarFoam, "FarFoam", "Far Foam"),
            (OceanTextureKind.DetailNormal, "DetailNormal", "Detail Normals"),
            (OceanTextureKind.LargeNormal, "LargeNormal", "Large Normals"),
        };

        /// <summary>
        /// Binds the section to <paramref name="pqs" />'s renderer and builds its texture settings.
        /// </summary>
        /// <param name="root">The inspector root containing the section.</param>
        /// <param name="pqs">The inspected PQS.</param>
        public static void Wire(VisualElement root, PQS pqs)
        {
            var hint = root.Q<Label>("ocean-hint");
            var assets = root.Q("ocean-assets");
            var textures = root.Q("ocean-textures");
            PQSRenderer renderer = pqs == null ? null
                : pqs.PQSRenderer != null ? pqs.PQSRenderer : pqs.GetComponentInChildren<PQSRenderer>(true);
            if (hint == null || assets == null || textures == null)
                return;

            if (renderer == null)
            {
                Show(hint, "This PQS has no renderer.");
                assets.style.display = DisplayStyle.None;
                textures.style.display = DisplayStyle.None;
                return;
            }

            var serializedRenderer = new SerializedObject(renderer);
            assets.Bind(serializedRenderer);
            Refresh(root, renderer, pqs);
            RefreshSpectrum(root, renderer);
            // The shoreline goes stale with edits to the terrain or sea level made anywhere, so it is checked on a timer.
            textures.schedule.Execute(() => RefreshShoreline(root, renderer, pqs)).Every(1000);
            // Only the ocean assets, since a running preview writes other renderer fields, such as its camera, every frame.
            assets.TrackPropertyValue(serializedRenderer.FindProperty("OceanWaterMaterial"), _ => Refresh(root, renderer, pqs));
            assets.TrackPropertyValue(serializedRenderer.FindProperty("_oceanSpectrum"), _ => RefreshSpectrum(root, renderer));
        }

        private static void RefreshSpectrum(VisualElement root, PQSRenderer renderer)
        {
            var foldout = root.Q("ocean-spectrum");
            var host = root.Q("ocean-spectrum-inline");
            if (foldout == null || host == null)
                return;

            host.Unbind();
            host.Clear();
            OceanWaveSpectrum spectrum = renderer != null ? renderer._oceanSpectrum : null;
            foldout.style.display = spectrum != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (spectrum != null)
            {
                host.Add(OceanWaveSpectrumInspector.Build(new SerializedObject(spectrum)));
            }
        }

        private static void RefreshShoreline(VisualElement root, PQSRenderer renderer, PQS pqs)
        {
            Material material = renderer != null ? renderer.OceanWaterMaterial : null;
            var status = root.Q<Label>("ocean-shoreline-status");
            if (material == null)
                return;

            string message = OceanShoreline.GetState(BodyResolver.FindBodyIncludingAsset(pqs), material) switch
            {
                ShorelineState.Missing => "No shoreline yet, so waves run right up to the coast. Generate it.",
                ShorelineState.Stale => "The shoreline is out of date: the terrain or Ocean Altitude changed since it was made. Regenerate it.",
                _ => string.Empty,
            };
            Show(status, message);
        }

        // The texture settings belong to whichever material the renderer holds, so they are rebuilt when it changes.
        private static void Refresh(VisualElement root, PQSRenderer renderer, PQS pqs)
        {
            var hint = root.Q<Label>("ocean-hint");
            var textures = root.Q("ocean-textures");
            var settingsHost = root.Q("ocean-texture-settings");
            var generateAll = root.Q<Button>("ocean-generate-all");
            var status = root.Q<Label>("ocean-textures-status");
            var materialFoldout = root.Q("ocean-material");
            var materialHost = root.Q("ocean-material-inline");
            Material material = renderer != null ? renderer.OceanWaterMaterial : null;
            settingsHost.Unbind();
            settingsHost.Clear();
            materialHost?.Clear();
            Show(status, string.Empty);
            if (material == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material)))
            {
                Show(hint, "No ocean on this body. Turn on Has Ocean, then use Add Ocean in Quick Tools.");
                textures.style.display = DisplayStyle.None;
                if (materialFoldout != null)
                {
                    materialFoldout.style.display = DisplayStyle.None;
                }

                return;
            }

            Show(hint, string.Empty);
            textures.style.display = DisplayStyle.Flex;
            if (materialFoldout != null)
            {
                materialFoldout.style.display = DisplayStyle.Flex;
                materialHost?.Add(OceanMaterialInspector.Build(material));
            }
            OceanMaterialAuthoring sidecar = AuthoringSidecars.GetOrCreateOcean(material);
            if (sidecar == null)
                return;

            var serializedSidecar = new SerializedObject(sidecar);
            SerializedProperty colorProperty = serializedSidecar.FindProperty("ScaledOceanColor");
            var color = new PropertyField(colorProperty, "Color From Orbit");
            color.BindProperty(colorProperty);
            settingsHost.Add(color);
            foreach ((OceanTextureKind kind, string path, string label) in TEXTURES)
            {
                SerializedProperty property = serializedSidecar.FindProperty(path);
                var field = new PropertyField(property, label);
                field.BindProperty(property);
                settingsHost.Add(field);

                var generate = new Button { text = $"Generate {label}" };
                generate.AddToClassList("sdk-action-button");
                generate.clicked += () =>
                {
                    serializedSidecar.ApplyModifiedProperties();
                    OceanTextureBaker.TryBake(material, kind, out string message);
                    serializedSidecar.Update();
                    Show(status, message);
                    SceneView.RepaintAll();
                };
                settingsHost.Add(generate);
            }

            // Replaced, not added to, so a rebuild leaves one handler on each button.
            root.Q<Button>("ocean-generate-shoreline").clickable = new Clickable(() =>
            {
                OceanShoreline.TryBake(BodyResolver.FindBodyIncludingAsset(pqs), material, out string message);
                serializedSidecar.Update();
                Show(status, message);
                RefreshShoreline(root, renderer, pqs);
                SceneView.RepaintAll();
            });
            generateAll.clickable = new Clickable(() =>
            {
                serializedSidecar.ApplyModifiedProperties();
                bool baked = OceanTextureBaker.TryBakeAll(material, out string message);
                if (baked && !OceanShoreline.TryBake(BodyResolver.FindBodyIncludingAsset(pqs), material, out string shoreline))
                {
                    message += $" The shoreline was not generated: {shoreline}";
                }

                serializedSidecar.Update();
                Show(status, message);
                RefreshShoreline(root, renderer, pqs);
                SceneView.RepaintAll();
            });
            RefreshShoreline(root, renderer, pqs);
        }

        private static void Show(Label label, string message)
        {
            if (label == null)
                return;

            label.text = message;
            label.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
