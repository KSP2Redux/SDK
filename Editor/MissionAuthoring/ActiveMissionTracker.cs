namespace Ksp2UnityTools.Editor.MissionAuthoring
{
    /// <summary>
    /// Tracks the mission the user is currently editing.
    /// </summary>
    /// <remarks>
    /// The Mission Editor window sets it on Bind, and the Validation Report window reads it
    /// for the Use Active button and to auto-populate on open. Static state resets on domain
    /// reload, which is the right invalidation lifetime.
    /// </remarks>
    public static class ActiveMissionTracker
    {
        /// <summary>
        /// Gets or sets the mission currently being edited.
        /// </summary>
        public static Mission Current { get; set; }
    }
}
