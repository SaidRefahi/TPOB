using System.Collections.Generic;
using Game.Core.Interfaces;
using Game.Core.Settings;
using Game.Gameplay.Player.Legs;
using Game.Network.Scopes;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Game.Gameplay.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SettingsView))]
    public sealed class SettingsPresenter : MonoBehaviour
    {
        private static readonly int[] FpsValues = { 30, 60, 120, 144, -1 };
        private static readonly Color ActiveTabColor = new Color(0f, 0.94f, 1f, 1f);
        private static readonly Color InactiveTabColor = new Color(0.18f, 0.25f, 0.34f, 0.55f);

        private SettingsView _view;
        private ISettingsService _settingsService;
        private readonly List<Resolution> _filteredResolutions = new List<Resolution>(16);

        [Inject]
        public void Construct(ISettingsService settingsService = null)
        {
            _settingsService = settingsService;
        }

        private void Awake()
        {
            _view = GetComponent<SettingsView>();
        }

        private void Start()
        {
            ResolveSettingsService();
            InitializeDisplayOptions();
            InitializeRebindRows();
            ApplyCurrentValuesToUI();
            ShowTab(0);
            SubscribeEvents();
        }

        private void OnEnable()
        {
            if (_view != null && _settingsService != null)
            {
                ApplyCurrentValuesToUI();
                RefreshAllRebindRows();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void ResolveSettingsService()
        {
            if (_settingsService != null)
            {
                return;
            }

            var scope = GameLifetimeScope.Instance;
            if (scope != null && scope.Container != null)
            {
                try
                {
                    _settingsService = scope.Container.Resolve<ISettingsService>();
                }
                catch
                {
                    // Fallback to standalone instance if container does not have it registered yet
                }
            }

            if (_settingsService == null)
            {
                _settingsService = new SettingsService();
            }
        }

        private void InitializeDisplayOptions()
        {
            if (_view.FullscreenModeDropdown != null)
            {
                _view.FullscreenModeDropdown.ClearOptions();
                _view.FullscreenModeDropdown.AddOptions(new List<string>
                {
                    "Pantalla Completa Exclusiva",
                    "Ventana Sin Bordes (Borderless)",
                    "Ventana (Windowed)"
                });
            }

            if (_view.QualityDropdown != null)
            {
                _view.QualityDropdown.ClearOptions();
                string[] qualityNames = QualitySettings.names;
                var qList = new List<string>(qualityNames != null ? qualityNames.Length : 3);
                if (qualityNames != null && qualityNames.Length > 0)
                {
                    for (int i = 0; i < qualityNames.Length; i++)
                    {
                        qList.Add(FormatQualityName(qualityNames[i]));
                    }
                }
                else
                {
                    qList.Add("Baja (Performant)");
                    qList.Add("Media (Balanced)");
                    qList.Add("Ultra (High Fidelity)");
                }
                _view.QualityDropdown.AddOptions(qList);
            }

            if (_view.FpsLimitDropdown != null)
            {
                _view.FpsLimitDropdown.ClearOptions();
                _view.FpsLimitDropdown.AddOptions(new List<string>
                {
                    "30 FPS",
                    "60 FPS",
                    "120 FPS",
                    "144 FPS",
                    "Ilimitado (Sin Límite)"
                });
            }

            if (_view.ResolutionDropdown != null)
            {
                _view.ResolutionDropdown.ClearOptions();
                _filteredResolutions.Clear();

                Resolution[] allResolutions = Screen.resolutions;
                var options = new List<string>(allResolutions.Length);
                int currentResIndex = 0;

                for (int i = 0; i < allResolutions.Length; i++)
                {
                    Resolution res = allResolutions[i];
                    bool duplicate = false;
                    for (int j = 0; j < _filteredResolutions.Count; j++)
                    {
                        if (_filteredResolutions[j].width == res.width && _filteredResolutions[j].height == res.height)
                        {
                            duplicate = true;
                            break;
                        }
                    }

                    if (!duplicate)
                    {
                        _filteredResolutions.Add(res);
                        options.Add($"{res.width} x {res.height}");

                        if (res.width == Screen.width && res.height == Screen.height)
                        {
                            currentResIndex = _filteredResolutions.Count - 1;
                        }
                    }
                }

                if (options.Count == 0)
                {
                    options.Add($"{Screen.width} x {Screen.height}");
                }

                _view.ResolutionDropdown.AddOptions(options);
                _view.ResolutionDropdown.SetValueWithoutNotify(currentResIndex);
                _view.ResolutionDropdown.RefreshShownValue();
            }
        }

        private static string FormatQualityName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "Estándar";
            string lower = rawName.ToLowerInvariant();
            if (lower.Contains("performant") || lower.Contains("low")) return "Baja — Rendimiento (" + rawName + ")";
            if (lower.Contains("balanced") || lower.Contains("medium")) return "Media — Equilibrada (" + rawName + ")";
            if (lower.Contains("high") || lower.Contains("ultra")) return "Alta — Fidelidad URP (" + rawName + ")";
            return rawName;
        }

        private void InitializeRebindRows()
        {
            InputActionAsset asset = _view.InputActionsAsset;
            if (asset != null)
            {
                LegsInputReader.EnsureFuseActionAndLoadOverrides(asset);
            }

            BindRowArray(_view.RebindRows, asset);
            BindRowArray(_view.Player2RebindRows, asset);
        }

        private void BindRowArray(InputRebindRowUI[] rows, InputActionAsset asset)
        {
            if (rows == null) return;
            for (int i = 0; i < rows.Length; i++)
            {
                InputRebindRowUI row = rows[i];
                if (row != null)
                {
                    if (asset != null)
                    {
                        row.BindToAsset(asset);
                    }
                    row.OnBindingUpdated += HandleRowBindingUpdated;
                }
            }
        }

        private void RefreshAllRebindRows()
        {
            RefreshRowArray(_view.RebindRows);
            RefreshRowArray(_view.Player2RebindRows);
        }

        private static void RefreshRowArray(InputRebindRowUI[] rows)
        {
            if (rows == null) return;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] != null)
                {
                    rows[i].RefreshDisplay();
                }
            }
        }

        private void ApplyCurrentValuesToUI()
        {
            if (_settingsService == null)
            {
                return;
            }

            if (_view.MasterVolumeSlider != null)
            {
                _view.MasterVolumeSlider.SetValueWithoutNotify(_settingsService.MasterVolume);
                UpdateMasterVolumeLabel(_settingsService.MasterVolume);
            }

            if (_view.SfxVolumeSlider != null)
            {
                _view.SfxVolumeSlider.SetValueWithoutNotify(_settingsService.SfxVolume);
                UpdateSfxVolumeLabel(_settingsService.SfxVolume);
            }

            if (_view.MusicVolumeSlider != null)
            {
                _view.MusicVolumeSlider.SetValueWithoutNotify(_settingsService.MusicVolume);
                UpdateMusicVolumeLabel(_settingsService.MusicVolume);
            }

            if (_view.MuteInBackgroundToggle != null)
            {
                _view.MuteInBackgroundToggle.SetIsOnWithoutNotify(_settingsService.MuteInBackground);
            }

            if (_view.SensitivitySlider != null)
            {
                _view.SensitivitySlider.SetValueWithoutNotify(_settingsService.AimSensitivity);
                UpdateSensitivityLabel(_settingsService.AimSensitivity);
            }

            if (_view.InvertYToggle != null)
            {
                _view.InvertYToggle.SetIsOnWithoutNotify(_settingsService.InvertYAxis);
            }

            if (_view.FullscreenModeDropdown != null)
            {
                _view.FullscreenModeDropdown.SetValueWithoutNotify(_settingsService.FullscreenModeIndex);
                _view.FullscreenModeDropdown.RefreshShownValue();
            }

            if (_view.QualityDropdown != null)
            {
                _view.QualityDropdown.SetValueWithoutNotify(_settingsService.QualityLevelIndex);
                _view.QualityDropdown.RefreshShownValue();
            }

            if (_view.FpsLimitDropdown != null)
            {
                int fpsIndex = MapFpsToDropdownIndex(_settingsService.TargetFrameRate);
                _view.FpsLimitDropdown.SetValueWithoutNotify(fpsIndex);
                _view.FpsLimitDropdown.RefreshShownValue();
            }

            if (_view.VSyncToggle != null)
            {
                _view.VSyncToggle.SetIsOnWithoutNotify(_settingsService.VSyncEnabled);
            }
        }

        private static int MapFpsToDropdownIndex(int targetFps)
        {
            for (int i = 0; i < FpsValues.Length; i++)
            {
                if (FpsValues[i] == targetFps)
                {
                    return i;
                }
            }
            return FpsValues.Length - 1;
        }

        private void SubscribeEvents()
        {
            if (_view.GraphicsTabButton != null)
                _view.GraphicsTabButton.onClick.AddListener(HandleGraphicsTabClicked);

            if (_view.AudioTabButton != null)
                _view.AudioTabButton.onClick.AddListener(HandleAudioTabClicked);

            if (_view.ControlsTabButton != null)
                _view.ControlsTabButton.onClick.AddListener(HandleControlsTabClicked);

            if (_view.MasterVolumeSlider != null)
                _view.MasterVolumeSlider.onValueChanged.AddListener(HandleMasterVolumeChanged);

            if (_view.SfxVolumeSlider != null)
                _view.SfxVolumeSlider.onValueChanged.AddListener(HandleSfxVolumeChanged);

            if (_view.MusicVolumeSlider != null)
                _view.MusicVolumeSlider.onValueChanged.AddListener(HandleMusicVolumeChanged);

            if (_view.MuteInBackgroundToggle != null)
                _view.MuteInBackgroundToggle.onValueChanged.AddListener(HandleMuteInBackgroundChanged);

            if (_view.SensitivitySlider != null)
                _view.SensitivitySlider.onValueChanged.AddListener(HandleSensitivityChanged);

            if (_view.InvertYToggle != null)
                _view.InvertYToggle.onValueChanged.AddListener(HandleInvertYChanged);

            if (_view.FullscreenModeDropdown != null)
                _view.FullscreenModeDropdown.onValueChanged.AddListener(HandleFullscreenModeChanged);

            if (_view.ResolutionDropdown != null)
                _view.ResolutionDropdown.onValueChanged.AddListener(HandleResolutionChanged);

            if (_view.QualityDropdown != null)
                _view.QualityDropdown.onValueChanged.AddListener(HandleQualityChanged);

            if (_view.FpsLimitDropdown != null)
                _view.FpsLimitDropdown.onValueChanged.AddListener(HandleFpsLimitChanged);

            if (_view.VSyncToggle != null)
                _view.VSyncToggle.onValueChanged.AddListener(HandleVSyncChanged);

            if (_view.ResetDefaultsButton != null)
                _view.ResetDefaultsButton.onClick.AddListener(HandleResetDefaultsClicked);

            if (_view.CloseButton != null)
                _view.CloseButton.onClick.AddListener(HandleCloseClicked);

            if (_view.Player1ControlsTabButton != null)
                _view.Player1ControlsTabButton.onClick.AddListener(HandlePlayer1ControlsTabClicked);

            if (_view.Player2ControlsTabButton != null)
                _view.Player2ControlsTabButton.onClick.AddListener(HandlePlayer2ControlsTabClicked);
        }

        private void UnsubscribeEvents()
        {
            if (_view == null)
            {
                return;
            }

            if (_view.GraphicsTabButton != null)
                _view.GraphicsTabButton.onClick.RemoveListener(HandleGraphicsTabClicked);

            if (_view.AudioTabButton != null)
                _view.AudioTabButton.onClick.RemoveListener(HandleAudioTabClicked);

            if (_view.ControlsTabButton != null)
                _view.ControlsTabButton.onClick.RemoveListener(HandleControlsTabClicked);

            if (_view.Player1ControlsTabButton != null)
                _view.Player1ControlsTabButton.onClick.RemoveListener(HandlePlayer1ControlsTabClicked);

            if (_view.Player2ControlsTabButton != null)
                _view.Player2ControlsTabButton.onClick.RemoveListener(HandlePlayer2ControlsTabClicked);

            if (_view.MasterVolumeSlider != null)
                _view.MasterVolumeSlider.onValueChanged.RemoveListener(HandleMasterVolumeChanged);

            if (_view.SfxVolumeSlider != null)
                _view.SfxVolumeSlider.onValueChanged.RemoveListener(HandleSfxVolumeChanged);

            if (_view.MusicVolumeSlider != null)
                _view.MusicVolumeSlider.onValueChanged.RemoveListener(HandleMusicVolumeChanged);

            if (_view.MuteInBackgroundToggle != null)
                _view.MuteInBackgroundToggle.onValueChanged.RemoveListener(HandleMuteInBackgroundChanged);

            if (_view.SensitivitySlider != null)
                _view.SensitivitySlider.onValueChanged.RemoveListener(HandleSensitivityChanged);

            if (_view.InvertYToggle != null)
                _view.InvertYToggle.onValueChanged.RemoveListener(HandleInvertYChanged);

            if (_view.FullscreenModeDropdown != null)
                _view.FullscreenModeDropdown.onValueChanged.RemoveListener(HandleFullscreenModeChanged);

            if (_view.ResolutionDropdown != null)
                _view.ResolutionDropdown.onValueChanged.RemoveListener(HandleResolutionChanged);

            if (_view.QualityDropdown != null)
                _view.QualityDropdown.onValueChanged.RemoveListener(HandleQualityChanged);

            if (_view.FpsLimitDropdown != null)
                _view.FpsLimitDropdown.onValueChanged.RemoveListener(HandleFpsLimitChanged);

            if (_view.VSyncToggle != null)
                _view.VSyncToggle.onValueChanged.RemoveListener(HandleVSyncChanged);

            if (_view.ResetDefaultsButton != null)
                _view.ResetDefaultsButton.onClick.RemoveListener(HandleResetDefaultsClicked);

            if (_view.CloseButton != null)
                _view.CloseButton.onClick.RemoveListener(HandleCloseClicked);

            UnsubscribeRowArray(_view.RebindRows);
            UnsubscribeRowArray(_view.Player2RebindRows);
        }

        private void UnsubscribeRowArray(InputRebindRowUI[] rows)
        {
            if (rows == null) return;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] != null)
                {
                    rows[i].OnBindingUpdated -= HandleRowBindingUpdated;
                }
            }
        }

        private void HandleGraphicsTabClicked() => ShowTab(0);
        private void HandleAudioTabClicked() => ShowTab(1);
        private void HandleControlsTabClicked() => ShowTab(2);

        private void ShowTab(int tabIndex)
        {
            if (_view.GraphicsPanel != null) _view.GraphicsPanel.SetActive(tabIndex == 0);
            if (_view.AudioPanel != null) _view.AudioPanel.SetActive(tabIndex == 1);
            if (_view.ControlsPanel != null) _view.ControlsPanel.SetActive(tabIndex == 2);

            if (_view.GraphicsTabIndicator != null)
                _view.GraphicsTabIndicator.color = tabIndex == 0 ? ActiveTabColor : InactiveTabColor;

            if (_view.AudioTabIndicator != null)
                _view.AudioTabIndicator.color = tabIndex == 1 ? ActiveTabColor : InactiveTabColor;

            if (_view.ControlsTabIndicator != null)
                _view.ControlsTabIndicator.color = tabIndex == 2 ? ActiveTabColor : InactiveTabColor;

            if (tabIndex == 2)
            {
                ShowControlsPlayerTab(0);
            }
        }

        private void ShowControlsPlayerTab(int playerIndex)
        {
            if (_view.Player1ControlsContainer != null)
                _view.Player1ControlsContainer.SetActive(playerIndex == 0);

            if (_view.Player2ControlsContainer != null)
                _view.Player2ControlsContainer.SetActive(playerIndex == 1);

            if (_view.Player1ControlsIndicator != null)
                _view.Player1ControlsIndicator.color = playerIndex == 0 ? ActiveTabColor : InactiveTabColor;

            if (_view.Player2ControlsIndicator != null)
                _view.Player2ControlsIndicator.color = playerIndex == 1 ? ActiveTabColor : InactiveTabColor;
        }

        private void HandlePlayer1ControlsTabClicked() => ShowControlsPlayerTab(0);
        private void HandlePlayer2ControlsTabClicked() => ShowControlsPlayerTab(1);

        private void HandleRowBindingUpdated()
        {
            InputActionAsset asset = _view.InputActionsAsset;
            if (asset != null && _settingsService != null)
            {
                string json = asset.SaveBindingOverridesAsJson();
                _settingsService.SetInputBindingOverrides(json);
            }
            RefreshAllRebindRows();
        }

        private void HandleMasterVolumeChanged(float value)
        {
            if (_settingsService != null) _settingsService.SetMasterVolume(value);
            UpdateMasterVolumeLabel(value);
        }

        private void HandleSfxVolumeChanged(float value)
        {
            if (_settingsService != null) _settingsService.SetSfxVolume(value);
            UpdateSfxVolumeLabel(value);
        }

        private void HandleMusicVolumeChanged(float value)
        {
            if (_settingsService != null) _settingsService.SetMusicVolume(value);
            UpdateMusicVolumeLabel(value);
        }

        private void HandleMuteInBackgroundChanged(bool mute)
        {
            if (_settingsService != null) _settingsService.SetMuteInBackground(mute);
        }

        private void HandleSensitivityChanged(float value)
        {
            if (_settingsService != null) _settingsService.SetAimSensitivity(value);
            UpdateSensitivityLabel(value);
        }

        private void HandleInvertYChanged(bool invert)
        {
            if (_settingsService != null) _settingsService.SetInvertYAxis(invert);
        }

        private void HandleFullscreenModeChanged(int modeIndex)
        {
            if (_settingsService != null) _settingsService.SetFullscreenMode(modeIndex);
        }

        private void HandleResolutionChanged(int index)
        {
            if (index >= 0 && index < _filteredResolutions.Count && _settingsService != null)
            {
                Resolution target = _filteredResolutions[index];
                _settingsService.SetResolution(target.width, target.height, (int)target.refreshRateRatio.value);
            }
        }

        private void HandleQualityChanged(int qualityIndex)
        {
            if (_settingsService != null) _settingsService.SetQualityLevel(qualityIndex);
        }

        private void HandleFpsLimitChanged(int dropdownIndex)
        {
            if (dropdownIndex >= 0 && dropdownIndex < FpsValues.Length && _settingsService != null)
            {
                _settingsService.SetTargetFrameRate(FpsValues[dropdownIndex]);
            }
        }

        private void HandleVSyncChanged(bool enabled)
        {
            if (_settingsService != null) _settingsService.SetVSync(enabled);
        }

        private void HandleResetDefaultsClicked()
        {
            InputActionAsset asset = _view.InputActionsAsset;
            if (asset != null)
            {
                asset.RemoveAllBindingOverrides();
            }

            if (_settingsService != null)
            {
                _settingsService.ResetDefaults();
            }

            ApplyCurrentValuesToUI();
            RefreshAllRebindRows();
        }

        private void HandleCloseClicked()
        {
            if (_settingsService != null)
            {
                _settingsService.SaveSettings();
            }
            _view.SetActive(false);
        }

        private void UpdateMasterVolumeLabel(float volume)
        {
            if (_view.MasterVolumeValueText != null)
            {
                _view.MasterVolumeValueText.text = $"{Mathf.RoundToInt(volume * 100)}%";
            }
        }

        private void UpdateSfxVolumeLabel(float volume)
        {
            if (_view.SfxVolumeValueText != null)
            {
                _view.SfxVolumeValueText.text = $"{Mathf.RoundToInt(volume * 100)}%";
            }
        }

        private void UpdateMusicVolumeLabel(float volume)
        {
            if (_view.MusicVolumeValueText != null)
            {
                _view.MusicVolumeValueText.text = $"{Mathf.RoundToInt(volume * 100)}%";
            }
        }

        private void UpdateSensitivityLabel(float sensitivity)
        {
            if (_view.SensitivityValueText != null)
            {
                _view.SensitivityValueText.text = $"{sensitivity:0.0}x";
            }
        }
    }
}
