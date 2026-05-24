using System;
using System.Collections.Generic;
using KSP;
using Ksp2UnityTools.Editor.PartAuthoring.Windows;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Windows
{
    /// <summary>Dockable window that filters stock parts by family and size and renders one block per matching bucket.</summary>
    /// <remarks>
    /// Subscribes to <see cref="ActivePartTracker" /> so selection in the project / hierarchy /
    /// prefab stage seeds the Family and Size filter fields. Loads the shipped
    /// <see cref="StockStatsLookup" /> from the package's <c>Assets/</c> subfolder via
    /// <see cref="AssetDatabase.LoadAssetAtPath" />. The lookup type lives in the editor assembly
    /// so the asset cannot ship at runtime.
    ///
    /// Filter semantics are case-insensitive <c>String.Contains</c> on both axes. Empty filter on
    /// an axis matches everything on that axis. Each surviving bucket renders as a Foldout with
    /// its field ranges and reference-part list. Multiple matches simultaneously expand into
    /// multiple blocks so authors can compare related buckets side by side.
    /// </remarks>
    public sealed class ReferencePartsWindow : EditorWindow
    {
        private const string UXML_PATH = "/Assets/Windows/PartAuthoring/Windows/ReferencePartsWindow.uxml";
        private const string USS_PATH = "/Assets/Windows/PartAuthoring/Windows/ReferencePartsWindow.uss";
        private const string LOOKUP_ASSET_PATH = SDKConfiguration.BasePath + "/Assets/StockStats/StockStatsLookup.asset";

        private VisualElement _bucketOverrideSlot;
        private AutocompleteField _familyField;
        private AutocompleteField _sizeField;
        private Label _matchCountLabel;
        private VisualElement _emptyStateSlot;
        private VisualElement _bucketsContainer;
        private VisualElement _stalenessSlot;

        private StockStatsLookup _lookup;
        private CorePartData _activePart;
        private List<StockBucket> _matchingBuckets = new();
        private string _familyKey;
        private string _sizeKey;

        [MenuItem(PartAuthoringWindows.MENU_ROOT + "Reference Parts %#r", priority = PartAuthoringWindows.PRIORITY_REFERENCE_PARTS)]
        public static void ShowWindow()
        {
            var window = GetWindow<ReferencePartsWindow>();
            window.titleContent = new GUIContent("Reference Parts");
            window.minSize = new Vector2(420f, 360f);
        }

        private void OnEnable()
        {
            ActivePartTracker.OnChanged += OnActivePartChanged;
        }

        private void OnDisable()
        {
            ActivePartTracker.OnChanged -= OnActivePartChanged;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UXML_PATH);
            if (tree == null)
            {
                root.Add(new Label($"Failed to load {UXML_PATH}"));
                return;
            }
            tree.CloneTree(root);
            Ksp2UnityToolsStyles.Apply(root, USS_PATH);

            _bucketOverrideSlot = root.Q<VisualElement>("bucket-override-slot");
            _matchCountLabel = root.Q<Label>("match-count-label");
            _emptyStateSlot = root.Q<VisualElement>("empty-state-slot");
            _bucketsContainer = root.Q<VisualElement>("buckets-container");
            _stalenessSlot = root.Q<VisualElement>("staleness-slot");

            _familyField = new AutocompleteField(
                initialValue: string.Empty,
                label: "Family",
                suggestionSource: GetFamilyChoices,
                onValueChanged: OnFamilyChanged);
            _sizeField = new AutocompleteField(
                initialValue: string.Empty,
                label: "Size",
                suggestionSource: GetSizeChoices,
                onValueChanged: OnSizeChanged);
            _bucketOverrideSlot.Add(_familyField);
            _bucketOverrideSlot.Add(_sizeField);

            _lookup = AssetDatabase.LoadAssetAtPath<StockStatsLookup>(LOOKUP_ASSET_PATH);
            _activePart = ActivePartTracker.Current;
            SyncFieldsFromActivePart();
            ResolveBuckets();
            Refresh();
        }

        private void OnActivePartChanged(CorePartData newActive)
        {
            _activePart = newActive;
            SyncFieldsFromActivePart();
            ResolveBuckets();
            Refresh();
        }

        private void OnFamilyChanged(string newValue)
        {
            _familyKey = newValue ?? string.Empty;
            ResolveBuckets();
            Refresh();
        }

        private void OnSizeChanged(string newValue)
        {
            _sizeKey = newValue ?? string.Empty;
            ResolveBuckets();
            Refresh();
        }

        private void SyncFieldsFromActivePart()
        {
            _familyKey = _activePart?.Data?.family ?? string.Empty;
            _sizeKey = _activePart?.Data != null ? _activePart.Data.sizeCategory.ToString() : string.Empty;
            _familyField?.SetValueWithoutNotify(_familyKey);
            _sizeField?.SetValueWithoutNotify(_sizeKey);
        }

        private void ResolveBuckets()
        {
            _matchingBuckets = new List<StockBucket>();
            if (_lookup?.Buckets == null)
            {
                return;
            }
            foreach (var bucket in _lookup.Buckets)
            {
                if (bucket == null)
                {
                    continue;
                }
                if (!MatchesFilter(bucket.Family, _familyKey))
                {
                    continue;
                }
                if (!MatchesFilter(bucket.SizeCategory, _sizeKey))
                {
                    continue;
                }
                _matchingBuckets.Add(bucket);
            }
        }

        private static bool MatchesFilter(string value, string filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                return true;
            }
            return (value ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private IEnumerable<string> GetFamilyChoices()
        {
            if (_lookup?.Buckets == null)
            {
                yield break;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var bucket in _lookup.Buckets)
            {
                if (bucket == null || string.IsNullOrEmpty(bucket.Family))
                {
                    continue;
                }
                if (seen.Add(bucket.Family))
                {
                    yield return bucket.Family;
                }
            }
        }

        private IEnumerable<string> GetSizeChoices()
        {
            if (_lookup?.Buckets == null)
            {
                yield break;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var bucket in _lookup.Buckets)
            {
                if (bucket == null || string.IsNullOrEmpty(bucket.SizeCategory))
                {
                    continue;
                }
                if (!MatchesFilter(bucket.Family, _familyKey))
                {
                    continue;
                }
                if (seen.Add(bucket.SizeCategory))
                {
                    yield return bucket.SizeCategory;
                }
            }
        }

        private void Refresh()
        {
            ClearEmptyState();
            ClearStalenessWarning();
            _bucketsContainer.Clear();

            if (_lookup == null)
            {
                _matchCountLabel.text = string.Empty;
                ShowEmptyState("Lookup asset not found. Open Modding > Part Authoring > Stock Stats Bake to generate it.", HelpBoxMessageType.Warning);
                return;
            }
            if (_matchingBuckets.Count == 0)
            {
                _matchCountLabel.text = "0 matching buckets";
                ShowEmptyState("No buckets match the current filter. Clear a field to broaden the search, or type something different.", HelpBoxMessageType.Info);
                MaybeRenderStaleness();
                return;
            }

            _matchCountLabel.text = _matchingBuckets.Count == 1
                ? "1 matching bucket"
                : $"{_matchingBuckets.Count} matching buckets";

            foreach (var bucket in _matchingBuckets)
            {
                _bucketsContainer.Add(BuildBucketBlock(bucket));
            }
            MaybeRenderStaleness();
        }

        private VisualElement BuildBucketBlock(StockBucket bucket)
        {
            int partCount = bucket.ContributingParts?.Count ?? 0;
            string header = $"{bucket.Family} / {bucket.SizeCategory}  ({partCount} {(partCount == 1 ? "part" : "parts")})";
            var foldout = new Foldout { text = header, value = true };
            foldout.AddToClassList("reference-parts-bucket-block");

            var rangesHeader = new Label("Field ranges");
            rangesHeader.AddToClassList("reference-parts-subsection-label");
            foldout.Add(rangesHeader);
            BuildRanges(foldout, bucket);

            var partsHeader = new Label("Reference parts");
            partsHeader.AddToClassList("reference-parts-subsection-label");
            foldout.Add(partsHeader);
            BuildParts(foldout, bucket);

            return foldout;
        }

        private void BuildRanges(VisualElement parent, StockBucket bucket)
        {
            if (bucket.Fields == null || bucket.Fields.Count == 0)
            {
                parent.Add(new Label("Bucket has no tracked fields.") { tooltip = "Check the bake or the extractor registry." });
                return;
            }
            var resolved = new List<(StockFieldEntry Entry, StockField Field, string Module, string SubKey)>();
            foreach (var field in bucket.Fields)
            {
                if (field == null)
                {
                    continue;
                }
                var entry = StockFieldPaths.Find(field.Name);
                if (entry == null)
                {
                    continue;
                }
                resolved.Add((entry, field, ExtractModuleKey(field.Name), entry.SubKey ?? string.Empty));
            }
            RenderGrouped(parent, resolved.Count,
                moduleOf: i => resolved[i].Module,
                subKeyOf: i => resolved[i].SubKey,
                renderRow: i => BuildRangeRow(resolved[i].Field, resolved[i].Entry));
        }

        private VisualElement BuildRangeRow(StockField field, StockFieldEntry entry)
        {
            var row = new VisualElement();
            row.AddToClassList("reference-parts-range-row");

            var name = new Label(entry.DisplayName);
            name.AddToClassList("reference-parts-range-name");
            row.Add(name);

            string text = $"{FormatValue(field.Min, entry)}  to  {FormatValue(field.Max, entry)}    median {FormatValue(field.Median, entry)} ({field.Count})";
            var rangeText = new Label(text);
            rangeText.AddToClassList("reference-parts-range-text");
            row.Add(rangeText);

            float activeValue = 0f;
            bool haveActive = _activePart != null
                && (ActivePartFieldReader.TryRead(field.Name, _activePart, out activeValue)
                    || TryComputeDerivedActive(field.Name, out activeValue));
            if (haveActive)
            {
                var active = new Label($"this: {FormatValue(activeValue, entry)}");
                active.AddToClassList("reference-parts-range-active");
                if (activeValue < field.Min || activeValue > field.Max)
                {
                    active.AddToClassList("reference-parts-range-active--out");
                }
                row.Add(active);
            }
            return row;
        }

        private bool TryComputeDerivedActive(string fieldName, out float value)
        {
            value = 0f;
            if (_activePart?.Data == null || _lookup == null)
            {
                return false;
            }
            if (fieldName == StockFieldNames.TankResourcePercent)
            {
                return TryComputeTankResourcePercent(out value);
            }
            return false;
        }

        private bool TryComputeTankResourcePercent(out float value)
        {
            value = 0f;
            var data = _activePart.Data;
            if (data.resourceContainers == null || data.resourceContainers.Count == 0)
            {
                return false;
            }
            if (data.mass <= 0f)
            {
                return false;
            }
            float fuelMass = 0f;
            foreach (KSP.Sim.ResourceSystem.ContainedResourceDefinition c in data.resourceContainers)
            {
                if (c == null || string.IsNullOrEmpty(c.name))
                {
                    continue;
                }
                if (!_lookup.TryGetResourceMass(c.name, out float massPerUnit))
                {
                    return false;
                }
                fuelMass += (float)c.capacityUnits * massPerUnit;
            }
            if (fuelMass <= 0f)
            {
                return false;
            }
            value = fuelMass / ((float)data.mass + fuelMass) * 100f;
            return true;
        }

        private static readonly string[] _moduleRenderOrder = { "engine", "tank", "rcs" };

        private static readonly Dictionary<string, string> _moduleDisplayNames =
            new(StringComparer.Ordinal)
            {
                { "engine", "Engine" },
                { "tank", "Tank" },
                { "rcs", "RCS" },
            };

        private void BuildParts(VisualElement parent, StockBucket bucket)
        {
            if (bucket.ContributingParts == null || bucket.ContributingParts.Count == 0)
            {
                parent.Add(new Label("No contributing parts in this bucket."));
                return;
            }
            foreach (var partRef in bucket.ContributingParts)
            {
                if (partRef == null)
                {
                    continue;
                }
                var foldout = new Foldout { text = partRef.PartName ?? "(unnamed)", value = false };
                foldout.AddToClassList("reference-parts-part-foldout");
                BuildPartFields(foldout, partRef);
                parent.Add(foldout);
            }
        }

        private void BuildPartFields(Foldout foldout, StockPartRef partRef)
        {
            if (partRef.FieldValues == null)
            {
                return;
            }
            var resolved = new List<(StockFieldEntry Entry, StockPartFieldValue Value, string Module, string SubKey)>();
            foreach (var fv in partRef.FieldValues)
            {
                if (fv == null)
                {
                    continue;
                }
                var entry = StockFieldPaths.Find(fv.Name);
                if (entry == null)
                {
                    continue;
                }
                resolved.Add((entry, fv, ExtractModuleKey(fv.Name), entry.SubKey ?? string.Empty));
            }
            RenderGrouped(foldout, resolved.Count,
                moduleOf: i => resolved[i].Module,
                subKeyOf: i => resolved[i].SubKey,
                renderRow: i => BuildFieldRow(partRef, resolved[i].Value));
        }

        /// <summary>
        /// Walks <paramref name="count" /> indices in the natural order, emitting module headers
        /// when the module changes, sub-key sub-headers when the sub-key changes within a
        /// module, and per-row content via <paramref name="renderRow" />. Within each module the
        /// unkeyed entries are emitted first, then per-sub-key groups in case-insensitive
        /// sub-key order.
        /// </summary>
        private void RenderGrouped(
            VisualElement parent,
            int count,
            System.Func<int, string> moduleOf,
            System.Func<int, string> subKeyOf,
            System.Func<int, VisualElement> renderRow)
        {
            var basePart = new List<int>();
            var byModule = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
            {
                string module = moduleOf(i) ?? string.Empty;
                if (string.IsNullOrEmpty(module))
                {
                    basePart.Add(i);
                    continue;
                }
                if (!byModule.TryGetValue(module, out var list))
                {
                    list = new List<int>();
                    byModule[module] = list;
                }
                list.Add(i);
            }
            foreach (int i in basePart)
            {
                parent.Add(renderRow(i));
            }
            foreach (var moduleKey in _moduleRenderOrder)
            {
                if (!byModule.TryGetValue(moduleKey, out var indices) || indices.Count == 0)
                {
                    continue;
                }
                parent.Add(BuildModuleHeader(_moduleDisplayNames[moduleKey]));
                var moduleGroup = new VisualElement();
                moduleGroup.AddToClassList("reference-parts-module-group");
                parent.Add(moduleGroup);
                RenderModuleSubGroups(moduleGroup, indices, subKeyOf, renderRow);
            }
            foreach (var kvp in byModule)
            {
                if (System.Array.IndexOf(_moduleRenderOrder, kvp.Key) >= 0)
                {
                    continue;
                }
                if (kvp.Value.Count == 0)
                {
                    continue;
                }
                parent.Add(BuildModuleHeader(TitleCase(kvp.Key)));
                var moduleGroup = new VisualElement();
                moduleGroup.AddToClassList("reference-parts-module-group");
                parent.Add(moduleGroup);
                RenderModuleSubGroups(moduleGroup, kvp.Value, subKeyOf, renderRow);
            }
        }

        private void RenderModuleSubGroups(
            VisualElement parent,
            List<int> indices,
            System.Func<int, string> subKeyOf,
            System.Func<int, VisualElement> renderRow)
        {
            foreach (int i in indices)
            {
                if (string.IsNullOrEmpty(subKeyOf(i)))
                {
                    parent.Add(renderRow(i));
                }
            }
            var bySubKey = new SortedDictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            foreach (int i in indices)
            {
                string sub = subKeyOf(i);
                if (string.IsNullOrEmpty(sub))
                {
                    continue;
                }
                if (!bySubKey.TryGetValue(sub, out var list))
                {
                    list = new List<int>();
                    bySubKey[sub] = list;
                }
                list.Add(i);
            }
            foreach (var kvp in bySubKey)
            {
                parent.Add(BuildSubKeyHeader(kvp.Key));
                var subContainer = new VisualElement();
                subContainer.AddToClassList("reference-parts-subkey-group");
                foreach (int i in kvp.Value)
                {
                    subContainer.Add(renderRow(i));
                }
                parent.Add(subContainer);
            }
        }

        private VisualElement BuildFieldRow(StockPartRef partRef, StockPartFieldValue fv)
        {
            var entry = StockFieldPaths.Find(fv.Name);
            if (entry == null)
            {
                return new VisualElement();
            }
            var row = new VisualElement();
            row.AddToClassList("reference-parts-field-row");

            var fieldName = new Label(entry.DisplayName);
            fieldName.AddToClassList("reference-parts-field-name");
            row.Add(fieldName);

            var valueLabel = new Label(FormatValue(fv.Value, entry));
            valueLabel.AddToClassList("reference-parts-field-value");
            row.Add(valueLabel);

            if (entry.IsCopyable && _activePart != null)
            {
                float capturedValue = fv.Value;
                var copy = new Button(() => DoCopy(entry, capturedValue))
                {
                    text = "Copy",
                    tooltip = $"Copy {entry.DisplayName} = {FormatValue(capturedValue, entry)} from {partRef.PartName} to the active part.",
                };
                copy.AddToClassList("reference-parts-copy-button");
                row.Add(copy);
            }
            else if (!entry.IsCopyable)
            {
                var info = new Label("ⓘ")
                {
                    tooltip = entry.NonCopyableReason ?? "Read-only in V1.",
                };
                info.AddToClassList("reference-parts-info-icon");
                row.Add(info);
            }
            return row;
        }

        private static Label BuildModuleHeader(string text)
        {
            var label = new Label(text);
            label.AddToClassList("reference-parts-module-header");
            return label;
        }

        private static Label BuildSubKeyHeader(string text)
        {
            var label = new Label(text);
            label.AddToClassList("reference-parts-subkey-header");
            return label;
        }

        private static string ExtractModuleKey(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName))
            {
                return string.Empty;
            }
            int dot = fieldName.IndexOf('.');
            return dot <= 0 ? string.Empty : fieldName.Substring(0, dot);
        }

        private static string TitleCase(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return key;
            }
            return char.ToUpperInvariant(key[0]) + key.Substring(1);
        }

        private void MaybeRenderStaleness()
        {
            if (_lookup == null || string.IsNullOrEmpty(_lookup.SourceHash))
            {
                return;
            }
            string sourceDir = EditorPrefs.GetString("Ksp2UnityTools.StockStats.SourceDir", string.Empty);
            if (string.IsNullOrEmpty(sourceDir) || !System.IO.Directory.Exists(sourceDir))
            {
                return;
            }
            string currentHash = StockStatsBaker.ComputeSourceHash(sourceDir);
            if (string.IsNullOrEmpty(currentHash) || currentHash == _lookup.SourceHash)
            {
                return;
            }
            var help = new HelpBox(
                "Source changed since the last bake. Open Stock Stats Bake to refresh the lookup.",
                HelpBoxMessageType.Warning);
            _stalenessSlot.Add(help);
        }

        private void DoCopy(StockFieldEntry entry, float value)
        {
            if (entry?.Copier == null || _activePart == null)
            {
                return;
            }
            bool ok = entry.Copier(_activePart, value, out string error);
            if (!ok)
            {
                EditorUtility.DisplayDialog("Copy failed", error ?? "Unknown error.", "OK");
            }
            else
            {
                Refresh();
            }
        }

        private void ShowEmptyState(string message, HelpBoxMessageType type)
        {
            if (_emptyStateSlot == null)
            {
                return;
            }
            _emptyStateSlot.Add(new HelpBox(message, type));
        }

        private void ClearEmptyState()
        {
            _emptyStateSlot?.Clear();
        }

        private void ClearStalenessWarning()
        {
            _stalenessSlot?.Clear();
        }

        private static string FormatValue(float value, StockFieldEntry entry)
        {
            string number = string.Format(System.Globalization.CultureInfo.InvariantCulture, entry.Format ?? "{0:0.00}", value);
            return number + (entry.UnitsSuffix ?? string.Empty);
        }
    }
}
