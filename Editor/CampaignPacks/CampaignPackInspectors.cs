using System;
using System.Collections.Generic;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.CampaignPacks
{
    /// <summary>
    /// Custom Unity inspector for campaign pack authoring assets.
    /// </summary>
    [CustomEditor(typeof(CampaignPack))]
    public sealed class CampaignPackInspector : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = CampaignPackInspectorUi.CreateRoot(serializedObject, target);
            var catalog = CampaignPackEditorDatabase.BuildCatalog();

            root.Add(CampaignPackInspectorUi.Autocomplete(serializedObject.FindProperty("id"), "Id", () => catalog.PackIds));
            root.Add(new PropertyField(serializedObject.FindProperty("nameLocKey")));
            root.Add(new PropertyField(serializedObject.FindProperty("descriptionLocKey")));
            root.Add(CampaignPackInspectorUi.Autocomplete(serializedObject.FindProperty("galaxyDefinitionKey"), "Galaxy Definition Key", () => catalog.GalaxyKeys));
            root.Add(new PropertyField(serializedObject.FindProperty("techTreeSet")));
            root.Add(new PropertyField(serializedObject.FindProperty("missionSet")));
            root.Add(new PropertyField(serializedObject.FindProperty("scienceSet")));
            root.Add(new PropertyField(serializedObject.FindProperty("extensions")));
            CampaignPackInspectorUi.AddValidation(root, CampaignPackValidator.Validate((CampaignPack)target, catalog));
            return root;
        }
    }

    /// <summary>
    /// Custom Unity inspector for tech tree set authoring assets.
    /// </summary>
    [CustomEditor(typeof(TechTreeSet))]
    public sealed class TechTreeSetInspector : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = CampaignPackInspectorUi.CreateRoot(serializedObject, target);
            var catalog = CampaignPackEditorDatabase.BuildCatalog();
            root.Add(CampaignPackInspectorUi.Autocomplete(serializedObject.FindProperty("id"), "Id", () => catalog.TechTreeSetIds));
            root.Add(CampaignPackInspectorUi.StringList(serializedObject.FindProperty("techNodeIds"), "Tech Nodes", "Add Tech Node", () => catalog.TechNodeIds));
            CampaignPackInspectorUi.AddValidation(root, CampaignPackValidator.Validate((TechTreeSet)target, catalog));
            return root;
        }
    }

    /// <summary>
    /// Custom Unity inspector for mission set authoring assets.
    /// </summary>
    [CustomEditor(typeof(MissionSet))]
    public sealed class MissionSetInspector : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = CampaignPackInspectorUi.CreateRoot(serializedObject, target);
            var catalog = CampaignPackEditorDatabase.BuildCatalog();
            root.Add(CampaignPackInspectorUi.Autocomplete(serializedObject.FindProperty("id"), "Id", () => catalog.MissionSetIds));
            root.Add(CampaignPackInspectorUi.StringList(serializedObject.FindProperty("missionIds"), "Missions", "Add Mission", () => catalog.MissionIds));
            CampaignPackInspectorUi.AddValidation(root, CampaignPackValidator.Validate((MissionSet)target, catalog));
            return root;
        }
    }

    /// <summary>
    /// Custom Unity inspector for science set authoring assets.
    /// </summary>
    [CustomEditor(typeof(ScienceSet))]
    public sealed class ScienceSetInspector : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = CampaignPackInspectorUi.CreateRoot(serializedObject, target);
            var catalog = CampaignPackEditorDatabase.BuildCatalog();
            root.Add(CampaignPackInspectorUi.Autocomplete(serializedObject.FindProperty("id"), "Id", () => catalog.ScienceSetIds));
            root.Add(CampaignPackInspectorUi.StringList(serializedObject.FindProperty("experimentIds"), "Experiments", "Add Experiment", () => catalog.ExperimentIds));
            root.Add(CampaignPackInspectorUi.StringList(serializedObject.FindProperty("scienceRegionIds"), "Science Regions", "Add Science Region", () => catalog.ScienceRegionIds));
            root.Add(CampaignPackInspectorUi.StringList(serializedObject.FindProperty("discoverableIds"), "Discoverables", "Add Discoverable", () => catalog.DiscoverableIds));
            CampaignPackInspectorUi.AddValidation(root, CampaignPackValidator.Validate((ScienceSet)target, catalog));
            return root;
        }
    }

    /// <summary>
    /// Custom Unity inspector for campaign pack extension authoring assets.
    /// </summary>
    [CustomEditor(typeof(CampaignPackExtension))]
    public sealed class CampaignPackExtensionInspector : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = CampaignPackInspectorUi.CreateRoot(serializedObject, target);
            var catalog = CampaignPackEditorDatabase.BuildCatalog();

            root.Add(CampaignPackInspectorUi.Autocomplete(serializedObject.FindProperty("id"), "Id", () => Array.Empty<string>()));
            root.Add(CampaignPackInspectorUi.Autocomplete(serializedObject.FindProperty("targetCampaignPackId"), "Target Campaign Pack", () => catalog.PackIds));
            root.Add(CampaignPackInspectorUi.Autocomplete(serializedObject.FindProperty("targetTechTreeSetId"), "Target Tech Tree Set", () => catalog.TechTreeSetIds));
            root.Add(CampaignPackInspectorUi.Autocomplete(serializedObject.FindProperty("targetMissionSetId"), "Target Mission Set", () => catalog.MissionSetIds));
            root.Add(CampaignPackInspectorUi.Autocomplete(serializedObject.FindProperty("targetScienceSetId"), "Target Science Set", () => catalog.ScienceSetIds));

            root.Add(CampaignPackInspectorUi.StringList(serializedObject.FindProperty("addTechNodeIds"), "Add Tech Nodes", "Add Tech Node", () => catalog.TechNodeIds));
            root.Add(CampaignPackInspectorUi.StringList(serializedObject.FindProperty("removeTechNodeIds"), "Remove Tech Nodes", "Add Tech Node", () => catalog.TechNodeIds));
            root.Add(CampaignPackInspectorUi.StringList(serializedObject.FindProperty("addMissionIds"), "Add Missions", "Add Mission", () => catalog.MissionIds));
            root.Add(CampaignPackInspectorUi.StringList(serializedObject.FindProperty("removeMissionIds"), "Remove Missions", "Add Mission", () => catalog.MissionIds));
            root.Add(CampaignPackInspectorUi.StringList(serializedObject.FindProperty("addExperimentIds"), "Add Experiments", "Add Experiment", () => catalog.ExperimentIds));
            root.Add(CampaignPackInspectorUi.StringList(serializedObject.FindProperty("removeExperimentIds"), "Remove Experiments", "Add Experiment", () => catalog.ExperimentIds));
            root.Add(CampaignPackInspectorUi.StringList(serializedObject.FindProperty("addScienceRegionIds"), "Add Science Regions", "Add Science Region", () => catalog.ScienceRegionIds));
            root.Add(CampaignPackInspectorUi.StringList(serializedObject.FindProperty("removeScienceRegionIds"), "Remove Science Regions", "Add Science Region", () => catalog.ScienceRegionIds));
            root.Add(CampaignPackInspectorUi.StringList(serializedObject.FindProperty("addDiscoverableIds"), "Add Discoverables", "Add Discoverable", () => catalog.DiscoverableIds));
            root.Add(CampaignPackInspectorUi.StringList(serializedObject.FindProperty("removeDiscoverableIds"), "Remove Discoverables", "Add Discoverable", () => catalog.DiscoverableIds));

            CampaignPackInspectorUi.AddValidation(root, CampaignPackValidator.Validate((CampaignPackExtension)target, catalog));
            return root;
        }
    }

    internal static class CampaignPackInspectorUi
    {
        public static VisualElement CreateRoot(SerializedObject serializedObject, UnityEngine.Object target)
        {
            var root = new VisualElement();
            Ksp2UnityToolsStyles.Apply(root, "/Assets/Windows/DataEditors.uss");

            var row = new VisualElement();
            row.AddToClassList("data-editor-inline-row");
            row.Add(new Button(() => CampaignPackAuthoringActions.BakeToJson(target)) { text = "Bake to JSON" });
            row.Add(new Button(() => CampaignPackBrowserWindow.Open()) { text = "Open Browser" });
            root.Add(row);

            root.Bind(serializedObject);
            return root;
        }

        public static VisualElement Autocomplete(SerializedProperty prop, string label, Func<IEnumerable<string>> source)
        {
            var field = new AutocompleteField(prop, label, source);
            field.AddToClassList("unity-base-field__aligned");
            return field;
        }

        public static VisualElement StringList(
            SerializedProperty arrayProp,
            string title,
            string addText,
            Func<IEnumerable<string>> source)
        {
            var outer = new VisualElement();
            outer.AddToClassList("data-editor-section");

            var header = new VisualElement();
            header.AddToClassList("data-editor-section-header-row");
            var countLabel = new Label();
            countLabel.AddToClassList("data-editor-section-header");
            header.Add(countLabel);
            var add = new Button { text = addText };
            header.Add(add);
            outer.Add(header);

            var list = new VisualElement();
            list.AddToClassList("data-editor-section-list");
            outer.Add(list);

            void Rebuild()
            {
                arrayProp.serializedObject.Update();
                list.Clear();
                countLabel.text = $"{title} ({arrayProp.arraySize})";
                for (var i = 0; i < arrayProp.arraySize; i++)
                {
                    var index = i;
                    var row = new VisualElement();
                    row.AddToClassList("data-editor-inline-row");
                    var field = new AutocompleteField(arrayProp.GetArrayElementAtIndex(index), string.Empty, source)
                    {
                        style =
                        {
                            flexGrow = 1f
                        }
                    };
                    row.Add(field);
                    row.Add(new Button(() =>
                    {
                        arrayProp.serializedObject.Update();
                        arrayProp.DeleteArrayElementAtIndex(index);
                        arrayProp.serializedObject.ApplyModifiedProperties();
                        Rebuild();
                    }) { text = "X" });
                    list.Add(row);
                }
            }

            add.clicked += () =>
            {
                arrayProp.serializedObject.Update();
                arrayProp.arraySize++;
                arrayProp.GetArrayElementAtIndex(arrayProp.arraySize - 1).stringValue = string.Empty;
                arrayProp.serializedObject.ApplyModifiedProperties();
                Rebuild();
            };

            Rebuild();
            return outer;
        }

        public static void AddValidation(VisualElement root, IReadOnlyList<CampaignPackIssue> issues)
        {
            var section = new VisualElement();
            section.AddToClassList("data-editor-section");
            var title = new Label($"Validation ({issues.Count})");
            title.AddToClassList("data-editor-section-header");
            section.Add(title);

            if (issues.Count == 0)
            {
                section.Add(new Label("No issues found."));
            }
            else
            {
                foreach (var issue in issues)
                {
                    section.Add(new Label($"{issue.Severity}: {issue.Message}"));
                }
            }
            root.Add(section);
        }
    }
}
