using KSP;
using KSP.OAB;
using KSP.Sim;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Tools
{
    /// <summary>
    /// Merges a part's <c>attachNodes</c> list with attach-node markers found in its prefab hierarchy.
    /// </summary>
    /// <remarks>
    /// Explicit <see cref="AttachmentNode" /> components supply transform, sizing, and joint metadata.
    /// Empty marker transforms are inferred when their names partially match known stock attach-node IDs.
    /// </remarks>
    public static class AttachNodeAutoGenerator
    {
        public sealed class Result
        {
            public int Added { get; set; }
            public int Updated { get; set; }
            public int SkippedDuplicates { get; set; }
            public List<Candidate> Candidates { get; } = new();

            public string ToStatusText()
            {
                if (Candidates.Count == 0)
                {
                    return "No AttachmentNode components or empty marker transforms matching known attach-node names were found.";
                }

                var lines = new List<string>
                {
                    $"Auto-detected {Candidates.Count} attach-node marker object(s). Added {Added}, updated {Updated}, skipped {SkippedDuplicates} duplicate name(s)."
                };

                foreach (var candidate in Candidates.Take(24))
                {
                    string action = candidate.WasAdded ? "added" : "updated";
                    string match = string.IsNullOrWhiteSpace(candidate.MatchedName)
                        ? candidate.Source
                        : $"{candidate.Source}, matched '{candidate.MatchedName}'";
                    lines.Add($"- {candidate.NodeId} ({action}; {match})");
                }

                if (Candidates.Count > 24)
                {
                    lines.Add($"- ...and {Candidates.Count - 24} more");
                }

                return string.Join("\n", lines);
            }
        }

        public sealed class Candidate
        {
            public string NodeId { get; set; }
            public string MatchedName { get; set; }
            public string Source { get; set; }
            public bool WasAdded { get; set; }
        }

        /// <summary>
        /// Merges attach-node definitions from every <see cref="AttachmentNode" /> component and
        /// empty marker transform in the part's hierarchy.
        /// </summary>
        /// <remarks>
        /// Existing nodes are preserved. Matching node IDs are updated from the detected marker;
        /// unmatched existing entries stay in the list. Empty marker transforms are detected by
        /// partial name matches against the hardcoded stock attach-node autocomplete catalog.
        /// Marks <paramref name="target" /> dirty so the change is persisted. No-op when
        /// <paramref name="target" /> is null.
        /// </remarks>
        /// <param name="target">The part to regenerate attach nodes for.</param>
        public static Result RegenerateFromHierarchy(CorePartData target)
        {
            var result = new Result();
            if (target == null || target.Core == null) return result;

            target.Core.data.attachNodes ??= new List<AttachNodeDefinition>();
            var indexById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < target.Core.data.attachNodes.Count; i++)
            {
                var existingNode = target.Core.data.attachNodes[i];
                if (!string.IsNullOrWhiteSpace(existingNode.nodeID) && !indexById.ContainsKey(existingNode.nodeID))
                {
                    indexById.Add(existingNode.nodeID, i);
                }
            }

            var seenDetectedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var detected in EnumerateDetectedNodes(target))
            {
                if (!seenDetectedIds.Add(detected.Definition.nodeID))
                {
                    result.SkippedDuplicates++;
                    continue;
                }

                bool exists = indexById.TryGetValue(detected.Definition.nodeID, out int existingIndex);
                if (exists)
                {
                    AttachNodeDefinition existing = target.Core.data.attachNodes[existingIndex];
                    existing = detected.ApplyTo(existing);
                    target.Core.data.attachNodes[existingIndex] = existing;
                    result.Updated++;
                }
                else
                {
                    target.Core.data.attachNodes.Add(detected.Definition);
                    indexById.Add(detected.Definition.nodeID, target.Core.data.attachNodes.Count - 1);
                    result.Added++;
                }

                result.Candidates.Add(new Candidate
                {
                    NodeId = detected.Definition.nodeID,
                    MatchedName = detected.MatchedName,
                    Source = detected.Source,
                    WasAdded = !exists
                });
            }

            if (result.Added > 0 || result.Updated > 0)
            {
                EditorUtility.SetDirty(target);
            }

            return result;
        }

        private static IEnumerable<DetectedNode> EnumerateDetectedNodes(CorePartData target)
        {
            foreach (var attachmentNode in target.gameObject.GetComponentsInChildren<AttachmentNode>(true))
            {
                var obj = attachmentNode.gameObject;
                yield return new DetectedNode
                {
                    Definition = FromAttachmentNode(target, attachmentNode),
                    Source = nameof(AttachmentNode),
                    MatchedName = obj.name
                };
            }

            foreach (var transform in target.gameObject.GetComponentsInChildren<Transform>(true))
            {
                if (!IsEmptyMarkerTransform(transform) || !TryMatchKnownNodeName(transform.name, out string matchedName))
                {
                    continue;
                }

                yield return new DetectedNode
                {
                    Definition = FromMarkerTransform(target, transform, matchedName),
                    Source = "empty transform",
                    MatchedName = matchedName
                };
            }
        }

        private static AttachNodeDefinition FromAttachmentNode(CorePartData target, AttachmentNode attachmentNode)
        {
            var obj = attachmentNode.gameObject;
            var pos = target.transform.InverseTransformPoint(obj.transform.position);
            var dir = target.transform.InverseTransformDirection(obj.transform.forward);
            string sizeKey = string.IsNullOrWhiteSpace(attachmentNode.sizeKey)
                ? PartSizeRegistry.GetAttachNodeSizeKey(null, attachmentNode.size)
                : attachmentNode.sizeKey.Trim();
            return new AttachNodeDefinition
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
                sizeKey = sizeKey,
                visualSize = attachmentNode.visualSize,
                isResourceCrossfeed = attachmentNode.isResourceCrossFeed,
                isRigid = attachmentNode.isRigid,
                angularStrengthMultiplier = attachmentNode.angularStrengthMultiplier,
                contactArea = attachmentNode.contactArea,
                overrideDragArea = attachmentNode.overrideDragArea,
                isCompoundJoint = attachmentNode.isCompoundJoint
            };
        }

        private static AttachNodeDefinition FromMarkerTransform(CorePartData target, Transform marker, string matchedName)
        {
            var pos = target.transform.InverseTransformPoint(marker.position);
            var dir = target.transform.InverseTransformDirection(marker.forward);
            string sizeKey = string.IsNullOrWhiteSpace(target.Core.data.sizeKey)
                ? PartSizeRegistry.DefaultSizeKey
                : target.Core.data.sizeKey.Trim();
            PartSizeDefinition knownSize = PartSizeRegistry.TryGet(sizeKey, out PartSizeDefinition definition)
                ? definition
                : PartSizeRegistry.DefaultDefinition;
            return new AttachNodeDefinition
            {
                nodeID = marker.name,
                nodeType = GetNodeType(matchedName),
                attachMethod = IsSurfaceNode(matchedName) ? AttachNodeMethod.HINGE_JOINT : AttachNodeMethod.FIXED_JOINT,
                position = pos,
                orientation = dir,
                size = knownSize.LegacyAttachNodeSizeAliases.Count > 0 ? knownSize.LegacyAttachNodeSizeAliases[0] : 0,
                sizeKey = sizeKey,
                visualSize = knownSize.Diameter,
                isResourceCrossfeed = !IsSurfaceNode(matchedName),
                angularStrengthMultiplier = 1f
            };
        }

        private static bool IsEmptyMarkerTransform(Transform transform)
        {
            if (transform == null)
            {
                return false;
            }

            var components = transform.GetComponents<Component>();
            return components.Length == 1 && components[0] is Transform;
        }

        private static bool TryMatchKnownNodeName(string objectName, out string matchedName)
        {
            foreach (string nodeName in PartAuthoringChoiceCatalog.GetStockAttachNodeIds()
                         .OrderByDescending(name => name.Length))
            {
                if (objectName.IndexOf(nodeName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matchedName = nodeName;
                    return true;
                }
            }

            matchedName = null;
            return false;
        }

        private static AttachNodeType GetNodeType(string matchedName)
        {
            if (IsSurfaceNode(matchedName))
            {
                return AttachNodeType.Surface;
            }

            return string.Equals(matchedName, "docking", StringComparison.OrdinalIgnoreCase)
                ? AttachNodeType.Dock
                : AttachNodeType.Stack;
        }

        private static bool IsSurfaceNode(string matchedName)
        {
            return string.Equals(matchedName, "surface", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(matchedName, "srf", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(matchedName, "srfAttach", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class DetectedNode
        {
            public AttachNodeDefinition Definition { get; set; }
            public string Source { get; set; }
            public string MatchedName { get; set; }

            public AttachNodeDefinition ApplyTo(AttachNodeDefinition existing)
            {
                if (Source == nameof(AttachmentNode))
                {
                    existing.NodeSymmetryGroupID = Definition.NodeSymmetryGroupID;
                    existing.nodeType = Definition.nodeType;
                    existing.attachMethod = Definition.attachMethod;
                    existing.IsMultiJoint = Definition.IsMultiJoint;
                    existing.MultiJointMaxJoint = Definition.MultiJointMaxJoint;
                    existing.MultiJointRadiusOffset = Definition.MultiJointRadiusOffset;
                    existing.MultiJointOnSingleAxis = Definition.MultiJointOnSingleAxis;
                    existing.SingleJointAxis = Definition.SingleJointAxis;
                    existing.MultiJointFullBreakStrength = Definition.MultiJointFullBreakStrength;
                    existing.size = Definition.size;
                    existing.sizeKey = Definition.sizeKey;
                    existing.visualSize = Definition.visualSize;
                    existing.isResourceCrossfeed = Definition.isResourceCrossfeed;
                    existing.isRigid = Definition.isRigid;
                    existing.angularStrengthMultiplier = Definition.angularStrengthMultiplier;
                    existing.contactArea = Definition.contactArea;
                    existing.overrideDragArea = Definition.overrideDragArea;
                    existing.isCompoundJoint = Definition.isCompoundJoint;
                }

                existing.position = Definition.position;
                existing.orientation = Definition.orientation;
                return existing;
            }
        }
    }
}
