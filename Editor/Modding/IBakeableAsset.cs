using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.Modding
{
    /// <summary>
    /// An authoring asset that bakes into the files a mod ships, such as JSON, Addressables entries and generator
    /// sidecars.
    /// </summary>
    /// <remarks>
    /// The mod build bakes every asset in the mod's folders that implements this and should bake, before anything is
    /// staged or any Addressables content is built, as it generates every <see cref="TextAssetGenerator" />. A build
    /// bake must never show UI. It returns its problems, and any problem fails the build.
    /// </remarks>
    public interface IBakeableAsset
    {
        /// <summary>
        /// Gets whether the mod build bakes the asset. An asset whose output the author owns by hand does not.
        /// </summary>
        bool ShouldBake { get; }

        /// <summary>
        /// Bakes the asset.
        /// </summary>
        /// <returns>One message per problem that stopped the bake, empty when the asset baked.</returns>
        List<string> Bake();
    }
}
