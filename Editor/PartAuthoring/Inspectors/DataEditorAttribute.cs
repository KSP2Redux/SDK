using System;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors
{
    /// <summary>
    /// Registers a class as the custom editor for a specific <c>Data_*</c> type.
    /// </summary>
    /// <remarks>
    /// The Modules tab's dispatch looks up registered editors by the data type and instantiates one
    /// per build. If no editor is registered, the generic <see cref="ReflectionModuleEditor" />
    /// rendering is used.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class DataEditorAttribute : Attribute
    {
        /// <summary>The <c>Data_*</c> type this editor handles.</summary>
        public Type DataType { get; }

        /// <summary>
        /// Creates a new <see cref="DataEditorAttribute" />.
        /// </summary>
        /// <param name="dataType">The <c>Data_*</c> type to register against.</param>
        public DataEditorAttribute(Type dataType)
        {
            DataType = dataType;
        }
    }
}
