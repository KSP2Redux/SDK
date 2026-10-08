using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets
{
    /// <summary>
    /// Editor field for a string SerializedProperty that holds the bare name of a single Transform somewhere inside the part hierarchy.
    /// </summary>
    /// <remarks>
    /// Renders as a <c>Transform</c>-typed ObjectField. The author drags a Transform from the prefab and the leaf <c>gameObject.name</c> is stored. Resolution mirrors the runtime, a recursive name walk via <see cref="TransformExtension.FindChildren" /> with the first match selected.
    /// </remarks>
    public sealed class TransformNameField : TransformReferenceFieldBase
    {
        /// <summary>
        /// Creates a new <see cref="TransformNameField" /> bound to the given string property.
        /// </summary>
        /// <param name="prop">The string SerializedProperty holding the transform's bare name.</param>
        /// <param name="label">The author-facing label. A trailing " Name" suffix is stripped for display.</param>
        /// <param name="partRoot">The part root used to resolve the stored name to a live Transform.</param>
        public TransformNameField(SerializedProperty prop, string label, Transform partRoot)
            : base(prop, label, partRoot, "transform-name-field", " Name")
        {
        }

        /// <inheritdoc />
        protected override Transform Resolve(string stored)
        {
            if (string.IsNullOrEmpty(stored) || PartRoot == null) return null;
            var matches = PartRoot.FindChildren(stored);
            return matches.Count > 0 ? matches[0] : null;
        }

        /// <inheritdoc />
        protected override string Compute(Transform target) => target.gameObject.name;
    }
}
