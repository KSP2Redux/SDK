using System;
using Ksp2UnityTools.Editor.Widgets;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Widgets
{
    /// <summary>
    /// <see cref="AutocompleteField" /> specialised for
    /// <c>MissionContentBranch.ID</c>. Suggests the 3 runtime-recognised IDs and accepts
    /// arbitrary author-defined IDs.
    /// </summary>
    public sealed class ContentBranchIdField : AutocompleteField
    {
        /// <summary>
        /// Creates a new <see cref="ContentBranchIdField" /> backed by a plain in-memory string.
        /// </summary>
        /// <param name="label">The author-facing label shown to the left of the field.</param>
        /// <param name="initialValue">Starting value of the field.</param>
        /// <param name="onValueChanged">Raised whenever the field's value changes via typing or picking.</param>
        public ContentBranchIdField(string label, string initialValue, Action<string> onValueChanged)
            : base(initialValue, label, ContentBranchIdCatalog.GetKnownContentBranchIds, onValueChanged)
        {
        }
    }
}
