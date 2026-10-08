using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring
{
    /// <summary>
    /// Tracks the directional Light treated as "the sun" by Preview Controls and rotates it in
    /// lock-step with the body during framing jumps.
    /// </summary>
    /// <remarks>
    /// The radical framing rework rotates the body instead of moving the camera. To keep the
    /// artist's authored sun-on-body relationship intact, the sun rotates by the same delta. This
    /// class owns the baseline rotation so the session can restore it on End without needing to
    /// snapshot mid-session changes itself. Preview Controls publishes the current Light, and framing
    /// helpers read and rotate through this single seam.
    /// </remarks>
    public static class SunCoupling
    {
        private static Light _currentSun;
        private static Quaternion _baselineRotation = Quaternion.identity;

        /// <summary>
        /// Gets or sets the directional Light treated as the sun for this session.
        /// </summary>
        /// <remarks>
        /// Setting a new Light captures its current rotation as the baseline so <see cref="ResetToBaseline" /> can restore it on session end.
        /// </remarks>
        public static Light CurrentSun
        {
            get => _currentSun;
            set
            {
                if (_currentSun == value) return;
                _currentSun = value;
                _baselineRotation = value != null ? value.transform.rotation : Quaternion.identity;
            }
        }

        /// <summary>
        /// Rotates the tracked Light by <paramref name="delta" /> on top of its current rotation.
        /// </summary>
        /// <param name="delta">The rotation delta to apply, in world space.</param>
        public static void ApplyRotationDelta(Quaternion delta)
        {
            if (_currentSun == null) return;
            _currentSun.transform.rotation = delta * _currentSun.transform.rotation;
        }

        /// <summary>
        /// Restores the tracked Light to the rotation it had when first registered with this class.
        /// </summary>
        /// <remarks>
        /// Called by PlanetAuthoringSession.End so the scene's sun returns to its authored state.
        /// </remarks>
        public static void ResetToBaseline()
        {
            if (_currentSun == null) return;
            _currentSun.transform.rotation = _baselineRotation;
        }
    }
}
