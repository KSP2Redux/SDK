using System.Reflection;
using KSP.Modules;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.PartAuthoring.Gizmos;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.DataEditors
{
    /// <summary>
    /// Custom editor for <see cref="Data_WheelBogey" />. Renders the standard generic field
    /// surface, then injects "Show Axis Gizmo" / "Show Up Axis Gizmo" toggles directly below
    /// the <c>bogeyAxis</c> / <c>bogeyUpAxis</c> rows so the visibility control for each
    /// SceneView arrow lives with the axis it controls.
    /// </summary>
    [DataEditor(typeof(Data_WheelBogey))]
    public sealed class WheelBogeyDataEditor : DataFieldIteratorEditor<Data_WheelBogey>
    {
        /// <inheritdoc />
        protected override VisualElement InjectAfter(FieldInfo field)
        {
            if (field.Name == nameof(Data_WheelBogey.bogeyAxis))
            {
                return BuildGizmoToggle(
                    "Show Axis Gizmo",
                    () => PartAuthoringGizmoSettings.ShowWheelBogeyAxis,
                    v => PartAuthoringGizmoSettings.ShowWheelBogeyAxis = v);
            }
            if (field.Name == nameof(Data_WheelBogey.bogeyUpAxis))
            {
                return BuildGizmoToggle(
                    "Show Up Axis Gizmo",
                    () => PartAuthoringGizmoSettings.ShowWheelBogeyUpAxis,
                    v => PartAuthoringGizmoSettings.ShowWheelBogeyUpAxis = v);
            }
            return null;
        }
    }
}
