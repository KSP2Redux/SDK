using System.Reflection;
using KSP.Game.Missions;
using Ksp2UnityTools.Editor.MissionAuthoring.Widgets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Actions.Rows
{
    /// <summary>
    /// Custom row for <see cref="FullscreenVideoAction" />.
    /// </summary>
    /// <remarks>
    /// Surfaces the on-finished message event type via <see cref="MessageEventTypeField" /> and the video addressable key via <see cref="TriumphLoopVideoKeyField" /> autocomplete.
    /// </remarks>
    [CustomActionRow(typeof(FullscreenVideoAction))]
    public sealed class FullscreenVideoActionRow : ActionRowBase
    {
        /// <summary>
        /// Creates a new <see cref="FullscreenVideoActionRow" /> bound to the supplied action and populates its body with the event-type, video-key, and stage-event fields.
        /// </summary>
        /// <param name="mission">The mission asset that owns the action.</param>
        /// <param name="action">The action instance to edit.</param>
        /// <param name="replace">Callback to swap the action with another or null.</param>
        /// <param name="notifyChanged">Callback fired when the action's state changes.</param>
        /// <param name="moveUp">Optional callback to reorder this row up within its parent list.</param>
        /// <param name="moveDown">Optional callback to reorder this row down within its parent list.</param>
        public FullscreenVideoActionRow(Mission mission, IMissionAction action, System.Action<IMissionAction> replace, System.Action notifyChanged, System.Action moveUp = null, System.Action moveDown = null)
            : base(mission, action, replace, notifyChanged, moveUp, moveDown)
        {
            var body = BuildCard();
            var typed = (FullscreenVideoAction)action;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            var eventField = typeof(FullscreenVideoAction).GetField("eventTypeOnFinished", flags);
            var keyField = typeof(FullscreenVideoAction).GetField("scriptableOjectKey", flags);

            if (eventField != null)
            {
                var eventTypeField = new MessageEventTypeField("Event On Finished", typed.eventTypeOnFinished, t =>
                {
                    Undo.RecordObject(Mission, "Edit event type on finished");
                    eventField.SetValue(typed, t);
                    EditorUtility.SetDirty(Mission);
                    NotifyChanged?.Invoke();
                });
                ApplyTooltip(eventTypeField, eventField);
                body.Add(eventTypeField);
            }

            if (keyField != null)
            {
                var videoKey = new TriumphLoopVideoKeyField("Video Key", typed.scriptableOjectKey ?? string.Empty, v =>
                {
                    Undo.RecordObject(Mission, "Edit video key");
                    keyField.SetValue(typed, v ?? string.Empty);
                    EditorUtility.SetDirty(Mission);
                    NotifyChanged?.Invoke();
                });
                videoKey.AddToClassList("condition-row-field");
                ApplyTooltip(videoKey, keyField);
                body.Add(videoKey);
            }

            BuildStageEventField(body);
        }
    }
}
