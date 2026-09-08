using AwesomeTechnologies.VegetationSystem;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ksp2UnityTools.Editor.PlanetAuthoring
{
    /// <summary>
    /// Per-session readout of what the scatter preview is currently doing.
    /// </summary>
    /// <remarks>
    /// Owned by <see cref="PlanetAuthoringSession" />, beside <see cref="PlanetPreviewState" />.
    ///
    /// Split deliberately into two tiers. <see cref="Update" /> is cheap enough to run every tick and
    /// reads only CPU side state. <see cref="ReadRenderedInstanceCount" /> stalls the graphics
    /// pipeline and is on demand only.
    /// </remarks>
    public sealed class ScatterPreviewState
    {
        /// <summary>
        /// Gets the active session's scatter state, or <c>null</c> when no session is running.
        /// </summary>
        public static ScatterPreviewState Active => PlanetAuthoringSession.Active?.ScatterState;

        private readonly ScatterPreviewDriver _driver;

        /// <summary>
        /// Creates a scatter state reading from <paramref name="driver" />.
        /// </summary>
        /// <param name="driver">The driver whose system is reported on.</param>
        public ScatterPreviewState(ScatterPreviewDriver driver)
        {
            _driver = driver;
        }

        /// <summary>
        /// Gets a value indicating whether the body has a scatter system at all.
        /// </summary>
        public bool HasSystem { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the driver has booted that system.
        /// </summary>
        public bool Booted { get; private set; }

        /// <summary>
        /// Gets the number of vegetation cells the system has built.
        /// </summary>
        public int CellCount { get; private set; }

        /// <summary>
        /// Gets the number of cells currently loaded.
        /// </summary>
        public int LoadedCellCount { get; private set; }

        /// <summary>
        /// Gets the spawn and render distance, in metres, for ordinary scatter items.
        /// </summary>
        /// <remarks>
        /// Worth showing next to any instance count. Nothing spawns or renders beyond this, so a
        /// count of zero from a camera further out than this reports the camera's position rather
        /// than anything about the body's scatter.
        /// </remarks>
        public float VegetationDistance { get; private set; }

        /// <summary>
        /// Gets the spawn and render distance, in metres, for items typed as trees.
        /// </summary>
        public float TreeDistance { get; private set; }

        /// <summary>
        /// Gets the spawn and render distance, in metres, for hero items.
        /// </summary>
        public float HeroDistance { get; private set; }

        /// <summary>
        /// Resamples the cheap CPU side scatter state.
        /// </summary>
        /// <returns>True if any reported value changed, false otherwise.</returns>
        public bool Update()
        {
            bool hasSystem = _driver?.System != null;
            bool booted = _driver != null && _driver.Booted;
            int cells = 0;
            int loaded = 0;
            float vegetation = 0f;
            float tree = 0f;
            float hero = 0f;

            if (hasSystem)
            {
                VegetationSystemPro system = _driver.System;
                cells = system.VegetationCellList.Count;
                loaded = system.LoadedVegetationCellList.Count;

                VegetationSettings settings = system.VegetationSettings;
                if (settings != null)
                {
                    vegetation = settings.GetVegetationDistance();
                    tree = settings.GetTreeDistance();
                    hero = settings.GetHeroDistance();
                }
            }

            bool changed =
                hasSystem != HasSystem ||
                booted != Booted ||
                cells != CellCount ||
                loaded != LoadedCellCount ||
                !Mathf.Approximately(vegetation, VegetationDistance) ||
                !Mathf.Approximately(tree, TreeDistance) ||
                !Mathf.Approximately(hero, HeroDistance);

            HasSystem = hasSystem;
            Booted = booted;
            CellCount = cells;
            LoadedCellCount = loaded;
            VegetationDistance = vegetation;
            TreeDistance = tree;
            HeroDistance = hero;
            return changed;
        }

        /// <summary>
        /// Reads back how many instances survived GPU culling for one item, for the given camera.
        /// </summary>
        /// <remarks>
        /// <c>GraphicsBuffer.CopyCount</c> already writes the post cull append counter into the
        /// indirect args buffer at uint index 1, which is the instance count
        /// <c>DrawMeshInstancedIndirect</c> consumes, so this is a plain synchronous read.
        ///
        /// It blocks until the GPU catches up. Call it from a button or a validator, never from a
        /// per frame readout.
        ///
        /// A result of zero means only that nothing survived for this camera this frame. Check
        /// <see cref="VegetationDistance" /> against the camera's altitude before reading anything
        /// into it.
        /// </remarks>
        /// <param name="vegetationPackageIndex">Index of the package holding the item.</param>
        /// <param name="vegetationItemIndex">Index of the item within that package.</param>
        /// <param name="cameraIndex">Index of the studio camera to report for.</param>
        /// <returns>
        /// The surviving instance count summed over LODs and submeshes, or -1 when it cannot be read.
        /// </returns>
        public int ReadRenderedInstanceCount(int vegetationPackageIndex, int vegetationItemIndex, int cameraIndex)
        {
            VegetationSystemPro system = _driver?.System;
            if (system == null || !system.InitDone)
                return -1;

            VegetationItemModelInfo modelInfo =
                system.GetVegetationItemModelInfo(vegetationPackageIndex, vegetationItemIndex);
            if (modelInfo == null)
                return -1;

            int total = 0;
            var args = new uint[5];
            for (int lod = 0; lod < modelInfo.LODCount; lod++)
            {
                var buffers = modelInfo.GetLODArgsBufferList(lod, cameraIndex, false);
                if (buffers == null)
                    continue;

                foreach (GraphicsBuffer buffer in buffers)
                {
                    if (buffer == null)
                        continue;

                    buffer.GetData(args);
                    total += (int)args[1];
                }
            }

            return total;
        }

        /// <summary>
        /// Resets every reported value.
        /// </summary>
        /// <returns>True if anything was non default before the reset, false otherwise.</returns>
        public bool Clear()
        {
            bool wasNonDefault =
                HasSystem || Booted || CellCount != 0 || LoadedCellCount != 0 ||
                VegetationDistance != 0f || TreeDistance != 0f || HeroDistance != 0f;

            HasSystem = false;
            Booted = false;
            CellCount = 0;
            LoadedCellCount = 0;
            VegetationDistance = 0f;
            TreeDistance = 0f;
            HeroDistance = 0f;
            return wasNonDefault;
        }
    }
}
