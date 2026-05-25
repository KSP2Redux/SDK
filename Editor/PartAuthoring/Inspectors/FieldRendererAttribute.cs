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
        /// </summary>
        /// <remarks>
        /// Used for nested object fields like <c>PropellantDefinition</c>.
        /// </remarks>
        Direct,

        /// <summary>
        /// Match fields that are arrays or generic single-arg containers (<c>T[]</c>, <c>List&lt;T&gt;</c>) whose element type is the registered <see cref="FieldRendererAttribute.Type" />.
        /// </summary>
        /// <remarks>
        /// Used for canonical list shapes like <c>List&lt;PartModuleResourceSetting&gt;</c>.
        /// </remarks>
        ArrayElement,
    }

    /// <summary>
    /// Registers a class as the canonical author-facing renderer for a field matching the given <see cref="Type" /> and <see cref="Kind" />.
    /// </summary>
    /// <remarks>
    /// Discovered via <see cref="FieldRendererRegistry" />.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class FieldRendererAttribute : Attribute
    {
        /// <summary>
        /// The field type (or array element type) this renderer matches against.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Whether the renderer matches a directly-typed field or the element type of an array or list.
        /// </summary>
        public FieldRendererKind Kind { get; }

        /// <summary>
        /// Creates a new <see cref="FieldRendererAttribute" />.
        /// </summary>
        /// <param name="type">The field type (or array element type) to register against.</param>
        /// <param name="kind">Whether the registration is for a direct-typed field or an array element.</param>
        public FieldRendererAttribute(Type type, FieldRendererKind kind = FieldRendererKind.Direct)
        {
            Type = type;
            Kind = kind;
        }
    }
}
