using AwesomeTechnologies.VegetationSystem;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Scatter
{
    /// <summary>
    /// Maps a scatter system's biomes to the mask channels that gate them.
    /// </summary>
    /// <remarks>
    /// Four channels against twenty-nine biome values, so most biomes are unmapped at any time and
    /// the mapping is the only thing that says which ground a package claims. Consumed by the
    /// validators, the package inspector's hint and the overlay legend, which is why it sits here
    /// rather than inside any one of them.
    /// </remarks>
    public static class ScatterBiomeChannels
    {
        /// <summary>
        /// Reports whether the whole-planet biome mask is actually gating anything.
        /// </summary>
        /// <remarks>
        /// With masking off or no mask assigned, every package spawns wherever its item rules allow
        /// and the channel mapping is inert. Anything reasoning about channels has to stay quiet in
        /// that state or it describes a system that is not running.
        /// </remarks>
        /// <param name="system">The body's scatter system. May be null.</param>
        /// <returns>True if the mask is on and a texture is assigned, false otherwise.</returns>
        public static bool MaskingActive(VegetationSystemPro system) =>
            system != null && system.UseBiomeTextureMask && system.BiomeTextureMask != null;

        /// <summary>
        /// Returns the biome a mask channel is mapped to.
        /// </summary>
        /// <param name="system">The body's scatter system.</param>
        /// <param name="channel">Channel index in the canonical R/G/B/A order.</param>
        /// <returns>The mapped biome.</returns>
        public static BiomeType ChannelBiome(VegetationSystemPro system, int channel) => channel switch
        {
            0 => system.BiomeTextureMaskRChannelType,
            1 => system.BiomeTextureMaskGChannelType,
            2 => system.BiomeTextureMaskBChannelType,
            _ => system.BiomeTextureMaskAChannelType,
        };

        /// <summary>
        /// Finds the mask channel a biome is mapped to.
        /// </summary>
        /// <remarks>
        /// An unmapped biome is legitimate rather than broken, since texture-rule gating alone is a
        /// documented way to place a package. Callers report it as a statement of what will happen.
        /// </remarks>
        /// <param name="system">The body's scatter system. May be null.</param>
        /// <param name="biome">The biome to look for.</param>
        /// <param name="channel">Channel index on success.</param>
        /// <returns>True if some channel maps to this biome, false otherwise.</returns>
        public static bool TryGetMaskChannel(VegetationSystemPro system, BiomeType biome, out int channel)
        {
            for (channel = 0; system != null && channel < PlanetAuthoringNaming.BiomeChannels.Length; channel++)
            {
                if (ChannelBiome(system, channel) == biome)
                    return true;
            }

            channel = -1;
            return false;
        }
    }
}
