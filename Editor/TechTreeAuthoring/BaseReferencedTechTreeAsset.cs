using UnityEngine;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// A tech tree asset authored against a tree it does not own, such as a single node placed in another tree or a
    /// patch that changes one.
    /// </summary>
    public abstract class BaseReferencedTechTreeAsset : TechTreeAsset
    {
        /// <summary>
        /// Where the tree the asset is authored against comes from.
        /// </summary>
        public TechNodeReferenceSource ReferenceSource;

        /// <summary>
        /// The tech tree asset, or the folder of tech node JSON, the asset is authored against when
        /// <see cref="ReferenceSource" /> is <see cref="TechNodeReferenceSource.Asset" />.
        /// </summary>
        public Object Reference;
    }

    /// <summary>
    /// Where the tree a <see cref="BaseReferencedTechTreeAsset" /> is authored against comes from.
    /// </summary>
    public enum TechNodeReferenceSource
    {
        /// <summary>
        /// Every tech node the project's own Addressables and the loaded catalogs hold, Redux's in place of the stock ones.
        /// </summary>
        GameNodes,

        /// <summary>
        /// A tech tree asset or a folder of tech node JSON.
        /// </summary>
        Asset,
    }
}
