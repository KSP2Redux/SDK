using AwesomeTechnologies.VegetationSystem;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Custom inspector for a <see cref="VegetationPackagePro" /> asset.
    /// </summary>
    /// <remarks>
    /// The package's authorable surface lives in <see cref="ScatterPackageSection" />, so the same UI
    /// serves both this asset inspector and the inline copy inside the
    /// <see cref="VegetationSystemPro" /> inspector.
    ///
    /// Editing the asset directly has no owning body, so the biome mapping annotation appears only
    /// when the same package is inspected through a body's system.
    /// </remarks>
    [CustomEditor(typeof(VegetationPackagePro))]
    public class VegetationPackageProEditor : UnityEditor.Editor
    {
        private const string UssPath = "/Assets/Windows/PlanetAuthoring/Inspectors/PQSInspector.uss";

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            Ksp2UnityToolsStyles.Apply(root, UssPath);
            ScatterPackageSection.Build(root, target as VegetationPackagePro, null);
            return root;
        }
    }
}
