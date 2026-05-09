using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Metadata
{
    /// <summary>
    /// One concrete property after axis expansion. The inspector iterates these directly —
    /// every template token has been substituted and every field is ready for binding.
    /// </summary>
    public readonly struct ExpandedProperty
    {
        public readonly string Id;
        public readonly string Label;
        public readonly string Doc;
        public readonly PropertyKind Kind;
        public readonly PropertySource Source;
        public readonly RebindClass RebindClass;
        public readonly string Widget;

        public ExpandedProperty(
            string id,
            string label,
            string doc,
            PropertyKind kind,
            PropertySource source,
            RebindClass rebindClass,
            string widget
        )
        {
            Id = id;
            Label = label;
            Doc = doc;
            Kind = kind;
            Source = source;
            RebindClass = rebindClass;
            Widget = widget;
        }
    }

    /// <summary>
    /// Expands templated <see cref="MetadataPropertyDef" /> definitions into the cartesian
    /// product of their declared axes, substituting <c>{C}</c>, <c>{i}</c>, <c>{sz}</c>, and
    /// <c>{sc}</c> tokens in the name, label, doc, and source paths.
    /// </summary>
    public static class ShaderInspectorMetadataExpander
    {
        private static readonly string[] BiomeChannels = { "R", "G", "B", "A" };
        private static readonly string[] LayerIndices = { "1", "2", "3", "4" };
        private static readonly string[] SubzoneTiers = { "3", "4" };
        private static readonly string[] SubzoneChannels = { "R", "G", "B", "A" };

        /// <summary>
        /// Yields one <see cref="ExpandedProperty" /> per cell in the template's axis product.
        /// A definition with no axes yields exactly one entry with the templates taken verbatim.
        /// </summary>
        public static IEnumerable<ExpandedProperty> Expand(MetadataPropertyDef def)
        {
            foreach (var (c, i, sz, sc) in IterateAxes(def.Axes))
            {
                var id = Substitute(def.IdTemplate, c, i, sz, sc);
                var label = Substitute(def.LabelTemplate, c, i, sz, sc);
                var doc = Substitute(def.DocSnippet, c, i, sz, sc);
                var source = ExpandSource(def.Source, c, i, sz, sc);

                yield return new ExpandedProperty(
                    id,
                    label,
                    doc,
                    def.Kind,
                    source,
                    def.RebindClass,
                    def.Widget
                );
            }
        }

        /// <summary>Yields every section's expanded property list in render order.</summary>
        public static IEnumerable<ExpandedProperty> Expand(MetadataSection section)
        {
            foreach (var def in section.Properties)
            foreach (var expanded in Expand(def))
                yield return expanded;
        }

        private static IEnumerable<(string c, string i, string sz, string sc)> IterateAxes(
            PropertyAxis axes
        )
        {
            var biomeSet = (axes & PropertyAxis.Biome) != 0 ? BiomeChannels : new[] { "" };
            var layerSet = (axes & PropertyAxis.Layer) != 0 ? LayerIndices : new[] { "" };
            var tierSet = (axes & PropertyAxis.SubzoneTier) != 0 ? SubzoneTiers : new[] { "" };
            var channelSet = (axes & PropertyAxis.SubzoneChannel) != 0 ? SubzoneChannels : new[] { "" };

            foreach (var c in biomeSet)
            foreach (var i in layerSet)
            foreach (var sz in tierSet)
            foreach (var sc in channelSet)
                yield return (c, i, sz, sc);
        }

        private static string Substitute(string template, string c, string i, string sz, string sc)
        {
            if (string.IsNullOrEmpty(template))
                return template;

            return template
                .Replace("{C}", c)
                .Replace("{i}", i)
                .Replace("{sz}", sz)
                .Replace("{sc}", sc);
        }

        private static PropertySource ExpandSource(
            PropertySource source,
            string c,
            string i,
            string sz,
            string sc
        )
        {
            if (source == null)
                return null;

            return new PropertySource
            {
                Asset = source.Asset,
                MaterialProperty = Substitute(source.MaterialProperty, c, i, sz, sc),
                PqsDataPath = Substitute(source.PqsDataPath, c, i, sz, sc),
                KeywordName = Substitute(source.KeywordName, c, i, sz, sc),
            };
        }
    }
}
