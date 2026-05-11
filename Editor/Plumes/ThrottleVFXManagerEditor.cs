using KSP.VFX;
using UnityEditor;

namespace KSP.Editor
{
    [CustomEditor(typeof(ThrottleVFXManager))]
    public class ThrottleVFXManagerEditor : UnityEditor.Editor
    {
        private ThrottleVFXManager _target;

        private void OnEnable()
        {
            _target = (ThrottleVFXManager)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            ThrottleVFXPreviewBridge.DrawPreviewControls(_target, showHeader: true);
        }
    }
}