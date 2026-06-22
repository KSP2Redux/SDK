using KSP;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.PartAuthoring.SceneTools;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections
{
    /// <summary>
    /// Builds the attach-node list as a <see cref="CardListSection" /> with a body builder that
    /// special-cases <c>position</c> and <c>orientation</c> with SceneView handle pickers.
    /// </summary>
    /// <remarks>
    /// External array mutations (e.g. the Auto-detect button replacing the array wholesale) require
    /// the caller to clear and re-build the section in a holder VisualElement, since CardListSection
    /// uses surgical add/remove rather than external full-refresh.
    /// </remarks>
    internal static class AttachNodesList
    {
        /// <summary>
        /// Builds the attach-node list for the given array SerializedProperty.
        /// </summary>
        /// <param name="arrayProp">The attach-node array SerializedProperty.</param>
        /// <param name="target">The Component whose hierarchy hosts the SceneView handles.</param>
        /// <returns>The built section element.</returns>
        public static VisualElement Build(SerializedProperty arrayProp, Component target)
        {
            return CardListSection.Build(arrayProp, new CardListSection.Config
            {
                Title = "Attach Nodes",
                AddButtonText = "+ Add Attach Node",
                IdentityFieldName = "nodeID",
                BuildBody = (entry, body) => BuildAttachNodeBody(entry, body, target),
                ApplyDefaultsToNew = (entry, i) =>
                {
                    var orientation = entry.FindPropertyRelative("orientation")!;
                    var x = orientation.FindPropertyRelative("x")!;
                    x.doubleValue = 1.0;
                    var y = orientation.FindPropertyRelative("y")!;
                    y.doubleValue = 0.0;
                    var z = orientation.FindPropertyRelative("z")!;
                    z.doubleValue = 0.0;
                }
            });
        }

        private static void BuildAttachNodeBody(SerializedProperty entry, VisualElement body, Component target)
        {
            var positionProp = entry.FindPropertyRelative("position");

            var iterator = entry.Copy();
            var endProp = iterator.GetEndProperty();
            if (!iterator.NextVisible(true)) return;
            do
            {
                if (SerializedProperty.EqualContents(iterator, endProp)) break;
                if (iterator.name == "nodeID") continue;
                if (iterator.name == "position")
                {
                    body.Add(new VectorHandleField(iterator.Copy(), target, SceneHandlePicker.HandleMode.Position));
                    continue;
                }
                if (iterator.name == "orientation")
                {
                    body.Add(new VectorHandleField(iterator.Copy(), target, SceneHandlePicker.HandleMode.Orientation, positionProp));
                    continue;
                }
                body.Add(new PropertyField(iterator.Copy()));
            }
            while (iterator.NextVisible(false));
        }
    }
}
