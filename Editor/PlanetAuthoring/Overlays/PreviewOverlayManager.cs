using System;
using System.Collections.Generic;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Overlays
{
    /// <summary>
    /// Tracks which preview overlays are enabled and routes their lifecycle through <see cref="PlanetAuthoringSession" />.
    /// </summary>
    /// <remarks>
    /// Overlay instances are lazily created on first enable and cached in a dictionary for the
    /// remainder of the editor session. Disabling an overlay removes it from the active PQSRenderer
    /// but does not dispose the instance. Disposal only runs on assembly reload, which clears the
    /// cache. When a session becomes active the enabled overlays are pushed into
    /// <see cref="PQSRenderer.AddOverlay" />. When the session ends they are removed and freshly
    /// rebound the next time a session starts. Enabled state survives domain reload via
    /// <see cref="SessionState" />. The actual material instances are not persisted and are rebuilt
    /// on demand.
    /// </remarks>
    [InitializeOnLoad]
    internal static class PreviewOverlayManager
    {
        private const string SessionStateKey = "Redux.PlanetAuthoring.PreviewOverlay.Enabled";
        private const string StrengthPrefKey = "Redux.PlanetAuthoring.PreviewOverlay.Strength";
        private const string BandHeightPrefKey = "Redux.PlanetAuthoring.PreviewOverlay.BandHeight";
        private const string ActiveLayerMaskPrefKey = "Redux.PlanetAuthoring.PreviewOverlay.ActiveLayerMask";
        private const string ScienceRegionModePrefKey = "Redux.PlanetAuthoring.PreviewOverlay.ScienceRegionMode";

        // 16 bits = 4 biomes (R/G/B/A) x 4 layers each. Bit (biome * 4 + layer) gates whether
        // that (biome, layer) pair contributes to the active-layer overlay.
        /// <summary>
        /// Default value for <see cref="ActiveLayerMask" /> with every (biome, layer) pair enabled.
        /// </summary>
        public const int ActiveLayerMaskAllOn = 0xFFFF;

        private static readonly Dictionary<PreviewOverlayKind, PreviewOverlay> _instances = new();
        private static readonly HashSet<PreviewOverlayKind> _enabled = new();
        private static PQS _registeredPqs;
        private static float _strength = 0.7f;
        private static float _bandHeight = 500f;
        private static int _activeLayerMask = ActiveLayerMaskAllOn;
        private static ScienceRegionPreviewOverlay.Mode _scienceRegionMode = ScienceRegionPreviewOverlay.Mode.BakedPalette;

        /// <summary>
        /// Raised when the enabled-set changes so UI listeners (Preview Controls toggles) can refresh.
        /// </summary>
        public static event Action StateChanged;

        private static double _lastPeriodicRefresh;
        private const double PeriodicRefreshIntervalSeconds = 0.75;

        static PreviewOverlayManager()
        {
            LoadEnabledFromSessionState();
            _strength = EditorPrefs.GetFloat(StrengthPrefKey, 0.7f);
            _bandHeight = EditorPrefs.GetFloat(BandHeightPrefKey, 500f);
            _activeLayerMask = EditorPrefs.GetInt(ActiveLayerMaskPrefKey, ActiveLayerMaskAllOn) & ActiveLayerMaskAllOn;
            _scienceRegionMode = (ScienceRegionPreviewOverlay.Mode)EditorPrefs.GetInt(ScienceRegionModePrefKey, (int)ScienceRegionPreviewOverlay.Mode.BakedPalette);
            PlanetPreviewState.ActiveChanged += OnSessionStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.update += OnEditorUpdate;
            SceneView.duringSceneGui += PreviewOverlayLegend.OnSceneGUI;
            // Re-attach to a session that may already be alive when the domain finishes reloading.
            EditorApplication.delayCall += SyncToActiveSession;
        }

        private static void OnEditorUpdate()
        {
            if (_enabled.Count == 0) return;
            var now = EditorApplication.timeSinceStartup;
            if (now - _lastPeriodicRefresh < PeriodicRefreshIntervalSeconds) return;
            _lastPeriodicRefresh = now;
            RefreshBindings();
        }

        /// <summary>
        /// Returns whether the given overlay kind is currently enabled.
        /// </summary>
        /// <param name="kind">The overlay kind to query.</param>
        /// <returns>True if the overlay is enabled, false otherwise.</returns>
        public static bool IsEnabled(PreviewOverlayKind kind) => _enabled.Contains(kind);

        /// <summary>
        /// Gets the set of overlay kinds currently enabled.
        /// </summary>
        public static IReadOnlyCollection<PreviewOverlayKind> EnabledKinds => _enabled;

        /// <summary>
        /// Gets or sets the shared 0..1 strength multiplier applied to every overlay material.
        /// </summary>
        public static float Strength
        {
            get => _strength;
            set
            {
                _strength = Mathf.Clamp01(value);
                EditorPrefs.SetFloat(StrengthPrefKey, _strength);
                ApplyStrengthToInstances();
                SceneView.RepaintAll();
            }
        }

        /// <summary>
        /// Gets or sets the altitude-band height in meters used by the altitude-contour overlay.
        /// </summary>
        public static float BandHeightMeters
        {
            get => _bandHeight;
            set
            {
                _bandHeight = Mathf.Max(1f, value);
                EditorPrefs.SetFloat(BandHeightPrefKey, _bandHeight);
                ApplyBandHeightToInstances();
                SceneView.RepaintAll();
            }
        }

        /// <summary>
        /// Gets the per-(biome, layer) enable mask for the active-layer overlay.
        /// </summary>
        /// <remarks>
        /// Bit index is biome * 4 + layer, where biome 0..3 maps to R/G/B/A and layer 0..3 maps to
        /// L1..L4. All-on (0xFFFF) by default.
        /// </remarks>
        public static int ActiveLayerMask => _activeLayerMask;

        /// <summary>
        /// Returns whether the given (biome, layer) pair is enabled in <see cref="ActiveLayerMask" />.
        /// </summary>
        /// <param name="biome">The biome index in the range 0..3 (R/G/B/A).</param>
        /// <param name="layer">The layer index in the range 0..3 (L1..L4).</param>
        /// <returns>True if the pair contributes to the active-layer overlay, false otherwise.</returns>
        public static bool IsLayerEnabled(int biome, int layer)
        {
            if (biome < 0 || biome > 3 || layer < 0 || layer > 3) return false;
            return (_activeLayerMask & (1 << PlanetAuthoringNaming.CellIndex(biome, layer))) != 0;
        }

        /// <summary>
        /// Toggles the given (biome, layer) pair in <see cref="ActiveLayerMask" />.
        /// </summary>
        /// <param name="biome">The biome index in the range 0..3 (R/G/B/A).</param>
        /// <param name="layer">The layer index in the range 0..3 (L1..L4).</param>
        /// <param name="enabled">True to enable the pair, false to disable it.</param>
        public static void SetLayerEnabled(int biome, int layer, bool enabled)
        {
            if (biome < 0 || biome > 3 || layer < 0 || layer > 3) return;
            var bit = 1 << PlanetAuthoringNaming.CellIndex(biome, layer);
            var next = enabled ? (_activeLayerMask | bit) : (_activeLayerMask & ~bit);
            if (next == _activeLayerMask) return;
            _activeLayerMask = next & ActiveLayerMaskAllOn;
            EditorPrefs.SetInt(ActiveLayerMaskPrefKey, _activeLayerMask);
            ApplyActiveLayerMaskToInstances();
            StateChanged?.Invoke();
            SceneView.RepaintAll();
        }

        private static void ApplyActiveLayerMaskToInstances()
        {
            foreach (var overlay in _instances.Values)
            {
                if (overlay is ActiveLayerPreviewOverlay a)
                {
                    a.LayerEnableMask = _activeLayerMask;
                }
            }
        }

        /// <summary>
        /// Gets or sets the display mode for the science region overlay.
        /// </summary>
        /// <remarks>
        /// Either baked palette (post-bake, what the runtime sees) or source texture (what the
        /// artist is currently authoring).
        /// </remarks>
        public static ScienceRegionPreviewOverlay.Mode ScienceRegionMode
        {
            get => _scienceRegionMode;
            set
            {
                if (_scienceRegionMode == value) return;
                _scienceRegionMode = value;
                EditorPrefs.SetInt(ScienceRegionModePrefKey, (int)_scienceRegionMode);
                ApplyScienceRegionModeToInstances();
                StateChanged?.Invoke();
                SceneView.RepaintAll();
            }
        }

        /// <summary>
        /// Returns the science overlay instance if one has been created, otherwise null.
        /// </summary>
        /// <remarks>
        /// Used by the legend to surface stale-bake state without forcing the overlay to materialize.
        /// </remarks>
        /// <returns>The cached science region overlay, or null if it has not been created yet.</returns>
        public static ScienceRegionPreviewOverlay TryGetScienceRegionOverlay()
        {
            return _instances.TryGetValue(PreviewOverlayKind.ScienceRegion, out PreviewOverlay overlay)
                ? overlay as ScienceRegionPreviewOverlay
                : null;
        }

        private static void ApplyScienceRegionModeToInstances()
        {
            foreach (var overlay in _instances.Values)
            {
                if (overlay is ScienceRegionPreviewOverlay s)
                {
                    s.CurrentMode = _scienceRegionMode;
                    var pqs = PlanetAuthoringSession.Active?.Pqs;
                    if (pqs != null)
                    {
                        s.RefreshBindings(pqs);
                    }
                }
            }
        }

        private static void ApplyStrengthToInstances()
        {
            foreach (var overlay in _instances.Values)
            {
                switch (overlay)
                {
                    case MaskPreviewOverlay m:             m.Strength = _strength; break;
                    case HeightDerivedPreviewOverlay h:    h.Strength = _strength; break;
                    case ActiveLayerPreviewOverlay a:      a.Strength = _strength; break;
                    case ScienceRegionPreviewOverlay s:    s.Strength = _strength; break;
                }
            }
        }

        private static void ApplyBandHeightToInstances()
        {
            foreach (var overlay in _instances.Values)
            {
                if (overlay is HeightDerivedPreviewOverlay h)
                {
                    h.BandHeightMeters = _bandHeight;
                }
            }
        }

        /// <summary>
        /// Enables or disables the given overlay kind and reconciles it with the active session.
        /// </summary>
        /// <param name="kind">The overlay kind to toggle.</param>
        /// <param name="enabled">True to enable the overlay, false to disable it.</param>
        public static void SetEnabled(PreviewOverlayKind kind, bool enabled)
        {
            bool changed;
            if (enabled)
                changed = _enabled.Add(kind);
            else
                changed = _enabled.Remove(kind);

            if (!changed)
                return;

            SaveEnabledToSessionState();
            SyncToActiveSession();
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Re-pulls per-body inputs into every enabled overlay.
        /// </summary>
        /// <remarks>
        /// Call after the artist edits a mask texture or any other binding source so the
        /// visualization reflects current state.
        /// </remarks>
        public static void RefreshBindings()
        {
            var pqs = PlanetAuthoringSession.Active?.Pqs;
            if (pqs == null) return;
            foreach (var kind in _enabled)
            {
                var overlay = GetOrCreate(kind);
                overlay.RefreshBindings(pqs);
            }
            SceneView.RepaintAll();
        }

        private static void OnSessionStateChanged() => SyncToActiveSession();

        private static void SyncToActiveSession()
        {
            var pqs = PlanetAuthoringSession.Active?.Pqs;

            // Body switched or session ended. Detach every previously-registered overlay first.
            if (_registeredPqs != null && _registeredPqs != pqs)
            {
                DetachAllFromPqs(_registeredPqs);
                _registeredPqs = null;
            }

            if (pqs == null)
            {
                if (_enabled.Count > 0)
                {
                    Debug.Log($"[PreviewOverlayManager] {_enabled.Count} overlay(s) enabled but no active preview session yet. Start preview to render them.");
                }
                return;
            }
            if (pqs.PQSRenderer == null)
            {
                Debug.LogWarning("[PreviewOverlayManager] Active PQS has no PQSRenderer. Overlays will not render.");
                return;
            }

            // Reconcile against the enabled set: enable adds, disable removes, leftover instances detach.
            foreach (var kind in _enabled)
            {
                var overlay = GetOrCreate(kind);
                overlay.RefreshBindings(pqs);
                pqs.PQSRenderer.AddOverlay(overlay);
            }
            foreach (var entry in _instances)
            {
                if (!_enabled.Contains(entry.Key))
                {
                    pqs.PQSRenderer.RemoveOverlay(entry.Value);
                }
            }
            _registeredPqs = pqs;
            SceneView.RepaintAll();
        }

        private static void DetachAllFromPqs(PQS pqs)
        {
            if (pqs == null || pqs.PQSRenderer == null) return;
            foreach (var overlay in _instances.Values)
            {
                pqs.PQSRenderer.RemoveOverlay(overlay);
            }
            // PQSRenderer.DrawPQSOverlays clears its CommandBuffer at the top of each call,
            // but it stops being called once the session-end tears down the editor render hook,
            // so the buffer keeps the last frame's DrawProceduralIndirect commands cached and the
            // camera replays them every paint. Strip our buffer off every SceneView camera
            // ourselves so the overlay actually disappears.
            DetachOverlayCommandBuffersFromSceneViews();
        }

        private static void DetachOverlayCommandBuffersFromSceneViews()
        {
            // Clearing (not removing) keeps the buffer instance attached to the camera and the
            // PQSRenderer's _overlayCommandBuffer reference live, so the next session's
            // DrawPQSOverlays naturally re-fills it without needing to re-attach.
            foreach (var o in SceneView.sceneViews)
            {
                if (o is not SceneView sv || sv == null) continue;
                var cam = sv.camera;
                if (cam == null) continue;
                var buffers = cam.GetCommandBuffers(CameraEvent.BeforeForwardAlpha);
                if (buffers == null) continue;
                foreach (var buf in buffers)
                {
                    if (buf != null && buf.name == "PQS Overlays")
                    {
                        buf.Clear();
                    }
                }
            }
            SceneView.RepaintAll();
        }

        private static PreviewOverlay GetOrCreate(PreviewOverlayKind kind)
        {
            if (_instances.TryGetValue(kind, out var existing)) return existing;
            var created = Create(kind);
            _instances[kind] = created;
            // Push current global tuning values into the fresh instance.
            switch (created)
            {
                case MaskPreviewOverlay m:             m.Strength = _strength; break;
                case HeightDerivedPreviewOverlay h:    h.Strength = _strength; h.BandHeightMeters = _bandHeight; break;
                case ActiveLayerPreviewOverlay a:      a.Strength = _strength; a.LayerEnableMask = _activeLayerMask; break;
                case ScienceRegionPreviewOverlay s:    s.Strength = _strength; s.CurrentMode = _scienceRegionMode; break;
            }
            return created;
        }

        private static PreviewOverlay Create(PreviewOverlayKind kind) => kind switch
        {
            PreviewOverlayKind.BiomeMask     => new MaskPreviewOverlay(MaskPreviewOverlay.Source.BiomeMask),
            PreviewOverlayKind.SubzoneMask   => new MaskPreviewOverlay(MaskPreviewOverlay.Source.SubzoneMask),
            PreviewOverlayKind.Slope         => new HeightDerivedPreviewOverlay(HeightDerivedPreviewOverlay.Source.Slope),
            PreviewOverlayKind.AltitudeBands => new HeightDerivedPreviewOverlay(HeightDerivedPreviewOverlay.Source.AltitudeBands),
            PreviewOverlayKind.ActiveLayer   => new ActiveLayerPreviewOverlay(),
            PreviewOverlayKind.ScienceRegion => new ScienceRegionPreviewOverlay { CurrentMode = _scienceRegionMode },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        private static void OnBeforeAssemblyReload()
        {
            DetachAllFromPqs(_registeredPqs);
            foreach (var overlay in _instances.Values)
            {
                overlay.Dispose();
            }
            _instances.Clear();
            _registeredPqs = null;
        }

        private static void LoadEnabledFromSessionState()
        {
            _enabled.Clear();
            var serialized = SessionState.GetString(SessionStateKey, string.Empty);
            if (string.IsNullOrEmpty(serialized)) return;
            foreach (var token in serialized.Split(','))
            {
                if (Enum.TryParse(token, out PreviewOverlayKind kind))
                {
                    _enabled.Add(kind);
                }
            }
        }

        private static void SaveEnabledToSessionState()
        {
            SessionState.SetString(SessionStateKey, string.Join(",", _enabled));
        }
    }
}
