using System;
using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Picker;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Actions
{
    /// <summary>
    /// Modal picker for choosing an <see cref="KSP.Game.Missions.IMissionAction" /> implementation.
    /// </summary>
    /// <remarks>
    /// Mirrors the MessageCenterMessagePicker chrome via the shared <see cref="PickerWindowBase{TEntry}" />.
    /// </remarks>
    public sealed class ActionTypePicker : PickerWindowBase<ActionTypeCatalogEntry>
    {
        /// <summary>
        /// Opens the picker and invokes <paramref name="onConfirm" /> with the chosen catalog entry.
        /// </summary>
        /// <param name="onConfirm">Callback fired with the selected entry, or skipped when the user cancels.</param>
        public static void Open(Action<ActionTypeCatalogEntry> onConfirm)
        {
            OpenWindow<ActionTypePicker>("Pick Action Type", t =>
            {
                if (t == null) return;
                var entry = ActionTypeCatalog.GetEntries().FirstOrDefault(e => e.ActionType == t);
                onConfirm?.Invoke(entry);
            });
        }

        /// <inheritdoc />
        protected override string SearchHintText => "Search actions...";

        /// <inheritdoc />
        protected override IEnumerable<IGrouping<string, ActionTypeCatalogEntry>> GetEntriesByCategory()
            => ActionTypeCatalog.GetEntriesByCategory();

        /// <inheritdoc />
        protected override string GetDisplayName(ActionTypeCatalogEntry entry) => entry.DisplayName;

        /// <inheritdoc />
        protected override string GetCategory(ActionTypeCatalogEntry entry) => entry.Category;

        /// <inheritdoc />
        protected override string GetDescription(ActionTypeCatalogEntry entry) => entry.Description;

        /// <inheritdoc />
        protected override Type GetTypeForEntry(ActionTypeCatalogEntry entry) => entry.ActionType;
    }
}
