using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring
{
    /// <summary>
    /// A system the planet preview runs alongside the PQS terrain, such as scatter or the atmosphere.
    /// </summary>
    /// <remarks>
    /// <see cref="PlanetAuthoringSession" /> attaches drivers in order after the PQS boots, pumps them
    /// in that order from its camera hook after the terrain draw, and detaches them in reverse before
    /// the PQS comes down. A driver is additive: a body it has nothing to do for is a success, and
    /// one that keeps failing switches itself off rather than taking the session down.
    /// </remarks>
    public interface IPreviewDriver
    {
        /// <summary>
        /// Gets or sets a value indicating whether the driver is pumped.
        /// </summary>
        bool Enabled { get; set; }

        /// <summary>
        /// Gets a value indicating whether the driver has booted something it is now driving.
        /// </summary>
        bool Booted { get; }

        /// <summary>
        /// Gets a short account of anything the author can act on, or an empty string when the driver is running normally.
        /// </summary>
        string Status { get; }

        /// <summary>
        /// Boots whatever the driver runs for the previewed body.
        /// </summary>
        /// <returns>True if it booted cleanly or the body has nothing for it to run, false otherwise.</returns>
        bool Attach();

        /// <summary>
        /// Tears down whatever <see cref="Attach" /> booted. Safe to call more than once.
        /// </summary>
        void Detach();

        /// <summary>
        /// Advances the driver for the frame <paramref name="camera" /> is about to render.
        /// </summary>
        /// <remarks>
        /// Called from the session's render hook, because draws submitted from an editor tick land
        /// where nothing consumes them.
        /// </remarks>
        /// <param name="camera">The camera about to cull.</param>
        void Pump(Camera camera);
    }
}
