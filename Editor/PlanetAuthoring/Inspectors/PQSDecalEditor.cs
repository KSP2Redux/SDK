using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Custom inspector for <see cref="PQSDecal" />.
    /// </summary>
    /// <remarks>
    /// Layout lives in <c>PQSDecalInspector.uxml</c>. This editor wires up the place button and
    /// binds the texture sub-tree to the editor-only <see cref="PQSDecalTemplateAuthoring" /> sidecar.
    /// </remarks>
    [CustomEditor(typeof(PQSDecal))]
    public class PQSDecalEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "/Assets/Windows/PQSDecalInspector.uxml";

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                root.Add(new Label("Failed to load PQSDecalInspector.uxml"));
                return root;
            }
            tree.CloneTree(root);

            root.Q<Button>("place-button").clicked += OnPlaceClicked;

            // Bind the value-fields subtree to the PQSDecal asset.
            root.Q<VisualElement>("value-fields").Bind(serializedObject);

            // Bind the texture-fields subtree to the editor-only sidecar so its property paths resolve against the sidecar SO instead of PQSDecal.
            var decal = (PQSDecal)target;
            if (string.IsNullOrEmpty(decal.DecalID)) return root;
            var authoring = PlanetAuthoringRegistry.Instance.GetOrCreateDecalTemplate(decal.DecalID);
            if (authoring == null) return root;
            root.Q<VisualElement>("texture-fields").Bind(new SerializedObject(authoring));

            return root;
        }

        private void OnPlaceClicked()
        {
            PlaceDecalTool.Begin((PQSDecal)target);
        }
    }
}
