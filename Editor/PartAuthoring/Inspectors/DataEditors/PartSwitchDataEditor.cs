using KSP.Sim.Definitions;
using UnityEditor;
using UnityEngine.UIElements;
using VSwift.Modules.Data;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.DataEditors
{
    /// <summary>
    /// Surfaces <see cref="Data_PartSwitch" /> in the Modules tab as a deliberate placeholder that points the author at the Variants tab, where the same data has a purpose-built UI.
    /// </summary>
    /// <remarks>
    /// The Modules tab and Variants tab both bind to the same <see cref="Data_PartSwitch.VariantSets" /> data, so authoring works in either surface. This editor exists so the Modules-tab presentation doesn't render the full reflection editor (which would be noisy and harder to use than the Variants tab's dedicated layout).
    /// </remarks>
    [DataEditor(typeof(Data_PartSwitch))]
    public sealed class PartSwitchDataEditor : IDataEditor
    {
        /// <inheritdoc />
        public VisualElement Build(SerializedProperty dataProp, PartBehaviourModule module)
        {
            return new HelpBox(
                "Variants must be edited in the Variants tab.",
                HelpBoxMessageType.Info);
        }
    }
}
