using System;
using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Picker;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Conditions.Pickers
{
    /// <summary>
    /// Modal picker for choosing a <see cref="KSP.Messages.PropertyWatchers.PropertyWatcher" /> subclass to use on a <see cref="KSP.Game.Missions.PropertyCondition" />.
    /// </summary>
    /// <remarks>
    /// Mirrors the part-authoring AddModulePicker via the shared <see cref="PickerWindowBase{TEntry}" />.
    /// </remarks>
    public sealed class PropertyWatcherPicker : PickerWindowBase<PropertyWatcherCatalogEntry>
    {
        /// <summary>
        /// Opens the picker and invokes <paramref name="onConfirm" /> with the chosen catalog entry on confirm.
        /// </summary>
        /// <remarks>
        /// Cancel closes the window without firing the callback.
        /// </remarks>
        /// <param name="onConfirm">Callback invoked with the selected entry once the user confirms.</param>
        public static void Open(Action<PropertyWatcherCatalogEntry> onConfirm)
        {
            OpenWindow<PropertyWatcherPicker>("Pick Property Watcher", t =>
            {
                if (t == null) return;
                var entry = PropertyWatcherCatalog.GetEntries().FirstOrDefault(e => e.WatcherType == t);
                onConfirm?.Invoke(entry);
            });
        }

        /// <inheritdoc />
        protected override string SearchHintText => "Search watchers...";

        /// <inheritdoc />
        protected override IEnumerable<IGrouping<string, PropertyWatcherCatalogEntry>> GetEntriesByCategory()
            => PropertyWatcherCatalog.GetEntriesByCategory();

        /// <inheritdoc />
        protected override string GetDisplayName(PropertyWatcherCatalogEntry entry) => entry.DisplayName;

        /// <inheritdoc />
        protected override string GetCategory(PropertyWatcherCatalogEntry entry) => entry.Category;

        /// <inheritdoc />
        protected override string GetDescription(PropertyWatcherCatalogEntry entry) => entry.Description;

        /// <inheritdoc />
        protected override Type GetTypeForEntry(PropertyWatcherCatalogEntry entry) => entry.WatcherType;
    }
}
