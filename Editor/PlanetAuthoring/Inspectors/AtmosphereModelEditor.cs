using KSP.Rendering;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Custom inspector for a Bruneton <see cref="AtmosphereModel" /> asset.
    /// </summary>
    [CustomEditor(typeof(AtmosphereModel))]
    public class AtmosphereModelEditor : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI() =>
            AtmosphereModelInspector.Build((AtmosphereModel)target, serializedObject);
    }
}
