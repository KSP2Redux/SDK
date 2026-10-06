using KSP;
using KSP.VolumeCloud;
using Ksp2UnityTools.Editor.PlanetAuthoring.Clouds;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Builds the read-only view of a body's scaled clouds, with the bake that fills them.
    /// </summary>
    /// <remarks>
    /// Everything a <see cref="ScaledCloudConfiguration" /> holds is derived from the body's volumetric configuration,
    /// so it is shown rather than edited. Edit the layers in the cloud configuration on the Local prefab's PQS.
    /// </remarks>
    public static class ScaledCloudsInspector
    {
        /// <summary>
        /// Builds the view.
        /// </summary>
        /// <param name="scaled">The scaled cloud configuration to show, or null when none is wired.</param>
        /// <param name="configuration">The volumetric configuration it mirrors, or null when none is found.</param>
        /// <returns>The inspector root.</returns>
        public static VisualElement Build(ScaledCloudConfiguration scaled, VolumeCloudConfiguration configuration)
        {
            var root = new VisualElement();
            Ksp2UnityToolsStyles.Apply(root);
            if (scaled == null)
            {
                root.Add(new HelpBox("No scaled cloud configuration is wired. Run Add or Refit Clouds.", HelpBoxMessageType.Warning));
                return root;
            }

            var layers = new Foldout { text = "Scaled Layers", value = true };
            layers.AddToClassList("body-inspector-section");
            layers.tooltip = "Derived from the cloud configuration's layers. Edit those and these follow.";
            var list = new VisualElement();
            layers.Add(list);
            root.Add(layers);

            var status = new Label();
            status.AddToClassList("sdk-hint");
            var bake = new Button { text = "Bake Scaled Clouds" };
            bake.AddToClassList("sdk-action-button");
            bake.tooltip = "Bake every enabled layer's scaled clouds with the game's own baker. Each layer needs its distribution map and noise.";
            bake.SetEnabled(configuration != null);
            bake.clicked += () =>
            {
                SetStatus(status, BakeAll(configuration));
                Describe(list, scaled);
            };
            layers.Add(bake);
            layers.Add(status);
            SetStatus(status, configuration == null
                ? "No cloud configuration in the project to bake from."
                : string.Empty);

            Describe(list, scaled);
            return root;
        }

        /// <summary>
        /// Finds the volumetric configuration a body's scaled clouds mirror.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <returns>The configuration, or null when the body has none in the project.</returns>
        public static VolumeCloudConfiguration FindConfiguration(CoreCelestialBodyData body) =>
            body != null ? CloudSetup.FindConfiguration(CloudSetup.FindHelper(body)) : null;

        private static void Describe(VisualElement list, ScaledCloudConfiguration scaled)
        {
            list.Clear();
            if (scaled.scaledCloudLayers == null || scaled.scaledCloudLayers.Count == 0)
            {
                list.Add(new Label("No scaled layers yet.") { tooltip = string.Empty });
                return;
            }

            foreach (ScaledCloudConfiguration.scaledCloudMaterialData layer in scaled.scaledCloudLayers)
            {
                var row = new Foldout { text = $"{layer.CloudLayerName}{(layer.IsEnable ? string.Empty : " (off)")}", value = false };
                row.Add(ReadOnly(new FloatField("Radius (m)") { value = layer.Radius }));
                row.Add(ReadOnly(new UnityEditor.UIElements.ObjectField("Baked Clouds") { objectType = typeof(Cubemap), value = layer.CloudsTexture }));
                row.Add(ReadOnly(new UnityEditor.UIElements.ObjectField("Baked Normals") { objectType = typeof(Cubemap), value = layer.CloudsNormalTexture }));
                row.Add(ReadOnly(new UnityEditor.UIElements.ObjectField("Detail Noise") { objectType = typeof(Texture3D), value = layer.CloudDetailTexture }));
                if (layer.IsEnable && layer.CloudsTexture == null)
                {
                    row.Add(new HelpBox("Not baked, so the game shows no clouds from orbit.", HelpBoxMessageType.Warning));
                }

                list.Add(row);
            }
        }

        private static string BakeAll(VolumeCloudConfiguration configuration)
        {
            int baked = 0;
            for (int i = 0; i < configuration.cumulusList.Count; i++)
            {
                if (!configuration.cumulusList[i].isEnable)
                    continue;

                if (!ScaledCloudBaker.TryBake(configuration, i, out string message))
                    return message;

                baked++;
            }

            return baked > 0 ? $"Baked {baked} layer(s)." : "No enabled layers to bake.";
        }

        private static T ReadOnly<T>(T field) where T : VisualElement
        {
            field.SetEnabled(false);
            field.AddToClassList("unity-base-field__aligned");
            return field;
        }

        private static void SetStatus(Label status, string message)
        {
            status.text = message;
            status.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
