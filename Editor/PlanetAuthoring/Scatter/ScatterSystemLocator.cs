using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using KSP.Rendering.Planets;
using Uber.Scatter;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Scatter
{
    /// <summary>
    /// Finds, creates and verifies the <see cref="VegetationSystemPro" /> that carries a body's
    /// terrain scatter.
    /// </summary>
    /// <remarks>
    /// Every configuration item here is silent when wrong. A scatter system can be present, enabled
    /// and completely correct looking while spawning nothing at all, so this type exists to make the
    /// setup a single reviewable action rather than a checklist in a document.
    /// </remarks>
    public static class ScatterSystemLocator
    {
        /// <summary>
        /// Name of the child GameObject that carries the scatter system, matching stock.
        /// </summary>
        /// <remarks>
        /// Stock puts <see cref="VegetationSystemPro" /> on a dedicated child of the local space
        /// object rather than on the PQS itself, and the child sits at identity so it is purely
        /// organisational. <c>CelestialBodyBehavior.OnLocalSpaceViewInstantiated</c> looks the system
        /// up with <c>GetComponentInChildren</c>, so either layout runs, but matching stock keeps
        /// Redux bodies comparable to shipped ones.
        /// </remarks>
        public const string ScatterChildName = "VegetationSystemPro";

        /// <summary>
        /// Returns the scatter system belonging to <paramref name="pqs" />, or null when it has none.
        /// </summary>
        /// <remarks>
        /// Scoped to the body rather than the scene. An authoring scene can hold several bodies, and
        /// a scene-wide search would return whichever happened to be found first.
        /// </remarks>
        /// <param name="pqs">The body's PQS.</param>
        /// <returns>The scatter system, or null.</returns>
        public static VegetationSystemPro Find(PQS pqs)
        {
            if (pqs == null)
            {
                return null;
            }

            VegetationSystemPro system = pqs.GetComponent<VegetationSystemPro>();
            if (system == null)
            {
                system = pqs.GetComponentInChildren<VegetationSystemPro>(true);
            }

            return system;
        }

        /// <summary>
        /// Returns the terrain bridge the scatter system samples, or null when the body has none.
        /// </summary>
        /// <param name="pqs">The body's PQS.</param>
        /// <returns>The terrain bridge, or null.</returns>
        public static PqsTerrain FindTerrain(PQS pqs)
        {
            if (pqs == null)
            {
                return null;
            }

            PqsTerrain terrain = pqs.GetComponent<PqsTerrain>();
            if (terrain == null)
            {
                terrain = pqs.GetComponentInChildren<PqsTerrain>(true);
            }

            return terrain;
        }

        /// <summary>
        /// Adds a scatter system to <paramref name="pqs" /> if it has none, and configures whatever is
        /// there so it can spawn.
        /// </summary>
        /// <remarks>
        /// Registers a single undo group, so the whole action reverts as one step.
        ///
        /// Does not create or assign a vegetation package. That is a separate authoring decision and
        /// wants a saved asset the author names, rather than something invented here.
        /// </remarks>
        /// <param name="pqs">The body's PQS. The system is added to its GameObject.</param>
        /// <param name="body">The body being configured, used for the undo label only.</param>
        /// <returns>The configured system, or null when <paramref name="pqs" /> is null.</returns>
        public static VegetationSystemPro Configure(PQS pqs, CoreCelestialBodyData body)
        {
            if (pqs == null)
            {
                return null;
            }

            string label = body != null ? $"Add Scatter System to {body.name}" : "Add Scatter System";
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(label);

            VegetationSystemPro system = Find(pqs);
            if (system == null)
            {
                system = Undo.AddComponent<VegetationSystemPro>(GetOrCreateScatterChild(pqs, label));
            }
            else
            {
                Undo.RecordObject(system, label);
            }

            system.TerrainType = TerrainType.PolarSphere;
            system._suspendVegetationSystem = false;

            // Registering the terrain is what makes PolarSphereRadius, PolarSphereTransform and
            // PolarSphereMaxHeight resolve. They are derived in SetupPolarSphereInfo and would be
            // overwritten if set directly, so the terrain is the thing worth getting right.
            PqsTerrain terrain = FindTerrain(pqs);
            if (terrain != null && !system.VegetationStudioTerrainObjectList.Contains(terrain.gameObject))
            {
                system.AddTerrain(terrain.gameObject);
            }

            EditorUtility.SetDirty(system);
            Undo.CollapseUndoOperations(undoGroup);
            return system;
        }

        /// <summary>
        /// Returns the child GameObject that should carry the scatter system, creating it if absent.
        /// </summary>
        /// <remarks>
        /// The child is parented with <c>worldPositionStays</c> false so its local transform stays at
        /// identity. That matters more than it looks: whichever GameObject carries
        /// <c>PqsTerrain</c> defines the scatter field's origin through
        /// <c>VegetationSystemPro.PolarSphereTransform</c>, and a child that drifted from identity
        /// would move the whole field relative to the terrain.
        /// </remarks>
        /// <param name="pqs">The body's PQS, which parents the child.</param>
        /// <param name="undoLabel">Undo label applied to the created object.</param>
        /// <returns>The GameObject to host the scatter system.</returns>
        private static GameObject GetOrCreateScatterChild(PQS pqs, string undoLabel)
        {
            Transform existing = pqs.transform.Find(ScatterChildName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var child = new GameObject(ScatterChildName);
            Undo.RegisterCreatedObjectUndo(child, undoLabel);
            child.transform.SetParent(pqs.transform, false);
            return child;
        }
    }
}
