using System;

namespace Game.Core.Interfaces
{
    public interface ISettingsService
    {
        float MasterVolume { get; }
        float SfxVolume { get; }
        float MusicVolume { get; }
        float AimSensitivity { get; }
        int FullscreenModeIndex { get; }
        bool VSyncEnabled { get; }
        int QualityLevelIndex { get; }
        int TargetFrameRate { get; }
        bool InvertYAxis { get; }
        bool MuteInBackground { get; }
        string InputBindingOverridesJson { get; }
        string Language { get; }

        event Action OnSettingsChanged;

        void SetMasterVolume(float volume);
        void SetSfxVolume(float volume);
        void SetMusicVolume(float volume);
        void SetAimSensitivity(float sensitivity);
        void SetFullscreenMode(int modeIndex);
        void SetVSync(bool enabled);
        void SetResolution(int width, int height, int refreshRate);
        void SetQualityLevel(int qualityIndex);
        void SetTargetFrameRate(int fps);
        void SetInvertYAxis(bool invert);
        void SetMuteInBackground(bool mute);
        void SetInputBindingOverrides(string json);
        void SetLanguage(string lang);
        void ResetDefaults();
        void SaveSettings();
    }
}
