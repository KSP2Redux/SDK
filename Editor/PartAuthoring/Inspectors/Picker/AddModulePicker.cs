using System;
using System.Collections.Generic;
using System.Linq;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Picker
{
    /// <summary>
    /// Floating utility window with a search field, scrollable category list, and Cancel / Add Selected action buttons for picking a <c>Module_*</c> type to add.
    /// </summary>
    /// <remarks>
    /// Opened via <see cref="Open" /> from the Modules tab. On confirm, the caller's callback fires
    /// with the selected <see cref="Type" /> and the window closes. Cancel closes without firing.
    /// </remarks>
    public sealed class AddModulePicker : PickerWindowBase<ModuleCatalogEntry>
    {
        /// <summary>
        /// Opens the picker as a modal window and invokes <paramref name="onConfirm" /> when the user selects a module and presses Add.
        /// </summary>
        /// <remarks>
        /// Does nothing if the user cancels. <c>ShowModal</c> blocks the editor until the window
        /// closes, matching the design intent of a focused single-decision surface rather than a
        /// reference panel the user keeps open alongside other work.
        /// </remarks>
        /// <param name="onConfirm">Callback invoked with the selected module type when the user confirms.</param>
        public static void Open(Action<Type> onConfirm)
        {
            OpenWindow<AddModulePicker>("Add Module", onConfirm);
        }

        protected override string SearchHintText => "Search modules...";

        protected override IEnumerable<IGrouping<string, ModuleCatalogEntry>> GetEntriesByCategory()
            => PartModuleCatalog.GetEntriesByCategory();

        protected override string GetDisplayName(ModuleCatalogEntry entry) => entry.DisplayName;
        protected override string GetCategory(ModuleCatalogEntry entry) => entry.Category;
        protected override string GetDescription(ModuleCatalogEntry entry) => entry.Description;
        protected override Type GetTypeForEntry(ModuleCatalogEntry entry) => entry.ModuleType;
    }
}
