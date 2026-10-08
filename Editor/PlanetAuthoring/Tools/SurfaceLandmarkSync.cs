using System;
using System.Collections.Generic;
using KSP;
using KSP.Game.Science;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.ScriptableObjects;
using Redux.CelestialBody;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Reconciles a <see cref="SurfaceLandmark" />'s field state with its three managed children
    /// (smoothing decal, prefab spawner, discoverable entry).
    /// </summary>
    /// <remarks>
    /// Called from the landmark's <c>OnValidate</c> via delayCall, from the inspector on field
    /// edits, and from the place tool after creating a fresh landmark. Idempotent: repeat calls
    /// with the same field values are no-ops. Owns curvature-inset math and terrain-height
    /// resampling.
    /// </remarks>
    internal static class SurfaceLandmarkSync
    {
        private const string DecalChildName = "_SmoothingDecal";
        private const string LegacySpawnerChildName = "_PrefabSpawner";
        private const string SpawnerChildName = "_ReduxSurfaceSpawner";
        private const string DiscoverableIdPrefix = "NewLandmark";

        /// <summary>
        /// Pushes <paramref name="landmark" />'s field values to its managed children, creating or
        /// destroying them per the toggle state. Safe to call repeatedly.
        /// </summary>
        public static void Sync(SurfaceLandmark landmark)
        {
            if (landmark == null) return;
            var pqs = landmark.GetComponentInParent<PQS>();
            if (pqs == null) return;

            WriteWrapperTransform(landmark, pqs);
            SyncDecal(landmark, pqs);
            SyncSpawner(landmark, pqs);
            SyncDiscoverable(landmark, pqs);
        }

        /// <summary>
        /// Writes the wrapper's Unity transform from <paramref name="landmark" />'s authored doubles,
        /// so the scene view shows the landmark at its lat/lon and the move handle can pivot off it.
        /// </summary>
        private static void WriteWrapperTransform(SurfaceLandmark landmark, PQS pqs)
        {
            var pqsTransform = pqs.transform;
            var localDir = LatLon.GetRelSurfaceNVector(landmark.Latitude, landmark.Longitude);
            double radius;
            if (TrySurfaceHeight(pqs, localDir, out double terrainR))
            {
                radius = terrainR + landmark.Altitude;
            }
            else
            {
                radius = (pqs.CoreCelestialBodyData?.Data?.radius ?? 0.0) + landmark.Altitude;
            }
            Undo.RecordObject(landmark.transform, "Sync SurfaceLandmark transform");
            landmark.transform.position = pqsTransform.position + pqsTransform.rotation * ((Vector3)localDir * (float)radius);
            // Surface-normal up so children that key off transform.up (the decal handle, the science
            // overlay gizmos) still get a sensible orientation.
            var surfaceUpWorld = (pqsTransform.rotation * (Vector3)localDir).normalized;
            landmark.transform.rotation = Quaternion.FromToRotation(landmark.transform.up, surfaceUpWorld) * landmark.transform.rotation;
        }

        private static bool TrySurfaceHeight(PQS pqs, Vector3 localDir, out double height)
        {
            height = 0.0;
            if (pqs.PQSRenderer == null
                || pqs.PQSRenderer.PqsDecalController == null
                || pqs.PQSRenderer.PqsDecalController.PqsDecalData == null)
            {
                return false;
            }
            try
            {
                height = pqs.GetSurfaceHeight(localDir, includeDecals: false);
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        // -- Decal ---------------------------------------------------------------

        private static void SyncDecal(SurfaceLandmark landmark, PQS pqs)
        {
            if (!landmark.EnableDecal)
            {
                if (landmark.ManagedDecal != null)
                {
                    UnityEngine.Object.DestroyImmediate(landmark.ManagedDecal.gameObject);
                    landmark.ManagedDecal = null;
                    EditorUtility.SetDirty(landmark);
                }
                return;
            }

            var controller = DecalControllerHelper.Resolve(pqs);
            if (controller == null) return;

            if (landmark.ManagedDecal == null)
            {
                landmark.ManagedDecal = FindOrCreateDecalChild(landmark, controller);
                EditorUtility.SetDirty(landmark);
            }

            double lat = landmark.Latitude;
            double lon = landmark.Longitude;
            var radius = (float)pqs.CoreCelestialBodyData?.Data?.radius;
            // Sample terrain at lat/lon WITHOUT decals so the smoothing decal is keyed to the
            // natural terrain height under the landmark, not whatever some prior decal raised it to.
            // Skip the sample when the PQS isn't bootstrapped (no preview session) so the runtime's
            // decal-controller dereference inside HeightSample doesn't NRE.
            var localDir = LatLon.GetRelSurfaceNVector(lat, lon);
            float terrainHeight;
            if (pqs.PQSRenderer != null && pqs.PQSRenderer.PqsDecalController != null
                && pqs.PQSRenderer.PqsDecalController.PqsDecalData != null)
            {
                try
                {
                    terrainHeight = (float)pqs.GetSurfaceHeight(localDir, includeDecals: false) - radius;
                }
                catch (System.ObjectDisposedException)
                {
                    terrainHeight = 0f;
                }
            }
            else
            {
                terrainHeight = 0f;
            }

            var decal = landmark.ManagedDecal;
            Undo.RecordObject(decal, "Sync SurfaceLandmark decal");
            // Use the artist's chosen template if one is assigned, otherwise fall back to the
            // SDK's default smoothing pad. The smoothing-mode overrides below only run when
            // EnableSmoothing is on. In pure-cosmetic mode the decal sources height from its own
            // template defaults (which for a flat shared heightmap means no height modification).
            decal.PQSDecal = landmark.SmoothingDecal != null ? landmark.SmoothingDecal : SmoothingPadAsset.Get();
            decal.PqsDecalController = controller;
            decal.LatLong = new Vector2((float)lat, (float)lon);
            decal.Scale = landmark.SmoothingRadius * 2f;
            if (landmark.EnableSmoothing)
            {
                decal.OverrideHeightBlendMode = true;
                decal.HeightBlendMode = PQSDecalHeightBlendMode.Replace;
                decal.OverrideHeightOffset = true;
                decal.HeightOffset = 0.5f;
                decal.OverrideHeightScale = true;
                decal.HeightScale = terrainHeight;
                decal.OverrideFadeShape = true;
                decal.FadeShape = PQSDecalAlphaFadeShape.Circular;
                decal.OverrideFadeStrength = true;
                decal.FadeStrength = Mathf.Clamp(landmark.SmoothingFadeStrength, 0.25f, 2f);
            }
            else
            {
                // Cosmetic mode: hand control of every height-and-fade override over to the
                // landmark inspector's per-instance override rows. Clearing the flags here means
                // the artist's toggles are authoritative - not clearing would leave the smoothing-
                // mode values frozen in place.
                decal.OverrideHeightBlendMode = false;
                decal.OverrideHeightOffset = false;
                decal.OverrideHeightScale = false;
                decal.OverrideFadeShape = false;
                decal.OverrideFadeStrength = false;
            }
            decal.UpdateDecalTransform();
            EditorUtility.SetDirty(decal);
            controller.RefreshDecalInstances();
            DecalBaker.QueueRebuild(controller);
        }

        private static PQSDecalInstance FindOrCreateDecalChild(SurfaceLandmark landmark, PQSDecalController controller)
        {
            var existing = landmark.transform.Find(DecalChildName);
            if (existing != null)
            {
                var inst = existing.GetComponent<PQSDecalInstance>();
                if (inst != null) return inst;
            }
            var go = new GameObject(DecalChildName);
            go.transform.SetParent(landmark.transform, worldPositionStays: false);
            MoveToLandmarkScene(landmark, go);
            Undo.RegisterCreatedObjectUndo(go, "Create SurfaceLandmark decal child");
            var instance = go.AddComponent<PQSDecalInstance>();
            instance.PqsDecalController = controller;
            instance.DecalInstanceID = Guid.NewGuid().ToString();
            return instance;
        }

        // -- Prefab spawner ------------------------------------------------------

        private static void SyncSpawner(SurfaceLandmark landmark, PQS pqs)
        {
            RemoveLegacySpawnerChild(landmark);

            if (!landmark.EnablePrefab)
            {
                if (landmark.ManagedSpawner != null)
                {
                    UnityEngine.Object.DestroyImmediate(landmark.ManagedSpawner.gameObject);
                    landmark.ManagedSpawner = null;
                    EditorUtility.SetDirty(landmark);
                }
                return;
            }

            if (landmark.ManagedSpawner == null)
            {
                landmark.ManagedSpawner = FindOrCreateSpawnerChild(landmark);
                EditorUtility.SetDirty(landmark);
            }

            var spawner = landmark.ManagedSpawner;
            Undo.RecordObject(spawner, "Sync SurfaceLandmark prefab");
            string key = landmark.UseRawAddressableKey
                ? landmark.PrefabAddressableKey
                : AddressableKeyLookup.GetKey(landmark.Prefab);
            // Inset the spawn DOWN by the curvature drop across the prefab's footprint so the prefab's
            // edges sit at the local horizon. Inset = R - sqrt(R^2 - (W/2)^2). Clamps to 0 when the
            // half-width meets or exceeds the body radius (asteroid-scale edge case). With
            // ReduxSurfaceSpawner, the inset folds into the altitude rather than a child transform
            // offset - the spawner's own transform is irrelevant to runtime placement.
            var bodyRadius = (float)(pqs.CoreCelestialBodyData?.Data?.radius ?? 0.0);
            var halfWidth = Mathf.Max(0f, landmark.PrefabWidth * 0.5f);
            var inset = ComputeCurvatureInset(bodyRadius, halfWidth);
            spawner.AddressableKey = string.IsNullOrEmpty(key) ? string.Empty : key;
            spawner.Latitude = landmark.Latitude;
            spawner.Longitude = landmark.Longitude;
            spawner.Altitude = landmark.Altitude - inset;
            spawner.YawDegrees = 0f;
            spawner.AlignToSurfaceNormal = true;
            EditorUtility.SetDirty(spawner);
            // Field writes don't trigger hierarchyChanged, so the editor preview wouldn't pick up
            // a new addressable key otherwise.
            SurfacePrefabPreviewSync.Refresh();
        }

        private static void RemoveLegacySpawnerChild(SurfaceLandmark landmark)
        {
            var legacy = landmark.transform.Find(LegacySpawnerChildName);
            if (legacy == null) return;
            // Only destroy if it actually carries a legacy PrefabSpawner. Otherwise it might be a
            // user-authored sibling that happens to share the name - leave it alone.
            if (legacy.GetComponent<PrefabSpawner>() == null) return;
            Undo.DestroyObjectImmediate(legacy.gameObject);
        }

        private static ReduxSurfaceSpawner FindOrCreateSpawnerChild(SurfaceLandmark landmark)
        {
            var existing = landmark.transform.Find(SpawnerChildName);
            if (existing != null)
            {
                var spawner = existing.GetComponent<ReduxSurfaceSpawner>();
                if (spawner != null) return spawner;
            }
            var go = new GameObject(SpawnerChildName);
            go.transform.SetParent(landmark.transform, worldPositionStays: false);
            MoveToLandmarkScene(landmark, go);
            Undo.RegisterCreatedObjectUndo(go, "Create SurfaceLandmark spawner child");
            return go.AddComponent<ReduxSurfaceSpawner>();
        }

        // -- Discoverable -------------------------------------------------------

        private static void SyncDiscoverable(SurfaceLandmark landmark, PQS pqs)
        {
            var data = ResolveBodyScienceRegion(pqs);
            if (data == null) return;

            if (!landmark.EnableDiscoverable)
            {
                // Toggle-off does not remove the existing discoverable entry. Orphan removal is
                // surfaced by LandmarkOrphanedDiscoverableValidator and resolved via the Discoverable
                // Manager window, so the artist isn't surprised by silent deletions on a stray click.
                return;
            }

            if (string.IsNullOrEmpty(landmark.DiscoverableRegionId))
            {
                landmark.DiscoverableRegionId = GenerateUniqueRegionId(data);
                EditorUtility.SetDirty(landmark);
            }

            EnsureRegionDefinition(data, landmark.DiscoverableRegionId);

            // Discoverable position is body-local Unity Vector3 (rotated with the body transform).
            // Derive it from the landmark's authored lat/lon/altitude in doubles, then cast to float
            // at the assignment.
            var localDir = LatLon.GetRelSurfaceNVector(landmark.Latitude, landmark.Longitude);
            double radius = TrySurfaceHeight(pqs, localDir, out double terrainR)
                ? terrainR + landmark.Altitude
                : (pqs.CoreCelestialBodyData?.Data?.radius ?? 0.0) + landmark.Altitude;
            Vector3 local = (Vector3)(localDir * radius);

            data.discoverables ??= new List<CelestialBodyDiscoverablePosition>();
            CelestialBodyDiscoverablePosition entry = null;
            foreach (var d in data.discoverables)
            {
                if (d != null && string.Equals(d.ScienceRegionId, landmark.DiscoverableRegionId, StringComparison.Ordinal))
                {
                    entry = d;
                    break;
                }
            }
            if (entry == null)
            {
                entry = new CelestialBodyDiscoverablePosition { ScienceRegionId = landmark.DiscoverableRegionId };
                data.discoverables.Add(entry);
            }
            Undo.RecordObject(data, "Sync SurfaceLandmark discoverable");
            entry.Position = local;
            entry.Radius = landmark.DiscoverableRadius;
            EditorUtility.SetDirty(data);
        }

        private static void EnsureRegionDefinition(ScienceRegionData data, string regionId)
        {
            if (data.information == null) return;
            var defs = data.information.ScienceRegionDefinitions ?? Array.Empty<ScienceRegionData.ExtendedScienceRegionDefinition>();
            foreach (var def in defs)
            {
                if (def != null && string.Equals(def.Id, regionId, StringComparison.Ordinal)) return;
            }
            // Mirrors PlaceDiscoverableTool's region-creation pattern: MapId = -1 (no baked
            // coverage), Landed-only scalar so the discoverable triggers when the vessel is at rest.
            var newRegion = new ScienceRegionData.ExtendedScienceRegionDefinition
            {
                Id = regionId,
                MapId = -1,
                AtmosphereScalar = -1f,
                SplashedScalar = -1f,
                LandedScalar = 1f,
                RegionColor = Color.gray,
            };
            var next = new ScienceRegionData.ExtendedScienceRegionDefinition[defs.Length + 1];
            Array.Copy(defs, next, defs.Length);
            next[^1] = newRegion;
            data.information.ScienceRegionDefinitions = next;
        }

        private static string GenerateUniqueRegionId(ScienceRegionData data)
        {
            var existing = new HashSet<string>(StringComparer.Ordinal);
            if (data.information?.ScienceRegionDefinitions != null)
            {
                foreach (var def in data.information.ScienceRegionDefinitions)
                {
                    if (def != null && !string.IsNullOrEmpty(def.Id)) existing.Add(def.Id);
                }
            }
            if (data.discoverables != null)
            {
                foreach (var d in data.discoverables)
                {
                    if (d != null && !string.IsNullOrEmpty(d.ScienceRegionId)) existing.Add(d.ScienceRegionId);
                }
            }
            var n = 1;
            while (true)
            {
                var candidate = DiscoverableIdPrefix + n;
                if (!existing.Contains(candidate)) return candidate;
                n++;
            }
        }

        private static ScienceRegionData ResolveBodyScienceRegion(PQS pqs)
        {
            var bodyName = pqs.CoreCelestialBodyData?.Data?.bodyName;
            if (string.IsNullOrEmpty(bodyName)) return null;
            return ScienceRegionAssetLocator.FindForBody(bodyName);
        }

        // -- Helpers -------------------------------------------------------------

        /// <summary>
        /// Computes how far below a tangent plane of half-width <paramref name="halfWidth" /> the
        /// surface of a sphere of radius <paramref name="bodyRadius" /> drops at the rim.
        /// </summary>
        public static float ComputeCurvatureInset(float bodyRadius, float halfWidth)
        {
            if (bodyRadius <= 0f) return 0f;
            if (halfWidth <= 0f) return 0f;
            if (halfWidth >= bodyRadius) return bodyRadius;
            return bodyRadius - Mathf.Sqrt(bodyRadius * bodyRadius - halfWidth * halfWidth);
        }

        private static void MoveToLandmarkScene(SurfaceLandmark landmark, GameObject child)
        {
            var scene = landmark.gameObject.scene;
            if (scene.IsValid() && child.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(child, scene);
            }
        }
    }
}
