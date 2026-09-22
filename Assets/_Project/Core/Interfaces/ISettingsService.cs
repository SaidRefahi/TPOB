using System;

namespace Game.Core.Interfaces
{
    public interface ISettingsService
    {
        float MasterVolume { get; }
        float SfxVolume { get; }
        float AimSensitivity { get; }
        int FullscreenModeIndex { get; }
        bool VSyncEnabled { get; }

        event Action OnSettingsChanged;

        void SetMasterVolume(float volume);
        void SetSfxVolume(float volume);
        void SetAimSensitivity(float sensitivity);
        void SetFullscreenMode(int modeIndex);
        void SetVSync(bool enabled);
        void SetResolution(int width, int height, int refreshRate);
        void SaveSettings();
    }
}
