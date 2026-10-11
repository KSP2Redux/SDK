using System.Runtime.CompilerServices;

// The KSP2 Redux game project covers parts of this assembly with its EditMode suite.
[assembly: InternalsVisibleTo("Ksp2Redux.Editor.Tests")]

#if !REDUX
[assembly: ThunderKit.Core.Config.ImportExtensions]
#endif
