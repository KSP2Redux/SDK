#if REDUX
using System;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats
{
    /// <summary>Declares the runtime type discriminator a <see cref="DataObjectMirror" /> subclass maps to.</summary>
    /// <remarks>
    /// Adding a new module type to the bake is one new class plus this attribute. The converter
    /// scans all subclasses of <see cref="DataObjectMirror" /> once, builds a lookup table, and
    /// routes <c>$type</c> strings to the matching mirror. The value is just the type-name
    /// portion of the discriminator (everything before the assembly comma), so e.g.
    /// <c>"KSP.Modules.Data_Engine"</c> matches the JSON's
    /// <c>"KSP.Modules.Data_Engine, Assembly-CSharp"</c>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    internal sealed class DataObjectMirrorAttribute : Attribute
    {
        /// <summary>
        /// Creates a new mirror attribute bound to the given JSON type discriminator.
        /// </summary>
        /// <param name="typeDiscriminator">Type-name portion of the JSON <c>$type</c> string, without the assembly part.</param>
        public DataObjectMirrorAttribute(string typeDiscriminator)
        {
            TypeDiscriminator = typeDiscriminator;
        }

        /// <summary>Type-name portion of the JSON <c>$type</c> string, without the assembly part.</summary>
        public string TypeDiscriminator { get; }
    }
}
#endif
