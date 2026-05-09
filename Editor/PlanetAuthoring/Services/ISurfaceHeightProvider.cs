using KSP.Rendering.Planets;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Services
{
    public interface ISurfaceHeightProvider
    {
        double GetSurfaceHeight(PQS pqs, Vector3d radialDirection, bool includeDecals);
    }
}
