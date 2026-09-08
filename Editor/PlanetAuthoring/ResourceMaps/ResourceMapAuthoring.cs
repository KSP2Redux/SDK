using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.ResourceMaps
{
    /// <summary>
    /// Which LibNoise fractal module generates a channel's field.
    /// </summary>
    public enum NoiseBasis
    {
        /// <summary>Summed gradient noise. The general purpose basis.</summary>
        Perlin,
        /// <summary>Summed absolute gradient noise, which produces rounded clumps.</summary>
        Billow,
        /// <summary>Ridged multifractal, which produces sharp crests and veins.</summary>
        RidgedMultifractal,
    }

    /// <summary>
    /// Fractal noise settings for one biome channel of one resource.
    /// </summary>
    /// <remarks>
    /// The field is evaluated on the surface of a unit sphere rather than on a plane, so it is
    /// exactly periodic across the left and right edges of the equirectangular output and keeps a
    /// uniform feature size from the equator to the poles. See
    /// <see cref="SphericalFractalNoise" /> for the evaluation.
    /// </remarks>
    [Serializable]
    public class FractalNoiseChannel
    {
        /// <summary>Whether this channel contributes any density. A disabled channel reads as empty rather than being ignored.</summary>
        /// <remarks>
        /// Off by default, so a resource starts out present nowhere and the artist opts each biome
        /// in. A resource occurring in every biome of a body is the exception rather than the rule.
        /// </remarks>
        public bool Enabled;

        /// <summary>Artist-facing name for the biome this channel covers. Empty falls back to the channel letter.</summary>
        public string Label = "";

        /// <summary>Final multiplier on the channel's density, from 0 for none to 1 for full.</summary>
        public float Opacity = 1f;

        /// <summary>Which fractal module generates the field.</summary>
        public NoiseBasis Basis = NoiseBasis.Perlin;

        /// <summary>Frequency of the first octave. Higher values give smaller features.</summary>
        public float Frequency = 4f;

        /// <summary>Number of octaves summed.</summary>
        public int Octaves = 6;

        /// <summary>Frequency multiplier between successive octaves.</summary>
        public float Lacunarity = 2f;

        /// <summary>Amplitude multiplier between successive octaves. Inert for <see cref="NoiseBasis.RidgedMultifractal" />, which uses its own spectral weights.</summary>
        public float Persistence = 0.5f;

        /// <summary>Seed for the gradient tables. Changing it produces an unrelated field.</summary>
        public int Seed;

        /// <summary>Translates the sampling sphere through the noise field, varying the pattern continuously without changing feature size.</summary>
        public float Evolution;

        /// <summary>Rotation about the polar axis, in degrees. A pure phase shift, so it moves the pattern east or west without distorting it.</summary>
        public float RotationLongitude;

        /// <summary>Tilt of the pattern relative to the poles, in degrees.</summary>
        public float RotationLatitude;

        /// <summary>Whether the sampling coordinate is displaced by a turbulence pass before the basis is evaluated.</summary>
        public bool WarpEnabled;

        /// <summary>How far the turbulence pass displaces the sampling coordinate.</summary>
        public float WarpPower = 0.25f;

        /// <summary>Frequency of the turbulence pass's own noise.</summary>
        public float WarpFrequency = 2f;

        /// <summary>Octave count of the turbulence pass's own noise.</summary>
        public int WarpRoughness = 3;

        /// <summary>Gain applied about mid grey. 1 leaves the field unchanged.</summary>
        public float Contrast = 1f;

        /// <summary>Offset added after contrast. 0 leaves the field unchanged.</summary>
        public float Brightness;

        /// <summary>Whether the field is flipped before contrast and brightness are applied.</summary>
        public bool Invert;

        /// <summary>
        /// Returns an independent copy of this channel.
        /// </summary>
        /// <remarks>
        /// The previews render on a worker thread while the artist keeps dragging sliders, so a
        /// render works from a snapshot rather than from the live serialized object.
        /// </remarks>
        /// <returns>A copy sharing no state with this instance.</returns>
        public FractalNoiseChannel Clone() => (FractalNoiseChannel)MemberwiseClone();
    }

    /// <summary>
    /// One resource's map settings for a celestial body: four biome channels plus the output metadata.
    /// </summary>
    [Serializable]
    public class ResourceMapEntry
    {
        /// <summary>Resource this map is for. Matches a file name in the project's ResourceDefinitions folder.</summary>
        public string ResourceName = "";

        /// <summary>Overlay brightness written into the generated definition. Display only, with no effect on mining yield.</summary>
        /// <remarks>
        /// Whole numbers only. The definition field it is written to is a float, but every shipped
        /// map uses an integer and the overlay gains nothing from finer steps.
        /// </remarks>
        public int MapBrightness = 1;

        /// <summary>Whether <see cref="MapBrightness" /> is recomputed from the baked map on every generate.</summary>
        public bool AutoMapBrightness = true;

        /// <summary>Whether generating skips the confirmation prompt when the output files already exist.</summary>
        public bool OverwriteExisting;

        /// <summary>Noise settings for the mask's red, green, blue and alpha channels, in that order.</summary>
        public FractalNoiseChannel[] Channels = CreateDefaultChannels();


        /// <summary>
        /// Returns an independent copy of this entry, for rendering off the main thread.
        /// </summary>
        /// <returns>A copy whose channels share no state with this instance.</returns>
        public ResourceMapEntry Clone()
        {
            FractalNoiseChannel[] source = Channels ?? CreateDefaultChannels();
            var channels = new FractalNoiseChannel[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                channels[index] = source[index]?.Clone() ?? new FractalNoiseChannel();
            }

            return new ResourceMapEntry
            {
                ResourceName = ResourceName,
                MapBrightness = MapBrightness,
                AutoMapBrightness = AutoMapBrightness,
                OverwriteExisting = OverwriteExisting,
                Channels = channels,
            };
        }

        /// <summary>
        /// Builds the four default channels, each with a distinct seed.
        /// </summary>
        /// <returns>Four channels seeded so a new resource does not start as four identical fields.</returns>
        public static FractalNoiseChannel[] CreateDefaultChannels()
        {
            var channels = new FractalNoiseChannel[ResourceMapAuthoring.CHANNEL_COUNT];
            for (var channel = 0; channel < channels.Length; channel++)
            {
                // Distinct seeds per channel, otherwise every biome starts out carrying the same
                // field and the four tabs look broken rather than merely untuned.
                channels[channel] = new FractalNoiseChannel { Seed = 1000 * (channel + 1) };
            }
            return channels;
        }
    }

    /// <summary>
    /// Editor-only authoring data for one celestial body's resource density maps.
    /// </summary>
    /// <remarks>
    /// One asset per body, which matches the grain of the biome masks and of the generated
    /// <c>{Body}_{Resource}.png</c> outputs. The biome mask itself is not stored here: only its file
    /// name within the mask folder configured on the Resource Maps window, since the masks are
    /// ripped stock art that stays out of version control.
    /// </remarks>
    public class ResourceMapAuthoring : ScriptableObject
    {
        /// <summary>
        /// Number of biome channels in a mask.
        /// </summary>
        public const int CHANNEL_COUNT = 4;

        /// <summary>
        /// Names of the four mask channels, used when a channel has no artist label.
        /// </summary>
        public static readonly string[] CHANNEL_NAMES = { "Red", "Green", "Blue", "Alpha" };

        /// <summary>Body these maps belong to. Must match the runtime body name exactly.</summary>
        public string CelestialBodyName = "";

        /// <summary>File name of the biome mask within the configured mask folder.</summary>
        public string BiomeMaskFileName = "";

        /// <summary>Resources authored for this body.</summary>
        public List<ResourceMapEntry> Resources = new();
    }
}
