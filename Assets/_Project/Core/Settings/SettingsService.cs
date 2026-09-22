using System;
using Game.Core.Interfaces;
using UnityEngine;

namespace Game.Core.Settings
{
    public sealed class SettingsService : ISettingsService
    {
        private const string KeyMasterVol = "TPOB_MasterVol";
        private const string KeySfxVol = "TPOB_SfxVol";
        private const string KeyAimSens = "TPOB_AimSens";
        private const string KeyFullscreen = "TPOB_Fullscreen";
        private const string KeyVSync = "TPOB_VSync";
        private const string KeyResWidth = "TPOB_ResWidth";
        private const string KeyResHeight = "TPOB_ResHeight";
        private const string KeyRefreshRate = "TPOB_RefreshRate";

        private readonly IAudioService _audioService;

        public float MasterVolume { get; private set; } = 1f;
        public float SfxVolume { get; private set; } = 1f;
        public float AimSensitivity { get; private set; } = 1f;
        public int FullscreenModeIndex { get; private set; } = 1; // Default Borderless/FullScreenWindow
        public bool VSyncEnabled { get; private set; } = true;

        public event Action OnSettingsChanged;

        public SettingsService(IAudioService audioService = null)
        {
            _audioService = audioService;
            LoadSettings();
        }

        private void LoadSettings()
        {
            MasterVolume = PlayerPrefs.GetFloat(KeyMasterVol, 1f);
            SfxVolume = PlayerPrefs.GetFloat(KeySfxVol, 1f);
            AimSensitivity = PlayerPrefs.GetFloat(KeyAimSens, 1f);
            FullscreenModeIndex = PlayerPrefs.GetInt(KeyFullscreen, 1);
            VSyncEnabled = PlayerPrefs.GetInt(KeyVSync, 1) == 1;

            ApplyAudio();
            ApplyVSync();

            int savedW = PlayerPrefs.GetInt(KeyResWidth, -1);
            int savedH = PlayerPrefs.GetInt(KeyResHeight, -1);
            int savedHz = PlayerPrefs.GetInt(KeyRefreshRate, 60);

            if (savedW > 0 && savedH > 0)
            {
                SetResolution(savedW, savedH, savedHz);
            }
        }

        public void SetMasterVolume(float volume)
        {
            MasterVolume = Mathf.Clamp01(volume);
            _audioService?.SetMasterVolume(MasterVolume);
            OnSettingsChanged?.Invoke();
        }

        public void SetSfxVolume(float volume)
        {
            SfxVolume = Mathf.Clamp01(volume);
            _audioService?.SetSfxVolume(SfxVolume);
            OnSettingsChanged?.Invoke();
        }

        public void SetAimSensitivity(float sensitivity)
        {
            AimSensitivity = Mathf.Clamp(sensitivity, 0.1f, 5f);
            OnSettingsChanged?.Invoke();
        }

        public void SetFullscreenMode(int modeIndex)
        {
            FullscreenModeIndex = Mathf.Clamp(modeIndex, 0, 2);
            FullScreenMode mode = FullscreenModeIndex switch
            {
                0 => FullScreenMode.ExclusiveFullScreen,
                1 => FullScreenMode.FullScreenWindow,
                2 => FullScreenMode.Windowed,
                _ => FullScreenMode.FullScreenWindow
            };

            Screen.fullScreenMode = mode;
            OnSettingsChanged?.Invoke();
        }

        public void SetVSync(bool enabled)
        {
            VSyncEnabled = enabled;
            ApplyVSync();
            OnSettingsChanged?.Invoke();
        }

        public void SetResolution(int width, int height, int refreshRate)
        {
            FullScreenMode mode = FullscreenModeIndex switch
            {
                0 => FullScreenMode.ExclusiveFullScreen,
                1 => FullScreenMode.FullScreenWindow,
                2 => FullScreenMode.Windowed,
                _ => FullScreenMode.FullScreenWindow
            };

            var refresh = new RefreshRate { numerator = (uint)Mathf.Max(30, refreshRate), denominator = 1 };
            Screen.SetResolution(width, height, mode, refresh);

            PlayerPrefs.SetInt(KeyResWidth, width);
            PlayerPrefs.SetInt(KeyResHeight, height);
            PlayerPrefs.SetInt(KeyRefreshRate, refreshRate);
            OnSettingsChanged?.Invoke();
        }

        public void SaveSettings()
        {
            PlayerPrefs.SetFloat(KeyMasterVol, MasterVolume);
            PlayerPrefs.SetFloat(KeySfxVol, SfxVolume);
            PlayerPrefs.SetFloat(KeyAimSens, AimSensitivity);
            PlayerPrefs.SetInt(KeyFullscreen, FullscreenModeIndex);
            PlayerPrefs.SetInt(KeyVSync, VSyncEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void ApplyAudio()
        {
            _audioService?.SetMasterVolume(MasterVolume);
            _audioService?.SetSfxVolume(SfxVolume);
        }

        private void ApplyVSync()
        {
            QualitySettings.vSyncCount = VSyncEnabled ? 1 : 0;
        }
    }
}
