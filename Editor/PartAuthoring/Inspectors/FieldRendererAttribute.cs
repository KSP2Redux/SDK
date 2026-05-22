using System;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors
{
    /// <summary>
    /// What shape of field a <see cref="IFieldRenderer" /> matches against.
    /// </summary>
    public enum FieldRendererKind
    {
        /// <summary>
        /// Match fields whose declared type is the registered <see cref="FieldRendererAttribute.Type" />.
        /// Used for nested object fields like <c>PropellantDefinition</c>.
        /// </summary>
        Direct,

        /// <summary>
        /// Match fields that are arrays or generic single-arg containers (<c>T[]</c>, <c>List&lt;T&gt;</c>)
        /// whose element type is the registered <see cref="FieldRendererAttribute.Type" />.
        /// Used for canonical list shapes like <c>List&lt;PartModuleResourceSetting&gt;</c>.
        /// </summary>
        ArrayElement,
    }

    /// <summary>
    /// Registers a class as the canonical author-facing renderer for a field matching the
    /// given <see cref="Type" /> and <see cref="Kind" />. Discovered via
    /// <see cref="FieldRendererRegistry" />.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class FieldRendererAttribute : Attribute
    {
        public Type Type { get; }
        public FieldRendererKind Kind { get; }

        public FieldRendererAttribute(Type type, FieldRendererKind kind = FieldRendererKind.Direct)
        {
            Type = type;
            Kind = kind;
        }
    }
}
