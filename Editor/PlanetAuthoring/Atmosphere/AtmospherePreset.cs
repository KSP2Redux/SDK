using System.Collections.Generic;
using KSP.Rendering;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Atmosphere
{
    /// <summary>
    /// Stock KSP2 atmosphere settings a new atmosphere can start from.
    /// </summary>
    /// <remarks>
    /// Values are the shipped <c>&lt;body&gt;_atmosphere_model</c> assets read from the game's
    /// <c>celestialbody-shared-&lt;body&gt;</c> bundles. Applying one copies every field except the
    /// bottom radius, which belongs to the body, and the sun zenith cutoff, which takes
    /// <see cref="AtmosphereSetup.SUN_ZENITH_ANGLE" />. Heights are stock's absolute kilometers, so on a body
    /// much smaller or larger than the preset's the visual height and scale heights want rescaling.
    /// </remarks>
    public class AtmospherePreset
    {
        /// <summary>Gets the display name.</summary>
        public string Name { get; set; }

        /// <summary>Gets a value indicating whether the preset is a gas giant's.</summary>
        public bool IsGasGiant { get; set; }

        /// <summary>Gets the exposure pair.</summary>
        public Vector2 Exposure { get; set; }

        /// <summary>Gets the sun's angular radius.</summary>
        public float SunAngleRadius { get; set; }

        /// <summary>Gets the solar irradiance color.</summary>
        public Vector3 SolarIrradiance { get; set; }

        /// <summary>Gets the sun direction exposure modifier.</summary>
        public float SunDirectionExposureModifier { get; set; }

        /// <summary>Gets the transmittance tint.</summary>
        public float TransmittanceTint { get; set; }

        /// <summary>Gets the noon color strength.</summary>
        public float NoonColorStrength { get; set; }

        /// <summary>Gets the sunset color strength.</summary>
        public float SunsetColorStrength { get; set; }

        /// <summary>Gets the color transition scale.</summary>
        public float ColorTransitionScale { get; set; }

        /// <summary>Gets the preset body's bottom radius in kilometers, for reference only.</summary>
        public float BottomRadius { get; set; }

        /// <summary>Gets the visual atmosphere height in kilometers.</summary>
        public float AtmosphereHeight { get; set; }

        /// <summary>Gets the ground albedo.</summary>
        public Color GroundAlbedo { get; set; }

        /// <summary>Gets the Rayleigh scattering color.</summary>
        public Vector3 RayleighScattering { get; set; }

        /// <summary>Gets the Rayleigh scattering scale.</summary>
        public float RayleighScatteringScale { get; set; }

        /// <summary>Gets the Rayleigh scale height in kilometers.</summary>
        public float RayleighExponentialDistribution { get; set; }

        /// <summary>Gets the Mie scattering color.</summary>
        public Vector3 MieScattering { get; set; }

        /// <summary>Gets the Mie scattering scale.</summary>
        public float MieScatteringScale { get; set; }

        /// <summary>Gets the Mie phase anisotropy.</summary>
        public float MieAnisotropy { get; set; }

        /// <summary>Gets the Mie scale height in kilometers.</summary>
        public float MieExponentialDistribution { get; set; }

        /// <summary>Gets the absorption scale.</summary>
        public float AbsorptionScale { get; set; }

        /// <summary>Gets the absorption color.</summary>
        public Vector3 Absorption { get; set; }

        /// <summary>Gets the absorption band's peak density.</summary>
        public float AbsorptionMaxDensity { get; set; }

        /// <summary>Gets the absorption band's bottom and top heights in kilometers.</summary>
        public Vector2 AbsorptionHeightMinMax { get; set; }

        /// <summary>
        /// Copies the preset into <paramref name="model" />, leaving its bottom radius and tables alone.
        /// </summary>
        /// <param name="model">The model to overwrite.</param>
        public void ApplyTo(AtmosphereModel model)
        {
            model.IsGasGiant = IsGasGiant;
            model.Exposure = Exposure;
            model.SunAngleRadius = SunAngleRadius;
            model.SunZenithAngle = AtmosphereSetup.SUN_ZENITH_ANGLE;
            model.SolarIrradiance = SolarIrradiance;
            model.SunDirectionExposureModifier = SunDirectionExposureModifier;
            model.TransmittanceTint = TransmittanceTint;
            model.NoonColorStrengh = NoonColorStrength;
            model.SunsetColorStrengh = SunsetColorStrength;
            model.ColorTransitionScale = ColorTransitionScale;
            model.AtmosphereHeight = AtmosphereHeight;
            model.GroundAlbedo = GroundAlbedo;
            model.RayleighScattering = RayleighScattering;
            model.RayleighScatteringScale = RayleighScatteringScale;
            model.RayleighExponentialDistribution = RayleighExponentialDistribution;
            model.MieScattering = MieScattering;
            model.MieScatteringScale = MieScatteringScale;
            model.MieAnisotropy = MieAnisotropy;
            model.MieExponentialDistribution = MieExponentialDistribution;
            model.AbsorptionScale = AbsorptionScale;
            model.Absorption = Absorption;
            model.AbsorptionMaxDensity = AbsorptionMaxDensity;
            model.AbsorptionHeightMinMax = AbsorptionHeightMinMax;
        }

        /// <summary>
        /// Gets every stock preset, in the order a picker lists them.
        /// </summary>
        public static IReadOnlyList<AtmospherePreset> Stock { get; } = new[]
        {
            new AtmospherePreset
            {
                Name = "Kerbin",
                Exposure = new Vector2(41.21339f, 50f),
                SunAngleRadius = 0.04675f,
                SolarIrradiance = new Vector3(1f, 1f, 1f),
                SunDirectionExposureModifier = 0.29f,
                TransmittanceTint = 1f,
                SunsetColorStrength = 1f,
                ColorTransitionScale = 1f,
                BottomRadius = 600f,
                AtmosphereHeight = 60f,
                GroundAlbedo = Color.black,
                RayleighScattering = new Vector3(0.17529719f, 0.40959996f, 1f),
                RayleighScatteringScale = 1f,
                RayleighExponentialDistribution = 3f,
                MieScattering = new Vector3(1f, 1f, 1f),
                MieScatteringScale = 1f,
                MieAnisotropy = 0.113f,
                MieExponentialDistribution = 0.5f,
                AbsorptionScale = 0.113f,
                Absorption = new Vector3(1f, 0.51257068f, 0.04705882f),
                AbsorptionMaxDensity = 1f,
                AbsorptionHeightMinMax = new Vector2(10f, 66.37167f),
            },
            new AtmospherePreset
            {
                Name = "Duna",
                Exposure = new Vector2(40.396f, 50f),
                SunAngleRadius = 0.203f,
                SolarIrradiance = new Vector3(0.90588236f, 0.78402305f, 0.74554116f),
                TransmittanceTint = 1f,
                SunsetColorStrength = 1f,
                ColorTransitionScale = 1f,
                BottomRadius = 320f,
                AtmosphereHeight = 22f,
                GroundAlbedo = Color.black,
                RayleighScattering = new Vector3(0.67058825f, 0.29803923f, 0.1372549f),
                RayleighScatteringScale = 2.5f,
                RayleighExponentialDistribution = 2f,
                MieScattering = new Vector3(0.65882355f, 0.3882353f, 0.2901961f),
                MieScatteringScale = 0.0053f,
                MieAnisotropy = 0.825f,
                MieExponentialDistribution = 4.45f,
                AbsorptionScale = 0.0321f,
                Absorption = new Vector3(0f, 0.29045916f, 1f),
                AbsorptionMaxDensity = 1f,
                AbsorptionHeightMinMax = new Vector2(0f, 19.696945f),
            },
            new AtmospherePreset
            {
                Name = "Laythe",
                Exposure = new Vector2(9f, 10f),
                SunAngleRadius = 0.04675f,
                SolarIrradiance = new Vector3(0.75f, 0.75f, 0.75f),
                SunDirectionExposureModifier = 0.7f,
                TransmittanceTint = 1f,
                SunsetColorStrength = 1f,
                ColorTransitionScale = 1f,
                BottomRadius = 500f,
                AtmosphereHeight = 30f,
                GroundAlbedo = Color.black,
                RayleighScattering = new Vector3(0.3254717f, 0.6047155f, 1f),
                RayleighScatteringScale = 1.5f,
                RayleighExponentialDistribution = 2.8f,
                MieScattering = new Vector3(1f, 1f, 1f),
                MieScatteringScale = 20f,
                MieAnisotropy = 0.8f,
                MieExponentialDistribution = 1.2f,
                AbsorptionScale = 0.248f,
                Absorption = new Vector3(0.9620261f, 1f, 0.04705882f),
                AbsorptionMaxDensity = 1f,
                AbsorptionHeightMinMax = new Vector2(10f, 40f),
            },
            new AtmospherePreset
            {
                Name = "Eve",
                Exposure = new Vector2(30f, 30f),
                SunAngleRadius = 0.148f,
                SolarIrradiance = new Vector3(0.6117647f, 0.48235294f, 1f),
                TransmittanceTint = 0.7f,
                SunsetColorStrength = 1f,
                ColorTransitionScale = 1f,
                BottomRadius = 700f,
                AtmosphereHeight = 82f,
                GroundAlbedo = Color.black,
                RayleighScattering = new Vector3(0.31106594f, 0.22000003f, 1f),
                RayleighScatteringScale = 1.38f,
                RayleighExponentialDistribution = 7.4f,
                MieScattering = new Vector3(0.46978843f, 0.45069805f, 0.6901961f),
                MieScatteringScale = 5.1f,
                MieAnisotropy = 0f,
                MieExponentialDistribution = 1.03f,
                AbsorptionScale = 0.5f,
                Absorption = new Vector3(0.3529412f, 0.67058825f, 0.15686275f),
                AbsorptionMaxDensity = 2f,
                AbsorptionHeightMinMax = new Vector2(0f, 80.00002f),
            },
            new AtmospherePreset
            {
                Name = "Jool",
                IsGasGiant = true,
                Exposure = new Vector2(50f, 50f),
                SunAngleRadius = 0f,
                SolarIrradiance = new Vector3(0.8852837f, 1f, 0.56078434f),
                TransmittanceTint = 1f,
                SunsetColorStrength = 1f,
                ColorTransitionScale = 1f,
                BottomRadius = 6000f,
                AtmosphereHeight = 200f,
                GroundAlbedo = new Color(0.23507047f, 0.28235295f, 0.0627451f, 1f),
                RayleighScattering = new Vector3(0.9450981f, 0.9921569f, 0.50980395f),
                RayleighScatteringScale = 0.37f,
                RayleighExponentialDistribution = 7.1f,
                MieScattering = new Vector3(1f, 1f, 1f),
                MieScatteringScale = 0.082f,
                MieAnisotropy = 1f,
                MieExponentialDistribution = 17.5f,
                AbsorptionScale = 0.482f,
                Absorption = new Vector3(0.48235297f, 0.2784314f, 0.9333334f),
                AbsorptionMaxDensity = 1f,
                AbsorptionHeightMinMax = new Vector2(10f, 40f),
            },
        };
    }
}
