using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

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
        private const string UxmlPath = "/Assets/Windows/NewDecalPromptWindow.uxml";

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

        private Foldout _newDecalFoldout;
        private Button _confirmButton;
        private TextField _nameField;

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
            win.minSize = new Vector2(420f, 420f);
            win.maxSize = new Vector2(560f, 720f);
            win.ShowUtility();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                root.Add(new Label("Failed to load NewDecalPromptWindow.uxml"));
                return;
            }
            tree.CloneTree(root);

            Ksp2UnityToolsStyles.Apply(root);

            // HelpBoxes are added from C# since the project's UI Toolkit version doesn't accept the
            // HelpBox UXML tag.
            root.Q<VisualElement>("existing-hint-slot").Add(new HelpBox(
                "Drop a PQSDecal here to skip creation and place an instance of it instead.",
                HelpBoxMessageType.None));
            root.Q<VisualElement>("textures-hint-slot").Add(new HelpBox(
                "Heightmap and Alpha map are shared per body. Edit them on the Simulation prefab's PQS Decal Controller.",
                HelpBoxMessageType.None));

            var existing = root.Q<ObjectField>("existing-template-field");
            existing.SetValueWithoutNotify(_result.ExistingTemplate);
            existing.RegisterValueChangedCallback(evt =>
            {
                _result.ExistingTemplate = evt.newValue as PQSDecal;
                SyncEnabledState();
            });

            _nameField = root.Q<TextField>("name-field");
            _nameField.SetValueWithoutNotify(_result.Name);
            _nameField.RegisterValueChangedCallback(evt =>
            {
                _result.Name = evt.newValue;
                SyncEnabledState();
            });

            var heightScale = root.Q<FloatField>("height-scale-field");
            heightScale.SetValueWithoutNotify(_result.HeightScale);
            heightScale.RegisterValueChangedCallback(evt => _result.HeightScale = evt.newValue);

            var blendMode = root.Q<EnumField>("blend-mode-field");
            blendMode.Init(_result.BlendMode);
            blendMode.RegisterValueChangedCallback(evt => _result.BlendMode = (PQSDecalHeightBlendMode)evt.newValue);

            WireTexture(root, "diffuse-field", v => _result.Diffuse = v, _result.Diffuse);
            WireTexture(root, "normal-field", v => _result.Normal = v, _result.Normal);
            WireTexture(root, "alpha-mask-field", v => _result.AlphaMaskTexture = v, _result.AlphaMaskTexture);
            WireTexture(root, "peak-field", v => _result.Peak = v, _result.Peak);
            WireTexture(root, "slope-field", v => _result.Slope = v, _result.Slope);

            _newDecalFoldout = root.Q<Foldout>("new-decal-foldout");

            root.Q<Button>("cancel-button").clicked += Close;
            _confirmButton = root.Q<Button>("confirm-button");
            _confirmButton.clicked += () =>
            {
                _onCreate?.Invoke(_result);
                Close();
            };

            SyncEnabledState();
        }

        private static void WireTexture(VisualElement root, string name, Action<Texture2D> setter, Texture2D initial)
        {
            var field = root.Q<ObjectField>(name);
            field.SetValueWithoutNotify(initial);
            field.RegisterValueChangedCallback(evt => setter(evt.newValue as Texture2D));
        }

        private void SyncEnabledState()
        {
            var hasExisting = _result.ExistingTemplate != null;
            _newDecalFoldout?.SetEnabled(!hasExisting);
            var canConfirm = hasExisting || !string.IsNullOrWhiteSpace(_result.Name);
            _confirmButton?.SetEnabled(canConfirm);
            if (_confirmButton != null)
            {
                _confirmButton.text = hasExisting ? "Use" : "Create";
            }
        }
    }
}
