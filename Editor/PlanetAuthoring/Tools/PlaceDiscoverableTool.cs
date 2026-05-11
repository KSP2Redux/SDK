using System;
using System.Collections.Generic;
using KSP;
using KSP.Game.Science;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.ScriptableObjects;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Persistent SceneView EditorTool that places new <see cref="CelestialBodyDiscoverablePosition" />
    /// entries on the <see cref="ScienceRegionData" /> bound via <see cref="Begin" /> at each left-click.
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="PlaceDecalTool" />. On click: surface-pick gives a world hit + lat/lon, the
    /// hit is converted to a body-local Vector3d (the runtime stores discoverables in body-local
    /// space), and the baked region map at that lat/lon supplies the auto-filled
    /// <see cref="CelestialBodyDiscoverablePosition.ScienceRegionId" />. One-shot per click. Esc or
    /// right-click cancels without placing.
    /// </remarks>
    [EditorTool("Place Discoverable")]
    public sealed class PlaceDiscoverableTool : EditorTool
    {
        // Default discovery radius for fresh discoverables until the artist tunes it. 500 m matches
        // the magnitude the runtime checks against in ScienceRegionsDataProvider.GetScienceRegionAt.
        private const double DefaultRadiusMeters = 500.0;

        /// <summary>The Science Region asset that owns the discoverables list being appended to.</summary>
        public static ScienceRegionData TargetData { get; set; }

        private static PlaceDiscoverableTool _current;
        private GUIContent _toolbarIcon;

        /// <summary>The currently-active instance, or null if the tool is not active.</summary>
        public static PlaceDiscoverableTool Current => _current;

        /// <inheritdoc />
        public override GUIContent toolbarIcon =>
            _toolbarIcon ??= new GUIContent(EditorGUIUtility.IconContent("d_Favorite Icon").image, "Place Discoverable");

        /// <summary>Activates the tool with <paramref name="data" /> as the discoverables target.</summary>
        /// <param name="data">The Science Region asset to append placed discoverables to.</param>
        public static void Begin(ScienceRegionData data)
        {
            TargetData = data;
            ToolManager.SetActiveTool<PlaceDiscoverableTool>();
        }

        /// <inheritdoc />
        public override void OnActivated()
        {
            _current = this;
            SceneView.lastActiveSceneView?.Focus();
        }

        /// <inheritdoc />
        public override void OnWillBeDeactivated()
        {
            // Clear the static target so a later re-activation without Begin doesn't silently
            // append to whatever asset was last targeted.
            TargetData = null;
            if (_current == this)
            {
                _current = null;
            }
        }

        /// <inheritdoc />
        public override void OnToolGUI(EditorWindow window)
        {
            if (window is not SceneView sceneView) return;

            sceneView.wantsMouseMove = true;
            var controlId = GUIUtility.GetControlID(FocusType.Passive);
            var ev = Event.current;

            if (ev.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(controlId);
                return;
            }

            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
            {
                ev.Use();
                ToolManager.RestorePreviousTool();
                return;
            }
            if (ev.type == EventType.MouseDown && ev.button == 1)
            {
                ev.Use();
                ToolManager.RestorePreviousTool();
                return;
            }

            if (TargetData == null)
            {
                if (ev.type == EventType.Repaint)
                {
                    PlanetEditorHud.Draw(sceneView, "No ScienceRegionData target. Open a Science Region asset and click 'Place new on planet'.", null);
                }
                return;
            }

            var session = PlanetAuthoringSession.Active;
            var planet = session?.Pqs;
            if (planet == null)
            {
                if (ev.type == EventType.Repaint)
                {
                    PlanetEditorHud.Draw(sceneView, "No planet preview is active. Enable preview first.", null);
                }
                return;
            }

            // Body-name sanity check so we don't accidentally place a Mun discoverable on Kerbin.
            var body = planet.GetComponentInParent<CoreCelestialBodyData>();
            var bodyName = body?.Data?.bodyName;
            var targetBody = TargetData.information?.BodyName;
            if (!string.IsNullOrEmpty(targetBody)
                && !string.IsNullOrEmpty(bodyName)
                && !string.Equals(targetBody, bodyName, StringComparison.OrdinalIgnoreCase))
            {
                if (ev.type == EventType.Repaint)
                {
                    PlanetEditorHud.Draw(sceneView, $"Active preview is '{bodyName}', but the target Science Region asset is for '{targetBody}'.", null);
                }
                return;
            }

            var ray = HandleUtility.GUIPointToWorldRay(ev.mousePosition);
            var hovering = PlanetSurfaceHit.TryHit(planet, ray, out var hitWorld, out var hitLatLon, out var hitAlt);

            if (ev.type == EventType.MouseDown && ev.button == 0)
            {
                if (hovering)
                {
                    CreateDiscoverableAt(planet, hitWorld, hitLatLon);
                    ev.Use();
                    ToolManager.RestorePreviousPersistentTool();
                }
                return;
            }

            if (ev.type == EventType.MouseMove)
            {
                sceneView.Repaint();
                return;
            }

            if (ev.type == EventType.Repaint)
            {
                var bakedRegionPreview = hovering ? ResolveRegionIdAtLatLon(TargetData, hitLatLon.x, hitLatLon.y) : null;
                var hint = $"Click to place a discoverable on '{TargetData.name}'. Esc or right-click to cancel.";
                if (hovering)
                {
                    var surfaceUp = (hitWorld - planet.transform.position).normalized;
                    var discSize = HandleUtility.GetHandleSize(planet.transform.position) * 0.04f;
                    using (new Handles.DrawingScope(Color.cyan))
                    {
                        Handles.DrawWireDisc(hitWorld, surfaceUp, discSize);
                    }
                    // The placed discoverable gets its own MapId=-1 region. The baked region under
                    // the click point is just context info ("you're placing in the Highlands biome").
                    var sub = $"Lat {hitLatLon.x:0.000}°  Lon {hitLatLon.y:0.000}°  Alt {hitAlt:0} m  Baked region here: {bakedRegionPreview}";
                    PlanetEditorHud.Draw(sceneView, hint, sub);
                }
                else
                {
                    PlanetEditorHud.Draw(sceneView, hint, null);
                }
            }
        }

        private static void CreateDiscoverableAt(PQS planet, Vector3 hitWorld, Vector2 latLon)
        {
            var data = TargetData;
            if (data == null) return;

            var bodyTransform = planet.GetComponentInParent<CoreCelestialBodyData>()?.transform ?? planet.transform;
            Vector3 bodyLocal = bodyTransform.InverseTransformPoint(hitWorld);

            Undo.RecordObject(data, "Place Discoverable");

            // Discoverables are paired with their own dedicated ScienceRegionDefinition entry
            // carrying MapId = -1 (no coverage in the baked map, only reachable via this
            // discoverable's radius). The region's Id IS the discoverable's name. Matches KSP2's
            // stock convention. See KerbinKSC, KerbinDevilsTower etc. in the shipped data.
            var regionId = GenerateUniqueDiscoverableRegionId(data);
            var newRegion = new ScienceRegionData.ExtendedScienceRegionDefinition
            {
                Id = regionId,
                MapId = -1,
                // Match the scalar convention KSP2 uses for discoverable regions: only triggers
                // when the vessel is landed. Artist tunes these afterwards.
                AtmosphereScalar = -1f,
                SplashedScalar = -1f,
                LandedScalar = 1f,
                RegionColor = Color.gray,
            };
            var existingRegions = data.information.ScienceRegionDefinitions
                ?? Array.Empty<ScienceRegionData.ExtendedScienceRegionDefinition>();
            var nextRegions = new ScienceRegionData.ExtendedScienceRegionDefinition[existingRegions.Length + 1];
            Array.Copy(existingRegions, nextRegions, existingRegions.Length);
            nextRegions[^1] = newRegion;
            data.information.ScienceRegionDefinitions = nextRegions;

            data.discoverables ??= new List<CelestialBodyDiscoverablePosition>();
            data.discoverables.Add(new CelestialBodyDiscoverablePosition
            {
                ScienceRegionId = regionId,
                Position = bodyLocal,
                Radius = DefaultRadiusMeters,
            });
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
        }

        /// <summary>
        /// Composes a fresh region Id for a new discoverable region. Starts at "NewDiscoverable1"
        /// and increments until the name doesn't collide with any existing region Id. The
        /// existing-Id set is built once up front so probing is O(1) per candidate.
        /// </summary>
        private static string GenerateUniqueDiscoverableRegionId(ScienceRegionData data)
        {
            var existingIds = new HashSet<string>(StringComparer.Ordinal);
            if (data.information?.ScienceRegionDefinitions != null)
            {
                foreach (var def in data.information.ScienceRegionDefinitions)
                {
                    if (def != null && !string.IsNullOrEmpty(def.Id))
                    {
                        existingIds.Add(def.Id);
                    }
                }
            }
            var n = 1;
            while (true)
            {
                var candidate = $"NewDiscoverable{n}";
                if (!existingIds.Contains(candidate)) return candidate;
                n++;
            }
        }

        /// <summary>
        /// Samples the bound Science Region asset's baked map at the given lat/lon (degrees) and returns the Id of the region matching that pixel's MapId.
        /// </summary>
        /// <remarks>Returns "(unmapped)" when the pixel is 0-MapId, or when there is no baked map yet.</remarks>
        /// <param name="data">The Science Region asset whose baked map is sampled.</param>
        /// <param name="latitude">The latitude to sample at, in degrees.</param>
        /// <param name="longitude">The longitude to sample at, in degrees.</param>
        /// <returns>The matching region Id, or "(unmapped)" when no region covers the sampled pixel.</returns>
        public static string ResolveRegionIdAtLatLon(ScienceRegionData data, float latitude, float longitude)
        {
            const string unmapped = "(unmapped)";
            if (data == null) return unmapped;
            var baked = ScienceRegionAssetLocator.FindBakedMap(data);
            if (baked == null || baked.MapData == null || baked.MapData.Length == 0) return unmapped;

            var uv = PQSJobUtil.GetSphericalUVsForLatLongSingle(latitude, longitude);
            var x = ((int)Mathf.Floor(uv.x * baked.Width)) % baked.Width;
            var y = ((int)Mathf.Floor(uv.y * baked.Height)) % baked.Height;
            if (x < 0) x += baked.Width;
            if (y < 0) y += baked.Height;
            var idx = x + y * baked.Width;
            if (idx < 0 || idx >= baked.MapData.Length) return unmapped;
            int mapId = baked.MapData[idx];
            if (mapId == 0) return unmapped;
            if (data.information?.ScienceRegionDefinitions == null) return unmapped;
            foreach (var def in data.information.ScienceRegionDefinitions)
            {
                if (def != null && def.MapId == mapId) return def.Id;
            }
            return unmapped;
        }
    }
}
