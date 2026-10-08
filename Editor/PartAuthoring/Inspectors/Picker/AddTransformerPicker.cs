using System;
using System.Collections.Generic;
using System.Linq;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Picker
{
    /// <summary>
    /// Floating utility window with a search field, scrollable category list, and Cancel / Add Selected action buttons for picking an <see cref="VSwift.Modules.Transformers.ITransformer" /> concrete type to add.
    /// </summary>
    /// <remarks>
    /// Opened via <see cref="Open" /> from the Variants tab's transformer list. On confirm, the
    /// caller's callback fires with the selected <see cref="Type" /> and the window closes. Cancel
    /// closes without firing. Mirrors <see cref="AddModulePicker" /> structurally for visual and
    /// behavioural parity with the Modules-tab picker.
    /// </remarks>
    public sealed class AddTransformerPicker : PickerWindowBase<TransformerCatalogEntry>
    {
        /// <summary>
        /// Opens the picker as a modal window and invokes <paramref name="onConfirm" /> when the user selects a transformer and presses Add.
        /// </summary>
        /// <remarks>
        /// Does nothing if the user cancels.
        /// </remarks>
        /// <param name="onConfirm">Callback invoked with the selected transformer type when the user confirms.</param>
        public static void Open(Action<Type> onConfirm)
        {
            OpenWindow<AddTransformerPicker>("Add Transformer", onConfirm);
        }

        protected override string SearchHintText => "Search transformers...";

        protected override IEnumerable<IGrouping<string, TransformerCatalogEntry>> GetEntriesByCategory()
            => TransformerCatalog.GetEntriesByCategory();

        protected override string GetDisplayName(TransformerCatalogEntry entry) => entry.DisplayName;
        protected override string GetCategory(TransformerCatalogEntry entry) => entry.Category;
        protected override string GetDescription(TransformerCatalogEntry entry) => entry.Description;
        protected override Type GetTypeForEntry(TransformerCatalogEntry entry) => entry.TransformerType;
    }
}
