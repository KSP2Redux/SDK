#if REDUX
using System.Collections.Generic;
using Ksp2UnityTools.Editor.Localization.Widgets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.Localization.Windows
{
    /// <summary>
    /// Dev-only sandbox for iterating on <see cref="LocTableView" /> ergonomics.
    /// </summary>
    /// <remarks>
    /// Mock data only. No CSV plumbing. Gated behind <c>REDUX</c> so it does not show for end users
    /// authoring mods with the SDK. Used to verify drag-resize, hide-via-dropdown, frozen-column
    /// behavior, and EditorPrefs persistence in isolation from CSV I/O.
    /// </remarks>
    public sealed class LocTableViewSandboxWindow : EditorWindow
    {
        private const string SandboxPersistenceKey = "__sandbox__";

        private static readonly string[] LanguageColumns =
        {
            "English", "French", "Italian", "German", "Spanish",
            "Japanese", "Korean", "Polish", "Russian",
            "Chinese-Simplified [zh-CN]", "Portuguese-Brazil [pt-BR]",
            "Chinese-Traditional [zh-TW]", "Pirate English [en-PI]",
            "Serbian", "Swedish", "Romanian",
        };

        private List<LocColumnSpec> _columns;
        private List<LocRow> _rows;
        private LocTableView _table;
        private VisualElement _tableHost;

        /// <summary>
        /// Opens or focuses the sandbox window populated with mock localization data.
        /// </summary>
        [MenuItem("Modding/Localization/(dev) LocTableView Sandbox")]
        public static void Open()
        {
            var window = GetWindow<LocTableViewSandboxWindow>();
            window.titleContent = new GUIContent("LocTable Sandbox");
            window.minSize = new Vector2(600, 300);
            window.Show();
        }

        private void CreateGUI()
        {
            rootVisualElement.style.flexGrow = 1f;

            var toolbar = BuildToolbar();
            rootVisualElement.Add(toolbar);

            _tableHost = new VisualElement();
            _tableHost.style.flexGrow = 1f;
            rootVisualElement.Add(_tableHost);

            BuildMockData();
            MountTable();
        }

        private VisualElement BuildToolbar()
        {
            var bar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    paddingTop = 4,
                    paddingBottom = 4,
                    paddingLeft = 6,
                    paddingRight = 6,
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0, 0, 0, 0.35f),
                },
            };

            var columnsBtn = new Button { text = "Columns" };
            columnsBtn.clicked += () => _table?.OpenColumnsMenu(columnsBtn);
            bar.Add(columnsBtn);

            var resetBtn = new Button(OnResetLayoutClicked) { text = "Reset layout" };
            resetBtn.tooltip = "Clear the sandbox EditorPrefs key and rebuild with default widths and visibility.";
            bar.Add(resetBtn);

            var addRowsBtn = new Button(OnAddRowsClicked) { text = "Add 500 rows" };
            addRowsBtn.tooltip = "Appends 500 synthetic rows to stress-test ListView virtualization.";
            bar.Add(addRowsBtn);

            var toggleFrozenBtn = new Button(OnToggleFrozenClicked) { text = "Toggle frozen" };
            toggleFrozenBtn.tooltip = "Flips the Key column's Frozen flag and rebuilds the table.";
            bar.Add(toggleFrozenBtn);

            return bar;
        }

        private void BuildMockData()
        {
            _columns = new List<LocColumnSpec>
            {
                BuildColumn("Key", frozen: true),
                BuildColumn("Type"),
                BuildColumn("Desc"),
            };
            foreach (var lang in LanguageColumns)
            {
                _columns.Add(BuildColumn(lang));
            }
            _columns.Add(BuildColumn("$Context"));
            _columns.Add(BuildColumn("$Status"));

            _rows = new List<LocRow>();
            AppendMockRows(10);

            // One Japanese row so CJK widths get exercised during ergonomic iteration.
            if (_rows.Count > 0)
            {
                _rows[0].Set("Key", "Science/Regions/MinmusBalancingRock");
                _rows[0].Set("Type", "Text");
                _rows[0].Set("Desc", "Science region on body Minmus");
                _rows[0].Set("English", "Teetering Rock");
                _rows[0].Set("Japanese", "シーソー・ロック");
                _rows[0].Set("Korean", "흔들 바위");
                _rows[0].Set("Russian", "Падающая скала");
                _rows[0].Set("$Status", "Localized");
            }
        }

        private static LocColumnSpec BuildColumn(string id, bool frozen = false)
        {
            return new LocColumnSpec
            {
                Id = id,
                HeaderLabel = id,
                DefaultWidth = LocColumnSpecDefaults.WidthFor(id),
                MinWidth = LocColumnSpecDefaults.MinWidthFor(id),
                Frozen = frozen,
            };
        }

        private void AppendMockRows(int count)
        {
            var baseIdx = _rows.Count;
            for (var i = 0; i < count; i++)
            {
                var n = baseIdx + i + 1;
                var row = new LocRow();
                row.Set("Key", "Sandbox/Row/" + n);
                row.Set("Type", "Text");
                row.Set("Desc", "Mock row " + n);
                row.Set("English", "Sample text " + n);
                row.Set("French", "Texte " + n);
                row.Set("German", "Beispieltext " + n);
                row.Set("$Status", "NotLocalized");
                _rows.Add(row);
            }
        }

        private void MountTable()
        {
            _tableHost.Clear();
            _table = new LocTableView(_columns, _rows, SandboxPersistenceKey);
            _table.style.flexGrow = 1f;
            _tableHost.Add(_table);
        }

        private void OnResetLayoutClicked()
        {
            EditorPrefs.DeleteKey("Redux.Localization.TableLayout." + SandboxPersistenceKey);
            foreach (var col in _columns)
            {
                col.CurrentWidth = col.DefaultWidth;
                col.Hidden = false;
            }
            MountTable();
        }

        private void OnAddRowsClicked()
        {
            AppendMockRows(500);
            _table?.Refresh();
        }

        private void OnToggleFrozenClicked()
        {
            var keyCol = _columns.Find(c => c.Id == "Key");
            if (keyCol == null) return;
            keyCol.Frozen = !keyCol.Frozen;
            MountTable();
        }
    }
}
#endif
