using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets
{
    /// <summary>
    /// Editor field for a string SerializedProperty that holds a slash-separated path to a Transform within the part hierarchy.
    /// </summary>
    /// <remarks>
    /// Renders as a <c>Transform</c>-typed ObjectField. The author drags a Transform from the prefab and the relative path string is written automatically. Resolution uses <see cref="Transform.Find" /> which matches the runtime path-aware lookup.
    /// </remarks>
    public sealed class TransformPathField : TransformReferenceFieldBase
    {
        /// <summary>
        /// Creates a new <see cref="TransformPathField" /> bound to the given string property.
        /// </summary>
        /// <param name="prop">The string SerializedProperty holding the slash-separated path.</param>
        /// <param name="label">The author-facing label. A trailing " Path" suffix is stripped for display.</param>
        /// <param name="partRoot">The part root used to resolve the stored path to a live Transform.</param>
        public TransformPathField(SerializedProperty prop, string label, Transform partRoot)
            : base(prop, label, partRoot, "transform-path-field", " Path")
        {
        }

        /// <inheritdoc />
        protected override Transform Resolve(string stored)
        {
            if (string.IsNullOrEmpty(stored) || PartRoot == null) return null;
            return PartRoot.Find(stored);
        }

        /// <inheritdoc />
        protected override string Compute(Transform target)
        {
            if (target == PartRoot) return string.Empty;
            var segments = new List<string>();
            var current = target;
            while (current != null && current != PartRoot)
            {
                segments.Add(current.name);
                current = current.parent;
            }
            segments.Reverse();
            return string.Join("/", segments);
        }
    }
}
