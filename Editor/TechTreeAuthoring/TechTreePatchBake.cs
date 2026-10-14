using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.Modding;
using Newtonsoft.Json.Linq;

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
            nameof(TechNodeData.RequiredMissionIDs),
            nameof(TechNodeData.RequiredResearchData),
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

            baseline.Data = Copy(reference.Data);
            baseline.EnglishName = reference.EnglishName;
            baseline.EnglishDescription = reference.EnglishDescription;
        }

        /// <summary>
        /// Builds the patch's Lua: one Patch Manager patch per changed override, and one per removed node.
        /// </summary>
        /// <param name="patch">The patch.</param>
        /// <param name="ownerId">The id of the mod the patch belongs to, which scopes changed text's keys.</param>
        /// <returns>The Lua, which patches nothing when the patch changes nothing.</returns>
        public static string BuildLua(TechTreePatchAsset patch, string ownerId)
        {
            var lua = new StringBuilder();
            lua.AppendLine($"-- Generated from the tech tree patch {patch.name}. Edit the patch asset and bake again.");
            string gates = BuildGates(patch, ownerId);
            foreach (TechTreeNode node in patch.Nodes.Where(node => patch.IsOverride(node.Data?.ID)))
            {
                List<string> body = BuildChanges(patch.FindBaseline(node.Data.ID), node, ownerId);
                if (body.Count == 0)
                    continue;

                AppendPatch(lua, $"{patch.name}_{node.Data.ID}", node.Data.ID, gates, body);
            }

            foreach (string removed in patch.RemovedNodeIds.Where(id => !string.IsNullOrEmpty(id)).Distinct())
            {
                AppendPatch(lua, $"{patch.name}_remove_{removed}", removed, gates, new List<string> { "return \"remove\"" });
            }

            return lua.ToString();
        }

        /// <summary>
        /// Copies a node's data, so the copy shares nothing with the original.
        /// </summary>
        /// <param name="data">The data to copy.</param>
        /// <returns>The copy.</returns>
        public static TechNodeData Copy(TechNodeData data) =>
            TechTreeJson.TryFromJson(TechTreeJson.ToJson(data), out TechNodeData copy) ? copy : new TechNodeData();

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

        private static void AppendPatch(StringBuilder lua, string patchName, string nodeId, string gates, List<string> body)
        {
            lua.AppendLine();
            lua.AppendLine($"PM.Science:PatchTechNodes({Quote(patchName)})");
            lua.AppendLine($"    :Named({Quote(nodeId)})");
            lua.Append(gates);
            lua.AppendLine("    :Do(function(node)");
            foreach (string line in body)
            {
                lua.AppendLine($"        {line}");
            }

            lua.AppendLine("    end)");
        }

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
                foreach (JToken added in Without(now, was))
                {
                    lines.Add($"node.{field}:Append({ToLua(added)})");
                }

                foreach (JToken removed in Without(was, now))
                {
                    lines.Add($"node.{field}:RemoveWhere(function(value) return {MatchOf(removed)} end)");
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
        private static string MatchOf(JToken item)
        {
            if (item is not JObject gate)
                return $"value == {ToLua(item)}";

            string experiment = gate[nameof(TechRequiredResearchData.ExperimentID)]?.ToString();
            string location = gate[nameof(TechRequiredResearchData.ResearchLocationID)]?.ToString();
            return $"value.{nameof(TechRequiredResearchData.ExperimentID)} == {QuoteOrNil(experiment)}"
                + $" and value.{nameof(TechRequiredResearchData.ResearchLocationID)} == {QuoteOrNil(location)}";
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
