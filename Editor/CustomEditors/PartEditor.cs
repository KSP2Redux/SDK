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

        private PartIconRenderSettings IconPreviewSettings => _iconPreviewSettings ??= PartIconRenderSettings.CreateDefault(TargetCore);

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
            _iconPreviewPreset = settings.CameraPreset;
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
                        _iconPreviewSettings = PartIconRenderSettings.CreateDefault(TargetCore);
                        _iconPreviewPreset = _iconPreviewSettings.CameraPreset;
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
                settings.CameraPreset = preset;
                changed = true;
            }

            EditorGUI.BeginChangeCheck();
            settings.cameraPadding = EditorGUILayout.Slider("Padding", settings.cameraPadding, 0.1f, 3f);
            settings.partTransformRotation =  EditorGUILayout.Vector3Field("Part Rotation", settings.partTransformRotation);
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
                    settings.overrideOrthographicSize = EditorGUILayout.Toggle("Override Camera Size", settings.overrideOrthographicSize);
                    settings.cameraOrthographicSize = EditorGUILayout.Slider("Orthographic Size", settings.cameraOrthographicSize, 0.1f, 40f);
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
            ReentryMeshGenerator.Result result;
            try
            {
                result = ReentryMeshGenerator.GenerateForPart(
                    TargetObject,
                    partName,
                    settings,
                    true,
                    (progress, status) =>
                        EditorUtility.DisplayProgressBar($"Generating reentry meshes for {partName}", status, progress)
                );
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (!result.Success)
            {
                _reentryMeshStatus = "No reentry meshes were generated. " + string.Join(" ", result.Warnings);
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar(
                    $"Generating reentry meshes for {partName}", "Saving generated meshes", 0.95f);
                SaveGeneratedReentryMeshAssets(result, prefabPath, partName);
                SavePrefabChanges(prefabPath);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
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
}
