using System;
using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Picker;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Conditions.Pickers
{
    /// <summary>
    /// Modal picker for choosing a <see cref="KSP.Messages.MessageCenterMessage" /> subclass to use on an <see cref="KSP.Game.Missions.EventCondition" />.
    /// </summary>
    /// <remarks>
    /// Mirrors the PropertyWatcherPicker chrome via the shared <see cref="PickerWindowBase{TEntry}" />.
    /// </remarks>
    public sealed class MessageCenterMessagePicker : PickerWindowBase<MessageCenterMessageCatalogEntry>
    {
        /// <summary>
        /// Opens the picker and invokes <paramref name="onConfirm" /> with the chosen catalog entry on confirm.
        /// </summary>
        /// <remarks>
        /// Cancel closes the window without firing the callback.
        /// </remarks>
        /// <param name="onConfirm">Callback invoked with the selected entry once the user confirms.</param>
        public static void Open(Action<MessageCenterMessageCatalogEntry> onConfirm)
        {
            OpenWindow<MessageCenterMessagePicker>("Pick Message Type", t =>
            {
                if (t == null) return;
                var entry = MessageCenterMessageCatalog.GetEntries().FirstOrDefault(e => e.MessageType == t);
                onConfirm?.Invoke(entry);
            });
        }

        /// <inheritdoc />
        protected override string SearchHintText => "Search messages...";

        /// <inheritdoc />
        protected override IEnumerable<IGrouping<string, MessageCenterMessageCatalogEntry>> GetEntriesByCategory()
            => MessageCenterMessageCatalog.GetEntriesByCategory();

        /// <inheritdoc />
        protected override string GetDisplayName(MessageCenterMessageCatalogEntry entry) => entry.DisplayName;

        /// <inheritdoc />
        protected override string GetCategory(MessageCenterMessageCatalogEntry entry) => entry.Category;

        /// <inheritdoc />
        protected override string GetDescription(MessageCenterMessageCatalogEntry entry) => entry.Description;

        /// <inheritdoc />
        protected override Type GetTypeForEntry(MessageCenterMessageCatalogEntry entry) => entry.MessageType;
    }
}
