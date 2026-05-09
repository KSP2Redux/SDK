using KSP.Rendering.Planets;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Services
{
    public sealed class DefaultSurfaceHeightProvider : ISurfaceHeightProvider
    {
        public double GetSurfaceHeight(PQS pqs, Vector3d radialDirection, bool includeDecals)
        {
            if (pqs == null)
                return 0.0;

            return pqs.GetSurfaceHeight(radialDirection, includeDecals);
        }
    }
}
