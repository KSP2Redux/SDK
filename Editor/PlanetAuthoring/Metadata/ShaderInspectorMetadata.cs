using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Metadata
{
    /// <summary>
    /// Authored description of a shader's editable surface. Drives the
    /// <c>SurfaceMaterialEditor</c>: section ordering and labels, default-collapsed state,
    /// keyword gating, tooltip docs, custom-widget hints, and the change-reactor classification
    /// each property feeds. One asset per shader, keyed on <see cref="ShaderName" />.
    /// </summary>
    /// <remarks>
    /// The schema covers <c>Redux/Environment/CelestialBody_Local</c>'s ~400 visible properties
    /// and the new sections that wrap PQSData fields (heightmap stack, pole settings, biome
    /// semantic mapping). Property definitions are <em>templated</em> over the biome, layer,
    /// and subzone axes; the inspector expands the cartesian product at load time so a single
    /// definition like <c>_SmallTint{C}{i}</c> covers all 16 concrete properties.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "ShaderInspectorMetadata",
        menuName = "Redux/Planet Authoring/Shader Inspector Metadata"
    )]
    public class ShaderInspectorMetadata : ScriptableObject
    {
        /// <summary>Schema version. Bumped when the data layout changes incompatibly.</summary>
        public int Version = 1;

        /// <summary>The shader this metadata describes (e.g. <c>Redux/Environment/CelestialBody_Local</c>).</summary>
        public string ShaderName;

        /// <summary>Sections rendered in inspector order.</summary>
        public List<MetadataSection> Sections = new();

        /// <summary>
        /// Mirrored bindings — pairs of <see cref="PropertyAsset.Material" /> and
        /// <see cref="PropertyAsset.PqsData" /> values that the dispatcher must keep in sync.
        /// Editing either side writes to both.
        /// </summary>
        public List<MirroredBinding> Mirrors = new();
    }

    /// <summary>
    /// One foldout in the inspector. Contains an ordered list of property definitions and
    /// optionally a keyword that hides the whole section when off.
    /// </summary>
    [Serializable]
    public class MetadataSection
    {
        /// <summary>Stable identifier used for foldout state and search anchoring.</summary>
        public string Id;

        /// <summary>Display label shown on the foldout header.</summary>
        public string Label;

        /// <summary>Path or anchor pointing back at PARAMS.md for "open docs" affordances.</summary>
        public string DocRef;

        /// <summary>Whether the foldout starts collapsed when the inspector first opens.</summary>
        public bool DefaultCollapsed;

        /// <summary>
        /// Optional shader keyword that gates this section. Empty string means always visible.
        /// When set, the section auto-hides if the keyword is off on the bound material.
        /// </summary>
        public string KeywordGate = "";

        /// <summary>
        /// Layout strategy that drives which custom <c>VisualElement</c> wraps the property list.
        /// </summary>
        public SectionLayout Layout = SectionLayout.PlainList;

        /// <summary>Property definitions in render order.</summary>
        public List<MetadataPropertyDef> Properties = new();
    }

    /// <summary>
    /// Visual layout pattern applied to a section. The plain list is the default; the others
    /// route into custom <c>VisualElement</c>s built around the per-biome and per-layer axes.
    /// </summary>
    public enum SectionLayout
    {
        /// <summary>One property per row, no wrapping group.</summary>
        PlainList,

        /// <summary>R/G/B/A tab strip — one set of properties shown per active biome channel.</summary>
        BiomeChannelTabs,

        /// <summary>Biome × layer 4×4 grid for §3.6 small biome detail.</summary>
        SmallLayerMatrix,

        /// <summary>Per-biome tabs with an extra subzone-tier (3 vs 4) split inside each tab.</summary>
        SubzoneTierTabs,
    }

    /// <summary>
    /// One templated property definition. Concrete properties are produced by expanding the
    /// declared axes over their value sets and substituting the tokens (<c>{C}</c>, <c>{i}</c>,
    /// <c>{sz}</c>, <c>{sc}</c>) into the name, label, and source paths.
    /// </summary>
    [Serializable]
    public class MetadataPropertyDef
    {
        /// <summary>
        /// Templated property identifier. Tokens: <c>{C}</c> = biome (R/G/B/A),
        /// <c>{i}</c> = layer index (1..4), <c>{sz}</c> = subzone tier (3/4),
        /// <c>{sc}</c> = subzone channel (R/G/B/A).
        /// </summary>
        public string IdTemplate;

        /// <summary>Templated display label. Same token set as <see cref="IdTemplate" />.</summary>
        public string LabelTemplate;

        /// <summary>Tooltip body sourced from PARAMS.md. May contain the same tokens.</summary>
        public string DocSnippet;

        /// <summary>Property type — drives the default editor field choice.</summary>
        public PropertyKind Kind;

        /// <summary>
        /// Which axes this definition expands over. <see cref="PropertyAxis.None" /> means the
        /// property is singular and the templates are taken verbatim.
        /// </summary>
        public PropertyAxis Axes = PropertyAxis.None;

        /// <summary>Where the property is read from and written to.</summary>
        public PropertySource Source;

        /// <summary>
        /// Reactor classification — what the live-preview pipeline must do after this property
        /// is edited. Drives the same routing the runtime uses when pushing material/data
        /// changes back into the GPU buffers.
        /// </summary>
        public RebindClass RebindClass = RebindClass.Free;

        /// <summary>
        /// Optional custom widget identifier (e.g. <c>fade-curve-graph</c>,
        /// <c>trapezoid-altitude</c>, <c>trapezoid-slope</c>). Empty falls back to the default
        /// editor field for the property's <see cref="Kind" />.
        /// </summary>
        public string Widget = "";
    }

    /// <summary>
    /// Concrete kind of a property. Maps roughly to the shader's declared property type but
    /// adds the structured "fade" and "trapezoid" vector shapes so widgets can pick the right
    /// editor without parsing the property name.
    /// </summary>
    public enum PropertyKind
    {
        Texture2D,
        Texture2DArray,
        TextureCube,
        Color,
        Vector4,
        Float,
        Range,
        Int,
        IntVector,

        /// <summary>Vector4 packed as <c>(start, range, nearOpacity, farOpacity)</c>.</summary>
        FadeVector,

        /// <summary>Vector4 packed as <c>(center, upRange, downRange, fadeOut)</c>.</summary>
        TrapezoidVector,

        /// <summary>Boolean shader keyword (toggleable from the inspector).</summary>
        Keyword,

        /// <summary>Boolean shader keyword that the runtime owns (read-only display).</summary>
        KeywordReadOnly,
    }

    /// <summary>
    /// Axes the property expands over. Bit flags so a single definition can multiply across
    /// several dimensions at once (e.g. §3.7 subzone overrides span biome × layer × subzone).
    /// </summary>
    [Flags]
    public enum PropertyAxis
    {
        None = 0,

        /// <summary>Biome channel — token <c>{C}</c>, values <c>R, G, B, A</c>.</summary>
        Biome = 1 << 0,

        /// <summary>Layer index inside a biome — token <c>{i}</c>, values <c>1, 2, 3, 4</c>.</summary>
        Layer = 1 << 1,

        /// <summary>Subzone tier — token <c>{sz}</c>, values <c>3, 4</c>. Used by §3.5.</summary>
        SubzoneTier = 1 << 2,

        /// <summary>
        /// Subzone channel suffix — token <c>{sc}</c>, values <c>R, G, B, A</c>. Used by the
        /// §3.7 tint family where the tint applies per subzone-channel rather than per-biome.
        /// </summary>
        SubzoneChannel = 1 << 3,
    }

    /// <summary>
    /// Where a property lives. The dispatcher reads this to decide which underlying object
    /// receives the write — the bound surface material, the bound PQSData, both (mirrored),
    /// or a shader keyword.
    /// </summary>
    [Serializable]
    public class PropertySource
    {
        /// <summary>Backing asset selector.</summary>
        public PropertyAsset Asset;

        /// <summary>
        /// Material property name template, used when <see cref="Asset" /> is
        /// <see cref="PropertyAsset.Material" /> or <see cref="PropertyAsset.Mirrored" />.
        /// Tokens are substituted from the parent definition's axes.
        /// </summary>
        public string MaterialProperty = "";

        /// <summary>
        /// Dotted <c>SerializedProperty</c> path on the bound PQSData, used when
        /// <see cref="Asset" /> is <see cref="PropertyAsset.PqsData" /> or
        /// <see cref="PropertyAsset.Mirrored" />. Example: <c>heightMapInfo.large{C}.heightMap</c>.
        /// </summary>
        public string PqsDataPath = "";

        /// <summary>
        /// Shader keyword name, used when <see cref="Asset" /> is
        /// <see cref="PropertyAsset.Keyword" /> or for a mirrored keyword.
        /// </summary>
        public string KeywordName = "";
    }

    /// <summary>Backing asset for a property's value.</summary>
    public enum PropertyAsset
    {
        /// <summary>Surface material. Writes go through <c>Material.SetXxx</c>.</summary>
        Material,

        /// <summary>PQSData ScriptableObject. Writes go through SerializedObject.</summary>
        PqsData,

        /// <summary>Both — the dispatcher writes to material and PQSData atomically.</summary>
        Mirrored,

        /// <summary>Shader keyword. Writes go through <c>Material.EnableKeyword</c>/<c>DisableKeyword</c>.</summary>
        Keyword,
    }

    /// <summary>
    /// Reactor classification — the post-edit work the live-preview pipeline must run.
    /// Mirrors the runtime push paths in <c>PQSRenderer</c> and <c>PQSDecalController</c>.
    /// </summary>
    public enum RebindClass
    {
        /// <summary>Material edit only. Repaint the SceneView and the shader picks it up next frame.</summary>
        Free,

        /// <summary>
        /// Material edit feeds the per-biome structured GPU buffer.
        /// Triggers <c>PQSRendererHelper.PushPQSMaterialToStructuredBuffers</c>.
        /// </summary>
        TriggerBiomeBufferRebuild,

        /// <summary>
        /// PQSData edit feeds the height-sampler pipeline.
        /// Triggers <c>PQSRenderer.SetPQSHeightSampleShaderValue</c>.
        /// </summary>
        TriggerHeightSamplerPush,

        /// <summary>
        /// Decal-related edit. Triggers <c>PQSDecalController.RefreshDecalInstances</c>.
        /// </summary>
        TriggerDecalRefresh,

        /// <summary>
        /// Shader keyword toggle. May invalidate buffer state — the reactor runs the relevant
        /// rebuild paths in addition to flipping the keyword.
        /// </summary>
        KeywordToggle,
    }

    /// <summary>
    /// One mirrored material/PQSData pair. The dispatcher writes both sides whenever either is
    /// edited. Mirrors are listed once at the asset level rather than spread across property
    /// definitions so a quick scan tells you which fields drift if edited out-of-band.
    /// </summary>
    [Serializable]
    public class MirroredBinding
    {
        /// <summary>Stable id, useful for diagnostics.</summary>
        public string Id;

        /// <summary>Material property name on one side of the mirror.</summary>
        public string MaterialProperty = "";

        /// <summary>PQSData SerializedProperty path on the other side.</summary>
        public string PqsDataPath = "";

        /// <summary>Keyword name when the mirror is keyword-driven (e.g. SUB_ZONES_ENABLED).</summary>
        public string KeywordName = "";
    }
}
