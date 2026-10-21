using KSP.VFX;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace KSP.Editor
{
    [CustomEditor(typeof(ThrottleParticleSystemData))]
    public class ThrottleParticleSystemDataEditor : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var effect = (ThrottleParticleSystemData)target;
            root.Add(ThrottleVFXPreviewBridge.CreatePreviewControls(effect.GetComponentInParent<ThrottleVFXManager>(true)));
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            root.RegisterCallback<SerializedPropertyChangeEvent>(_ => ThrottleVFXPreviewBridge.RefreshIfActive(effect));
            return root;
        }
    }
}
