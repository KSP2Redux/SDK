using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeTechnologies.VegetationSystem;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Scatter
{
    /// <summary>
    /// One surface stratum an author can restrict a scatter to.
    /// </summary>
    /// <remarks>
    /// Carries both index spaces. The cell index is what an author works in through the PQS
    /// inspector, and the slice index is what a scatter rule stores. The two agree only on a fully
    /// packed body, so a swatch showing both is what makes a mismatch diagnosable.
    /// </remarks>
    public readonly struct ScatterStratum
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ScatterStratum" /> struct.
        /// </summary>
        /// <param name="cellIndex">Flat index into the 16 small-layer slots.</param>
        /// <param name="sliceIndex">Index into the packed texture array, or -1 when unpacked.</param>
        /// <param name="displayName">Name to show in the picker.</param>
        /// <param name="albedo">Albedo texture for the swatch, or null.</param>
        /// <param name="layerMaterial">Shared layer template backing the cell, or null.</param>
        public ScatterStratum(
            int cellIndex,
            int sliceIndex,
            string displayName,
            Texture2D albedo,
            SmallLayerMaterial layerMaterial)
        {
            CellIndex = cellIndex;
            SliceIndex = sliceIndex;
            DisplayName = displayName;
            Albedo = albedo;
            LayerMaterial = layerMaterial;
        }

        /// <summary>
        /// Flat index into <c>PQSDataAuthoring.smallLayerSlots</c>, biome-major over R/G/B/A.
        /// </summary>
        public int CellIndex { get; }

        /// <summary>
        /// Index into the packed texture array that a scatter rule stores, or -1 when the cell was
        /// not packed.
        /// </summary>
        public int SliceIndex { get; }

        /// <summary>
        /// Name to show in the picker.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Albedo texture backing this stratum, or null when the cell has none.
        /// </summary>
        public Texture2D Albedo { get; }

        /// <summary>
        /// Shared layer template assigned to the cell, or null when the slot carries local values only.
        /// </summary>
        /// <remarks>
        /// Carried so a swatch can use the same rendered sphere thumbnail the PQS inspector shows for
        /// this cell.
        /// </remarks>
        public SmallLayerMaterial LayerMaterial { get; }

        /// <summary>
        /// Biome channel index, 0 to 3 for R/G/B/A.
        /// </summary>
        public int BiomeChannel => CellIndex / 4;

        /// <summary>
        /// One-based layer number within the biome, matching how the packer reports it.
        /// </summary>
        public int LayerNumber => (CellIndex % 4) + 1;

        /// <summary>
        /// Gets a value indicating whether this cell made it into the packed array and can therefore
        /// match a rule.
        /// </summary>
        public bool IsPacked => SliceIndex >= 0;
    }

    /// <summary>
    /// Resolves a body's surface strata to the index a scatter rule has to store.
    /// </summary>
    /// <remarks>
    /// A <c>TerrainTextureRule.TextureIndex</c> holds the packed slice index, while an author works
    /// in the 0-15 cell index the PQS inspector shows. <c>Texture2DArrayPacker.PackIntoArray</c>
    /// compacts only the filled cells and writes the resulting slice per cell into the surface
    /// material's <c>_SmallBiome{R,G,B,A}</c> vectors, leaving -1 for the rest. The two spaces
    /// coincide only when every preceding cell is filled, which is the uncommon case.
    ///
    /// The mapping is therefore read out of the material, which is what <c>PqsTerrain</c> hands the
    /// sampling compute shader.
    ///
    /// A body with no sidecar, or a surface material that does not declare the channel vectors,
    /// yields <see cref="Unavailable" /> so callers can fall back to a plain integer field.
    /// </remarks>
    public sealed class ScatterStratumLookup
    {
        /// <summary>
        /// Number of small-layer cells, four biome channels by four layers.
        /// </summary>
        public const int CellCount = 16;

        private const string ChannelVectorPrefix = "_SmallBiome";

        private readonly ScatterStratum[] _strata;

        private ScatterStratumLookup(ScatterStratum[] strata)
        {
            _strata = strata;
            if (strata == null)
                return;

            foreach (ScatterStratum stratum in strata)
            {
                if (stratum.IsPacked)
                {
                    PackedSliceCount++;
                }
            }
        }

        /// <summary>
        /// The lookup to use when a body's stratum names cannot be resolved.
        /// </summary>
        public static ScatterStratumLookup Unavailable { get; } = new ScatterStratumLookup(null);

        /// <summary>
        /// Gets a value indicating whether stratum names and indices are resolvable for this body.
        /// </summary>
        public bool IsAvailable => _strata != null;

        /// <summary>
        /// All sixteen cells in cell order, including unpacked ones.
        /// </summary>
        /// <remarks>
        /// Unpacked cells are kept so a picker can show them as unselectable.
        /// </remarks>
        public IReadOnlyList<ScatterStratum> Strata => _strata ?? Array.Empty<ScatterStratum>();

        /// <summary>
        /// Gets the number of cells that made it into the packed array.
        /// </summary>
        /// <remarks>
        /// Counted from the slice indices the material's channel vectors declare, so a stored index
        /// outside <c>0</c> to <c>PackedSliceCount - 1</c> matches no stratum.
        /// </remarks>
        public int PackedSliceCount { get; }

        /// <summary>
        /// Builds a lookup from a sidecar and the four per-biome slice-index vectors.
        /// </summary>
        /// <remarks>
        /// Takes the channel vectors as data rather than reading a <see cref="Material" />, so the
        /// mapping is reachable from the headless EditMode suite.
        /// </remarks>
        /// <param name="authoring">The body's PQS authoring sidecar. May be null.</param>
        /// <param name="channelSliceIndices">Four vectors in R/G/B/A order, each holding the four layer slice indices for that biome.</param>
        /// <returns>The lookup, or <see cref="Unavailable" /> when the inputs cannot describe a mapping.</returns>
        public static ScatterStratumLookup Build(PQSDataAuthoring authoring, Vector4[] channelSliceIndices)
        {
            if (channelSliceIndices == null || channelSliceIndices.Length < PlanetAuthoringNaming.BiomeChannels.Length)
                return Unavailable;

            var strata = new ScatterStratum[CellCount];
            for (var channel = 0; channel < PlanetAuthoringNaming.BiomeChannels.Length; channel++)
            {
                for (var layer = 0; layer < 4; layer++)
                {
                    int cell = PlanetAuthoringNaming.CellIndex(channel, layer);
                    SmallLayerSlot slot = SlotAt(authoring, cell);
                    int slice = Mathf.RoundToInt(channelSliceIndices[channel][layer]);
                    strata[cell] = new ScatterStratum(
                        cell,
                        slice < 0 ? -1 : slice,
                        ResolveName(slot, channel, layer),
                        slot?.EffectiveAlbedoTexture,
                        slot?.Material);
                }
            }

            return new ScatterStratumLookup(strata);
        }

        /// <summary>
        /// Builds a lookup by reading the slice indices off a body's surface material.
        /// </summary>
        /// <param name="authoring">The body's PQS authoring sidecar. May be null.</param>
        /// <param name="surfaceMaterial">The body's surface material.</param>
        /// <returns>The lookup, or <see cref="Unavailable" /> when the material does not declare the channel vectors.</returns>
        public static ScatterStratumLookup FromMaterial(PQSDataAuthoring authoring, Material surfaceMaterial) =>
            TryReadChannelSliceIndices(surfaceMaterial, out Vector4[] channels)
                ? Build(authoring, channels)
                : Unavailable;

        /// <summary>
        /// Reads the four per-biome slice-index vectors off a surface material.
        /// </summary>
        /// <remarks>
        /// Every property is checked before it is read, because <c>GetVector</c> on an undeclared
        /// property returns zero, which would read as "every cell is slice 0" and point every rule at
        /// one stratum.
        ///
        /// The repack remap calls this directly to capture the raw vectors before a pack rewrites
        /// them.
        /// </remarks>
        /// <param name="surfaceMaterial">The body's surface material. May be null.</param>
        /// <param name="channels">The four vectors in R/G/B/A order on success.</param>
        /// <returns>True if the material declares all four vectors, false otherwise.</returns>
        public static bool TryReadChannelSliceIndices(Material surfaceMaterial, out Vector4[] channels)
        {
            channels = null;
            if (surfaceMaterial == null)
                return false;

            var read = new Vector4[PlanetAuthoringNaming.BiomeChannels.Length];
            for (var channel = 0; channel < read.Length; channel++)
            {
                string property = ChannelVectorPrefix + PlanetAuthoringNaming.BiomeChannels[channel];
                if (!surfaceMaterial.HasProperty(property))
                    return false;

                read[channel] = surfaceMaterial.GetVector(property);
            }

            channels = read;
            return true;
        }

        /// <summary>
        /// Raised when a body's stratum numbering has changed, so open pickers can re-resolve.
        /// </summary>
        /// <remarks>
        /// A repack renumbers slices, which means an inspector built before it is showing names for
        /// the wrong indices. Each subscriber re-resolves its own body.
        /// </remarks>
        public static event Action OnStrataChanged;

        /// <summary>
        /// Gets the number of times stratum numbering has changed.
        /// </summary>
        /// <remarks>
        /// Lets a caller holding a resolved lookup detect that it has gone stale without subscribing.
        /// Resolving is not free, since inferring a package's body scans every loaded scene, so a
        /// caller with many consumers of one lookup can cache it against this value and re-resolve
        /// once.
        /// </remarks>
        public static int ChangeVersion { get; private set; }

        /// <summary>
        /// Announces that stratum numbering has changed.
        /// </summary>
        public static void NotifyStrataChanged()
        {
            ChangeVersion++;
            OnStrataChanged?.Invoke();
        }

        /// <summary>
        /// Builds a lookup for a body from its PQS.
        /// </summary>
        /// <remarks>
        /// Resolves the sidecar with <c>AuthoringSidecars.Find</c>, so opening an inspector stays a
        /// read and never writes a new sidecar asset to disk.
        /// </remarks>
        /// <param name="pqs">The body's PQS. May be null.</param>
        /// <returns>The lookup, or <see cref="Unavailable" /> when the body has no resolvable mapping.</returns>
        public static ScatterStratumLookup FromPqs(PQS pqs) => FromPqsData(pqs == null ? null : pqs.data);

        /// <summary>
        /// Builds a lookup from a body's PQS data asset.
        /// </summary>
        /// <param name="data">The body's PQS data. May be null.</param>
        /// <returns>The lookup, or <see cref="Unavailable" /> when the body has no resolvable mapping.</returns>
        public static ScatterStratumLookup FromPqsData(PQSData data)
        {
            if (data == null || data.materialSettings == null)
                return Unavailable;

            return FromMaterial(AuthoringSidecars.Find(data), data.materialSettings.surfaceMaterial);
        }

        /// <summary>
        /// Builds a lookup for the body a package belongs to, without being told which body that is.
        /// </summary>
        /// <remarks>
        /// A package carries no reference to a body, so the association has to be inferred. Two
        /// sources, strongest first: a scatter system in a loaded scene that lists this package, then
        /// the PQS data asset sitting nearest it on disk. Either is a guess, so both require a single
        /// unambiguous answer and yield <see cref="Unavailable" /> otherwise.
        /// </remarks>
        /// <param name="package">The package being edited. May be null.</param>
        /// <returns>The lookup, or <see cref="Unavailable" /> when no single body can be inferred.</returns>
        public static ScatterStratumLookup FromPackage(VegetationPackagePro package)
        {
            if (package == null)
                return Unavailable;

            VegetationSystemPro[] systems = UnityEngine.Object.FindObjectsByType<VegetationSystemPro>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            VegetationSystemPro claimed = null;
            foreach (VegetationSystemPro system in systems)
            {
                if (system == null || system.VegetationPackageProList == null ||
                    !system.VegetationPackageProList.Contains(package))
                    continue;

                if (claimed != null)
                {
                    // Two loaded bodies use this package, so the scene cannot say which strata to
                    // name. Fall through and let the folder decide, which it may still do cleanly.
                    claimed = null;
                    break;
                }

                claimed = system;
            }

            if (claimed != null)
                return FromSystem(claimed);

            string assetPath = AssetDatabase.GetAssetPath(package);
            if (string.IsNullOrEmpty(assetPath))
                return Unavailable;

            int slash = assetPath.LastIndexOf('/');
            string dataPath = slash < 0
                ? null
                : ResolveBodyDataPath(assetPath.Substring(0, slash), FindPqsDataIn);

            return string.IsNullOrEmpty(dataPath)
                ? Unavailable
                : FromPqsData(AssetDatabase.LoadAssetAtPath<PQSData>(dataPath));
        }

        /// <summary>
        /// Walks up from a folder looking for the one PQS data asset that owns it.
        /// </summary>
        /// <remarks>
        /// Bodies are laid out one folder per body, so a package stored beside or beneath a body's
        /// data belongs to it. The walk climbs while a level holds nothing and stops the moment a
        /// level holds more than one, because searching is recursive and a higher level can only add
        /// candidates.
        /// </remarks>
        /// <param name="startFolder">Folder holding the package.</param>
        /// <param name="findIn">Returns the PQS data asset paths at or beneath a folder.</param>
        /// <returns>The single owning data asset path, or null when no one answer is unambiguous.</returns>
        public static string ResolveBodyDataPath(string startFolder, Func<string, string[]> findIn)
        {
            string folder = startFolder;
            while (!string.IsNullOrEmpty(folder) && findIn != null)
            {
                string[] found = findIn(folder) ?? Array.Empty<string>();
                if (found.Length == 1)
                    return found[0];

                if (found.Length > 1)
                    return null;

                int slash = folder.LastIndexOf('/');
                if (slash < 0)
                    return null;

                folder = folder.Substring(0, slash);
            }

            return null;
        }

        private static string[] FindPqsDataIn(string folder) =>
            AssetDatabase.FindAssets("t:PQSData", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToArray();

        /// <summary>
        /// Builds a lookup for the body a scatter system belongs to.
        /// </summary>
        /// <remarks>
        /// The inverse of <c>ScatterSystemLocator.Find</c>, which accepts the system either on the PQS
        /// or on a child of it, so the walk upward has to allow for both.
        /// </remarks>
        /// <param name="system">The body's scatter system. May be null.</param>
        /// <returns>The lookup, or <see cref="Unavailable" /> when the body has no resolvable mapping.</returns>
        public static ScatterStratumLookup FromSystem(VegetationSystemPro system) =>
            system == null ? Unavailable : FromPqs(system.GetComponentInParent<PQS>(true));

        /// <summary>
        /// Finds the stratum a stored rule index refers to.
        /// </summary>
        /// <param name="sliceIndex">The value stored in <c>TerrainTextureRule.TextureIndex</c>.</param>
        /// <param name="stratum">The matching stratum on success.</param>
        /// <returns>True if a packed cell claims this slice, false otherwise.</returns>
        public bool TryGetBySlice(int sliceIndex, out ScatterStratum stratum)
        {
            stratum = default;
            if (_strata == null || sliceIndex < 0)
                return false;

            foreach (ScatterStratum candidate in _strata)
            {
                if (candidate.SliceIndex == sliceIndex)
                {
                    stratum = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the stratum at a cell index.
        /// </summary>
        /// <param name="cellIndex">Flat index into the sixteen small-layer slots.</param>
        /// <param name="stratum">The matching stratum on success.</param>
        /// <returns>True if the cell index is in range, false otherwise.</returns>
        public bool TryGetByCell(int cellIndex, out ScatterStratum stratum)
        {
            stratum = default;
            if (_strata == null || cellIndex < 0 || cellIndex >= _strata.Length)
                return false;

            stratum = _strata[cellIndex];
            return true;
        }

        /// <summary>
        /// Reports whether a stored rule index falls inside the body's packed slice range.
        /// </summary>
        /// <remarks>
        /// Shared by the picker and the validator, so both work from one definition of a dead rule
        /// index.
        /// </remarks>
        /// <param name="sliceIndex">The value stored in <c>TerrainTextureRule.TextureIndex</c>.</param>
        /// <returns>True if the index addresses an allocated slice, false otherwise.</returns>
        public bool IsSliceInRange(int sliceIndex) => sliceIndex >= 0 && sliceIndex < PackedSliceCount;

        private static SmallLayerSlot SlotAt(PQSDataAuthoring authoring, int cell)
        {
            SmallLayerSlot[] slots = authoring == null ? null : authoring.smallLayerSlots;
            return slots != null && cell < slots.Length ? slots[cell] : null;
        }

        private static string ResolveName(SmallLayerSlot slot, int channel, int layer)
        {
            string fromMaterial = slot?.Material == null ? null : slot.Material.name;
            if (!string.IsNullOrWhiteSpace(fromMaterial))
                return fromMaterial;

            Texture2D albedo = slot?.EffectiveAlbedoTexture;
            if (albedo != null && !string.IsNullOrWhiteSpace(albedo.name))
                return albedo.name;

            return $"Biome {PlanetAuthoringNaming.BiomeChannels[channel]} Layer {layer + 1}";
        }
    }
}
