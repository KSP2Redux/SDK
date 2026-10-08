using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring
{
    /// <summary>
    /// Routes the SceneView to a predictable view (lat 0, lon 0, one body radius above the surface, Side mode) when a planet preview session starts so the artist does not land on whatever the SceneView was last aimed at.
    /// </summary>
    /// <remarks>
    /// Re-frames each time a session transitions from inactive to active. Subsequent jumps via
    /// Preview Controls override this. Camera math now lives in <see cref="SceneViewFraming" />,
    /// this just kicks it off.
    /// </remarks>
    [InitializeOnLoad]
    internal static class SessionInitialFraming
    {
        private static bool _previousActive;

        static SessionInitialFraming()
        {
            PlanetPreviewState.ActiveChanged += OnSessionChanged;
        }

        private static void OnSessionChanged()
        {
            bool active = PlanetAuthoringSession.Active != null;
            if (active && !_previousActive)
                FrameInitialView();
            _previousActive = active;
        }

        private static void FrameInitialView()
        {
            var session = PlanetAuthoringSession.Active;
            if (session?.Pqs == null) return;
            var radius = session.Pqs.CoreCelestialBodyData?.Data?.radius ?? 0;
            if (radius <= 0) return;
            // Resume where the artist last framed this PQS. Falls through to (0, 0) when there is
            // no prior framing record (fresh session, post-domain-reload, first time opening this PQS).
            SceneViewFraming.TryGetLastLatLon(session.Pqs, out var lat, out var lon);
            SceneViewFraming.FrameAtLatLonAndAltitude(session.Pqs, lat, lon, radius, SceneFramingMode.Side);
        }
    }
}
