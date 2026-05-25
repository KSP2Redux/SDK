using System.Reflection;
using KSP.Modules;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.PartAuthoring.Gizmos;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.DataEditors
{
    /// <summary>
    /// Custom editor for <see cref="Data_Heatshield" />. Renders the standard generic field
    /// surface, then injects a "Show Direction Gizmo" toggle directly below the
    /// <see cref="Data_Heatshield.ShieldingDirection" /> row so the visibility control for the
    /// SceneView gizmo lives with the field it controls.
    /// </summary>
    [DataEditor(typeof(Data_Heatshield))]
    public sealed class HeatshieldDataEditor : DataFieldIteratorEditor<Data_Heatshield>
    {
        /// <inheritdoc />
        protected override VisualElement InjectAfter(FieldInfo field)
        {
            if (field.Name == nameof(Data_Heatshield.ShieldingDirection))
            {
                return BuildGizmoToggle(
                    "Show Direction Gizmo",
                    () => PartAuthoringGizmoSettings.ShowHeatshieldShieldingDirection,
                    v => PartAuthoringGizmoSettings.ShowHeatshieldShieldingDirection = v);
            }
            return null;
        }
    }
}
