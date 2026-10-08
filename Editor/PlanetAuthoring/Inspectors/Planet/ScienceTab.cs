using System.Collections.Generic;
using Ksp2UnityTools.Editor.PlanetAuthoring.ResourceMaps;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.ScriptableObjects;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Planet
{
    /// <summary>
    /// Builds the Science tab: the body's science region data, and its resources and resource maps.
    /// </summary>
    public static class ScienceTab
    {
        /// <summary>
        /// Builds the tab into <paramref name="content" />.
        /// </summary>
        /// <param name="view">The inspector.</param>
        /// <param name="content">The tab content area.</param>
        public static void Build(PlanetInspectorView view, VisualElement content)
        {
            if (!PlanetInspectorView.CloneTemplate(content, "ScienceTab.uxml"))
                return;

            ScienceRegionData regions = ScienceRegionAssetLocator.FindForBody(view.Context.BodyName);
            EnvironmentTab.AddInspector(content.Q("science-slot"), regions);
            PlanetInspectorView.SetShown(content.Q("science-missing"), regions == null);
            content.Q<Button>("create-science").clicked += () => CreateScienceRegionData(view);

            BuildMineDustColor(content.Q("mine-dust-color-slot"), view.Context.BodyObject);
            BuildResourceMaps(view, content);
        }

        // The Resource Maps editor, bound to the body's authoring asset. Its card previews hold render textures, so it
        // lets them go when the tab closes.
        private static void BuildResourceMaps(PlanetInspectorView view, VisualElement content)
        {
            ResourceMapAuthoring authoring = FindResourceMaps(view.Context.BodyName);
            PlanetInspectorView.SetShown(content.Q("resource-maps-missing"), authoring == null);
            content.Q<Button>("create-resource-maps").clicked += () =>
            {
                string folder = ResourceMapSettings.AuthoringFolder;
                System.IO.Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
                string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{view.Context.BodyName}.asset");
                var created = ScriptableObject.CreateInstance<ResourceMapAuthoring>();
                created.CelestialBodyName = view.Context.BodyName;
                AssetDatabase.CreateAsset(created, path);
                AssetDatabase.SaveAssets();
                view.SetStatus($"Created {path}.");
                view.RebuildTab();
            };

            if (authoring == null)
                return;

            var editor = new Windows.ResourceMapsView(false);
            VisualElement slot = content.Q("resource-maps-slot");
            slot.Add(editor.Root);
            editor.Show(authoring);
            slot.RegisterCallback<DetachFromPanelEvent>(_ => editor.Dispose());
        }

        private static ResourceMapAuthoring FindResourceMaps(string bodyName)
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(ResourceMapAuthoring)}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ResourceMapAuthoring>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && string.Equals(asset.CelestialBodyName, bodyName, System.StringComparison.OrdinalIgnoreCase))
                    return asset;
            }

            return null;
        }

        private static void CreateScienceRegionData(PlanetInspectorView view)
        {
            string folder = view.Context.BodyFolder();
            if (folder == null)
            {
                view.SetStatus("Save the body as a prefab before creating its science region data.");
                return;
            }

            var created = new List<string>();
            Wizards.CreateCelestialBodyWindow.CreateScienceRegionData(view.Context.BodyName, folder, created);
            AssetDatabase.SaveAssets();
            ScienceRegionAssetLocator.InvalidateCache();
            view.SetStatus($"Created {string.Join(", ", created)}.");
            view.RebuildTab();
        }

        // The colour is stored as a Vector4, which a PropertyField would draw as four numbers.
        private static void BuildMineDustColor(VisualElement slot, SerializedObject body)
        {
            SerializedProperty property = body.FindProperty("core.data.MineDustColor");
            if (slot == null || property == null)
                return;

            var field = new ColorField("Mine Dust Color")
            {
                showAlpha = true,
                tooltip = "Color of the dust particles mining throws up on this body.",
                value = (Color)property.vector4Value,
            };
            field.AddToClassList("unity-base-field__aligned");
            field.RegisterValueChangedCallback(evt =>
            {
                property.vector4Value = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
            });
            slot.Add(field);
        }
    }
}
