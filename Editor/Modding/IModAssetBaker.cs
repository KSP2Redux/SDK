using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace Ksp2UnityTools.Editor.Modding
{
    /// <summary>
    /// Bakes one kind of authoring asset into the files a mod ships, such as JSON, Addressables entries and generator
    /// sidecars.
    /// </summary>
    /// <remarks>
    /// The mod build runs every implementation over each asset of its <see cref="AssetType" /> in the mod's folders,
    /// before anything is staged or any Addressables content is built. Implementations are found through
    /// <see cref="Reflection.ReduxTypeCache" /> and need a public parameterless constructor. A build bake must never
    /// show UI. It returns its problems, and any problem fails the build.
    /// </remarks>
    public interface IModAssetBaker
    {
        /// <summary>
        /// Gets the asset type this bakes, matched by name as <c>AssetDatabase.FindAssets</c> matches <c>t:</c>
        /// filters, so subclasses are baked too.
        /// </summary>
        Type AssetType { get; }

        /// <summary>
        /// Bakes one asset.
        /// </summary>
        /// <param name="asset">The asset, an instance of <see cref="AssetType" />.</param>
        /// <returns>One message per problem that stopped the bake, empty when the asset baked.</returns>
        List<string> Bake(Object asset);
    }
}
