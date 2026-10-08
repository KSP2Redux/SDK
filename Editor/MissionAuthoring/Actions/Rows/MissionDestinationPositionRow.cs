using System.Reflection;
using KSP.Game.Missions;
using Ksp2UnityTools.Editor.MissionAuthoring.Widgets;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Actions.Rows
{
    /// <summary>
    /// Custom row for <see cref="MissionDestinationPosition" />.
    /// </summary>
    /// <remarks>
    /// Replaces the plain <c>celestialBodyName</c> text field with a <see cref="CelestialBodyKeyField" /> autocomplete and renders <c>cb_Coords</c> as a Vector3 widget.
    /// </remarks>
    [CustomActionRow(typeof(MissionDestinationPosition))]
    public sealed class MissionDestinationPositionRow : ActionRowBase
    {
        /// <summary>
        /// Creates a new <see cref="MissionDestinationPositionRow" /> bound to the supplied action and populates its body with celestial-body, coordinates, and stage-event fields.
        /// </summary>
        /// <param name="mission">The mission asset that owns the action.</param>
        /// <param name="action">The action instance to edit.</param>
        /// <param name="replace">Callback to swap the action with another or null.</param>
        /// <param name="notifyChanged">Callback fired when the action's state changes.</param>
        /// <param name="moveUp">Optional callback to reorder this row up within its parent list.</param>
        /// <param name="moveDown">Optional callback to reorder this row down within its parent list.</param>
        public MissionDestinationPositionRow(Mission mission, IMissionAction action, System.Action<IMissionAction> replace, System.Action notifyChanged, System.Action moveUp = null, System.Action moveDown = null)
            : base(mission, action, replace, notifyChanged, moveUp, moveDown)
        {
            var body = BuildCard();
            var typed = (MissionDestinationPosition)action;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            var bodyNameField = typeof(MissionDestinationPosition).GetField("celestialBodyName", flags);
            var coordsField = typeof(MissionDestinationPosition).GetField("cb_Coords", flags);

            if (bodyNameField != null)
            {
                var bodyKey = new CelestialBodyKeyField("Celestial Body", typed.celestialBodyName ?? string.Empty, v =>
                {
                    Undo.RecordObject(Mission, "Edit celestial body name");
                    bodyNameField.SetValue(typed, v ?? string.Empty);
                    EditorUtility.SetDirty(Mission);
                    NotifyChanged?.Invoke();
                });
                bodyKey.AddToClassList("condition-row-field");
                ApplyTooltip(bodyKey, bodyNameField);
                body.Add(bodyKey);
            }

            if (coordsField != null)
            {
                var coords = new UnityEngine.UIElements.Vector3Field("Body Coords") { value = typed.cb_Coords };
                StyleFieldWidget(coords);
                ApplyTooltip(coords, coordsField);
                coords.RegisterValueChangedCallback(e =>
                {
                    Undo.RecordObject(Mission, "Edit body coords");
                    coordsField.SetValue(typed, e.newValue);
                    EditorUtility.SetDirty(Mission);
                    NotifyChanged?.Invoke();
                });
                body.Add(coords);
            }

            BuildStageEventField(body);
        }
    }
}
