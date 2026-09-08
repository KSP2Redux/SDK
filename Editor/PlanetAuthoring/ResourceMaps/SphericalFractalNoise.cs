using System;
using LibNoise;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.ResourceMaps
{
    /// <summary>
    /// Evaluates a <see cref="FractalNoiseChannel" /> over an equirectangular map by sampling
    /// three-dimensional noise on the surface of a sphere.
    /// </summary>
    /// <remarks>
    /// Sampling on a sphere rather than a plane buys two properties the output needs. A normalized
    /// longitude of 0 and of 1 resolve to the identical three-dimensional coordinate, so the map is
    /// exactly tileable across its left and right edges with no blending and no softened seam. And
    /// because every sample sits on a real sphere, features keep a uniform size from the equator to
    /// the poles instead of smearing into horizontal ribbons the way a cylindrical or planar domain
    /// makes them.
    ///
    /// An instance owns a LibNoise module and is not safe to share across threads. Build one per
    /// worker.
    /// </remarks>
    public sealed class SphericalFractalNoise
    {
        // Direction the Evolution parameter translates the sampling sphere along. Deliberately not
        // axis aligned, so successive whole values do not walk down a single axis of the gradient
        // lattice and produce visibly related fields.
        private static readonly double EVOLUTION_X = 0.5709;
        private static readonly double EVOLUTION_Y = 0.8131;
        private static readonly double EVOLUTION_Z = 0.1155;

        private readonly FractalNoiseChannel _channel;
        private readonly IModule _module;
        private readonly double _rangeMin;
        private readonly double _rangeSpan;
        private readonly double _longitudeOffset;
        private readonly double _tiltSin;
        private readonly double _tiltCos;

        /// <summary>
        /// Builds an evaluator for one channel's settings.
        /// </summary>
        /// <param name="channel">The channel to evaluate.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="channel" /> is null.</exception>
        public SphericalFractalNoise(FractalNoiseChannel channel)
        {
            if (channel == null)
                throw new ArgumentNullException(nameof(channel));

            _channel = channel;
            _module = BuildModule(channel);

            GetTheoreticalRange(channel, out double min, out double max);
            _rangeMin = min;
            // A degenerate range would divide by zero. It can only happen at zero octaves.
            _rangeSpan = max - min > 1e-9 ? max - min : 1.0;

            _longitudeOffset = channel.RotationLongitude * System.Math.PI / 180.0;
            double tilt = channel.RotationLatitude * System.Math.PI / 180.0;
            _tiltSin = System.Math.Sin(tilt);
            _tiltCos = System.Math.Cos(tilt);
        }

        /// <summary>
        /// Evaluates the channel at a normalized map coordinate.
        /// </summary>
        /// <param name="u">Normalized longitude, 0 at the antimeridian going east and 1 back at it.</param>
        /// <param name="v">Normalized latitude, 0 at the south pole and 1 at the north pole.</param>
        /// <returns>Density in the range 0 to 1, after the channel's transfer curve and opacity.</returns>
        public float Evaluate(double u, double v)
        {
            if (!_channel.Enabled)
                return 0f;

            // Matches the runtime's own convention in ISRUResourceManager: longitude runs from -180
            // at u = 0, latitude from -90 at v = 0.
            double longitude = (u - 0.5) * 2.0 * System.Math.PI + _longitudeOffset;
            double latitude = (v - 0.5) * System.Math.PI;

            double cosLatitude = System.Math.Cos(latitude);
            double x = cosLatitude * System.Math.Cos(longitude);
            double y = System.Math.Sin(latitude);
            double z = cosLatitude * System.Math.Sin(longitude);

            // Tilt about the z axis, which rotates the pattern relative to the poles. The seam
            // survives it because u = 0 and u = 1 already resolve to one point before the rotation.
            double tiltedX = x * _tiltCos - y * _tiltSin;
            double tiltedY = x * _tiltSin + y * _tiltCos;

            // Evolution translates the whole sampling sphere through the field. A translation keeps
            // feature size, where scaling the sphere's radius would change it.
            double evolution = _channel.Evolution;
            double raw = _module.GetValue(
                tiltedX + EVOLUTION_X * evolution,
                tiltedY + EVOLUTION_Y * evolution,
                z + EVOLUTION_Z * evolution
            );

            var normalized = (float)((raw - _rangeMin) / _rangeSpan);
            return ApplyTransfer(normalized, _channel);
        }

        /// <summary>
        /// Applies a channel's invert, contrast, brightness and opacity to a normalized noise value.
        /// </summary>
        /// <param name="normalized">Noise value mapped to the range 0 to 1.</param>
        /// <param name="channel">The channel whose transfer curve to apply.</param>
        /// <returns>Density in the range 0 to 1.</returns>
        public static float ApplyTransfer(float normalized, FractalNoiseChannel channel)
        {
            float value = Clamp01(normalized);
            if (channel.Invert)
            {
                value = 1f - value;
            }

            // Contrast pivots about mid grey so it opens and closes the field around its middle
            // rather than dragging it toward black.
            value = (value - 0.5f) * channel.Contrast + 0.5f;
            value += channel.Brightness;

            return Clamp01(value) * Clamp01(channel.Opacity);
        }

        /// <summary>
        /// Returns the range a basis can produce for the given settings, before normalization.
        /// </summary>
        /// <remarks>
        /// Derived from each LibNoise module's own summation rather than measured, so a channel's
        /// mid grey is the field's actual midpoint at every octave count and persistence.
        /// </remarks>
        /// <param name="channel">The channel whose basis and octave settings define the range.</param>
        /// <param name="min">Receives the lowest value the basis can return.</param>
        /// <param name="max">Receives the highest value the basis can return.</param>
        public static void GetTheoreticalRange(FractalNoiseChannel channel, out double min, out double max)
        {
            int octaves = ClampOctaves(channel.Octaves);
            switch (channel.Basis)
            {
                case NoiseBasis.Billow:
                {
                    // Billow sums signals in -1 to 1 with persistence falloff, then adds 0.5.
                    double amplitude = GeometricSum(channel.Persistence, octaves);
                    min = 0.5 - amplitude;
                    max = 0.5 + amplitude;
                    return;
                }
                case NoiseBasis.RidgedMultifractal:
                {
                    // Ridged sums non-negative signals weighted by lacunarity powers, then rescales
                    // by 1.25 and offsets by -1.
                    double weightSum = GeometricSum(1.0 / System.Math.Max(channel.Lacunarity, 1e-6), octaves);
                    min = -1.0;
                    max = weightSum * 1.25 - 1.0;
                    return;
                }
                default:
                {
                    double amplitude = GeometricSum(channel.Persistence, octaves);
                    min = -amplitude;
                    max = amplitude;
                    return;
                }
            }
        }

        /// <summary>
        /// Clamps an octave count to what LibNoise accepts.
        /// </summary>
        /// <param name="octaves">The requested octave count.</param>
        /// <returns>The octave count LibNoise will actually run.</returns>
        public static int ClampOctaves(int octaves) => System.Math.Clamp(octaves, 1, 30);

        private static IModule BuildModule(FractalNoiseChannel channel)
        {
            int octaves = ClampOctaves(channel.Octaves);
            IModule basis = channel.Basis switch
            {
                NoiseBasis.Billow => new Billow
                {
                    Frequency = channel.Frequency,
                    Lacunarity = channel.Lacunarity,
                    Persistence = channel.Persistence,
                    OctaveCount = octaves,
                    Seed = channel.Seed,
                    NoiseQuality = NoiseQuality.Standard,
                },
                // RidgedMultifractal has no Persistence. Its octave amplitudes come from spectral
                // weights derived from the lacunarity, which is why the window greys that slider out.
                NoiseBasis.RidgedMultifractal => new RidgedMultifractal
                {
                    Frequency = channel.Frequency,
                    Lacunarity = channel.Lacunarity,
                    OctaveCount = octaves,
                    Seed = channel.Seed,
                    NoiseQuality = NoiseQuality.Standard,
                },
                _ => new Perlin
                {
                    Frequency = channel.Frequency,
                    Lacunarity = channel.Lacunarity,
                    Persistence = channel.Persistence,
                    OctaveCount = octaves,
                    Seed = channel.Seed,
                    NoiseQuality = NoiseQuality.Standard,
                },
            };

            if (!channel.WarpEnabled)
                return basis;

            // Turbulence displaces the sampling coordinate by a deterministic function of that
            // coordinate, so identical inputs still produce identical outputs and the seam holds.
            return new Turbulence(basis)
            {
                Power = channel.WarpPower,
                Frequency = channel.WarpFrequency,
                Roughness = ClampOctaves(channel.WarpRoughness),
                Seed = channel.Seed + 7919,
            };
        }

        // Sum of ratio^0 through ratio^(count-1), which is the amplitude an octave sum reaches.
        private static double GeometricSum(double ratio, int count)
        {
            if (count <= 0)
                return 0.0;
            if (System.Math.Abs(ratio - 1.0) < 1e-9)
                return count;
            return (1.0 - System.Math.Pow(ratio, count)) / (1.0 - ratio);
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
