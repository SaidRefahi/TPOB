using System;
using Game.Core.Interfaces;
using UnityEngine;

namespace Game.Core.Settings
{
    public sealed class SettingsService : ISettingsService
    {
        public const string KeyMasterVol = "TPOB_MasterVol";
        public const string KeySfxVol = "TPOB_SfxVol";
        public const string KeyMusicVol = "TPOB_MusicVol";
        public const string KeyAimSens = "TPOB_AimSens";
        public const string KeyFullscreen = "TPOB_Fullscreen";
        public const string KeyVSync = "TPOB_VSync";
        public const string KeyResWidth = "TPOB_ResWidth";
        public const string KeyResHeight = "TPOB_ResHeight";
        public const string KeyRefreshRate = "TPOB_RefreshRate";
        public const string KeyQualityLevel = "TPOB_QualityLevel";
        public const string KeyTargetFps = "TPOB_TargetFps";
        public const string KeyInvertY = "TPOB_InvertY";
        public const string KeyMuteBg = "TPOB_MuteBg";
        public const string KeyInputOverrides = "TPOB_InputOverrides";
        public const string KeyLanguage = "TPOB_Language";

        private readonly IAudioService _audioService;

        public float MasterVolume { get; private set; } = 1f;
        public float SfxVolume { get; private set; } = 1f;
        public float MusicVolume { get; private set; } = 1f;
        public float AimSensitivity { get; private set; } = 1f;
        public int FullscreenModeIndex { get; private set; } = 1;
        public bool VSyncEnabled { get; private set; } = true;
        public int QualityLevelIndex { get; private set; } = 2;
        public int TargetFrameRate { get; private set; } = -1;
        public bool InvertYAxis { get; private set; }
        public bool MuteInBackground { get; private set; }
        public string InputBindingOverridesJson { get; private set; } = string.Empty;
        public string Language { get; private set; } = "es";

        public event Action OnSettingsChanged;
        public static event Action OnGlobalSettingsChanged;

        private void NotifySettingsChanged()
        {
            if (OnSettingsChanged != null)
            {
                OnSettingsChanged.Invoke();
            }
            if (OnGlobalSettingsChanged != null)
            {
                OnGlobalSettingsChanged.Invoke();
            }
        }

        public SettingsService(IAudioService audioService = null)
        {
            _audioService = audioService;
            LoadSettings();
        }

        private void LoadSettings()
        {
            MasterVolume = PlayerPrefs.GetFloat(KeyMasterVol, 1f);
            SfxVolume = PlayerPrefs.GetFloat(KeySfxVol, 1f);
            MusicVolume = PlayerPrefs.GetFloat(KeyMusicVol, 1f);
            AimSensitivity = PlayerPrefs.GetFloat(KeyAimSens, 1f);
            FullscreenModeIndex = PlayerPrefs.GetInt(KeyFullscreen, 1);
            VSyncEnabled = PlayerPrefs.GetInt(KeyVSync, 1) == 1;

            int defaultQuality = Mathf.Max(0, QualitySettings.GetQualityLevel());
            QualityLevelIndex = PlayerPrefs.GetInt(KeyQualityLevel, defaultQuality);
            TargetFrameRate = PlayerPrefs.GetInt(KeyTargetFps, -1);
            InvertYAxis = PlayerPrefs.GetInt(KeyInvertY, 0) == 1;
            MuteInBackground = PlayerPrefs.GetInt(KeyMuteBg, 0) == 1;
            InputBindingOverridesJson = PlayerPrefs.GetString(KeyInputOverrides, string.Empty);
            int langIdx = PlayerPrefs.GetInt(KeyLanguage, 0);
            Language = langIdx == 1 ? "en" : "es";
            Game.Core.Localization.LocalizationService.SetLanguage(langIdx == 1 ? Game.Core.Localization.AppLanguage.English : Game.Core.Localization.AppLanguage.Spanish);

            ApplyAudio();
            ApplyVSync();
            ApplyQualityLevel();
            ApplyTargetFrameRate();
            ApplyBackgroundBehavior();

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
            if (_audioService != null)
            {
                _audioService.SetMasterVolume(MasterVolume);
            }
            NotifySettingsChanged();
        }

        public void SetSfxVolume(float volume)
        {
            SfxVolume = Mathf.Clamp01(volume);
            if (_audioService != null)
            {
                _audioService.SetSfxVolume(SfxVolume);
            }
            NotifySettingsChanged();
        }

        public void SetMusicVolume(float volume)
        {
            MusicVolume = Mathf.Clamp01(volume);
            if (_audioService != null)
            {
                _audioService.SetMusicVolume(MusicVolume);
            }
            NotifySettingsChanged();
        }

        public void SetAimSensitivity(float sensitivity)
        {
            AimSensitivity = Mathf.Clamp(sensitivity, 0.1f, 5f);
            PlayerPrefs.SetFloat(KeyAimSens, AimSensitivity);
            NotifySettingsChanged();
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
            NotifySettingsChanged();
        }

        public void SetVSync(bool enabled)
        {
            VSyncEnabled = enabled;
            ApplyVSync();
            NotifySettingsChanged();
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
            NotifySettingsChanged();
        }

        public void SetQualityLevel(int qualityIndex)
        {
            int count = QualitySettings.names != null ? QualitySettings.names.Length : 1;
            QualityLevelIndex = Mathf.Clamp(qualityIndex, 0, Mathf.Max(0, count - 1));
            ApplyQualityLevel();
            NotifySettingsChanged();
        }

        public void SetTargetFrameRate(int fps)
        {
            TargetFrameRate = fps <= 0 ? -1 : Mathf.Clamp(fps, 30, 360);
            ApplyTargetFrameRate();
            NotifySettingsChanged();
        }

        public void SetInvertYAxis(bool invert)
        {
            InvertYAxis = invert;
            PlayerPrefs.SetInt(KeyInvertY, InvertYAxis ? 1 : 0);
            NotifySettingsChanged();
        }

        public void SetMuteInBackground(bool mute)
        {
            MuteInBackground = mute;
            ApplyBackgroundBehavior();
            NotifySettingsChanged();
        }

        public void SetInputBindingOverrides(string json)
        {
            InputBindingOverridesJson = json ?? string.Empty;
            PlayerPrefs.SetString(KeyInputOverrides, InputBindingOverridesJson);
            PlayerPrefs.Save();
            NotifySettingsChanged();
        }

        public void SetLanguage(string lang)
        {
            Language = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "es";
            PlayerPrefs.SetInt(KeyLanguage, Language == "en" ? 1 : 0);
            PlayerPrefs.Save();
            Game.Core.Localization.LocalizationService.SetLanguage(Language == "en" ? Game.Core.Localization.AppLanguage.English : Game.Core.Localization.AppLanguage.Spanish);
            NotifySettingsChanged();
        }

        public void ResetDefaults()
        {
            MasterVolume = 1f;
            SfxVolume = 1f;
            MusicVolume = 1f;
            AimSensitivity = 1f;
            FullscreenModeIndex = 1;
            VSyncEnabled = true;
            int maxQuality = QualitySettings.names != null && QualitySettings.names.Length > 0
                ? QualitySettings.names.Length - 1
                : 0;
            QualityLevelIndex = maxQuality;
            TargetFrameRate = -1;
            InvertYAxis = false;
            MuteInBackground = false;
            InputBindingOverridesJson = string.Empty;

            ApplyAudio();
            SetFullscreenMode(FullscreenModeIndex);
            ApplyVSync();
            ApplyQualityLevel();
            ApplyTargetFrameRate();
            ApplyBackgroundBehavior();
            SaveSettings();

            NotifySettingsChanged();
        }

        public void SaveSettings()
        {
            PlayerPrefs.SetFloat(KeyMasterVol, MasterVolume);
            PlayerPrefs.SetFloat(KeySfxVol, SfxVolume);
            PlayerPrefs.SetFloat(KeyMusicVol, MusicVolume);
            PlayerPrefs.SetFloat(KeyAimSens, AimSensitivity);
            PlayerPrefs.SetInt(KeyFullscreen, FullscreenModeIndex);
            PlayerPrefs.SetInt(KeyVSync, VSyncEnabled ? 1 : 0);
            PlayerPrefs.SetInt(KeyQualityLevel, QualityLevelIndex);
            PlayerPrefs.SetInt(KeyTargetFps, TargetFrameRate);
            PlayerPrefs.SetInt(KeyInvertY, InvertYAxis ? 1 : 0);
            PlayerPrefs.SetInt(KeyMuteBg, MuteInBackground ? 1 : 0);
            PlayerPrefs.SetInt(KeyLanguage, Language == "en" ? 1 : 0);
            PlayerPrefs.SetString(KeyInputOverrides, InputBindingOverridesJson ?? string.Empty);
            PlayerPrefs.Save();
        }

        private void ApplyAudio()
        {
            if (_audioService != null)
            {
                _audioService.SetMasterVolume(MasterVolume);
                _audioService.SetSfxVolume(SfxVolume);
                _audioService.SetMusicVolume(MusicVolume);
            }
        }

        private void ApplyVSync()
        {
            QualitySettings.vSyncCount = VSyncEnabled ? 1 : 0;
        }

        private void ApplyQualityLevel()
        {
            int count = QualitySettings.names != null ? QualitySettings.names.Length : 0;
            if (count > 0 && QualityLevelIndex >= 0 && QualityLevelIndex < count)
            {
                QualitySettings.SetQualityLevel(QualityLevelIndex, true);
            }
        }

        private void ApplyTargetFrameRate()
        {
            Application.targetFrameRate = TargetFrameRate;
        }

        private void ApplyBackgroundBehavior()
        {
            Application.runInBackground = !MuteInBackground;
        }
    }
}
