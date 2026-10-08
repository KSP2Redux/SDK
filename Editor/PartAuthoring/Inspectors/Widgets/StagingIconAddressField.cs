using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets
{
    /// <summary>
    /// Autocomplete field for staging icon asset addresses found in the stock export.
    /// </summary>
    public sealed class StagingIconAddressField : AutocompleteField
    {
        public StagingIconAddressField(SerializedProperty prop, string label)
            : base(prop, label, PartAuthoringChoiceCatalog.GetStagingIconAddresses)
        {
        }
    }
}
