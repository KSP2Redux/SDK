using KSP.Rendering.Planets;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Services
{
    public interface IGraphicsSettingsProvider
    {
        PQSGlobalSettings GetPQSGlobalSettings();

        bool TryGetBoolSetting(string name, out bool value);
    }
}
