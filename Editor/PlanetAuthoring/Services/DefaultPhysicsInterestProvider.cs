using System;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Services
{
    public sealed class DefaultPhysicsInterestProvider : IPhysicsInterestProvider
    {
        private static readonly Vector3d[] Empty = Array.Empty<Vector3d>();

        public ReadOnlySpan<Vector3d> GetInterestPositions() => Empty;
    }
}
