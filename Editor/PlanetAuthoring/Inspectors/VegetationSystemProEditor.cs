using System;
using System.IO;
using AwesomeTechnologies.VegetationSystem;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Custom inspector for <see cref="VegetationSystemPro" />, the per body terrain scatter surface.
    /// </summary>
    /// <remarks>
    /// The component carries well over a hundred fields, most of them runtime state or Vegetation
    /// Studio features that do not apply on a PQS body. This inspector shows the ones an author sets,
    /// plus a read only status block that answers "why is nothing showing" without the preview
    /// running.
    ///
    /// Setup problems are reported by the scatter validators, so they reach the validation chip and
    /// the report window with every other authoring problem.
    ///
    /// <c>TerrainType</c> has no correct value on a celestial body other than <c>PolarSphere</c>, and
    /// <c>_suspendVegetationSystem</c> none other than false, so both are left to Add Scatter System
    /// to set and to the validators to repair. The inspector's debug mode reaches the raw fields when
    /// something needs forcing.
    /// </remarks>
    [CustomEditor(typeof(VegetationSystemPro))]
    public class VegetationSystemProEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "/Assets/Windows/PlanetAuthoring/Inspectors/VegetationSystemProInspector.uxml";
        private const string UssPath = "/Assets/Windows/PlanetAuthoring/Inspectors/PQSInspector.uss";

        private const string DefaultPackageName = "ScatterPackage";

        private Label _statusLine;
        private Label _terrainLine;
        private Label _hint;

        private VegetationSystemPro TargetSystem => (VegetationSystemPro)target;

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                root.Add(new Label("Failed to load VegetationSystemProInspector.uxml"));
                return root;
            }
            tree.CloneTree(root);

            Ksp2UnityToolsStyles.Apply(root, UssPath);

            _statusLine = root.Q<Label>("scatter-status-line");
            _terrainLine = root.Q<Label>("scatter-terrain-line");
            _hint = root.Q<Label>("scatter-hint");

            var createPackage = root.Q<Button>("scatter-create-package");
            if (createPackage != null)
            {
                createPackage.clicked += OnCreatePackageClicked;
            }

            var syncBiomeMask = root.Q<Button>("scatter-sync-biome-mask");
            if (syncBiomeMask != null)
            {
                syncBiomeMask.clicked += OnSyncBiomeMaskClicked;
            }

            WirePreviewActions(root);

            BuildPackageList(root);
            root.Bind(serializedObject);

            RefreshStatus();
            root.schedule.Execute(RefreshStatus).Every(500);
            return root;
        }

        /// <summary>
        /// Builds the assigned-package list, each row hosting that package's editor inline.
        /// </summary>
        /// <remarks>
        /// The row header is the asset reference itself, so a slot can be seen and reassigned in
        /// place, and the package's own surface renders directly underneath it.
        /// </remarks>
        /// <param name="root">The inspector root holding the list container.</param>
        private void BuildPackageList(VisualElement root)
        {
            var container = root.Q<VisualElement>("scatter-package-list");
            if (container == null)
            {
                return;
            }

            SerializedProperty packages = serializedObject.FindProperty("VegetationPackageProList");
            container.Add(CardListSection.Build(packages, new CardListSection.Config
            {
                Title = "Packages",
                AddButtonText = "Add Slot",
                // No IdentityFieldName: the element is the package reference itself, so the header
                // field is built from the element rather than from a named child property.
                BuildIdentityField = entry => new PropertyField(entry, string.Empty),
                ChipFormatter = DescribePackage,
                BuildBody = (entry, body) => BuildPackageBody(entry, body),
            }));
        }

        /// <summary>
        /// Renders one package's editor into its card body, and keeps it matched to the slot's asset.
        /// </summary>
        /// <param name="entry">The list element, an object reference to the package.</param>
        /// <param name="body">The card body to populate.</param>
        private void BuildPackageBody(SerializedProperty entry, VisualElement body)
        {
            // Rebuilt whenever the slot is repointed, so the inline editor always matches the asset
            // named in the header rather than whichever package was there when the card was built.
            void Rebuild() =>
                ScatterPackageSection.Build(body, entry.objectReferenceValue as VegetationPackagePro, TargetSystem);

            body.TrackPropertyValue(entry, _ => Rebuild());
            Rebuild();
        }

        /// <summary>
        /// Summarises a package for its card's header chip.
        /// </summary>
        /// <param name="entry">The list element holding the package reference.</param>
        /// <returns>The biome and item count, or <c>empty</c> for an unassigned slot.</returns>
        private static string DescribePackage(SerializedProperty entry)
        {
            if (entry.objectReferenceValue is not VegetationPackagePro package)
            {
                return "empty";
            }

            int items = package.VegetationInfoList.Count;
            return $"{package.BiomeType}, {items} item{(items == 1 ? string.Empty : "s")}";
        }

        /// <summary>
        /// Wires the preview action buttons, which only do anything while a system is initialized.
        /// </summary>
        /// <param name="root">The inspector root.</param>
        private void WirePreviewActions(VisualElement root)
        {
            var refresh = root.Q<Button>("scatter-refresh-system");
            var clear = root.Q<Button>("scatter-clear-cells");
            var hint = root.Q<Label>("scatter-preview-hint");

            if (refresh != null)
            {
                refresh.clicked += () =>
                {
                    TargetSystem.RefreshVegetationSystem();
                    SetPreviewHint(hint, "Refreshed.");
                };
            }

            if (clear != null)
            {
                clear.clicked += () =>
                {
                    TargetSystem.OnDemandClearVegetationCells();
                    SetPreviewHint(hint, "Cells cleared. They respawn as the camera moves.");
                };
            }

            SetPreviewHint(hint, string.Empty);
        }

        private static void SetPreviewHint(Label hint, string message)
        {
            if (hint == null)
            {
                return;
            }

            hint.text = message;
            hint.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void RefreshStatus()
        {
            if (_statusLine == null || target == null)
            {
                return;
            }

            VegetationSystemPro system = TargetSystem;

            _statusLine.text = system.InitDone
                ? $"Initialized. Cells: {system.VegetationCellList.Count}   |   Loaded: {system.LoadedVegetationCellList.Count}"
                : "Not initialized. Start a planet preview to drive scatter in the editor.";

            int terrains = system.VegetationStudioTerrainObjectList.Count;
            int packages = system.VegetationPackageProList.Count;
            int items = 0;
            foreach (VegetationPackagePro package in system.VegetationPackageProList)
            {
                if (package != null)
                {
                    items += package.VegetationInfoList.Count;
                }
            }

            _terrainLine.text = $"Terrains: {terrains}    Packages: {packages}    Items: {items}";

            // The polar fields are derived from the registered terrain rather than authored, so they
            // are reported rather than offered as editable. A radius of zero here means the terrain
            // registration is what actually needs fixing.
            _hint.text = system.PolarSphereTransform != null
                ? $"Polar origin: {system.PolarSphereTransform.name}, radius {system.PolarSphereRadius:0} m (derived from the registered terrain)"
                : "Polar origin unresolved. It is derived from the registered terrain, so register one rather than setting it here.";
        }

        /// <summary>
        /// Points the scatter system at the body's own biome mask.
        /// </summary>
        /// <remarks>
        /// A convenience, not a rule. Scatter keeps its own mask reference and is free to use a
        /// different one, so nothing validates that the two agree.
        ///
        /// Only the texture is assigned. Which VSP biome slot each channel maps to is an authoring
        /// decision tied to the author's packages, and those dropdowns sit above this button.
        /// </remarks>
        private void OnSyncBiomeMaskClicked()
        {
            VegetationSystemPro system = TargetSystem;
            if (system == null)
            {
                return;
            }

            // Explicit comparisons rather than a null-conditional chain, which bypasses Unity's
            // overloaded equality and so treats a destroyed PQS as live.
            PQS pqs = system.GetComponentInParent<PQS>(true);
            PQSData data = pqs == null ? null : pqs.data;
            Texture2D mask = data == null || data.heightMapInfo == null ? null : data.heightMapInfo.mask;
            if (mask == null)
            {
                EditorUtility.DisplayDialog(
                    "No biome map on this body",
                    "This body has no biome mask assigned on its PQSData, so there is nothing to point "
                    + "the scatter system at. Assign one in the PQS inspector first.",
                    "OK");
                return;
            }

            Undo.RecordObject(system, "Use Body Biome Map");
            system.BiomeTextureMask = mask;
            EditorUtility.SetDirty(system);
            serializedObject.Update();
        }

        private void OnCreatePackageClicked()
        {
            VegetationSystemPro system = TargetSystem;

            string directory = ResolveDefaultDirectory(system);
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Vegetation Package",
                DefaultPackageName,
                "asset",
                "Choose where to save the new scatter package for this body.",
                directory);

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var package = CreateInstance<VegetationPackagePro>();
            package.PackageName = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(package, path);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(system, "Assign Vegetation Package");
            system.VegetationPackageProList.Add(package);
            EditorUtility.SetDirty(system);

            // Rebuild so the new package is picked up by a preview that is already running, rather
            // than only on the next session.
            if (system.InitDone)
            {
                system.RefreshVegetationSystem();
            }

            serializedObject.Update();
            // The list block builds its rows once, so a package added from outside it needs the
            // inspector rebuilt for the new row to appear.
            Repaint();
            EditorGUIUtility.PingObject(package);
        }

        /// <summary>
        /// Returns the folder holding the open scene, so a new package lands beside the body it
        /// belongs to.
        /// </summary>
        /// <remarks>
        /// Falls back to the body prefab's folder when the system is being inspected outside a saved
        /// scene, which covers selecting it on the prefab asset directly.
        /// </remarks>
        /// <param name="system">The system the package is being created for.</param>
        /// <returns>A project relative folder path, or an empty string to let Unity choose.</returns>
        private static string ResolveDefaultDirectory(VegetationSystemPro system)
        {
            string scenePath = system.gameObject.scene.path;
            if (!string.IsNullOrEmpty(scenePath))
            {
                return ToProjectFolder(scenePath);
            }

            GameObject prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(system.gameObject);
            string prefabPath = prefabSource != null ? AssetDatabase.GetAssetPath(prefabSource) : null;
            if (string.IsNullOrEmpty(prefabPath))
            {
                prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(system.gameObject);
            }

            return string.IsNullOrEmpty(prefabPath) ? string.Empty : ToProjectFolder(prefabPath);
        }

        /// <summary>
        /// Returns the containing folder of an asset path, with forward slashes.
        /// </summary>
        /// <remarks>
        /// <c>Path.GetDirectoryName</c> hands back backslashes on Windows, which the project relative
        /// save panel does not accept.
        /// </remarks>
        /// <param name="assetPath">A project relative asset path.</param>
        /// <returns>The containing folder, or an empty string.</returns>
        private static string ToProjectFolder(string assetPath)
        {
            string folder = Path.GetDirectoryName(assetPath);
            return string.IsNullOrEmpty(folder) ? string.Empty : folder.Replace('\\', '/');
        }
    }
}
