using KSP;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets;
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
            var section = CardListSection.Build(arrayProp, new CardListSection.Config
            {
                Title = "Attach Nodes",
                AddButtonText = "+ Add Attach Node",
                IdentityFieldName = "nodeID",
                BuildIdentityField = idProp => new AutocompleteField(idProp, string.Empty, PartAuthoringChoiceCatalog.GetStockAttachNodeIds),
                ApplyDefaultsToNew = SeedNewAttachNode,
                BuildBody = (entry, body) => BuildAttachNodeBody(entry, body, target)
            });
            section.AddToClassList("attach-nodes-container");
            return section;
        }

        private static void SeedNewAttachNode(SerializedProperty entry, int index)
        {
            var sizeKeyProp = entry.FindPropertyRelative("sizeKey");
            if (sizeKeyProp != null && string.IsNullOrWhiteSpace(sizeKeyProp.stringValue))
            {
                sizeKeyProp.stringValue = KSP.OAB.PartSizeRegistry.DefaultSizeKey;
            }

            var orientationProp = entry.FindPropertyRelative("orientation");
            if (orientationProp == null)
            {
                return;
            }

            orientationProp.FindPropertyRelative("x").doubleValue = 1d;
            orientationProp.FindPropertyRelative("y").doubleValue = 0d;
            orientationProp.FindPropertyRelative("z").doubleValue = 0d;
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
                if (iterator.name == "size") continue;
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
                if (iterator.name == "sizeKey")
                {
                    body.Add(new AutocompleteField(
                        iterator.Copy(),
                        "Size Key",
                        PartAuthoringChoiceCatalog.GetKnownSizeKeys,
                        detailSource: PartAuthoringChoiceCatalog.GetKnownSizeKeyDetail,
                        preserveSourceOrderForEqualScores: true));
                    continue;
                }
                body.Add(new PropertyField(iterator.Copy()));
            }
            while (iterator.NextVisible(false));
        }
    }
}
