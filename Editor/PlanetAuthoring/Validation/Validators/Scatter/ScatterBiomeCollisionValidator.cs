using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using Ksp2UnityTools.Editor.PlanetAuthoring.Scatter;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Warns when two mask channels are mapped to the same biome.
    /// </summary>
    /// <remarks>
    /// <c>GetBiomeTextureMaskChannelIndex</c> tests R, G, B then A and returns on the first match, so
    /// a biome mapped to two channels only ever resolves to the earlier one. The later channel is
    /// never reachable, and nothing about the data says so.
    /// </remarks>
    public sealed class ScatterBiomeCollisionValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "SCATTER_BIOME_COLLISION";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            VegetationSystemPro system = ScatterValidatorHelper.FindSystem(body);
            if (system == null)
                yield break;

            if (!ScatterBiomeChannels.MaskingActive(system))
                yield break;

            var channelFor = new Dictionary<BiomeType, string>();
            for (var channel = 0; channel < PlanetAuthoringNaming.BiomeChannels.Length; channel++)
            {
                BiomeType biome = ScatterBiomeChannels.ChannelBiome(system, channel);

                // Default is the enum's zero value, so it is what an unmapped channel reads as.
                // Several channels sharing it means several are unassigned, not that they collide.
                if (biome == BiomeType.Default)
                    continue;

                string name = PlanetAuthoringNaming.BiomeChannels[channel];
                if (channelFor.TryGetValue(biome, out string first))
                {
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Warning,
                        $"Mask channels {first} and {name} both map to biome {biome}, so channel {name} "
                        + "can never be the one that matches.");
                    continue;
                }

                channelFor[biome] = name;
            }
        }
    }
}
