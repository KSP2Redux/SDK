using System.Collections.Generic;
using KSP;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets
{
    /// <summary>
    /// <see cref="AutocompleteField" /> specialised for attach-node-ID strings, with suggestions sourced from the part's <see cref="CorePartData" />.
    /// </summary>
    /// <remarks>
    /// The suggestion list is rebuilt each time the popup opens so renames or additions to the part's attach node list are picked up without an editor reload. Falls back to a plain TextField when no <see cref="CorePartData" /> can be resolved.
    /// </remarks>
    public sealed class AttachNodeIdField : AutocompleteField
    {
        /// <summary>
        /// Creates a new <see cref="AttachNodeIdField" /> bound to the given string property.
        /// </summary>
        /// <param name="prop">The string SerializedProperty to read/write.</param>
        /// <param name="label">The author-facing label shown to the left of the field.</param>
        /// <param name="target">The owning part's <see cref="CorePartData" />, used to enumerate
        /// the live attach node ID list.</param>
        public AttachNodeIdField(SerializedProperty prop, string label, CorePartData target)
            : base(prop, label, () => EnumerateNodeIds(target))
        {
        }

        private static IEnumerable<string> EnumerateNodeIds(CorePartData target)
        {
            if (target == null || target.Data?.attachNodes == null)
            {
                yield break;
            }
            foreach (var node in target.Data.attachNodes)
            {
                if (!string.IsNullOrEmpty(node.nodeID))
                {
                    yield return node.nodeID;
                }
            }
        }
    }
}
