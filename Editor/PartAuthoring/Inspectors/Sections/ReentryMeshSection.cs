using System.Collections.Generic;
using KSP;
using Ksp2UnityTools.Editor.PartAuthoring.Tools;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections
{
    /// <summary>
    /// UI Toolkit section that drives the reentry-mesh generator.
    /// </summary>
    /// <remarks>
    /// Surfaces the three reentry-mesh actions (Validate / Generate / Remove) plus a thumbnail
    /// grid of the LOD meshes currently on the part. Status text appears in a
    /// <see cref="HelpBox" /> immediately under the button row. Thumbnails are polled via
    /// <see cref="EditorApplication.delayCall" /> while Unity's asset-preview generator finishes.
    /// </remarks>
    public sealed class ReentryMeshSection : VisualElement
    {
        private const string UXML_PATH = "/Assets/Windows/PartAuthoring/Inspectors/Sections/ReentryMeshSection.uxml";
        private const string USS_PATH = "/Assets/Windows/PartAuthoring/Inspectors/Sections/ReentryMeshSection.uss";

        private readonly CorePartData _target;
        private HelpBox _statusBox;
        private Label _previewSummary;
        private VisualElement _previewGrid;
        private bool _previewPollQueued;

        /// <summary>
        /// Creates a reentry-mesh section bound to <paramref name="target" />.
        /// </summary>
        /// <param name="target">The part the section operates on.</param>
        public ReentryMeshSection(CorePartData target)
        {
            _target = target;

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UXML_PATH);
            if (tree == null)
            {
                Add(new Label("Failed to load ReentryMeshSection.uxml"));
                return;
            }
            tree.CloneTree(this);

            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(SDKConfiguration.BasePath + USS_PATH);
            if (sheet != null)
            {
                styleSheets.Add(sheet);
            }

            _statusBox = this.Q<HelpBox>("reentry-status-helpbox");
            _previewSummary = this.Q<Label>("reentry-preview-summary");
            _previewGrid = this.Q<VisualElement>("reentry-preview-grid");

            WireButton("reentry-validate-button", OnValidate);
            WireButton("reentry-generate-button", OnGenerate);
            WireButton("reentry-remove-button", OnRemove);

            RefreshPreview();
            HideStatus();
            RegisterCallback<DetachFromPanelEvent>(_ => Cleanup());
        }

        private void WireButton(string name, System.Action onClick)
        {
            var btn = this.Q<Button>(name);
            if (btn != null)
            {
                btn.clicked += onClick;
            }
        }

        private void OnValidate()
        {
            ShowStatus(ReentryMeshBaker.Validate(_target), HelpBoxMessageType.Info);
        }

        private void OnGenerate()
        {
            ShowStatus(ReentryMeshBaker.Bake(_target), HelpBoxMessageType.Info);
            RefreshPreview();
        }

        private void OnRemove()
        {
            ShowStatus(ReentryMeshBaker.RemoveGenerated(_target), HelpBoxMessageType.Info);
            RefreshPreview();
        }

        private void ShowStatus(string text, HelpBoxMessageType messageType)
        {
            if (_statusBox == null)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                HideStatus();
                return;
            }
            _statusBox.text = text;
            _statusBox.messageType = messageType;
            _statusBox.style.display = DisplayStyle.Flex;
        }

        private void HideStatus()
        {
            if (_statusBox == null)
            {
                return;
            }
            _statusBox.text = string.Empty;
            _statusBox.style.display = DisplayStyle.None;
        }

        private void RefreshPreview()
        {
            if (_previewGrid == null || _previewSummary == null)
            {
                return;
            }

            _previewGrid.Clear();

            var entries = CollectEntries(_target?.gameObject);
            if (entries.Count == 0)
            {
                _previewSummary.text = "No reentry mesh renderer meshes found on this part.";
                return;
            }

            var groupNames = new HashSet<string>();
            foreach (var entry in entries)
            {
                groupNames.Add(entry.GroupName);
            }
            _previewSummary.text =
                $"Showing {entries.Count} reentry mesh preview{(entries.Count == 1 ? string.Empty : "s")} across {groupNames.Count} group{(groupNames.Count == 1 ? string.Empty : "s")}.";

            bool anyPending = false;
            foreach (var entry in entries)
            {
                _previewGrid.Add(BuildCell(entry, out bool pending));
                anyPending |= pending;
            }

            if (anyPending)
            {
                QueuePreviewPoll();
            }
        }

        private VisualElement BuildCell(PreviewEntry entry, out bool pendingPreview)
        {
            pendingPreview = false;
            var cell = new VisualElement { name = "reentry-preview-cell" };
            cell.AddToClassList("reentry-preview-cell");

            var thumb = new VisualElement { name = "reentry-preview-thumb" };
            thumb.AddToClassList("reentry-preview-thumb");
            thumb.userData = entry.Mesh;
            thumb.RegisterCallback<ClickEvent>(_ =>
            {
                if (entry.Mesh == null)
                {
                    return;
                }
                Selection.activeObject = entry.Mesh;
                EditorGUIUtility.PingObject(entry.Mesh);
            });
            cell.Add(thumb);

            Texture2D preview = AssetPreview.GetAssetPreview(entry.Mesh);
            if (preview == null)
            {
                preview = AssetPreview.GetMiniThumbnail(entry.Mesh);
                pendingPreview = true;
            }
            if (preview != null)
            {
                thumb.style.backgroundImage = new StyleBackground(preview);
            }

            var label = new Label($"{entry.GroupName}\nLOD {entry.LodIndex}  {entry.Mesh.vertexCount}v");
            label.AddToClassList("reentry-preview-label");
            cell.Add(label);

            return cell;
        }

        private void QueuePreviewPoll()
        {
            if (_previewPollQueued)
            {
                return;
            }
            _previewPollQueued = true;
            EditorApplication.delayCall -= ProcessPreviewPoll;
            EditorApplication.delayCall += ProcessPreviewPoll;
        }

        private void ProcessPreviewPoll()
        {
            _previewPollQueued = false;
            if (_target == null || _previewGrid == null)
            {
                return;
            }
            RefreshPreview();
        }

        private static List<PreviewEntry> CollectEntries(GameObject root)
        {
            var entries = new List<PreviewEntry>();
            if (root == null)
            {
                return entries;
            }
            foreach (KSP.VFX.Reentry.ReentryMesh reentryMesh in root.GetComponentsInChildren<KSP.VFX.Reentry.ReentryMesh>(true))
            {
                if (reentryMesh == null || reentryMesh.ReentryRenderers == null)
                {
                    continue;
                }
                string groupName = reentryMesh.gameObject.name;
                for (int i = 0; i < reentryMesh.ReentryRenderers.Length; i++)
                {
                    Renderer renderer = reentryMesh.ReentryRenderers[i];
                    if (renderer == null || !renderer.TryGetComponent(out MeshFilter meshFilter) ||
                        meshFilter.sharedMesh == null)
                    {
                        continue;
                    }
                    entries.Add(new PreviewEntry
                    {
                        GroupName = groupName,
                        LodIndex = i,
                        Mesh = meshFilter.sharedMesh,
                    });
                }
            }
            return entries;
        }

        private void Cleanup()
        {
            EditorApplication.delayCall -= ProcessPreviewPoll;
        }

        private struct PreviewEntry
        {
            public string GroupName;
            public int LodIndex;
            public Mesh Mesh;
        }
    }
}
