using System.IO;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Globalization;
using KSP;
using KSP.IO;
using KSP.Modules;
using KSP.OAB;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Redux.Assets.PartIconRendering;
using Redux.VFX.ReentryMeshGeneration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ksp2UnityTools.Editor.CustomEditors
{
    [CustomEditor(typeof(CorePartData))]
    public class PartEditor : UnityEditor.Editor
    {
        private static bool _initialized = false;
        private static readonly Color ComColor = new(Color.yellow.r, Color.yellow.g, Color.yellow.b, 0.5f);

        // private static string _jsonPath = "%NAME%.json";

        private static bool _centerOfMassGizmos = true;
        private static bool _centerOfLiftGizmos = true;
        private static bool _attachNodeGizmos = true;


        public static bool DragCubeGizmos = true;

        private bool _iconPreviewFoldout = true;
        private bool _iconPreviewCameraFoldout;
        private bool _iconPreviewLightingFoldout;
        private bool _iconPreviewColorsFoldout;
        private bool _iconPreviewOutlineFoldout;
        private PartIconCameraPreset _iconPreviewPreset = PartIconCameraPreset.Diagonal;
        private PartIconRenderSettings _iconPreviewSettings;
        private Texture2D _iconPreviewTexture;
        private bool _iconPreviewRefreshQueued;
        private bool _reentryMeshFoldout = true;
        private bool _reentryMeshPreviewFoldout = true;
        private string _reentryMeshStatus;

        // Just initialize all the conversion stuff
        private static void Initialize()
        {
            IOProvider.Init();
            _initialized = true;
        }

        private static PersistentDictionary _prefabAddressOverrides;

        private static PersistentDictionary PrefabAddressOverrides => _prefabAddressOverrides ??=
            KSP2UnityToolsManager.GetDictionary("PrefabAddressOverrides");

        private static PersistentDictionary _iconAddressOverrides;

        private static PersistentDictionary IconAddressOverrides =>
            _iconAddressOverrides ??= KSP2UnityToolsManager.GetDictionary("IconAddressOverrides");


        private GameObject TargetObject => TargetData.gameObject;
        private CorePartData TargetData => target as CorePartData;
        private PartCore TargetCore => TargetData.Core;

        private PartIconRenderSettings IconPreviewSettings =>
            _iconPreviewSettings ??= PartIconRenderSettings.CreateDefault();

        private void OnDisable()
        {
            EditorApplication.delayCall -= ProcessQueuedIconPreviewRefresh;
            DestroyIconPreview();
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DrawIconPreview();
            DrawReentryMeshGenerator();
            GUILayout.Label("Attach Node Settings");
            if (GUILayout.Button("Auto Generate AttachNodes"))
            {
                TargetCore.data.attachNodes.Clear();
                // Attach node naming scheme
                foreach (AttachmentNode attachmentNode in TargetObject.GetComponentsInChildren<AttachmentNode>())
                {
                    GameObject obj = attachmentNode.gameObject;
                    Vector3 pos = TargetObject.transform.InverseTransformPoint(obj.transform.position);
                    Vector3 dir = Quaternion.Euler(
                        TargetObject.transform.InverseTransformDirection(obj.transform.rotation.eulerAngles)
                    ) * Vector3.forward;
                    var newDefinition = new AttachNodeDefinition
                    {
                        nodeID = obj.name,
                        NodeSymmetryGroupID = attachmentNode.nodeSymmetryGroupID,
                        nodeType = attachmentNode.nodeType,
                        attachMethod = attachmentNode.attachMethod,
                        IsMultiJoint = attachmentNode.isMultiJoint,
                        MultiJointMaxJoint = attachmentNode.multiJointMaxJoint,
                        MultiJointRadiusOffset = attachmentNode.multiJointRadiusOffset,
                        position = pos,
                        orientation = dir,
                        size = attachmentNode.size,
                        sizeKey = PartSizeRegistry.GetAttachNodeSizeKey(attachmentNode.sizeKey, attachmentNode.size),
                        visualSize = attachmentNode.visualSize,
                        angularStrengthMultiplier = attachmentNode.angularStrengthMultiplier,
                        contactArea = attachmentNode.contactArea,
                        overrideDragArea = attachmentNode.overrideDragArea,
                        isCompoundJoint = attachmentNode.isCompoundJoint
                    };
                    TargetCore.data.attachNodes.Add(newDefinition);
                }

                EditorUtility.SetDirty(target);
            }

            GUILayout.Label("Gizmo Settings", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _centerOfMassGizmos = EditorGUILayout.Toggle("CoM gizmos", _centerOfMassGizmos);
            _centerOfLiftGizmos = EditorGUILayout.Toggle("CoL gizmos", _centerOfLiftGizmos);
            _attachNodeGizmos = EditorGUILayout.Toggle("Attach Node Gizmos", _attachNodeGizmos);
            DragCubeGizmos = EditorGUILayout.Toggle("Drag Cube Gizmos", DragCubeGizmos);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(target);
            }

            // GUILayout.Label("Address Overrides (Only Works With Patch Manager)", EditorStyles.boldLabel);
            // var prefabAddress = "%NAME%.prefab";
            // var iconAddress = "%NAME%.png";
            // if (PrefabAddressOverrides.TryGetValue(TargetObject.name, out var newPrefabAddress))
            //     prefabAddress = newPrefabAddress;
            // if (IconAddressOverrides.TryGetValue(TargetObject.name, out var newIconAddress))
            //     iconAddress = newIconAddress;
            // PrefabAddressOverrides[TargetObject.name] =
            //     prefabAddress = EditorGUILayout.TextField("Prefab Address", prefabAddress);
            // IconAddressOverrides[TargetObject.name] =
            //     iconAddress = EditorGUILayout.TextField("Icon Address", iconAddress);

            GUILayout.Label("Part Saving", EditorStyles.boldLabel);
            // var patchPath = "plugin_template/patches/%NAME%.patch";
            string prefabPath = PathUtils.GetPrefabOrAssetPath(TargetData, TargetObject);
            if (string.IsNullOrEmpty(prefabPath))
            {
                return;
            }

            string jsonPath = Path.GetDirectoryName(prefabPath) + $"/{TargetData.name}.json";
            if (GUILayout.Button("Save Part JSON"))
            {
                SavePartJson(jsonPath);
            }
        }

        private void SavePartJson(string jsonPath)
        {
            if (!_initialized)
            {
                Initialize();
            }

            if (TargetCore == null)
            {
                return;
            }

            // Clear out the serialized part modules and reserialize them
            TargetCore.data.serializedPartModules.Clear();
            foreach (Component child in TargetObject.GetComponents<Component>())
            {
                if (child is not PartBehaviourModule partBehaviourModule)
                {
                    continue;
                }

                MethodInfo addMethod =
                    child.GetType().GetMethod("AddDataModules", BindingFlags.Instance | BindingFlags.NonPublic) ??
                    child.GetType().GetMethod("AddDataModules", BindingFlags.Instance | BindingFlags.Public);
                addMethod?.Invoke(child, new object[] { });
                foreach (ModuleData data in partBehaviourModule.DataModules.Values)
                {
                    MethodInfo rebuildMethod =
                        data.GetType()
                            .GetMethod(
                                "RebuildDataContext",
                                BindingFlags.Instance | BindingFlags.NonPublic
                            ) ?? data.GetType()
                            .GetMethod("RebuildDataContext", BindingFlags.Instance | BindingFlags.Public);
                    rebuildMethod?.Invoke(data, new object[] { });
                }

                TargetCore.data.serializedPartModules.Add(new SerializedPartModule(partBehaviourModule, false));
            }

            string json = IOProvider.ToJson(TargetCore);
            JObject jObject = JObject.Parse(json);
            json = jObject.ToString(Formatting.Indented);
            string path = jsonPath.Replace("%NAME%", TargetCore.data.partName);
            string directoryName = new FileInfo(path).DirectoryName;
            Directory.CreateDirectory(directoryName);
            File.WriteAllText($"{path}", json);
            AssetDatabase.ImportAsset(path);
            bool madeAddressable = false;
            if (KSP2UnityTools.FindParentMod(target) is { } mod)
            {
                madeAddressable = true;
                AddressablesTools.MakeAddressable(
                    mod.partsGroup,
                    path,
                    $"{TargetCore.data.partName}.json",
                    "parts_data"
                );
            }

            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "Part Exported",
                !madeAddressable
                    ? $"Json is at: {path}, you need to manually make it addressable"
                    : $"Json is at: {path}",
                "ok"
            );
        }

        private void SavePartIcon(string prefabPath)
        {
            if (!_initialized)
            {
                Initialize();
            }

            if (TargetCore == null)
            {
                return;
            }

            string partName = GetTargetPartName();
            PartIconRenderSettings settings = IconPreviewSettings.Clone();
            settings.backgroundColor = new Color(0f, 0f, 0f, 0f);
            settings.supersampleScale = 2;
            settings.makeTextureNoLongerReadable = false;

            Texture2D texture = PartIconRenderer.RenderTexture2D(
                partName + "-editor-saved-icon",
                TargetCore,
                TargetObject,
                settings
            );
            if (texture == null)
            {
                EditorUtility.DisplayDialog(
                    "Icon Export Failed",
                    "No renderable meshes were found for this part.",
                    "ok"
                );
                return;
            }

            string path = Path.Combine(Path.GetDirectoryName(prefabPath), $"{partName}_icon.png")
                .Replace('\\', '/');
            try
            {
                Directory.CreateDirectory(new FileInfo(path).DirectoryName);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(path);
            ConfigureIconTextureImporter(path);
            bool madeAddressable = false;
            if (KSP2UnityTools.FindParentMod(target) is { } mod)
            {
                madeAddressable = true;
                AddressablesTools.MakeAddressable(
                    mod.partsGroup,
                    path,
                    $"{partName}_icon.png"
                );
            }

            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "Part Icon Exported",
                !madeAddressable
                    ? $"Icon is at: {path}, you need to manually make it addressable"
                    : $"Icon is at: {path}",
                "ok"
            );
        }

        private string GetTargetPartName()
        {
            return !string.IsNullOrWhiteSpace(TargetCore?.data?.partName)
                ? TargetCore.data.partName
                : TargetObject.name;
        }

        private static void ConfigureIconTextureImporter(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        private void DrawIconPreview()
        {
            GUILayout.Space(8f);
            _iconPreviewFoldout = EditorGUILayout.Foldout(_iconPreviewFoldout, "Part Icon Preview", true);
            if (!_iconPreviewFoldout)
            {
                return;
            }

            PartIconRenderSettings settings = IconPreviewSettings;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (_iconPreviewTexture == null)
                {
                    QueueIconPreviewRefresh();
                }

                Rect previewRect = GUILayoutUtility.GetRect(144f, 144f, GUILayout.ExpandWidth(false));
                if (_iconPreviewTexture != null)
                {
                    EditorGUI.DrawPreviewTexture(previewRect, _iconPreviewTexture, null, ScaleMode.ScaleToFit);
                }
                else
                {
                    EditorGUI.HelpBox(previewRect, "No renderable meshes found.", MessageType.Info);
                }

                bool changed = DrawIconPreviewControls(settings);

                string prefabPath = PathUtils.GetPrefabOrAssetPath(TargetData, TargetObject);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(prefabPath)))
                    {
                        if (GUILayout.Button("Save Part Icon"))
                        {
                            SavePartIcon(prefabPath);
                        }
                    }

                    if (GUILayout.Button("Reset"))
                    {
                        DestroyIconPreview();
                        _iconPreviewSettings = PartIconRenderSettings.CreateDefault();
                        _iconPreviewPreset = PartIconCameraPreset.Diagonal;
                        QueueIconPreviewRefresh();
                    }
                }

                if (changed)
                {
                    QueueIconPreviewRefresh();
                }
            }
        }

        private bool DrawIconPreviewControls(PartIconRenderSettings settings)
        {
            bool changed = false;

            EditorGUI.BeginChangeCheck();
            var preset = (PartIconCameraPreset)EditorGUILayout.EnumPopup("Preset", _iconPreviewPreset);
            if (EditorGUI.EndChangeCheck())
            {
                _iconPreviewPreset = preset;
                settings.ApplyPreset(preset);
                changed = true;
            }

            EditorGUI.BeginChangeCheck();
            settings.cameraPadding = EditorGUILayout.Slider("Padding", settings.cameraPadding, 0.6f, 3f);
            changed |= EditorGUI.EndChangeCheck();

            _iconPreviewCameraFoldout = DrawIconPreviewSectionFoldout(_iconPreviewCameraFoldout, "Camera");
            if (_iconPreviewCameraFoldout)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUI.BeginChangeCheck();
                    settings.cameraYawDegrees = EditorGUILayout.Slider("Yaw", settings.cameraYawDegrees, -180f, 180f);
                    settings.cameraPitchDegrees = EditorGUILayout.Slider(
                        "Pitch",
                        settings.cameraPitchDegrees,
                        -80f,
                        80f
                    );
                    settings.cameraOrbitDegrees = EditorGUILayout.Slider(
                        "Roll",
                        settings.cameraOrbitDegrees,
                        -180f,
                        180f
                    );
                    changed |= EditorGUI.EndChangeCheck();
                }
            }

            _iconPreviewLightingFoldout = DrawIconPreviewSectionFoldout(_iconPreviewLightingFoldout, "Lighting");
            if (_iconPreviewLightingFoldout)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUI.BeginChangeCheck();
                    settings.frontKeyIntensity = EditorGUILayout.Slider(
                        "Front Key",
                        settings.frontKeyIntensity,
                        0f,
                        2f
                    );
                    settings.frontKeyDirection = EditorGUILayout.Vector3Field(
                        "Front Key Direction",
                        settings.frontKeyDirection
                    );
                    settings.topKeyIntensity = EditorGUILayout.Slider("Top Key", settings.topKeyIntensity, 0f, 2f);
                    settings.topKeyDirection = EditorGUILayout.Vector3Field(
                        "Top Key Direction",
                        settings.topKeyDirection
                    );
                    settings.rimIntensity = EditorGUILayout.Slider("Rim", settings.rimIntensity, 0f, 1.5f);
                    settings.rimDirection = EditorGUILayout.Vector3Field("Rim Direction", settings.rimDirection);
                    settings.fillIntensity = EditorGUILayout.Slider("Uniform Fill", settings.fillIntensity, 0f, 1.5f);
                    settings.keyLightSpreadDegrees = EditorGUILayout.Slider(
                        "Key Softness",
                        settings.keyLightSpreadDegrees,
                        0f,
                        35f
                    );
                    changed |= EditorGUI.EndChangeCheck();
                }
            }

            _iconPreviewColorsFoldout = DrawIconPreviewSectionFoldout(_iconPreviewColorsFoldout, "Colors");
            if (_iconPreviewColorsFoldout)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUI.BeginChangeCheck();
                    settings.applyModuleColorPalette = EditorGUILayout.Toggle(
                        "Use Icon Palette",
                        settings.applyModuleColorPalette
                    );
                    using (new EditorGUI.DisabledScope(!settings.applyModuleColorPalette))
                    {
                        settings.moduleColorBase = EditorGUILayout.ColorField("Base", settings.moduleColorBase);
                        settings.moduleColorAccent = EditorGUILayout.ColorField("Accent", settings.moduleColorAccent);
                    }

                    changed |= EditorGUI.EndChangeCheck();
                }
            }

            _iconPreviewOutlineFoldout = DrawIconPreviewSectionFoldout(_iconPreviewOutlineFoldout, "Outline");
            if (_iconPreviewOutlineFoldout)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUI.BeginChangeCheck();
                    settings.addOutline = EditorGUILayout.Toggle("Enabled", settings.addOutline);
                    using (new EditorGUI.DisabledScope(!settings.addOutline))
                    {
                        settings.outlineRadius = EditorGUILayout.IntSlider("Radius", settings.outlineRadius, 0, 12);
                    }

                    changed |= EditorGUI.EndChangeCheck();
                }
            }

            return changed;
        }

        private static bool DrawIconPreviewSectionFoldout(bool value, string label)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(12f);
                return EditorGUILayout.Foldout(value, label, true);
            }
        }

        private void QueueIconPreviewRefresh()
        {
            if (_iconPreviewRefreshQueued)
            {
                return;
            }

            _iconPreviewRefreshQueued = true;
            EditorApplication.delayCall -= ProcessQueuedIconPreviewRefresh;
            EditorApplication.delayCall += ProcessQueuedIconPreviewRefresh;
        }

        private void ProcessQueuedIconPreviewRefresh()
        {
            _iconPreviewRefreshQueued = false;
            if (target == null)
            {
                return;
            }

            RefreshIconPreview();
            Repaint();
        }

        private void RefreshIconPreview()
        {
            DestroyIconPreview();
            if (TargetObject == null || TargetCore == null)
            {
                return;
            }

            string partName = !string.IsNullOrWhiteSpace(TargetCore.data?.partName)
                ? TargetCore.data.partName
                : TargetObject.name;
            PartIconRenderSettings settings = IconPreviewSettings.Clone();
            settings.backgroundColor = new Color(0x1c / 255f, 0x1f / 255f, 0x27 / 255f, 1f);
            settings.supersampleScale = 2;
            _iconPreviewTexture = PartIconRenderer.RenderTexture2D(
                partName + "-editor-preview",
                TargetCore,
                TargetObject,
                settings
            );
        }

        private void DestroyIconPreview()
        {
            if (_iconPreviewTexture == null)
            {
                return;
            }

            DestroyImmediate(_iconPreviewTexture);
            _iconPreviewTexture = null;
        }

        private void DrawReentryMeshGenerator()
        {
            GUILayout.Space(8f);
            _reentryMeshFoldout = EditorGUILayout.Foldout(_reentryMeshFoldout, "Reentry Mesh Generator", true);
            if (!_reentryMeshFoldout)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string prefabPath = PathUtils.GetPrefabOrAssetPath(TargetData, TargetObject);
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(prefabPath)))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Validate Existing"))
                        {
                            ValidateReentryMeshes();
                        }

                        if (GUILayout.Button("Generate/Rebuild"))
                        {
                            GenerateReentryMeshes(prefabPath);
                        }

                        if (GUILayout.Button("Remove Generated"))
                        {
                            RemoveGeneratedReentryMeshes(prefabPath);
                        }
                    }
                }

                if (string.IsNullOrEmpty(prefabPath))
                {
                    EditorGUILayout.HelpBox(
                        "Open or select a prefab-backed part to generate reentry meshes.",
                        MessageType.Info
                    );
                }
                else if (!string.IsNullOrWhiteSpace(_reentryMeshStatus))
                {
                    EditorGUILayout.HelpBox(_reentryMeshStatus, MessageType.Info);
                }

                DrawReentryMeshPreview();
            }
        }

        private void DrawReentryMeshPreview()
        {
            Rect foldoutRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            foldoutRect.x += 12f;
            foldoutRect.width = Mathf.Max(1f, foldoutRect.width - 12f);
            _reentryMeshPreviewFoldout = EditorGUI.Foldout(
                foldoutRect,
                _reentryMeshPreviewFoldout,
                "Reentry Mesh Preview",
                true
            );
            if (!_reentryMeshPreviewFoldout)
            {
                return;
            }

            List<ReentryMeshPreviewEntry> entries = CollectReentryMeshPreviewEntries(TargetObject);
            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox("No reentry mesh renderer meshes found on this part.", MessageType.Info);
                return;
            }

            int groupCount = CountReentryPreviewGroups(entries);
            EditorGUILayout.LabelField(
                $"Showing {entries.Count} reentry mesh preview{(entries.Count == 1 ? string.Empty : "s")} across {groupCount} group{(groupCount == 1 ? string.Empty : "s")}.",
                EditorStyles.miniLabel
            );

            const float previewSize = 96f;
            const float labelHeight = 44f;
            const float cellPadding = 8f;
            float cellWidth = previewSize + cellPadding;
            float cellHeight = previewSize + labelHeight + cellPadding;
            float availableWidth = Mathf.Max(cellWidth, EditorGUIUtility.currentViewWidth - 64f);
            int columns = Mathf.Max(1, Mathf.FloorToInt(availableWidth / cellWidth));
            int rows = Mathf.CeilToInt(entries.Count / (float)columns);
            Rect gridRect = GUILayoutUtility.GetRect(availableWidth, rows * cellHeight, GUILayout.ExpandWidth(true));
            bool pendingPreview = DrawReentryMeshPreviewGrid(
                entries,
                gridRect,
                columns,
                previewSize,
                labelHeight,
                cellWidth,
                cellHeight
            );

            if (pendingPreview)
            {
                Repaint();
            }
        }

        private static List<ReentryMeshPreviewEntry> CollectReentryMeshPreviewEntries(GameObject root)
        {
            var entries = new List<ReentryMeshPreviewEntry>();
            if (root == null)
            {
                return entries;
            }

            foreach (KSP.VFX.Reentry.ReentryMesh reentryMesh in root
                .GetComponentsInChildren<KSP.VFX.Reentry.ReentryMesh>(true))
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

                    entries.Add(
                        new ReentryMeshPreviewEntry
                        {
                            GroupName = groupName,
                            LodIndex = i,
                            Mesh = meshFilter.sharedMesh
                        }
                    );
                }
            }

            return entries;
        }

        private static int CountReentryPreviewGroups(List<ReentryMeshPreviewEntry> entries)
        {
            var groups = new HashSet<string>();
            foreach (ReentryMeshPreviewEntry entry in entries)
            {
                groups.Add(entry.GroupName);
            }

            return groups.Count;
        }

        private static bool DrawReentryMeshPreviewGrid(
            List<ReentryMeshPreviewEntry> entries,
            Rect gridRect,
            int columns,
            float previewSize,
            float labelHeight,
            float cellWidth,
            float cellHeight
        )
        {
            bool pendingPreview = false;
            for (int i = 0; i < entries.Count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                Rect cellRect = new(
                    gridRect.x + column * cellWidth,
                    gridRect.y + row * cellHeight,
                    previewSize,
                    cellHeight
                );
                pendingPreview |= DrawReentryMeshPreviewEntry(entries[i], cellRect, previewSize, labelHeight);
            }

            return pendingPreview;
        }

        private static bool DrawReentryMeshPreviewEntry(
            ReentryMeshPreviewEntry entry,
            Rect cellRect,
            float previewSize,
            float labelHeight
        )
        {
            bool pendingPreview = false;
            Rect previewRect = new(cellRect.x, cellRect.y, previewSize, previewSize);
            GUI.Box(previewRect, GUIContent.none);
            if (Event.current.type == EventType.MouseDown && previewRect.Contains(Event.current.mousePosition))
            {
                Selection.activeObject = entry.Mesh;
                EditorGUIUtility.PingObject(entry.Mesh);
                Event.current.Use();
            }

            Texture2D preview = AssetPreview.GetAssetPreview(entry.Mesh);
            if (preview == null)
            {
                preview = AssetPreview.GetMiniThumbnail(entry.Mesh);
                pendingPreview = true;
            }

            if (preview != null)
            {
                EditorGUI.DrawPreviewTexture(previewRect, preview, null, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUI.LabelField(previewRect, "No Preview", EditorStyles.centeredGreyMiniLabel);
            }

            string label = $"{entry.GroupName}\nLOD {entry.LodIndex}  {entry.Mesh.vertexCount}v";
            Rect labelRect = new(cellRect.x, cellRect.y + previewSize + 2f, previewSize, labelHeight);
            EditorGUI.LabelField(labelRect, label, EditorStyles.wordWrappedMiniLabel);

            return pendingPreview;
        }

        private struct ReentryMeshPreviewEntry
        {
            public string GroupName;
            public int LodIndex;
            public Mesh Mesh;
        }

        private void ValidateReentryMeshes()
        {
            int reentryMeshCount = TargetObject.GetComponentsInChildren<KSP.VFX.Reentry.ReentryMesh>(true).Length;
            int generatedCount = TargetObject.GetComponentsInChildren<GeneratedReentryMeshRoot>(true).Length;
            int rendererCount = 0;
            foreach (Renderer renderer in TargetObject.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer ||
                    renderer.GetComponentInParent<KSP.VFX.Reentry.ReentryMesh>(true) != null ||
                    renderer.GetComponentInParent<GeneratedReentryMeshRoot>(true) != null)
                {
                    continue;
                }

                rendererCount++;
            }

            _reentryMeshStatus =
                $"ReentryMesh components: {reentryMeshCount}. Generated roots: {generatedCount}. Candidate source renderers: {rendererCount}.";
        }

        private void GenerateReentryMeshes(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath))
            {
                return;
            }

            var settings = ReentryMeshGenerationSettings.CreateDefault();
            settings.previewMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/ReduxAssets/Definitions/Parts/Common/ReentryMat.mat"
            );

            string partName = GetTargetPartName();
            ReentryMeshGenerator.Result result = ReentryMeshGenerator.GenerateForPart(
                TargetObject,
                partName,
                settings,
                true
            );

            if (!result.Success)
            {
                _reentryMeshStatus = "No reentry meshes were generated. " + string.Join(" ", result.Warnings);
                return;
            }

            SaveGeneratedReentryMeshAssets(result, prefabPath, partName);
            SavePrefabChanges(prefabPath);
            _reentryMeshStatus =
                $"Generated {result.Groups.Count} reentry groups from {result.SourceRendererCount} source renderers " +
                $"and {result.SourceVertexCount} source vertices.";
            if (result.Warnings.Count > 0)
            {
                _reentryMeshStatus += " Warnings: " + string.Join(" ", result.Warnings);
            }
        }

        private void RemoveGeneratedReentryMeshes(string prefabPath)
        {
            string partName = GetTargetPartName();
            ReentryMeshGenerator.RemoveGenerated(TargetObject);
            DeleteGeneratedReentryMeshAssets(prefabPath, partName);
            SavePrefabChanges(prefabPath);
            _reentryMeshStatus = "Removed generated reentry mesh roots and generated mesh assets for this part.";
        }

        private static void SaveGeneratedReentryMeshAssets(
            ReentryMeshGenerator.Result result,
            string prefabPath,
            string partName
        )
        {
            string directory = Path.Combine(Path.GetDirectoryName(prefabPath), "ReentryMeshes").Replace('\\', '/');
            Directory.CreateDirectory(directory);

            foreach (ReentryMeshGenerator.GeneratedGroup group in result.Groups)
            {
                for (int i = 0; i < group.LodMeshes.Length; i++)
                {
                    Mesh mesh = group.LodMeshes[i];
                    string assetPath =
                        $"{directory}/{SanitizeAssetName(partName)}_{SanitizeAssetName(group.Name)}_lod{i}.asset";
                    AssetDatabase.DeleteAsset(assetPath);
                    AssetDatabase.CreateAsset(mesh, assetPath);
                    var savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                    if (savedMesh != null && group.LodRenderers[i] != null &&
                        group.LodRenderers[i].TryGetComponent(out MeshFilter meshFilter))
                    {
                        meshFilter.sharedMesh = savedMesh;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void DeleteGeneratedReentryMeshAssets(string prefabPath, string partName)
        {
            if (string.IsNullOrEmpty(prefabPath))
            {
                return;
            }

            string directory = Path.Combine(Path.GetDirectoryName(prefabPath), "ReentryMeshes").Replace('\\', '/');
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(directory, $"{SanitizeAssetName(partName)}_*_lod*.asset"))
            {
                AssetDatabase.DeleteAsset(file.Replace('\\', '/'));
            }

            AssetDatabase.Refresh();
        }

        private void SavePrefabChanges(string prefabPath)
        {
            EditorUtility.SetDirty(TargetObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(TargetObject);
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot != null &&
                TargetObject.transform.root == stage.prefabContentsRoot.transform)
            {
                PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, prefabPath);
                return;
            }

            if (PrefabUtility.IsPartOfPrefabAsset(TargetObject))
            {
                PrefabUtility.SavePrefabAsset(TargetObject.transform.root.gameObject);
            }

            AssetDatabase.SaveAssets();
        }

        private static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "part";
            }

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(Path.GetInvalidFileNameChars(), chars[i]) >= 0 || chars[i] == ' ')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawGizmoForPartCoreData(CorePartData data, GizmoType gizmoType)
        {
            Matrix4x4 localToWorldMatrix = data.transform.localToWorldMatrix;
            if (_centerOfMassGizmos)
            {
                Vector3 centerOfMassPosition = data.Data.coMassOffset;
                centerOfMassPosition = localToWorldMatrix.MultiplyPoint(centerOfMassPosition);
                Gizmos.DrawIcon(
                    centerOfMassPosition,
                    SDKConfiguration.BasePath + "/Assets/Gizmos/com_icon.png",
                    false
                );
            }

            if (_centerOfLiftGizmos)
            {
                Vector3 centerOfLiftPosition = data.Data.coLiftOffset;
                centerOfLiftPosition = localToWorldMatrix.MultiplyPoint(centerOfLiftPosition);
                Gizmos.DrawIcon(
                    centerOfLiftPosition,
                    SDKConfiguration.BasePath + "/Assets/Gizmos/col_icon.png",
                    false
                );
            }

            if (!_attachNodeGizmos)
            {
                return;
            }

            Gizmos.color = new Color(Color.green.r, Color.green.g, Color.green.b, 0.5f);
            foreach (AttachNodeDefinition attachNode in data.Data.attachNodes)
            {
                Vector3d pos = attachNode.position;
                pos = localToWorldMatrix.MultiplyPoint(pos);
                Vector3d dir = attachNode.orientation;
                dir = localToWorldMatrix.MultiplyVector(dir);
                Gizmos.DrawRay(pos, dir * 0.25f);
                Gizmos.DrawSphere(pos, 0.05f);
            }
        }

        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawGizmoForAttachmentNode(AttachmentNode node, GizmoType gizmoType)
        {
            if (!_attachNodeGizmos)
            {
                return;
            }

            Gizmos.color = new Color(Color.green.r, Color.green.g, Color.green.b, 0.5f);
            Vector3 pos = node.transform.position;
            Gizmos.DrawRay(pos, node.transform.rotation * Vector3.forward * 0.25f);
            Gizmos.DrawSphere(pos, 0.05f);
        }
    }

    [CustomEditor(typeof(Module_Fairing))]
    public class FairingModuleEditor : UnityEditor.Editor
    {
        private Module_Fairing TargetModule => target as Module_Fairing;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            ShroudPreviewEditor.DrawInspector(TargetModule);
        }

        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawGizmoForFairing(Module_Fairing module, GizmoType gizmoType)
        {
            ShroudPreviewEditor.DrawGizmo(module);
        }
    }

    internal static class ShroudPreviewEditor
    {
        private sealed class ShroudPreviewSettings
        {
            public bool Foldout = true;
            public bool Enabled = true;
            public string TargetSizeKey;
        }

        private struct ShroudPreviewMetrics
        {
            public float HostDiameter;
            public float TargetDiameter;
            public float ResolvedDiameter;
            public float BaseRadius;
            public float TargetRadius;
            public float StartHeight;
            public float EndHeight;
        }

        private static readonly Dictionary<int, ShroudPreviewSettings> SettingsByModule = new();

        private static readonly FieldInfo FairingDataField =
            typeof(Module_Fairing).GetField("_dataFairing", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Color PreviewFillColor = new(0.16f, 0.55f, 0.95f, 0.16f);
        private static readonly Color PreviewLineColor = new(0.16f, 0.85f, 1f, 0.9f);
        private static GUIContent[] _sizeOptions;

        public static void DrawInspector(Module_Fairing module)
        {
            Data_Fairing fairing = GetFairingData(module);
            if (module == null || fairing == null)
            {
                return;
            }

            ShroudPreviewSettings settings = GetOrCreateSettings(module, fairing);

            EditorGUILayout.Space();
            settings.Foldout = EditorGUILayout.Foldout(settings.Foldout, "Shroud Preview", true);
            if (!settings.Foldout)
            {
                return;
            }

            int originalIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();

            settings.Enabled = EditorGUILayout.Toggle("Show Scene Preview", settings.Enabled);
            settings.TargetSizeKey = DrawBuiltInSizePopup("Target Part Size", settings.TargetSizeKey);

            Transform moduleTransform = module.gameObject.transform;
            Transform modelTransform = GetPreviewModelTransform(moduleTransform);
            ShroudPreviewMetrics metrics = CalculateMetrics(fairing, settings.TargetSizeKey, modelTransform);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Host Diameter", FormatMeters(metrics.HostDiameter));
                EditorGUILayout.TextField("Target Diameter", FormatMeters(metrics.TargetDiameter));
                EditorGUILayout.TextField("Preview Diameter", FormatMeters(metrics.ResolvedDiameter));
                EditorGUILayout.TextField(
                    "Height Range",
                    $"{FormatMeters(metrics.StartHeight)} to {FormatMeters(metrics.EndHeight)}"
                );
            }

            if (EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll();
            }

            EditorGUI.indentLevel = originalIndent;
        }

        public static void DrawGizmo(Module_Fairing module)
        {
            if (module == null ||
                !SettingsByModule.TryGetValue(module.GetInstanceID(), out ShroudPreviewSettings settings) ||
                !settings.Enabled)
            {
                return;
            }

            Data_Fairing fairing = GetFairingData(module);
            if (fairing == null)
            {
                return;
            }

            Transform moduleTransform = module.gameObject.transform;
            Transform modelTransform = GetPreviewModelTransform(moduleTransform);
            ShroudPreviewMetrics metrics = CalculateMetrics(fairing, settings.TargetSizeKey, modelTransform);
            DrawFrustum(modelTransform != null ? modelTransform : moduleTransform, fairing, metrics);
        }

        private static Data_Fairing GetFairingData(Module_Fairing module)
        {
            return module == null || FairingDataField == null
                ? null
                : FairingDataField.GetValue(module) as Data_Fairing;
        }

        private static ShroudPreviewSettings GetOrCreateSettings(Module_Fairing module, Data_Fairing fairing)
        {
            int instanceId = module.GetInstanceID();
            if (SettingsByModule.TryGetValue(instanceId, out ShroudPreviewSettings settings))
            {
                if (!PartSizeRegistry.IsValidKey(settings.TargetSizeKey))
                {
                    settings.TargetSizeKey = GetDefaultTargetKey(module, fairing);
                }

                return settings;
            }

            settings = new ShroudPreviewSettings
            {
                TargetSizeKey = GetDefaultTargetKey(module, fairing)
            };
            SettingsByModule[instanceId] = settings;
            return settings;
        }

        private static string GetDefaultTargetKey(Module_Fairing module, Data_Fairing fairing)
        {
            CorePartData coreData = module.GetComponent<CorePartData>() ?? module.GetComponentInParent<CorePartData>();
            string partSizeKey = coreData?.Data == null ? null : PartSizeRegistry.GetPartSizeKey(coreData.Data);
            return PartSizeRegistry.IsValidKey(partSizeKey)
                ? partSizeKey
                : GetSmallestSizeKeyAtLeast(fairing.BaseRadius * 2f);
        }

        private static string GetSmallestSizeKeyAtLeast(float diameter)
        {
            foreach (PartSizeDefinition definition in PartSizeRegistry.Definitions)
            {
                if (definition.Diameter >= diameter)
                {
                    return definition.Key;
                }
            }

            return PartSizeRegistry.GetLargest().Key;
        }

        private static string DrawBuiltInSizePopup(string label, string currentKey)
        {
            EnsureSizeOptions();
            IReadOnlyList<PartSizeDefinition> definitions = PartSizeRegistry.Definitions;
            int selectedIndex = 0;
            for (int i = 0; i < definitions.Count; i++)
            {
                if (string.Equals(definitions[i].Key, currentKey, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                    break;
                }
            }

            int newIndex = EditorGUILayout.Popup(new GUIContent(label), selectedIndex, _sizeOptions);
            return definitions[Mathf.Clamp(newIndex, 0, definitions.Count - 1)].Key;
        }

        private static void EnsureSizeOptions()
        {
            if (_sizeOptions != null)
            {
                return;
            }

            IReadOnlyList<PartSizeDefinition> definitions = PartSizeRegistry.Definitions;
            _sizeOptions = new GUIContent[definitions.Count];
            for (int i = 0; i < definitions.Count; i++)
            {
                PartSizeDefinition definition = definitions[i];
                _sizeOptions[i] = new GUIContent(
                    definition.DisplayName + " (" +
                    definition.Diameter.ToString("0.####", CultureInfo.InvariantCulture) + " m)"
                );
            }
        }

        private static ShroudPreviewMetrics CalculateMetrics(
            Data_Fairing fairing,
            string targetSizeKey,
            Transform modelTransform
        )
        {
            float hostDiameter = Mathf.Max(0.001f, fairing.BaseRadius * 2f);
            float targetDiameter = PartSizeRegistry.Get(targetSizeKey).Diameter;
            float resolvedDiameter = targetDiameter >= hostDiameter ? targetDiameter : hostDiameter;

            if (fairing.MaxAutoFairingTargetRadius > 0 && resolvedDiameter > hostDiameter)
            {
                float maxDiameter = PartSizeRegistry.GetLegacyAttachNodeSize(fairing.MaxAutoFairingTargetRadius)
                    .Diameter;
                resolvedDiameter = Mathf.Max(hostDiameter, Mathf.Min(maxDiameter, resolvedDiameter));
            }
            else if (fairing.MinAutoFairingTargetRadius > 0 && resolvedDiameter < hostDiameter)
            {
                float minDiameter = PartSizeRegistry.GetLegacyAttachNodeSize(fairing.MinAutoFairingTargetRadius)
                    .Diameter;
                resolvedDiameter = Mathf.Max(minDiameter, resolvedDiameter);
            }

            resolvedDiameter = PartSizeRegistry.SnapDownToKnownDiameter(resolvedDiameter);
            float maxRadius = fairing.MaxRadius > 0f ? fairing.MaxRadius : resolvedDiameter * 0.5f;
            float minRadius = Mathf.Min(Mathf.Max(0f, fairing.CapRadius), maxRadius);
            float height = Mathf.Max(fairing.CrossSectionHeightMin, fairing.CrossSectionHeightMax);
            float startHeight = GetRuntimeStartHeight(fairing, modelTransform);

            return new ShroudPreviewMetrics
            {
                HostDiameter = hostDiameter,
                TargetDiameter = targetDiameter,
                ResolvedDiameter = resolvedDiameter,
                BaseRadius = Mathf.Max(0f, fairing.BaseRadius),
                TargetRadius = Mathf.Clamp(resolvedDiameter * 0.5f, minRadius, maxRadius),
                StartHeight = startHeight,
                EndHeight = startHeight + height
            };
        }

        private static float GetRuntimeStartHeight(Data_Fairing fairing, Transform modelTransform)
        {
            float localUpSign = Mathf.Sign(fairing.LocalUpAxis.x) * Mathf.Sign(fairing.LocalUpAxis.y) *
                Mathf.Sign(fairing.LocalUpAxis.z);
            float startHeight = fairing.FairingStartHeight * localUpSign;
            if (modelTransform == null)
            {
                return startHeight;
            }

            float modelTransformSign = Mathf.Sign(modelTransform.localPosition.x) *
                Mathf.Sign(modelTransform.localPosition.y) *
                Mathf.Sign(modelTransform.localPosition.z);
            return startHeight - Vector3.Scale(modelTransform.localPosition, fairing.LocalUpAxis).magnitude *
                localUpSign * modelTransformSign;
        }

        private static string FormatMeters(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture) + " m";
        }

        private static Transform GetPreviewModelTransform(Transform partTransform)
        {
            return partTransform == null ? null : FindChildRecursive(partTransform, "model");
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent.name == childName)
            {
                return parent;
            }

            foreach (Transform child in parent)
            {
                Transform match = FindChildRecursive(child, childName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static void DrawFrustum(
            Transform parentTransform,
            Data_Fairing fairing,
            ShroudPreviewMetrics metrics
        )
        {
            Vector3 axis = fairing.LocalUpAxis.sqrMagnitude > 0.0001f
                ? fairing.LocalUpAxis.normalized
                : Vector3.up;
            Vector3 radial = Vector3.ProjectOnPlane(Vector3.forward, axis);
            if (radial.sqrMagnitude < 0.0001f)
            {
                radial = Vector3.ProjectOnPlane(Vector3.right, axis);
            }

            radial.Normalize();
            Vector3 tangent = Vector3.Cross(axis, radial).normalized;

            const int SegmentCount = 48;
            var baseRing = new Vector3[SegmentCount + 1];
            var targetRing = new Vector3[SegmentCount + 1];
            for (int i = 0; i <= SegmentCount; i++)
            {
                float radians = Mathf.PI * 2f * i / SegmentCount;
                Vector3 circleDirection = radial * Mathf.Cos(radians) + tangent * Mathf.Sin(radians);
                baseRing[i] = TransformPoint(
                    parentTransform,
                    fairing.Pivot,
                    axis,
                    circleDirection,
                    metrics.StartHeight,
                    metrics.BaseRadius
                );
                targetRing[i] = TransformPoint(
                    parentTransform,
                    fairing.Pivot,
                    axis,
                    circleDirection,
                    metrics.EndHeight,
                    metrics.TargetRadius
                );
            }

            CompareFunction previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;
            Handles.color = PreviewFillColor;
            for (int i = 0; i < SegmentCount; i++)
            {
                Handles.DrawAAConvexPolygon(baseRing[i], baseRing[i + 1], targetRing[i + 1], targetRing[i]);
            }

            Handles.color = PreviewLineColor;
            Handles.DrawAAPolyLine(2f, baseRing);
            Handles.DrawAAPolyLine(2f, targetRing);
            for (int i = 0; i < SegmentCount; i += SegmentCount / 8)
            {
                Handles.DrawAAPolyLine(2f, baseRing[i], targetRing[i]);
            }

            Handles.zTest = previousZTest;
        }

        private static Vector3 TransformPoint(
            Transform parentTransform,
            Vector3 pivot,
            Vector3 axis,
            Vector3 circleDirection,
            float height,
            float radius
        )
        {
            return parentTransform.TransformPoint(pivot + axis * height + circleDirection * radius);
        }
    }

    [CustomPropertyDrawer(typeof(PartData))]
    public class PartDataDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return PartSizeInspectorFields.GetFoldoutPropertyHeight(
                property,
                PartSizeInspectorFields.PartSizeFieldName,
                PartSizeInspectorFields.LegacyPartSizeCategoryFieldName
            );
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            PartSizeInspectorFields.DrawFoldoutProperty(
                position,
                property,
                label,
                PartSizeInspectorFields.PartSizeFieldName,
                PartSizeInspectorFields.LegacyPartSizeCategoryFieldName,
                "Part Size",
                PartSizeInspectorFields.LegacyPartSizeMode.PartCategory
            );
        }
    }

    [CustomPropertyDrawer(typeof(AttachNodeDefinition))]
    public class AttachNodeDefinitionDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return PartSizeInspectorFields.GetFoldoutPropertyHeight(
                property,
                PartSizeInspectorFields.AttachNodeSizeFieldName,
                PartSizeInspectorFields.AttachNodeSizeKeyFieldName
            );
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            PartSizeInspectorFields.DrawFoldoutProperty(
                position,
                property,
                label,
                PartSizeInspectorFields.AttachNodeSizeFieldName,
                PartSizeInspectorFields.AttachNodeSizeKeyFieldName,
                "Node Size",
                PartSizeInspectorFields.LegacyPartSizeMode.AttachNode
            );
        }
    }

    [CustomEditor(typeof(AttachmentNode))]
    public class AttachmentNodeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty sizeProperty =
                serializedObject.FindProperty(PartSizeInspectorFields.AttachNodeSizeFieldName);
            SerializedProperty sizeKeyProperty =
                serializedObject.FindProperty(PartSizeInspectorFields.AttachNodeSizeKeyFieldName);
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (iterator.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(iterator, true);
                    }

                    continue;
                }

                if (iterator.name == PartSizeInspectorFields.AttachNodeSizeFieldName)
                {
                    PartSizeInspectorFields.DrawSizeKeyLayout(
                        "Node Size",
                        sizeKeyProperty,
                        sizeProperty,
                        PartSizeInspectorFields.LegacyPartSizeMode.AttachNode
                    );
                    continue;
                }

                if (iterator.name == PartSizeInspectorFields.AttachNodeSizeKeyFieldName)
                {
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }

    internal static class PartSizeInspectorFields
    {
        public const string PartSizeFieldName = "sizeKey";
        public const string LegacyPartSizeCategoryFieldName = "sizeCategory";
        public const string AttachNodeSizeFieldName = "size";
        public const string AttachNodeSizeKeyFieldName = "sizeKey";

        private const string AutoCategoryName = "Auto";
        private const float VerticalSpacing = 2f;

        private static readonly HashSet<string> CustomModePropertyPaths = new();
        private static GUIContent[] _presetOptions;
        private static string[] _presetKeys;

        public enum LegacyPartSizeMode
        {
            PartCategory,
            AttachNode
        }

        public static float GetFoldoutPropertyHeight(
            SerializedProperty property,
            string sizeFieldName,
            string hiddenCompatibilityFieldName
        )
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
            {
                return height;
            }

            SerializedProperty iterator = property.Copy();
            SerializedProperty endProperty = property.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
            {
                enterChildren = false;
                if (iterator.depth != property.depth + 1)
                {
                    continue;
                }

                if (iterator.name == hiddenCompatibilityFieldName)
                {
                    continue;
                }

                height += VerticalSpacing;
                height += iterator.name == sizeFieldName
                    ? GetSizeKeyFieldHeight(GetSizeKeyProperty(property, sizeFieldName))
                    : EditorGUI.GetPropertyHeight(iterator, true);
            }

            return height;
        }

        public static void DrawFoldoutProperty(
            Rect position,
            SerializedProperty property,
            GUIContent label,
            string sizeFieldName,
            string hiddenCompatibilityFieldName,
            string sizeLabel,
            LegacyPartSizeMode legacyMode
        )
        {
            EditorGUI.BeginProperty(position, label, property);
            Rect foldoutRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            int originalIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel++;

            float y = foldoutRect.yMax;
            SerializedProperty iterator = property.Copy();
            SerializedProperty endProperty = property.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
            {
                enterChildren = false;
                if (iterator.depth != property.depth + 1)
                {
                    continue;
                }

                if (iterator.name == hiddenCompatibilityFieldName)
                {
                    continue;
                }

                y += VerticalSpacing;
                float height = iterator.name == sizeFieldName
                    ? GetSizeKeyFieldHeight(GetSizeKeyProperty(property, sizeFieldName))
                    : EditorGUI.GetPropertyHeight(iterator, true);
                Rect childRect = new(position.x, y, position.width, height);

                if (iterator.name == sizeFieldName)
                {
                    DrawSizeKeyField(
                        childRect,
                        sizeLabel,
                        GetSizeKeyProperty(property, sizeFieldName),
                        legacyMode == LegacyPartSizeMode.AttachNode
                            ? iterator
                            : GetLegacyProperty(property, hiddenCompatibilityFieldName),
                        legacyMode
                    );
                }
                else
                {
                    EditorGUI.PropertyField(childRect, iterator, true);
                }

                y += height;
            }

            EditorGUI.indentLevel = originalIndent;
            EditorGUI.EndProperty();
        }

        public static void DrawSizeKeyLayout(
            string label,
            SerializedProperty sizeKeyProperty,
            SerializedProperty legacyProperty,
            LegacyPartSizeMode legacyMode
        )
        {
            Rect rect = EditorGUILayout.GetControlRect(true, GetSizeKeyFieldHeight(sizeKeyProperty));
            DrawSizeKeyField(rect, label, sizeKeyProperty, legacyProperty, legacyMode);
        }

        private static void DrawSizeKeyField(
            Rect position,
            string label,
            SerializedProperty sizeKeyProperty,
            SerializedProperty legacyProperty,
            LegacyPartSizeMode legacyMode
        )
        {
            EnsurePresetCache();

            string pathKey = GetPathKey(sizeKeyProperty);
            string currentKey = sizeKeyProperty?.stringValue ?? string.Empty;
            int customIndex = _presetKeys.Length;
            bool customMode = CustomModePropertyPaths.Contains(pathKey) || IsCustomKey(currentKey);
            int selectedIndex = customMode ? customIndex : GetPresetIndex(currentKey);

            if (selectedIndex < 0)
            {
                selectedIndex = GetPresetIndex(GetLegacyKey(legacyProperty, legacyMode));
            }

            if (selectedIndex < 0)
            {
                selectedIndex = GetPresetIndex(PartSizeRegistry.DefaultSizeKey);
            }

            Rect popupRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(popupRect, new GUIContent(label), selectedIndex, _presetOptions);
            if (EditorGUI.EndChangeCheck())
            {
                if (newIndex == customIndex)
                {
                    CustomModePropertyPaths.Add(pathKey);
                    if (!IsCustomKey(currentKey))
                    {
                        sizeKeyProperty.stringValue = string.Empty;
                    }

                    SetLegacyCustom(legacyProperty, legacyMode);
                }
                else
                {
                    CustomModePropertyPaths.Remove(pathKey);
                    sizeKeyProperty.stringValue = _presetKeys[newIndex];
                    SetLegacyPreset(legacyProperty, legacyMode, PartSizeRegistry.Get(_presetKeys[newIndex]));
                }
            }

            if (newIndex != customIndex && !customMode)
            {
                return;
            }

            Rect textRect = new(
                position.x,
                popupRect.yMax + VerticalSpacing,
                position.width,
                EditorGUIUtility.singleLineHeight
            );
            EditorGUI.PropertyField(textRect, sizeKeyProperty, new GUIContent("Custom Key"));
        }

        private static float GetSizeKeyFieldHeight(SerializedProperty sizeKeyProperty)
        {
            return IsCustomKey(sizeKeyProperty?.stringValue) ||
                CustomModePropertyPaths.Contains(GetPathKey(sizeKeyProperty))
                    ? EditorGUIUtility.singleLineHeight * 2f + VerticalSpacing
                    : EditorGUIUtility.singleLineHeight;
        }

        private static SerializedProperty GetSizeKeyProperty(SerializedProperty property, string sizeFieldName)
        {
            return sizeFieldName == AttachNodeSizeFieldName
                ? property.FindPropertyRelative(AttachNodeSizeKeyFieldName)
                : property.FindPropertyRelative(PartSizeFieldName);
        }

        private static SerializedProperty GetLegacyProperty(
            SerializedProperty property,
            string hiddenCompatibilityFieldName
        )
        {
            return property.FindPropertyRelative(hiddenCompatibilityFieldName);
        }

        private static string GetLegacyKey(SerializedProperty legacyProperty, LegacyPartSizeMode legacyMode)
        {
            if (legacyProperty == null)
            {
                return null;
            }

            if (legacyMode == LegacyPartSizeMode.AttachNode)
            {
                return PartSizeRegistry.GetAttachNodeSizeKey(null, legacyProperty.intValue);
            }

            string enumName = legacyProperty.enumNames[legacyProperty.enumValueIndex];
            return Enum.TryParse(enumName, out MetaAssemblySizeFilterType category)
                ? PartSizeRegistry.GetPartSizeKey(null, category)
                : null;
        }

        private static void SetLegacyPreset(
            SerializedProperty legacyProperty,
            LegacyPartSizeMode legacyMode,
            PartSizeDefinition definition
        )
        {
            if (legacyProperty == null)
            {
                return;
            }

            if (legacyMode == LegacyPartSizeMode.AttachNode)
            {
                if (definition.LegacyAttachNodeSizeAliases.Count > 0)
                {
                    legacyProperty.intValue = definition.LegacyAttachNodeSizeAliases[0];
                }

                return;
            }

            SetEnumValue(legacyProperty, GetMetaCategoryName(definition.Key));
        }

        private static void SetLegacyCustom(SerializedProperty legacyProperty, LegacyPartSizeMode legacyMode)
        {
            if (legacyProperty == null || legacyMode == LegacyPartSizeMode.AttachNode)
            {
                return;
            }

            SetEnumValue(legacyProperty, AutoCategoryName);
        }

        private static void SetEnumValue(SerializedProperty property, string enumName)
        {
            for (int i = 0; i < property.enumNames.Length; i++)
            {
                if (property.enumNames[i] == enumName)
                {
                    property.enumValueIndex = i;
                    return;
                }
            }
        }

        private static bool IsCustomKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && !PartSizeRegistry.IsValidKey(key);
        }

        private static int GetPresetIndex(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return -1;
            }

            for (int i = 0; i < _presetKeys.Length; i++)
            {
                if (string.Equals(_presetKeys[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string GetPathKey(SerializedProperty property)
        {
            return property == null
                ? string.Empty
                : property.serializedObject.targetObject.GetInstanceID() + ":" + property.propertyPath;
        }

        private static string GetMetaCategoryName(string key)
        {
            return key switch
            {
                PartSizeRegistry.XsMinus => nameof(MetaAssemblySizeFilterType.XSMINUS),
                PartSizeRegistry.Xs => nameof(MetaAssemblySizeFilterType.XS),
                PartSizeRegistry.XsPlus => nameof(MetaAssemblySizeFilterType.XSPLUS),
                PartSizeRegistry.Sm => nameof(MetaAssemblySizeFilterType.S),
                PartSizeRegistry.SmPlus => nameof(MetaAssemblySizeFilterType.SPLUS),
                PartSizeRegistry.Md => nameof(MetaAssemblySizeFilterType.M),
                PartSizeRegistry.MdPlus => nameof(MetaAssemblySizeFilterType.MPLUS),
                PartSizeRegistry.Lg => nameof(MetaAssemblySizeFilterType.L),
                PartSizeRegistry.LgPlus => nameof(MetaAssemblySizeFilterType.LPLUS),
                PartSizeRegistry.Xl => nameof(MetaAssemblySizeFilterType.XL),
                PartSizeRegistry.XlPlus => nameof(MetaAssemblySizeFilterType.XLPLUS),
                PartSizeRegistry.TwoXl => nameof(MetaAssemblySizeFilterType.XXL),
                PartSizeRegistry.ThreeXl => nameof(MetaAssemblySizeFilterType.XXXL),
                PartSizeRegistry.FourXl => nameof(MetaAssemblySizeFilterType.XXXXL),
                PartSizeRegistry.FiveXl => nameof(MetaAssemblySizeFilterType.XXXXXL),
                PartSizeRegistry.SixXl => nameof(MetaAssemblySizeFilterType.XXXXXXL),
                _ => AutoCategoryName
            };
        }

        private static void EnsurePresetCache()
        {
            if (_presetOptions != null)
            {
                return;
            }

            IReadOnlyList<PartSizeDefinition> definitions = PartSizeRegistry.Definitions;
            _presetKeys = new string[definitions.Count];
            _presetOptions = new GUIContent[definitions.Count + 1];

            for (int i = 0; i < definitions.Count; i++)
            {
                PartSizeDefinition definition = definitions[i];
                _presetKeys[i] = definition.Key;
                _presetOptions[i] = new GUIContent(
                    definition.DisplayName + " (" +
                    definition.Diameter.ToString("0.####", CultureInfo.InvariantCulture) + " m)"
                );
            }

            _presetOptions[^1] = new GUIContent("Custom...");
        }
    }
}