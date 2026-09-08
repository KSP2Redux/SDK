using System;
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
    /// Builds an include or exclude list of procedural texture rules over the stratum picker.
    /// </summary>
    /// <remarks>
    /// These rules are the surface restriction mechanism that decides where a scatter lands on a PQS
    /// body. A rule pairs a surface stratum with the blend weight window it has to fall inside, and
    /// <c>PqsTerrain</c> replays the body's own surface shader blend to evaluate it.
    ///
    /// Stock places vegetation far more by excluding rock than by including grass. Kerbin's grasses
    /// carry no include rule at all and exclude the two bare strata, and its poplars include one
    /// tree-friendly stratum then exclude the same two. Three strata carry the whole biome.
    /// </remarks>
    internal static class ScatterTextureRuleList
    {
        private const string RowUxmlPath = "/Assets/Windows/PlanetAuthoring/Inspectors/Shared/ScatterTextureRuleRow.uxml";
        private const string UssPath = "/Assets/Windows/PlanetAuthoring/PropertyFields/PropertyFields.uss";

        // Stock's include rules all open at a low but non-zero weight, which reads as "this stratum
        // is present at all" rather than "this stratum dominates".
        private const float DefaultMinimumWeight = 0.1f;
        private const float DefaultMaximumWeight = 1f;

        /// <summary>
        /// Builds the list into <paramref name="host" />.
        /// </summary>
        /// <param name="host">Element to populate. Emptied first.</param>
        /// <param name="rules">The rule list property, a list of <c>TerrainTextureRule</c>.</param>
        /// <param name="resolveLookup">Resolves the body's stratum lookup. Called on every build, so a repack is picked up.</param>
        /// <param name="title">Header text, with the rule count substituted at <c>{0}</c>.</param>
        /// <param name="emptyHint">Hint shown when the list is empty.</param>
        public static void Build(
            VisualElement host,
            SerializedProperty rules,
            Func<ScatterStratumLookup> resolveLookup,
            string title,
            string emptyHint)
        {
            if (host == null || rules == null || resolveLookup == null)
                return;

            Populate(host, rules, resolveLookup, title, emptyHint);

            // A repack renumbers slices, so a list built before one is naming the wrong strata. The
            // subscription is scoped to the panel because card bodies outlive neither a domain reload
            // nor a reselect, and a dangling handler would rebuild against a dead SerializedObject.
            void RebuildOnStrataChange()
            {
                if (host.panel == null || rules.serializedObject == null ||
                    rules.serializedObject.targetObject == null)
                    return;

                rules.serializedObject.Update();
                Populate(host, rules, resolveLookup, title, emptyHint);
            }

            host.RegisterCallback<AttachToPanelEvent>(_ => ScatterStratumLookup.OnStrataChanged += RebuildOnStrataChange);
            host.RegisterCallback<DetachFromPanelEvent>(_ => ScatterStratumLookup.OnStrataChanged -= RebuildOnStrataChange);
        }

        private static void Populate(
            VisualElement host,
            SerializedProperty rules,
            Func<ScatterStratumLookup> resolveLookup,
            string title,
            string emptyHint)
        {
            ScatterStratumLookup lookup = resolveLookup() ?? ScatterStratumLookup.Unavailable;

            host.Clear();

            // Attached to the host rather than to each row, since styles inherit down.
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(SDKConfiguration.BasePath + UssPath);
            if (styles != null && !host.styleSheets.Contains(styles))
            {
                host.styleSheets.Add(styles);
            }

            host.Add(InlineListBlock.Build(
                rules,
                titleFormat: title,
                addButtonText: "+ Add Rule",
                emptyHint: emptyHint,
                rowBuilder: (entry, _, onDelete) => BuildRow(entry, onDelete, lookup),
                onAdd: entry => SeedRule(entry, lookup)));
        }

        /// <summary>
        /// Seeds a freshly added rule with a window that does something.
        /// </summary>
        /// <remarks>
        /// A zeroed <c>TerrainTextureRule</c> is an all-pass window on whichever stratum happens to
        /// be slice 0. The seed points it at the first packed stratum and opens the window where
        /// stock opens it, so a new rule restricts something from the moment it is added.
        /// </remarks>
        /// <param name="entry">The newly appended element.</param>
        /// <param name="lookup">The body's stratum lookup.</param>
        private static void SeedRule(SerializedProperty entry, ScatterStratumLookup lookup)
        {
            SerializedProperty minimum = entry.FindPropertyRelative("MinimumValue");
            SerializedProperty maximum = entry.FindPropertyRelative("MaximumValue");
            if (minimum != null)
            {
                minimum.floatValue = DefaultMinimumWeight;
            }

            if (maximum != null)
            {
                maximum.floatValue = DefaultMaximumWeight;
            }

            SerializedProperty index = entry.FindPropertyRelative("TextureIndex");
            if (index == null || lookup == null)
                return;

            foreach (ScatterStratum stratum in lookup.Strata)
            {
                if (stratum.IsPacked)
                {
                    index.intValue = stratum.SliceIndex;
                    return;
                }
            }
        }

        private static VisualElement BuildRow(SerializedProperty entry, Action onDelete, ScatterStratumLookup lookup)
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + RowUxmlPath);
            if (tree == null)
                return new Label("Failed to load ScatterTextureRuleRow.uxml");

            // Bound to the element rather than the object, so the markup's binding paths resolve
            // against this rule without naming its index in the list.
            var row = new BindableElement();
            tree.CloneTree(row);

            var stratumHost = row.Q<VisualElement>("stratum-host");
            if (stratumHost != null)
            {
                // Relative to the row's own binding, as the item markup does.
                var picker = new ScatterStratumField(
                    null,
                    "Surface stratum this rule applies to.",
                    lookup)
                {
                    bindingPath = "TextureIndex",
                };
                stratumHost.Add(picker);
            }

            var minimumField = row.Q<FloatField>("minimum");
            var maximumField = row.Q<FloatField>("maximum");
            var slider = row.Q<MinMaxSlider>("window");
            var warning = row.Q<Label>("warning");

            if (minimumField != null)
            {
                minimumField.bindingPath = "MinimumValue";
            }

            if (maximumField != null)
            {
                maximumField.bindingPath = "MaximumValue";
            }

            // After the paths are set, so the first binding pass carries real values into the fields
            // the window wiring reads from.
            row.BindProperty(entry);
            WireWindow(minimumField, maximumField, slider, warning);

            var remove = row.Q<Button>("remove");
            if (remove != null)
            {
                remove.clicked += onDelete;
            }

            return row;
        }

        /// <summary>
        /// Keeps the slider, the two bounds and the impossible-window warning agreeing.
        /// </summary>
        /// <remarks>
        /// The slider is a view over the two bound fields. It sets their values and lets the existing
        /// binding carry the change, which keeps undo and multi-object editing working.
        ///
        /// Nothing in the spawn path clamps or warns about an inverted window, so it rejects every
        /// candidate and the item disappears with no diagnostic. The warning label covers that.
        /// </remarks>
        /// <param name="minimumField">The lower bound field.</param>
        /// <param name="maximumField">The upper bound field.</param>
        /// <param name="slider">The window slider.</param>
        /// <param name="warning">Label carrying the impossible-window message.</param>
        private static void WireWindow(FloatField minimumField, FloatField maximumField, MinMaxSlider slider, Label warning)
        {
            if (minimumField == null || maximumField == null)
                return;

            void Refresh()
            {
                float low = minimumField.value;
                float high = maximumField.value;

                slider?.SetValueWithoutNotify(new Vector2(Mathf.Clamp01(low), Mathf.Clamp01(high)));

                if (warning == null)
                    return;

                bool impossible = ScatterCountMath.IsImpossibleWindow(low, high);
                warning.text = impossible
                    ? "Minimum is above maximum, so this rule rejects every point and the item will not spawn."
                    : string.Empty;
                warning.style.display = impossible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            minimumField.RegisterValueChangedCallback(_ => Refresh());
            maximumField.RegisterValueChangedCallback(_ => Refresh());

            slider?.RegisterValueChangedCallback(evt =>
            {
                minimumField.value = evt.newValue.x;
                maximumField.value = evt.newValue.y;
            });

            Refresh();
        }
    }
}
