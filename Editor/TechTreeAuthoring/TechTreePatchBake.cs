using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.Modding;
using Newtonsoft.Json.Linq;
using Redux.TechTree;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// Works out what a tech tree patch changes and the Patch Manager Lua that makes those changes, without touching
    /// the project.
    /// </summary>
    /// <remarks>
    /// An override is compared with its baseline field by field, so only fields the author changed are written. A
    /// list is written as what was added and what was removed rather than as a whole, so another patch's additions to
    /// the same list survive. Changed text points the node at the mod's own keys.
    /// </remarks>
    public static class TechTreePatchBake
    {
        /// <summary>
        /// The name of the English name, compared as if it were a field.
        /// </summary>
        public const string NAME_FIELD = "EnglishName";

        /// <summary>
        /// The name of the English description, compared as if it were a field.
        /// </summary>
        public const string DESCRIPTION_FIELD = "EnglishDescription";

        // The node fields a patch may change and how each is written, in the order the patch writes them
        private static readonly string[] AssignedFields =
        {
            nameof(TechNodeData.TechTreePosition),
            nameof(TechNodeData.RequiredSciencePoints),
            nameof(TechNodeData.TierToUnlock),
            nameof(TechNodeData.MysteryTech),
            nameof(TechNodeData.IconID),
        };

        private static readonly string[] ListFields =
        {
            nameof(TechNodeData.RequiredTechNodeIDs),
            nameof(TechNodeData.UnlockedPartsIDs),
            nameof(TechNodeData.UnlockedVariants),
            nameof(TechNodeData.RequiredMissionIDs),
            nameof(TechNodeData.RequiredResearchData),
        };

        // What a removed list entry is matched on in the patched list, for the lists of entries rather than IDs
        private static readonly Dictionary<string, string[]> MatchedProperties = new()
        {
            [nameof(TechNodeData.UnlockedVariants)] = new[]
            {
                nameof(TechUnlockedVariant.PartName),
                nameof(TechUnlockedVariant.VariantSetId),
                nameof(TechUnlockedVariant.VariantId),
            },
            [nameof(TechNodeData.RequiredResearchData)] = new[]
            {
                nameof(TechRequiredResearchData.ExperimentID),
                nameof(TechRequiredResearchData.ResearchLocationID),
            },
        };

        /// <summary>
        /// Gets the name of the patch's Lua file.
        /// </summary>
        /// <param name="patch">The patch.</param>
        /// <returns>The file name, such as MyPatch_Patch.lua.</returns>
        public static string GetPatchFileName(TechTreePatchAsset patch) => $"{patch.name}_Patch.lua";

        /// <summary>
        /// Gets the fields one node changes from another.
        /// </summary>
        /// <param name="baseline">The node as it was.</param>
        /// <param name="current">The node as it is.</param>
        /// <returns>The names of the changed fields, the English text as <see cref="NAME_FIELD" /> and
        /// <see cref="DESCRIPTION_FIELD" />.</returns>
        public static List<string> GetChangedFields(TechTreeNode baseline, TechTreeNode current)
        {
            JObject before = ToJObject(baseline.Data);
            JObject after = ToJObject(current.Data);
            List<string> changed = AssignedFields.Concat(ListFields)
                .Where(field => !JToken.DeepEquals(Normalise(before[field]), Normalise(after[field])))
                .ToList();
            if ((baseline.EnglishName ?? string.Empty) != (current.EnglishName ?? string.Empty))
            {
                changed.Add(NAME_FIELD);
            }

            if ((baseline.EnglishDescription ?? string.Empty) != (current.EnglishDescription ?? string.Empty))
            {
                changed.Add(DESCRIPTION_FIELD);
            }

            return changed;
        }

        /// <summary>
        /// Gets the fields an override changed that the reference has also changed since the override was taken.
        /// </summary>
        /// <param name="baseline">The node as it was taken.</param>
        /// <param name="current">The override.</param>
        /// <param name="reference">The node as the reference has it now.</param>
        /// <returns>The names of the fields both changed.</returns>
        public static List<string> GetConflictingFields(TechTreeNode baseline, TechTreeNode current, TechTreeNode reference)
        {
            HashSet<string> moved = new(GetChangedFields(baseline, reference));
            return GetChangedFields(baseline, current).Where(moved.Contains).ToList();
        }

        /// <summary>
        /// Brings an override up to date with the reference: fields the author left alone take the reference's
        /// value, fields the author changed keep it, and the baseline becomes the reference's node.
        /// </summary>
        /// <param name="baseline">The node as it was taken, replaced in place by the reference's.</param>
        /// <param name="current">The override, updated in place.</param>
        /// <param name="reference">The node as the reference has it now.</param>
        public static void Rebase(TechTreeNode baseline, TechTreeNode current, TechTreeNode reference)
        {
            HashSet<string> changed = new(GetChangedFields(baseline, current));
            JObject currentData = ToJObject(current.Data);
            JObject referenceData = ToJObject(reference.Data);
            foreach (string field in AssignedFields.Concat(ListFields).Where(field => !changed.Contains(field)))
            {
                currentData[field] = referenceData[field]?.DeepClone();
            }

            if (TechTreeJson.TryFromJson(currentData.ToString(), out TechNodeData data))
            {
                current.Data = data;
            }

            if (!changed.Contains(NAME_FIELD))
            {
                current.EnglishName = reference.EnglishName;
            }

            if (!changed.Contains(DESCRIPTION_FIELD))
            {
                current.EnglishDescription = reference.EnglishDescription;
            }

            baseline.Data = TechTreeJson.Copy(reference.Data);
            baseline.EnglishName = reference.EnglishName;
            baseline.EnglishDescription = reference.EnglishDescription;
        }

        /// <summary>
        /// Gets the tiers one list of tiers names differently from another.
        /// </summary>
        /// <param name="baseline">The tiers as they were.</param>
        /// <param name="current">The tiers as they are.</param>
        /// <returns>The numbers of the changed tiers, counted from 1. A tier one list lacks counts as unnamed.</returns>
        public static List<int> GetChangedTiers(IReadOnlyList<TechTreeTier> baseline, IReadOnlyList<TechTreeTier> current)
        {
            var changed = new List<int>();
            int count = Math.Max(baseline.Count, current.Count);
            for (int i = 0; i < count; i++)
            {
                if (TierName(baseline, i) != TierName(current, i))
                {
                    changed.Add(i + 1);
                }
            }

            return changed;
        }

        /// <summary>
        /// Gets the tiers a patch names differently from the reference as the patch took it.
        /// </summary>
        /// <param name="patch">The patch.</param>
        /// <returns>The numbers of the changed tiers, counted from 1.</returns>
        public static List<int> GetChangedTiers(TechTreePatchAsset patch) =>
            GetChangedTiers(patch.TreeDataBaseline?.Tiers ?? new List<TechTreeTier>(), patch.Tiers);

        /// <summary>
        /// Checks whether two start nodes are the same, an empty one and a missing one alike.
        /// </summary>
        /// <param name="first">One start node ID.</param>
        /// <param name="second">The other start node ID.</param>
        /// <returns>True if they are the same, false otherwise.</returns>
        public static bool IsSameStartNode(string first, string second) =>
            string.Equals(first ?? string.Empty, second ?? string.Empty, StringComparison.Ordinal);

        /// <summary>
        /// Checks whether a patch changes the start node from the reference's as the patch took it.
        /// </summary>
        /// <param name="patch">The patch.</param>
        /// <returns>True if the patch changes the start node, false otherwise.</returns>
        public static bool ChangesStartNode(TechTreePatchAsset patch) =>
            !IsSameStartNode(patch.TreeDataBaseline?.StartNodeId, patch.StartNodeId);

        /// <summary>
        /// Brings a patch's tiers and start node up to date with the reference: what the author left alone takes the
        /// reference's value, what the author changed keeps it, and the baseline becomes the reference's tree data.
        /// </summary>
        /// <param name="patch">The patch, updated in place.</param>
        /// <param name="reference">The reference's tree data as it is now.</param>
        public static void RebaseTreeData(TechTreePatchAsset patch, ReferenceTreeData reference)
        {
            HashSet<int> changed = new(GetChangedTiers(patch));
            int count = Math.Max(reference.Tiers.Count, changed.Count > 0 ? changed.Max() : 0);
            var tiers = new List<TechTreeTier>();
            for (int i = 0; i < count; i++)
            {
                IReadOnlyList<TechTreeTier> source = changed.Contains(i + 1) ? patch.Tiers : reference.Tiers;
                tiers.Add(new TechTreeTier { EnglishName = TierName(source, i) });
            }

            patch.Tiers = tiers;
            if (!ChangesStartNode(patch))
            {
                patch.StartNodeId = reference.StartNodeId;
            }

            patch.TreeDataBaseline = reference.Copy();
        }

        /// <summary>
        /// Builds the patch's Lua: one Patch Manager patch for the changed tiers and start node, one per changed
        /// override, and one per removed node.
        /// </summary>
        /// <remarks>
        /// A patch whose reference has no tree data creates its tiers and start node as new tree data instead.
        /// </remarks>
        /// <param name="patch">The patch.</param>
        /// <param name="ownerId">The id of the mod the patch belongs to, which scopes changed text's keys.</param>
        /// <returns>The Lua, which patches nothing when the patch changes nothing.</returns>
        public static string BuildLua(TechTreePatchAsset patch, string ownerId)
        {
            var lua = new StringBuilder();
            lua.AppendLine($"-- Generated from the tech tree patch {patch.name}. Edit the patch asset and bake again.");
            string gates = BuildGates(patch, ownerId);
            if (patch.TreeDataBaseline == null)
            {
                // A reference without tree data has nothing to patch, so the patch's own is created whole
                AppendTreeDataCreation(lua, patch, ownerId, gates);
            }
            else
            {
                List<string> treeData = BuildTreeDataChanges(patch, ownerId);
                if (treeData.Count > 0)
                {
                    // A feature set loads one tree data, so every name matches the one that loads
                    AppendPatch(
                        lua,
                        $"PM.JSON:Patch({Quote(TechTreeData.LABEL)}, {Quote($"{patch.name}_TreeData")})",
                        "*",
                        gates,
                        "json",
                        treeData
                    );
                }
            }

            foreach (TechTreeNode node in patch.Nodes.Where(node => patch.IsOverride(node.Data?.ID)))
            {
                List<string> body = BuildChanges(patch.FindBaseline(node.Data.ID), node, ownerId);
                if (body.Count == 0)
                    continue;

                AppendNodePatch(lua, $"{patch.name}_{node.Data.ID}", node.Data.ID, gates, body);
            }

            foreach (string removed in patch.RemovedNodeIds.Where(id => !string.IsNullOrEmpty(id)).Distinct())
            {
                AppendNodePatch(lua, $"{patch.name}_remove_{removed}", removed, gates, new List<string> { "return \"remove\"" });
            }

            return lua.ToString();
        }

        // Ksp2Redux's own patches belong to its tech tree category, the gate every Redux patch file states
        private static string BuildGates(TechTreePatchAsset patch, string ownerId)
        {
            List<string> features = patch.Features.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            if (features.Count == 0 && ownerId == FeatureCatalog.REDUX_MOD_ID)
            {
                features.Add("Nodes");
            }

            List<string> noFeatures = patch.ExcludedFeatures.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            var gates = new StringBuilder();
            if (features.Count > 0)
            {
                gates.Append($"    :Feature({string.Join(", ", features.Select(Quote))})\n");
            }

            if (noFeatures.Count > 0)
            {
                gates.Append($"    :NoFeature({string.Join(", ", noFeatures.Select(Quote))})\n");
            }

            return gates.ToString();
        }

        private static void AppendNodePatch(StringBuilder lua, string patchName, string nodeId, string gates, List<string> body) =>
            AppendPatch(lua, $"PM.Science:PatchTechNodes({Quote(patchName)})", nodeId, gates, "node", body);

        private static void AppendPatch(
            StringBuilder lua,
            string registration,
            string assetName,
            string gates,
            string parameter,
            List<string> body
        )
        {
            lua.AppendLine();
            lua.AppendLine(registration);
            lua.AppendLine($"    :Named({Quote(assetName)})");
            lua.Append(gates);
            lua.AppendLine($"    :Do(function({parameter})");
            foreach (string line in body)
            {
                lua.AppendLine($"        {line}");
            }

            lua.AppendLine("    end)");
        }

        // Each tier is set by its place rather than the list replaced, so tiers another patch names stay named. A
        // tier past the end of the list grows it, the slots between left unnamed.
        private static List<string> BuildTreeDataChanges(TechTreePatchAsset patch, string ownerId)
        {
            var lines = new List<string>();
            if (ChangesStartNode(patch))
            {
                lines.Add($"json.{nameof(TechTreeData.StartNodeId)} = {Quote(patch.StartNodeId)}");
            }

            List<int> tiers = GetChangedTiers(patch);
            if (tiers.Count == 0)
                return lines;

            string keys = $"json.{nameof(TechTreeData.TierNameKeys)}";
            lines.Add($"if {keys} == nil then {keys} = J.Empty() end");
            lines.Add($"while {keys}.Count < {tiers.Max()} do {keys}:Append(\"\") end");
            foreach (int tier in tiers)
            {
                string key = string.IsNullOrEmpty(TierName(patch.Tiers, tier - 1))
                    ? string.Empty
                    : TechTreeKeys.GetTierNameKey(ownerId, tier);
                lines.Add($"{keys}[{tier}] = {Quote(key)}");
            }

            return lines;
        }

        private static void AppendTreeDataCreation(StringBuilder lua, TechTreePatchAsset patch, string ownerId, string gates)
        {
            if (patch.Tiers.Count == 0 && string.IsNullOrEmpty(patch.StartNodeId))
                return;

            // An unnamed tier keeps an empty key, which the game shows by the tier's number
            IEnumerable<string> tierKeys = patch.Tiers.Select((tier, index) => Quote(
                string.IsNullOrEmpty(tier?.EnglishName) ? string.Empty : TechTreeKeys.GetTierNameKey(ownerId, index + 1)
            ));
            string keys = patch.Tiers.Count == 0 ? "J.Empty()" : $"{{ {string.Join(", ", tierKeys)} }}";
            lua.AppendLine();
            lua.AppendLine($"PM.JSON:New({Quote(TechTreeData.LABEL)}, {Quote($"{patch.name}_TreeData")}, J({{");
            if (!string.IsNullOrEmpty(patch.StartNodeId))
            {
                lua.AppendLine($"    {nameof(TechTreeData.StartNodeId)} = {Quote(patch.StartNodeId)},");
            }

            lua.AppendLine($"    {nameof(TechTreeData.TierNameKeys)} = {keys},");
            lua.AppendLine("}))");
            lua.Append(gates);
        }

        private static string TierName(IReadOnlyList<TechTreeTier> tiers, int index) =>
            index < tiers.Count ? tiers[index]?.EnglishName ?? string.Empty : string.Empty;

        private static List<string> BuildChanges(TechTreeNode baseline, TechTreeNode current, string ownerId)
        {
            var lines = new List<string>();
            HashSet<string> changed = new(GetChangedFields(baseline, current));
            JObject before = ToJObject(baseline.Data);
            JObject after = ToJObject(current.Data);
            foreach (string field in AssignedFields.Where(changed.Contains))
            {
                lines.Add($"node.{field} = {ToLua(after[field])}");
            }

            foreach (string field in ListFields.Where(changed.Contains))
            {
                List<JToken> was = Items(before[field]);
                List<JToken> now = Items(after[field]);
                List<JToken> additions = Without(now, was).ToList();

                // Node JSON baked before a list existed has no slot for it, and Append needs one
                if (additions.Count > 0)
                {
                    lines.Add($"if node.{field} == nil then node.{field} = J.Empty() end");
                }

                foreach (JToken added in additions)
                {
                    lines.Add($"node.{field}:Append({ToLua(added)})");
                }

                foreach (JToken removed in Without(was, now))
                {
                    lines.Add($"node.{field}:RemoveWhere(function(value) return {MatchOf(field, removed)} end)");
                }
            }

            string nodeId = current.Data.ID;
            if (changed.Contains(NAME_FIELD))
            {
                lines.Add($"node.{nameof(TechNodeData.NameLocKey)} = {Quote(TechTreeKeys.GetNameKey(ownerId, nodeId))}");
            }

            if (changed.Contains(DESCRIPTION_FIELD))
            {
                lines.Add($"node.{nameof(TechNodeData.DescriptionLocKey)} = {Quote(TechTreeKeys.GetDescriptionKey(ownerId, nodeId))}");
            }

            return lines;
        }

        // A research gate matches by what it gates on. Its coordinates only change how it reads.
        private static string MatchOf(string field, JToken item)
        {
            if (item is not JObject entry || !MatchedProperties.TryGetValue(field, out string[] properties))
                return $"value == {ToLua(item)}";

            return string.Join(
                " and ",
                properties.Select(property => $"value.{property} == {QuoteOrNil(entry[property]?.ToString())}")
            );
        }

        // Each item of a list that the other lacks, a repeated item counted once per repeat
        private static IEnumerable<JToken> Without(List<JToken> items, List<JToken> others)
        {
            var remaining = new List<JToken>(others);
            foreach (JToken item in items)
            {
                int match = remaining.FindIndex(other => JToken.DeepEquals(other, item));
                if (match >= 0)
                {
                    remaining.RemoveAt(match);
                    continue;
                }

                yield return item;
            }
        }

        private static List<JToken> Items(JToken list) =>
            list is JArray array ? array.Select(Normalise).ToList() : new List<JToken>();

        // An empty location reads as no location, as the game treats it
        private static JToken Normalise(JToken token)
        {
            if (token is not JObject gate)
                return token ?? JValue.CreateNull();

            var copy = (JObject)gate.DeepClone();
            foreach (JProperty property in copy.Properties().ToList())
            {
                bool isEmpty = property.Value.Type == JTokenType.Null
                    || (property.Value.Type == JTokenType.String && string.IsNullOrEmpty((string)property.Value))
                    || (property.Value is JArray array && array.Count == 0);
                if (isEmpty)
                {
                    property.Remove();
                }
            }

            return copy;
        }

        private static string ToLua(JToken token)
        {
            switch (token?.Type)
            {
                case JTokenType.String:
                    return Quote((string)token);
                case JTokenType.Integer:
                case JTokenType.Float:
                    return Convert.ToDouble(((JValue)token).Value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture);
                case JTokenType.Boolean:
                    return (bool)token ? "true" : "false";
                case JTokenType.Array:
                    return $"{{ {string.Join(", ", token.Select(ToLua))} }}";
                case JTokenType.Object:
                    IEnumerable<string> fields = Normalise(token).Children<JProperty>()
                        .Select(property => $"{property.Name} = {ToLua(property.Value)}");
                    return $"{{ {string.Join(", ", fields)} }}";
                default:
                    return "nil";
            }
        }

        private static string QuoteOrNil(string text) => string.IsNullOrEmpty(text) ? "nil" : Quote(text);

        private static string Quote(string text)
        {
            var quoted = new StringBuilder("\"");
            foreach (char character in text ?? string.Empty)
            {
                switch (character)
                {
                    case '\\':
                        quoted.Append("\\\\");
                        break;
                    case '"':
                        quoted.Append("\\\"");
                        break;
                    case '\n':
                        quoted.Append("\\n");
                        break;
                    case '\r':
                        quoted.Append("\\r");
                        break;
                    default:
                        quoted.Append(character);
                        break;
                }
            }

            return quoted.Append('"').ToString();
        }

        private static JObject ToJObject(TechNodeData data) => JObject.Parse(TechTreeJson.ToJson(data));
    }
}
