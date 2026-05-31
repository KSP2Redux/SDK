using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.CampaignPacks
{
    /// <summary>
    /// Editor window for browsing campaign packs, previewing effective content, and baking authoring JSON.
    /// </summary>
    public sealed class CampaignPackBrowserWindow : EditorWindow
    {
        private VisualElement? _packList;
        private VisualElement? _details;
        private CampaignPack? _selected;

        /// <summary>
        /// Opens the Campaign Packs browser window.
        /// </summary>
        [MenuItem("Modding/Campaign Packs/Browser")]
        public static void Open()
        {
            var window = GetWindow<CampaignPackBrowserWindow>();
            window.titleContent = new UnityEngine.GUIContent("Campaign Packs");
            window.minSize = new UnityEngine.Vector2(720, 420);
            window.Refresh();
        }

        private void CreateGUI()
        {
            Ksp2UnityToolsStyles.Apply(rootVisualElement, "/Assets/Windows/DataEditors.uss");

            var toolbar = new VisualElement();
            toolbar.AddToClassList("data-editor-inline-row");
            toolbar.Add(new Button(Refresh) { text = "Refresh" });
            toolbar.Add(new Button(() =>
            {
                if (_selected != null) CampaignPackAuthoringActions.BakeAllForPack(_selected);
            }) { text = "Bake Selected Pack" });
            rootVisualElement.Add(toolbar);

            var split = new TwoPaneSplitView(0, 240, TwoPaneSplitViewOrientation.Horizontal);
            _packList = new ScrollView();
            _details = new ScrollView();
            split.Add(_packList);
            split.Add(_details);
            rootVisualElement.Add(split);

            Refresh();
        }

        private void Refresh()
        {
            if (_packList == null || _details == null) return;
            var packs = CampaignPackEditorDatabase.FindCampaignPacks()
                .OrderBy(p => p.id)
                .ToList();

            if (_selected == null || !packs.Contains(_selected))
            {
                _selected = packs.FirstOrDefault();
            }

            _packList.Clear();
            foreach (var pack in packs)
            {
                var captured = pack;
                var button = new Button(() =>
                {
                    _selected = captured;
                    DrawDetails();
                })
                {
                    text = string.IsNullOrWhiteSpace(pack.id) ? pack.name : pack.id
                };
                _packList.Add(button);
            }

            if (packs.Count == 0)
            {
                _packList.Add(new Label("No CampaignPack assets found."));
            }

            DrawDetails();
        }

        private void DrawDetails()
        {
            var details = _details;
            if (details == null) return;

            details.Clear();
            if (_selected == null)
            {
                details.Add(new Label("Select a campaign pack."));
                return;
            }

            var catalog = CampaignPackEditorDatabase.BuildCatalog();
            var packs = CampaignPackEditorDatabase.FindCampaignPacks();
            var techSets = CampaignPackEditorDatabase.FindTechTreeSets();
            var missionSets = CampaignPackEditorDatabase.FindMissionSets();
            var scienceSets = CampaignPackEditorDatabase.FindScienceSets();
            var extensions = CampaignPackEditorDatabase.FindExtensions();
            var effective = CampaignPackResolver.Resolve(_selected, extensions);
            var issues = CampaignPackValidator.ValidateAll(packs, techSets, missionSets, scienceSets, extensions, catalog)
                .Where(issue => string.IsNullOrWhiteSpace(issue.SourceId) ||
                    issue.SourceId == _selected.id ||
                    effective.AppliedExtensionIds.Contains(issue.SourceId) ||
                    issue.SourceId == (_selected.techTreeSet != null ? _selected.techTreeSet.id : null) ||
                    issue.SourceId == (_selected.missionSet != null ? _selected.missionSet.id : null) ||
                    issue.SourceId == (_selected.scienceSet != null ? _selected.scienceSet.id : null))
                .ToList();

            AddHeader(details, _selected.id);
            AddValue(details, "Name Loc Key", _selected.nameLocKey);
            AddValue(details, "Description Loc Key", _selected.descriptionLocKey);
            AddPingValue(details, "Galaxy", effective.GalaxyDefinitionKey);
            AddValue(details, "Tech Tree Set", _selected.techTreeSet != null ? _selected.techTreeSet.id : "");
            AddValue(details, "Mission Set", _selected.missionSet != null ? _selected.missionSet.id : "");
            AddValue(details, "Science Set", _selected.scienceSet != null ? _selected.scienceSet.id : "");

            AddHeader(details, "Applied Extensions");
            AddList(details, effective.AppliedExtensionIds);

            AddHeader(details, "Effective Tech Nodes");
            AddPingList(details, effective.TechNodeIds);

            AddHeader(details, "Effective Missions");
            AddPingList(details, effective.MissionIds);

            AddHeader(details, "Effective Experiments");
            AddPingList(details, effective.ExperimentIds);

            AddHeader(details, "Effective Science Regions");
            AddPingList(details, effective.ScienceRegionIds);

            AddHeader(details, "Effective Discoverables");
            AddPingList(details, effective.DiscoverableIds);

            AddHeader(details, $"Validation ({issues.Count})");
            if (issues.Count == 0)
            {
                details.Add(new Label("No issues found."));
            }
            else
            {
                foreach (var issue in issues)
                {
                    details.Add(new Label($"{issue.Severity}: {issue.Message}"));
                }
            }
        }

        private static void AddHeader(VisualElement parent, string text)
        {
            var header = new Label(string.IsNullOrWhiteSpace(text) ? "(empty)" : text);
            header.AddToClassList("data-editor-section-header");
            parent.Add(header);
        }

        private static void AddValue(VisualElement parent, string label, string value)
        {
            parent.Add(new Label($"{label}: {(string.IsNullOrWhiteSpace(value) ? "(none)" : value)}"));
        }

        private static void AddPingValue(VisualElement parent, string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("data-editor-inline-row");
            row.Add(new Label($"{label}: {(string.IsNullOrWhiteSpace(value) ? "(none)" : value)}"));
            row.Add(new Button(() => CampaignPackEditorDatabase.PingAssetForId(value)) { text = "Ping" });
            parent.Add(row);
        }

        private static void AddList(VisualElement parent, IReadOnlyList<string>? values)
        {
            if (values == null || values.Count == 0)
            {
                parent.Add(new Label("(empty)"));
                return;
            }

            foreach (var value in values)
            {
                parent.Add(new Label(value));
            }
        }

        private static void AddPingList(VisualElement parent, IReadOnlyList<string>? values)
        {
            if (values == null || values.Count == 0)
            {
                parent.Add(new Label("(empty)"));
                return;
            }

            foreach (var value in values)
            {
                AddPingValue(parent, "", value);
            }
        }
    }
}
