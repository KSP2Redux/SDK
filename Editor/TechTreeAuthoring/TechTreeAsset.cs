using System;
using System.Collections.Generic;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.Modding;
using UnityEngine;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// A whole tech tree as one authoring asset: its tiers and every node in it.
    /// </summary>
    /// <remarks>
    /// The asset is the source of truth. The node JSON the game loads, the tree data and the localization rows are
    /// all baked from it. The features that gate the tree are the feature labels of its baked files.
    /// </remarks>
    public class TechTreeAsset : ScriptableObject, IBakeableAsset
    {
        /// <summary>
        /// The node the R&amp;D Center focuses on when it first opens, unless a tree names another.
        /// </summary>
        public const string DEFAULT_START_NODE_ID = "tNode_1v_start";

        /// <summary>
        /// The ID of the node the R&amp;D Center focuses on when it first opens, or empty for
        /// <see cref="DEFAULT_START_NODE_ID" />.
        /// </summary>
        public string StartNodeId;

        /// <summary>
        /// The tiers, the first tier first. A tier without an English name is shown in game by its number.
        /// </summary>
        public List<TechTreeTier> Tiers = new();

        /// <summary>
        /// The nodes in the tree.
        /// </summary>
        public List<TechTreeNode> Nodes = new();

        /// <summary>
        /// The asset paths the last bake wrote, so the next bake can remove the files of nodes since removed or
        /// renamed.
        /// </summary>
        [HideInInspector] public List<string> BakedFiles = new();

        /// <inheritdoc />
        public virtual bool ShouldBake => true;

        /// <inheritdoc />
        public List<string> Bake() => TechTreeBaker.BakeQuietly(this);
    }

    /// <summary>
    /// One tier of a <see cref="TechTreeAsset" />.
    /// </summary>
    [Serializable]
    public class TechTreeTier
    {
        /// <summary>
        /// The tier's name in English, or empty to show the tier by its number.
        /// </summary>
        public string EnglishName;
    }

    /// <summary>
    /// One node of a <see cref="TechTreeAsset" />: the data the game loads, and the English text its keys export.
    /// </summary>
    [Serializable]
    public class TechTreeNode
    {
        /// <summary>
        /// The node as the game loads it.
        /// </summary>
        public TechNodeData Data = new();

        /// <summary>
        /// The node's name in English.
        /// </summary>
        public string EnglishName;

        /// <summary>
        /// The node's description in English.
        /// </summary>
        [TextArea(2, 6)] public string EnglishDescription;
    }
}
