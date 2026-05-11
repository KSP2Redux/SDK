using KSP.VFX;
using UnityEditor;
using UnityEngine;

namespace KSP.Editor
{
    [CustomEditor(typeof(ThrottleLightData))]
    public class ThrottleLightDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            DrawParentManagerPreview();
        }

        private void DrawParentManagerPreview()
        {
            var lightData = (ThrottleLightData)target;
            var manager = lightData.GetComponentInParent<ThrottleVFXManager>();

            EditorGUILayout.LabelField("Preview (Parent Manager)", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("Parent Manager", manager, typeof(ThrottleVFXManager), true);
            EditorGUI.EndDisabledGroup();

            if (manager == null)
            {
                EditorGUILayout.HelpBox(
                    "No parent ThrottleVFXManager found. Add one to use unified preview controls.",
                    MessageType.Info
                );
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select Manager"))
            {
                Selection.activeObject = manager;
                EditorGUIUtility.PingObject(manager);
            }

            if (GUILayout.Button("Focus Preview on Manager"))
            {
                ThrottleVFXPreviewBridge.ActivateAndRefresh(manager);
            }

            EditorGUILayout.EndHorizontal();
            ThrottleVFXPreviewBridge.DrawPreviewControls(manager, showHeader: false);
        }
    }
}
