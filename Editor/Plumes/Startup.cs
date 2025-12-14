using Redux.VFX.Plume;
using Redux.VFX.Plume.Services;
using Ksp2UnityTools.Editor.Plumes.Services;
using UnityEditor;

namespace Ksp2UnityTools.Editor.Plumes
{
    [InitializeOnLoad]
    public class Startup
    {
        static Startup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            ServiceProvider.RegisterService<IPlumeLogger>(new UnityLogger());
            ServiceProvider.RegisterService<IAssetManager>(new UnityAssetManager());
        }
    }
}