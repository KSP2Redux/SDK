using Redux.VFX.Plume;
using Redux.VFX.Plume.Services;
using Redux.VFX.Plumes.Editor.Services;
using UnityEditor;

namespace Redux.VFX.Plumes.Editor
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