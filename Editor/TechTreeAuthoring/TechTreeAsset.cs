using System;
using System.Collections.Generic;
using KSP.Game.Science;
using Redux.Packs;
using UnityEngine;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// A whole tech tree layer as one authoring asset: its tiers and every node in it.
    /// </summary>
    /// <remarks>
    /// The asset is the source of truth. The node JSON the game loads, the tree data and the localization rows are
    /// all baked from it.
    /// </remarks>
    public class TechTreeAsset : ScriptableObject
    {
        /// <summary>
        /// The tech tree layer every node is baked into.
        /// </summary>
        public string Layer = CampaignPack.DEFAULT_LAYER;

        /// <summary>
        /// The tiers, the first tier first. A tier without an English name is shown in game by its number.
        /// </summary>
        public List<TechTreeTier> Tiers = new();

        /// <summary>
        /// The nodes in the tree.
        /// </summary>
        public List<TechTreeNode> Nodes = new();
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
