using System;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Variants
{
    /// <summary>
    /// Registers a class as the custom editor for a specific <see cref="VSwift.Modules.Transformers.ITransformer" /> concrete type. The Variants tab's transformer-row dispatch looks up registered editors by the transformer type; if no editor is registered the generic <see cref="ReflectionTransformerEditor" /> rendering is used.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TransformerEditorAttribute : Attribute
    {
        /// <summary>The transformer type this editor handles.</summary>
        public Type TransformerType { get; }

        /// <summary>
        /// Creates a new <see cref="TransformerEditorAttribute" />.
        /// </summary>
        /// <param name="transformerType">The transformer type to register against.</param>
        public TransformerEditorAttribute(Type transformerType)
        {
            TransformerType = transformerType;
        }
    }
}
