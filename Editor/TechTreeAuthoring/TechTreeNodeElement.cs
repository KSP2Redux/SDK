using Ksp2UnityTools.Editor.Validation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// One node on the tech tree canvas, drawn as the R&amp;D Center draws a locked node: its frame, icon, name and
    /// science cost.
    /// </summary>
    /// <remarks>
    /// The element covers the frame's opaque 152 by 90, so its children sit 6 units in from where the prefab places
    /// them inside the full 164 by 102 node rect.
    /// </remarks>
    public class TechTreeNodeElement : VisualElement
    {
        private const string SELECTED_CLASS = "tech-tree-node--selected";

        private const string REFERENCE_CLASS = "tech-tree-node--reference";

        private const string UNLOCKABLE_CLASS = "tech-tree-node--unlockable";

        private const string UNLOCKED_CLASS = "tech-tree-node--unlocked";

        private const string BADGE_ERROR_CLASS = "tech-tree-node__badge--error";

        private VisualElement _badge;

        // The prefab lays children out in the 164 by 102 rect, and the frame is inset from it by this much
        private const float RECT_INSET = 6f;

        /// <summary>
        /// Gets the ID of the node this element shows.
        /// </summary>
        public string NodeId { get; }

        /// <summary>
        /// Creates the element for a node.
        /// </summary>
        /// <param name="node">The node to show.</param>
        /// <param name="contentTop">The top of the node's frame in the canvas content.</param>
        public TechTreeNodeElement(TechTreeNode node, Vector2 contentTop)
        {
            NodeId = node.Data.ID;
            AddToClassList("tech-tree-node");
            MoveTo(contentTop);
            style.width = TechTreeCanvasGeometry.FRAME_WIDTH;
            style.height = TechTreeCanvasGeometry.FRAME_HEIGHT;

            var header = new VisualElement();
            header.AddToClassList("tech-tree-node__header");
            Add(header);

            Sprite icon = TechTreeIconCache.Get(node.Data.IconID);
            if (icon != null)
            {
                var iconElement = new Image { sprite = icon };
                iconElement.AddToClassList("tech-tree-node__icon");
                iconElement.style.left = 15f - RECT_INSET;
                iconElement.style.top = 10f - RECT_INSET;
                Add(iconElement);
            }

            var cost = new VisualElement();
            cost.AddToClassList("tech-tree-node__cost");
            cost.style.right = 11f - RECT_INSET;
            cost.style.top = 12f - RECT_INSET;
            var dot = new VisualElement();
            dot.AddToClassList("tech-tree-node__science");
            cost.Add(dot);
            cost.Add(new Label(node.Data.RequiredSciencePoints.ToString()));
            Add(cost);

            var name = new Label(string.IsNullOrEmpty(node.EnglishName) ? node.Data.ID : node.EnglishName);
            name.AddToClassList("tech-tree-node__name");
            name.style.left = 15f - RECT_INSET;
            name.style.top = 40f - RECT_INSET;
            Add(name);
        }

        /// <summary>
        /// Moves the element.
        /// </summary>
        /// <param name="contentTop">The top of the node's frame in the canvas content.</param>
        public void MoveTo(Vector2 contentTop)
        {
            style.left = contentTop.x;
            style.top = contentTop.y;
        }

        /// <summary>
        /// Shows the node as one of a reference tree, which the editor never changes, or as one of the tree edited.
        /// </summary>
        /// <param name="isReference">True for a reference node, false otherwise.</param>
        public void SetReference(bool isReference) => EnableInClassList(REFERENCE_CLASS, isReference);

        /// <summary>
        /// Draws the node in the frame colours of a preview state, or in the editor's own look.
        /// </summary>
        /// <param name="state">The state, or null outside the preview.</param>
        public void SetPreviewState(TechTreePreviewState? state)
        {
            EnableInClassList(UNLOCKABLE_CLASS, state == TechTreePreviewState.Unlockable);
            EnableInClassList(UNLOCKED_CLASS, state == TechTreePreviewState.Unlocked);
        }

        /// <summary>
        /// Shows a mark in the frame's corner for the worst problem validation found with the node.
        /// </summary>
        /// <param name="severity">The worst problem, or null for none worth marking.</param>
        public void SetBadge(ValidationSeverity? severity)
        {
            if (severity is not (ValidationSeverity.Error or ValidationSeverity.Warning))
            {
                _badge?.RemoveFromHierarchy();
                _badge = null;
                return;
            }

            if (_badge == null)
            {
                _badge = new VisualElement { pickingMode = PickingMode.Ignore };
                _badge.AddToClassList("tech-tree-node__badge");
                Add(_badge);
            }

            _badge.EnableInClassList(BADGE_ERROR_CLASS, severity == ValidationSeverity.Error);
        }

        /// <summary>
        /// Shows or clears the selection outline.
        /// </summary>
        /// <param name="isSelected">True to show the node as selected, false otherwise.</param>
        public void SetSelected(bool isSelected) => EnableInClassList(SELECTED_CLASS, isSelected);
    }
}
