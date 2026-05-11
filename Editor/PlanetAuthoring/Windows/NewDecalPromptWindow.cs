using System;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Windows
{
    /// <summary>
    /// Modal popup for creating a new <see cref="PQSDecal" /> or selecting an existing one to place.
    /// </summary>
    /// <remarks>
    /// Two paths: drop an existing PQSDecal asset into the Existing Template slot to skip the rest
    /// of the form, or fill in the name + config + optional textures to create a fresh one.
    /// </remarks>
    public class NewDecalPromptWindow : EditorWindow
    {
        /// <summary>
        /// The form payload returned to the caller's create callback.
        /// </summary>
        public class Result
        {
            /// <summary>The pre-existing PQSDecal to place an instance of, or null when creating a new template.</summary>
            public PQSDecal ExistingTemplate;
            /// <summary>The asset name for the new template.</summary>
            public string Name = "Decal";
            /// <summary>The initial height scale of the new template, in meters.</summary>
            public float HeightScale = 90f;
            /// <summary>The initial height blend mode of the new template.</summary>
            public PQSDecalHeightBlendMode BlendMode = PQSDecalHeightBlendMode.Add;
            /// <summary>Optional initial diffuse texture stored on the editor-only sidecar.</summary>
            public Texture2D Diffuse;
            /// <summary>Optional initial normal texture stored on the editor-only sidecar.</summary>
            public Texture2D Normal;
            /// <summary>Optional initial alpha-mask texture stored on the editor-only sidecar.</summary>
            public Texture2D AlphaMaskTexture;
            /// <summary>Optional initial peak gradience texture stored on the editor-only sidecar.</summary>
            public Texture2D Peak;
            /// <summary>Optional initial slope gradience texture stored on the editor-only sidecar.</summary>
            public Texture2D Slope;
        }

        private readonly Result _result = new();
        private Action<Result> _onCreate;
        private Vector2 _scroll;

        /// <summary>
        /// Opens the prompt window and invokes <paramref name="onCreate" /> when the user confirms.
        /// </summary>
        /// <param name="defaultName">The pre-filled name for a new template.</param>
        /// <param name="onCreate">Callback fired with the populated <see cref="Result" /> on confirm.</param>
        public static void Show(string defaultName, Action<Result> onCreate)
        {
            var win = CreateInstance<NewDecalPromptWindow>();
            win.titleContent = new GUIContent("New Decal");
            win._result.Name = defaultName;
            win._onCreate = onCreate;
            win.minSize = new Vector2(380f, 360f);
            win.maxSize = new Vector2(480f, 600f);
            win.ShowUtility();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Space(6f);

            EditorGUILayout.LabelField("Existing", EditorStyles.boldLabel);
            _result.ExistingTemplate = (PQSDecal)EditorGUILayout.ObjectField("Template", _result.ExistingTemplate, typeof(PQSDecal), false);
            EditorGUILayout.HelpBox("Drop a PQSDecal here to skip creation and place an instance of it instead.", MessageType.None);

            GUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(_result.ExistingTemplate != null))
            {
                EditorGUILayout.LabelField("New decal", EditorStyles.boldLabel);
                _result.Name = EditorGUILayout.TextField("Name", _result.Name);
                _result.HeightScale = EditorGUILayout.FloatField("Height Scale", _result.HeightScale);
                _result.BlendMode = (PQSDecalHeightBlendMode)EditorGUILayout.EnumPopup("Blend Mode", _result.BlendMode);

                GUILayout.Space(6f);
                EditorGUILayout.LabelField("Per-decal textures (optional)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Heightmap and Alpha map are shared per body. Edit them on the Simulation prefab's PQS Decal Controller.", MessageType.None);
                _result.Diffuse = (Texture2D)EditorGUILayout.ObjectField("Diffuse", _result.Diffuse, typeof(Texture2D), false);
                _result.Normal = (Texture2D)EditorGUILayout.ObjectField("Normal", _result.Normal, typeof(Texture2D), false);
                _result.AlphaMaskTexture = (Texture2D)EditorGUILayout.ObjectField("Alpha Mask Texture", _result.AlphaMaskTexture, typeof(Texture2D), false);
                _result.Peak = (Texture2D)EditorGUILayout.ObjectField("Peak", _result.Peak, typeof(Texture2D), false);
                _result.Slope = (Texture2D)EditorGUILayout.ObjectField("Slope", _result.Slope, typeof(Texture2D), false);
            }

            EditorGUILayout.EndScrollView();

            GUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel"))
                {
                    Close();
                }
                var canCreate = _result.ExistingTemplate != null || !string.IsNullOrWhiteSpace(_result.Name);
                using (new EditorGUI.DisabledScope(!canCreate))
                {
                    if (GUILayout.Button(_result.ExistingTemplate != null ? "Place" : "Create and place"))
                    {
                        _onCreate?.Invoke(_result);
                        Close();
                    }
                }
            }
        }
    }
}
