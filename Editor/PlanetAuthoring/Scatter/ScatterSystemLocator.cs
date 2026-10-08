using System;
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
    /// and correct looking while spawning nothing at all, so the setup is gathered into a single
    /// reviewable action.
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
        public static VegetationSystemPro Find(PQS pqs) =>
            pqs == null ? null : pqs.GetComponentInChildren<VegetationSystemPro>(true);

        /// <summary>
        /// Returns the terrain bridge the scatter system samples, or null when the body has none.
        /// </summary>
        /// <param name="pqs">The body's PQS.</param>
        /// <returns>The terrain bridge, or null.</returns>
        public static PqsTerrain FindTerrain(PQS pqs) =>
            pqs == null ? null : pqs.GetComponentInChildren<PqsTerrain>(true);

        /// <summary>
        /// Adds a scatter system to <paramref name="pqs" /> if it has none, and configures whatever is
        /// there so it can spawn.
        /// </summary>
        /// <remarks>
        /// Registers a single undo group, so the whole action reverts as one step.
        ///
        /// Assigning a vegetation package is a separate authoring step.
        /// </remarks>
        /// <param name="pqs">The body's PQS. The system is added to its GameObject.</param>
        /// <param name="body">The body being configured, used for the undo label only.</param>
        /// <returns>The configured system, or null when <paramref name="pqs" /> is null.</returns>
        public static VegetationSystemPro Configure(PQS pqs, CoreCelestialBodyData body)
        {
            if (pqs == null)
                return null;

            string label = body != null ? $"Add Scatter System to {body.name}" : "Add Scatter System";
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(label);

            VegetationSystemPro system = Find(pqs);
            if (system == null)
            {
                // Captured before the component exists, because adding it makes Unity call Reset,
                // which creates a WindZone in the scene. See DiscardResetWindZone.
                WindZone[] windZonesBefore = UnityEngine.Object.FindObjectsByType<WindZone>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);

                system = Undo.AddComponent<VegetationSystemPro>(GetOrCreateScatterChild(pqs, label));
                DiscardResetWindZone(system, windZonesBefore);
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
        /// Removes the WindZone that adding the scatter system caused Unity to create.
        /// </summary>
        /// <remarks>
        /// <c>VegetationSystemPro.Reset</c> calls <c>FindWindZone</c>, which creates a plain
        /// <c>WindZone</c> GameObject at the scene root when the scene has none. Unity runs Reset when
        /// a component is added, so adding a scatter system silently left one behind, saved into the
        /// authoring scene with default hide flags.
        ///
        /// It is not wanted on a Redux body. Wind reaches nothing here: <c>KSPScatterController</c>
        /// defaults <c>SampleWind</c> to false and its <c>UpdateWind</c> is an empty method, and every
        /// wind controller in the fork belongs to a third-party integration this project does not use.
        /// Every one of them also guards on a null zone, so clearing the reference is safe.
        ///
        /// Only a zone that did not exist beforehand is removed. A scene that already had one, or a
        /// body an author wired up deliberately, is left alone.
        /// </remarks>
        /// <param name="system">The scatter system that was just added.</param>
        /// <param name="before">Every WindZone present before the component was added.</param>
        private static void DiscardResetWindZone(VegetationSystemPro system, WindZone[] before)
        {
            WindZone created = system == null ? null : system.SelectedWindZone;
            if (created == null || Array.IndexOf(before, created) >= 0)
                return;

            system.SelectedWindZone = null;
            Undo.DestroyObjectImmediate(created.gameObject);
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
                return existing.gameObject;

            var child = new GameObject(ScatterChildName);
            Undo.RegisterCreatedObjectUndo(child, undoLabel);
            child.transform.SetParent(pqs.transform, false);
            return child;
        }
    }
}
