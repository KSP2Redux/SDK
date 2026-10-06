using KSP.Rendering;
using KSP.Rendering.Planets;
using KSP.Rendering.Utility;
using KSP.VolumeCloud;
using Ksp2UnityTools.Editor.PlanetAuthoring.Atmosphere;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Clouds
{
    /// <summary>
    /// The cloud environment of a planet preview: the session's sun, its PQS and the atmosphere the preview draws.
    /// </summary>
    /// <remarks>
    /// The authoring scene has no time warp and no floating origin, and its body has no ambient probe, so those read as
    /// empty. The water depth comes from the ocean preview when one is drawing.
    /// </remarks>
    public class CloudPreviewEnvironment : IVolumeCloudEnvironment
    {
        private readonly PQS _pqs;
        private readonly AtmospherePreviewDriver _atmosphere;
        private Vector3 _previousOrigin;

        /// <summary>
        /// Creates the environment of a preview.
        /// </summary>
        /// <param name="pqs">The previewed body's PQS.</param>
        /// <param name="atmosphere">The preview's atmosphere driver, whose model the clouds composite inside.</param>
        /// <param name="camera">The camera the clouds draw for.</param>
        public CloudPreviewEnvironment(PQS pqs, AtmospherePreviewDriver atmosphere, Camera camera)
        {
            _pqs = pqs;
            _atmosphere = atmosphere;
            Camera = camera;
            _previousOrigin = pqs.transform.position;
        }

        /// <summary>
        /// Gets or sets the camera the clouds draw for.
        /// </summary>
        public Camera Camera { get; set; }

        /// <inheritdoc />
        public float TimeMultiplier => 1f;

        /// <inheritdoc />
        public double WindTime => Time.time;

        /// <inheritdoc />
        public bool CanCastShadows => true;

        /// <inheritdoc />
        public RenderTexture OceanDepthTexture => Ocean.OceanPreviewDriver.ActiveWaterDepth;

        /// <inheritdoc />
        public Light GetLocalLight() => SunCoupling.CurrentSun;

        /// <inheritdoc />
        public Light GetScaledLight() => SunCoupling.CurrentSun;

        /// <inheritdoc />
        public PQS FindPqs(CloudRenderHelper helper) => _pqs;

        /// <inheritdoc />
        public void BindAtmosphere(Material material)
        {
            AtmosphereModel model = _atmosphere != null && _atmosphere.Enabled ? _atmosphere.Model : null;
            if (model == null)
            {
                material.SetFloat("atmosphere_active", 0f);
                return;
            }

            Light sun = SunCoupling.CurrentSun;
            AtmosphereScatterManager.BindLocalAtmosphere(
                material,
                model,
                Camera,
                _pqs.transform.position * AtmosphereConstants.LENGTH_UNIT_IN_KILOMETERS,
                sun != null ? -sun.transform.forward : Vector3.up,
                null
            );
        }

        /// <inheritdoc />
        public SphericalHarmonicsL2 GetGIProbeSHTerm(string bodyName) => default;

        /// <inheritdoc />
        public Vector3 GetFloatingOriginChange() => _pqs.transform.position - _previousOrigin;

        /// <inheritdoc />
        public void EndFrame()
        {
            _previousOrigin = _pqs.transform.position;
        }

        /// <inheritdoc />
        public void Release()
        {
        }
    }
}
