using System;
using AwesomeTechnologies.Utility;
using AwesomeTechnologies.VegetationSystem;
using Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Fields;
using Ksp2UnityTools.Editor.PlanetAuthoring.Scatter;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Builds the authorable surface of one <see cref="VegetationPackagePro" />.
    /// </summary>
    /// <remarks>
    /// Hosted twice: by the package asset's own inspector, and inline inside the
    /// <see cref="VegetationSystemPro" /> inspector once per assigned package. It takes the package
    /// explicitly and binds to a <see cref="SerializedObject" /> of its own, so the same UI can sit
    /// under a component inspector whose target is a different object.
    /// </remarks>
    internal static class ScatterPackageSection
    {
        private const string UxmlPath = "/Assets/Windows/PlanetAuthoring/Inspectors/Shared/ScatterPackageSection.uxml";
        private const string ItemUxmlPath = "/Assets/Windows/PlanetAuthoring/Inspectors/Shared/ScatterItemSection.uxml";

        // Object picker control IDs only have to be unique, not GUI-allocated. Handing out a fresh
        // one per built section keeps a selection made in one package's picker from being read by
        // another's handler, which matters because several sections are alive at once inside the
        // VegetationSystemPro inspector.
        private static int _nextPickerId = 0x5CA7;

        /// <summary>
        /// Builds the package UI into <paramref name="container" />, bound to <paramref name="package" />.
        /// </summary>
        /// <remarks>
        /// Clears <paramref name="container" /> first, so a host can call this again when its package
        /// assignment changes.
        /// </remarks>
        /// <param name="container">Element to populate. Emptied before building.</param>
        /// <param name="package">The package being edited. Null renders a placeholder.</param>
        /// <param name="owningSystem">The system the package is assigned to, used to annotate biome mapping. May be null.</param>
        public static void Build(VisualElement container, VegetationPackagePro package, VegetationSystemPro owningSystem)
        {
            if (container == null)
                return;

            container.Clear();

            if (package == null)
            {
                var empty = new Label("Empty package slot.");
                empty.AddToClassList("sdk-hint");
                container.Add(empty);
                return;
            }

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                container.Add(new Label("Failed to load ScatterPackageSection.uxml"));
                return;
            }

            tree.CloneTree(container);

            var serialized = new SerializedObject(package);
            BuildItemList(container, serialized, owningSystem);
            WireAddItem(container, package, serialized, owningSystem);
            RefreshBiomeHint(container, package, owningSystem);

            // Bind to the package rather than to whatever the host inspector targets.
            container.Bind(serialized);
        }

        /// <summary>
        /// Builds the package's item list as a card list.
        /// </summary>
        /// <remarks>
        /// Reordering is on because item order is load bearing for dependency rules. Spawning walks
        /// the list in order and a dependency rule only takes effect if the item it depends on
        /// already has its buffer, so an item that depends on one further down the list has its rule
        /// silently ignored.
        ///
        /// Adding goes through the prefab row and the package's own API, which issues the ID, shader
        /// controller and render mode a new item needs.
        /// </remarks>
        /// <param name="container">The built section.</param>
        /// <param name="serialized">The package's serialized object.</param>
        private static void BuildItemList(VisualElement container, SerializedObject serialized, VegetationSystemPro owningSystem)
        {
            RebuildItemList(container, serialized, owningSystem);
        }

        /// <summary>
        /// Rebuilds the item card list from scratch.
        /// </summary>
        /// <remarks>
        /// Needed because <c>AddVegetationItem</c> mutates the backing list directly rather than
        /// through the serialized property, so the card list cannot observe the change and would
        /// keep showing a stale count.
        /// </remarks>
        /// <param name="container">The built section.</param>
        /// <param name="serialized">The package's serialized object.</param>
        private static void RebuildItemList(VisualElement container, SerializedObject serialized, VegetationSystemPro owningSystem)
        {
            var host = container.Q<VisualElement>("scatter-package-items");
            if (host == null)
                return;

            host.Clear();
            serialized.Update();

            // Resolved lazily, once per package rather than once per item. Inferring a package's body
            // can scan every loaded scene, and every item on the package wants the same answer. The
            // change counter is what lets a repack invalidate it without an open inspector caching a
            // stale numbering for the rest of the session.
            var package = serialized.targetObject as VegetationPackagePro;
            ScatterStratumLookup cachedLookup = null;
            int cachedVersion = -1;

            ScatterStratumLookup ResolveLookup()
            {
                if (cachedLookup != null && cachedVersion == ScatterStratumLookup.ChangeVersion)
                    return cachedLookup;

                cachedLookup = owningSystem != null
                    ? ScatterStratumLookup.FromSystem(owningSystem)
                    : ScatterStratumLookup.FromPackage(package);
                cachedVersion = ScatterStratumLookup.ChangeVersion;
                return cachedLookup;
            }

            SerializedProperty items = serialized.FindProperty("VegetationInfoList");
            host.Add(CardListSection.Build(items, new CardListSection.Config
            {
                Title = "Items",
                AddButtonText = null,
                AllowReorder = true,
                IdentityFieldName = "Name",
                ChipFieldName = "VegetationType",
                ChipFormatter = prop => prop.enumDisplayNames[prop.enumValueIndex],
                BuildBody = (entry, body) => BuildItemBody(entry, body, owningSystem, ResolveLookup),
                OnDuplicate = index => DuplicateItem(container, serialized, owningSystem, index),
            }));
        }

        /// <summary>
        /// Duplicates the item at <paramref name="index" />.
        /// </summary>
        /// <remarks>
        /// Goes through the package's own duplicate method so the copy is issued a fresh
        /// <c>VegetationItemID</c>, which is what hand placed instances and dependency rules refer
        /// to.
        /// </remarks>
        /// <param name="container">The built section, rebuilt afterwards.</param>
        /// <param name="serialized">The package's serialized object.</param>
        /// <param name="index">Index of the item to duplicate.</param>
        private static void DuplicateItem(VisualElement container, SerializedObject serialized, VegetationSystemPro owningSystem, int index)
        {
            if (serialized.targetObject is not VegetationPackagePro package ||
                index < 0 || index >= package.VegetationInfoList.Count)
                return;

            Undo.RecordObject(package, "Duplicate Scatter Item");
            package.DuplicateVegetationItem(package.VegetationInfoList[index]);
            EditorUtility.SetDirty(package);

            RebuildItemList(container, serialized, owningSystem);
        }

        /// <summary>
        /// Renders one item's fields into its card body.
        /// </summary>
        /// <param name="entry">The item element.</param>
        /// <param name="body">The card body to populate.</param>
        /// <param name="owningSystem">The system the package is assigned to, used for the derived spacing readout. May be null.</param>
        private static void BuildItemBody(
            SerializedProperty entry,
            VisualElement body,
            VegetationSystemPro owningSystem,
            Func<ScatterStratumLookup> resolveLookup)
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + ItemUxmlPath);
            if (tree == null)
            {
                body.Add(new Label("Failed to load ScatterItemSection.uxml"));
                return;
            }

            // A BindableElement wrapper, because binding a subtree to a property rather than to an
            // object needs a receiver that can carry that binding. With it bound to the list element,
            // the markup's binding paths resolve against the item without naming its index.
            var bindable = new BindableElement();
            tree.CloneTree(bindable);
            bindable.BindProperty(entry);
            body.Add(bindable);

            WireSpacingReadout(bindable, entry, owningSystem);
            WireSurfaceRestriction(bindable, entry, resolveLookup);

            // The shader controller decides which appearance properties exist by probing the material,
            // so the set cannot be expressed in markup and is built from the descriptors instead.
            ControllerPropertyList.Build(
                bindable.Q<VisualElement>("scatter-shader-properties"),
                entry.FindPropertyRelative("ShaderControllerSettings.ControlerPropertyList"));
        }

        /// <summary>
        /// Replaces the two procedural rule lists with stratum-picker lists.
        /// </summary>
        /// <remarks>
        /// A rule stores the packed slice index, which the picker resolves to a named stratum. That
        /// index is not a number anyone can be expected to know or to derive from the PQS inspector.
        ///
        /// The lookup depends on the body rather than on the package, so it arrives as a callback the
        /// caller resolves and caches. Editing the package asset standalone has no system to ask, so
        /// the body is inferred from the scene or the folder instead. An unresolvable body yields the
        /// plain numeric field.
        /// </remarks>
        /// <param name="root">The built item subtree.</param>
        /// <param name="entry">The item element.</param>
        private static void WireSurfaceRestriction(
            VisualElement root,
            SerializedProperty entry,
            Func<ScatterStratumLookup> resolveLookup)
        {
            ScatterTextureRuleList.Build(
                root.Q<VisualElement>("scatter-include-rules"),
                entry.FindPropertyRelative("ProceduralTextureIncludeRuleList"),
                resolveLookup,
                "Include rules ({0})",
                "(none - the item is not restricted to any stratum)");

            ScatterTextureRuleList.Build(
                root.Q<VisualElement>("scatter-exclude-rules"),
                entry.FindPropertyRelative("ProceduralTextureExcludeRuleList"),
                resolveLookup,
                "Exclude rules ({0})",
                "(none - the item is kept off nothing)");
        }

        /// <summary>
        /// Keeps the derived spacing line under Grid Spacing up to date.
        /// </summary>
        /// <remarks>
        /// The authored Grid Spacing is not the spacing the spawner uses. The global per-type density
        /// multiplier divides it, and the polar factor divides it again, so an author tuning this
        /// number cannot tell what it costs without being told. The arithmetic comes from
        /// <see cref="ScatterCountMath" />, which mirrors the spawner.
        /// </remarks>
        /// <param name="root">The built item subtree.</param>
        /// <param name="entry">The item element.</param>
        /// <param name="owningSystem">The system the package is assigned to. Null when editing the asset standalone.</param>
        private static void WireSpacingReadout(VisualElement root, SerializedProperty entry, VegetationSystemPro owningSystem)
        {
            var hint = root.Q<Label>("scatter-item-spacing-hint");
            SerializedProperty spacing = entry.FindPropertyRelative("SampleDistance");
            SerializedProperty type = entry.FindPropertyRelative("VegetationType");
            if (hint == null || spacing == null || type == null)
                return;

            void Refresh()
            {
                if (owningSystem == null || owningSystem.VegetationSettings == null)
                {
                    SetStatus(hint, string.Empty);
                    return;
                }

                float globalDensity =
                    owningSystem.VegetationSettings.GetVegetationItemDensity((VegetationType)type.enumValueIndex);
                float effective = ScatterCountMath.EffectiveSpacingMeters(spacing.floatValue, globalDensity);
                if (effective <= 0f)
                {
                    SetStatus(hint, "Global density for this type is zero, so this item spawns nothing.");
                    return;
                }

                long perCell = ScatterCountMath.CandidateCount(
                    CoordinateUtility.GetPolarCellSize(owningSystem.VegetationCellSize, owningSystem.PolarSphereRadius),
                    spacing.floatValue,
                    globalDensity,
                    0f,
                    owningSystem.PolarSphereRadius);

                SetStatus(hint, $"Effective spacing {effective:0.##} m after the global density for this type. "
                    + $"About {perCell} candidates per cell at the equator, before rules.");
            }

            hint.TrackPropertyValue(spacing, _ => Refresh());
            hint.TrackPropertyValue(type, _ => Refresh());
            Refresh();
        }

        /// <summary>
        /// Wires the add button to Unity's asset picker.
        /// </summary>
        /// <remarks>
        /// The picker reports its result as an IMGUI command, so a zero sized
        /// <see cref="IMGUIContainer" /> rides along to catch it. That is the supported way to use
        /// the object picker from a UITK inspector.
        /// </remarks>
        /// <param name="container">The built section.</param>
        /// <param name="package">The package being added to.</param>
        /// <param name="serialized">The package's serialized object, refreshed after a structural change.</param>
        private static void WireAddItem(VisualElement container, VegetationPackagePro package, SerializedObject serialized, VegetationSystemPro owningSystem)
        {
            var addButton = container.Q<Button>("scatter-package-add-button");
            var status = container.Q<Label>("scatter-package-status");
            if (addButton == null)
                return;

            int pickerId = ++_nextPickerId;

            var pickerEvents = new IMGUIContainer(() =>
            {
                Event current = Event.current;
                if (current.type != EventType.ExecuteCommand ||
                    current.commandName != "ObjectSelectorClosed" ||
                    EditorGUIUtility.GetObjectPickerControlID() != pickerId)
                    return;

                if (EditorGUIUtility.GetObjectPickerObject() is not GameObject prefab)
                    return;

                Undo.RecordObject(package, "Add Scatter Item");
                // Goes through the package's own API so the item gets its GUID, shader controller
                // settings and render mode set the way the runtime expects.
                package.AddVegetationItem(prefab, VegetationType.Objects, true);
                EditorUtility.SetDirty(package);
                serialized.Update();

                // AddVegetationItem mutates the list directly rather than through the serialized
                // property, so the card list has no idea it grew. Rebuild it.
                RebuildItemList(container, serialized, owningSystem);
                SetStatus(status, $"Added '{prefab.name}'.");
            });
            pickerEvents.style.height = 0;
            container.Add(pickerEvents);

            addButton.clicked += () =>
                EditorGUIUtility.ShowObjectPicker<GameObject>(null, false, string.Empty, pickerId);
        }

        /// <summary>
        /// Notes whether the package's biome is mapped to a mask channel on the owning system.
        /// </summary>
        /// <remarks>
        /// An unmapped non-default biome spawns only where texture rules allow. Stock Kerbin's fifth
        /// package is set up exactly that way, so the hint reports the state rather than flagging it
        /// as an error.
        /// </remarks>
        /// <param name="container">The built section.</param>
        /// <param name="package">The package being described.</param>
        /// <param name="owningSystem">The system the package is assigned to, or null when editing the asset standalone.</param>
        private static void RefreshBiomeHint(VisualElement container, VegetationPackagePro package, VegetationSystemPro owningSystem)
        {
            var hint = container.Q<Label>("scatter-package-biome-hint");
            if (hint == null)
                return;

            if (owningSystem == null || package.BiomeType == BiomeType.Default)
            {
                SetStatus(hint, string.Empty);
                return;
            }

            bool mapped = ScatterBiomeChannels.TryGetMaskChannel(owningSystem, package.BiomeType, out _);

            SetStatus(hint, mapped
                ? string.Empty
                : $"Biome '{package.BiomeType}' is not mapped to a mask channel on this body, so this package spawns only where its item rules allow.");
        }

        private static void SetStatus(Label label, string message)
        {
            if (label == null)
                return;

            label.text = message;
            label.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
