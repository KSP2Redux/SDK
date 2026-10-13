using System.Collections.Generic;
using UnityEngine;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// The R&amp;D Center's layout values and rules, so the tech tree editor draws a tree where the game does.
    /// </summary>
    /// <remarks>
    /// The values are read from the shipped <c>RDCenterUI.prefab</c> and its sprites. The frame and connector
    /// sprites are mostly transparent padding, so what shows is smaller than the rects the prefab declares.
    /// Content space here has its origin at the bottom-left of the tree strip and y growing upward, as the game's.
    /// </remarks>
    public static class TechTreeCanvasGeometry
    {
        /// <summary>
        /// The offset the game adds to every node's tech tree position.
        /// </summary>
        public const float PLACEMENT_OFFSET = 100f;

        /// <summary>
        /// The width of a node's rect.
        /// </summary>
        public const float NODE_WIDTH = 164f;

        /// <summary>
        /// The height of a node's rect.
        /// </summary>
        public const float NODE_HEIGHT = 102f;

        /// <summary>
        /// The width of the opaque part of a node's frame.
        /// </summary>
        public const float FRAME_WIDTH = 152f;

        /// <summary>
        /// The height of the opaque part of a node's frame.
        /// </summary>
        public const float FRAME_HEIGHT = 90f;

        /// <summary>
        /// The height of the R&amp;D Center's tree mask, the band the game shows without scrolling.
        /// </summary>
        public const float BAND_HEIGHT = 962f;

        /// <summary>
        /// The width of one tier page.
        /// </summary>
        public const float PAGE_WIDTH = 1500f;

        /// <summary>
        /// How far right of a prerequisite a connector turns when the two nodes share neither row nor column.
        /// </summary>
        public const float CONNECTOR_BEND = 90f;

        /// <summary>
        /// The width of the opaque body of a connector.
        /// </summary>
        public const float CONNECTOR_WIDTH = 2.5f;

        /// <summary>
        /// The stock tree's column spacing.
        /// </summary>
        public const float GRID_X = 180f;

        /// <summary>
        /// The stock tree's row spacing.
        /// </summary>
        public const float GRID_Y = 110f;

        /// <summary>
        /// The y of the stock tree's lowest regular row.
        /// </summary>
        public const float GRID_Y_ORIGIN = 90f;

        /// <summary>
        /// Gets where a node's centre sits in content space.
        /// </summary>
        /// <param name="techTreePosition">The node's tech tree position.</param>
        /// <returns>The centre in content space.</returns>
        public static Vector2 ToContent(Vector2 techTreePosition) =>
            techTreePosition + new Vector2(PLACEMENT_OFFSET, PLACEMENT_OFFSET);

        /// <summary>
        /// Gets the points of the connector the game draws from a prerequisite to the node that requires it.
        /// </summary>
        /// <remarks>
        /// Nodes on one row or one column get a straight line. Any other pair gets an elbow that leaves the
        /// prerequisite to the right, turns <see cref="CONNECTOR_BEND" /> along, and runs into the node on its row.
        /// </remarks>
        /// <param name="prerequisite">The prerequisite's tech tree position.</param>
        /// <param name="node">The requiring node's tech tree position.</param>
        /// <returns>The connector's points, in tech tree positions.</returns>
        public static List<Vector2> GetConnectorPoints(Vector2 prerequisite, Vector2 node)
        {
            if (Mathf.Approximately(prerequisite.x, node.x) || Mathf.Approximately(prerequisite.y, node.y))
                return new List<Vector2> { prerequisite, node };

            float turnX = prerequisite.x + CONNECTOR_BEND;
            return new List<Vector2>
            {
                prerequisite,
                new(turnX, prerequisite.y),
                new(turnX, node.y),
                node,
            };
        }

        /// <summary>
        /// Snaps a tech tree position to the stock grid.
        /// </summary>
        /// <param name="techTreePosition">The position to snap.</param>
        /// <returns>The nearest grid point.</returns>
        public static Vector2 Snap(Vector2 techTreePosition) =>
            new(
                Mathf.Round(techTreePosition.x / GRID_X) * GRID_X,
                Mathf.Round((techTreePosition.y - GRID_Y_ORIGIN) / GRID_Y) * GRID_Y + GRID_Y_ORIGIN
            );
    }
}
