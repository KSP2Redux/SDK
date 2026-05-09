using System;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Services
{
    public interface IPhysicsInterestProvider
    {
        ReadOnlySpan<Vector3d> GetInterestPositions();
    }
}
