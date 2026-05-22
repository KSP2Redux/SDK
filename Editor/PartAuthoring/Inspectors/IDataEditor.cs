using KSP.Sim.Definitions;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors
{
    /// <summary>
    /// Contract for a custom editor that renders a specific <c>Data_*</c> type. Implementations
    /// decorate themselves with <see cref="DataEditorAttribute" /> to register against a data type.
    /// </summary>
    /// <remarks>
    /// A new instance is created per inspector build by the dispatch, so editors can hold per-build
    /// state (collapsed/expanded states, transient UI flags, etc.) without conflicting across
    /// inspector instances.
    ///
    /// The returned <see cref="VisualElement" /> is the CONTENT of the data block (not including
    /// the chrome header label). The dispatch wraps it in the standard data-block chrome so
    /// custom editors stay visually consistent with the rest of the inspector.
    /// </remarks>
    public interface IDataEditor
    {
        /// <summary>
        /// Builds the editor's content for the given Data_* serialized property.
        /// </summary>
        /// <param name="dataProp">The SerializedProperty pointing at the Data_* instance.</param>
        /// <param name="module">The module Component the data lives on. Use this to access sibling
        /// components, the part GameObject, and prefab-hierarchy queries.</param>
        VisualElement Build(SerializedProperty dataProp, PartBehaviourModule module);
    }
}
