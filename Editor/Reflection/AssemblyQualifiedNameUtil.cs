namespace Ksp2UnityTools.Editor.Reflection
{
    /// <summary>
    /// Helpers for the assembly-qualified type names embedded in authored mission assets.
    /// </summary>
    public static class AssemblyQualifiedNameUtil
    {
        /// <summary>
        /// Reduces an assembly-qualified name to its FullName for catalog lookups.
        /// </summary>
        /// <remarks>
        /// Authored mission JSONs store bare FullNames (no assembly suffix), but
        /// Type.AssemblyQualifiedName always includes one. Collapsing both forms to FullName
        /// makes the editor's catalog dictionary keys match either input shape, and
        /// incidentally survives assembly version drift since Version/Culture/PublicKeyToken
        /// get dropped too.
        /// </remarks>
        /// <param name="aqn">The assembly-qualified name to reduce.</param>
        /// <returns>The FullName portion of the input, or the input itself if it carries no assembly suffix.</returns>
        public static string Normalize(string aqn)
        {
            if (string.IsNullOrEmpty(aqn)) return aqn;
            int firstComma = aqn.IndexOf(',');
            return (firstComma < 0 ? aqn : aqn.Substring(0, firstComma)).Trim();
        }
    }
}
