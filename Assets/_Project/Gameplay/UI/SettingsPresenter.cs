using System.Collections.Generic;
using Game.Core.Interfaces;
using TMPro;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Gameplay.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SettingsView))]
    public sealed class SettingsPresenter : MonoBehaviour
    {
        private SettingsView _view;
        private ISettingsService _settingsService;
        private readonly List<Resolution> _filteredResolutions = new(16);

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
            ApplyCurrentValuesToUI();
            SubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void ResolveSettingsService()
        {
            if (_settingsService != null) return;

            var scopes = Object.FindObjectsByType<LifetimeScope>(FindObjectsSortMode.None);
            for (int i = 0; i < scopes.Length; i++)
            {
                if (scopes[i] != null && scopes[i].Container != null)
                {
                    try
                    {
                        _settingsService = scopes[i].Container.Resolve<ISettingsService>();
                        if (_settingsService != null) break;
                    }
                    catch { }
                }
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

            if (_view.ResolutionDropdown != null)
            {
                _view.ResolutionDropdown.ClearOptions();
                _filteredResolutions.Clear();

                Resolution[] allResolutions = Screen.resolutions;
                var options = new List<string>(allResolutions.Length);
                int currentResIndex = 0;

                for (int i = 0; i < allResolutions.Length; i++)
                {
                    var res = allResolutions[i];
                    // Filter duplicates with identical width & height
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

                _view.ResolutionDropdown.AddOptions(options);
                _view.ResolutionDropdown.value = currentResIndex;
                _view.ResolutionDropdown.RefreshShownValue();
            }
        }

        private void ApplyCurrentValuesToUI()
        {
            if (_settingsService == null) return;

            if (_view.MasterVolumeSlider != null)
            {
                _view.MasterVolumeSlider.value = _settingsService.MasterVolume;
                UpdateMasterVolumeLabel(_settingsService.MasterVolume);
            }

            if (_view.SfxVolumeSlider != null)
            {
                _view.SfxVolumeSlider.value = _settingsService.SfxVolume;
                UpdateSfxVolumeLabel(_settingsService.SfxVolume);
            }

            if (_view.SensitivitySlider != null)
            {
                _view.SensitivitySlider.value = _settingsService.AimSensitivity;
                UpdateSensitivityLabel(_settingsService.AimSensitivity);
            }

            if (_view.FullscreenModeDropdown != null)
            {
                _view.FullscreenModeDropdown.value = _settingsService.FullscreenModeIndex;
                _view.FullscreenModeDropdown.RefreshShownValue();
            }

            if (_view.VSyncToggle != null)
            {
                _view.VSyncToggle.isOn = _settingsService.VSyncEnabled;
            }
        }

        private void SubscribeEvents()
        {
            if (_view.MasterVolumeSlider != null)
                _view.MasterVolumeSlider.onValueChanged.AddListener(HandleMasterVolumeChanged);

            if (_view.SfxVolumeSlider != null)
                _view.SfxVolumeSlider.onValueChanged.AddListener(HandleSfxVolumeChanged);

            if (_view.SensitivitySlider != null)
                _view.SensitivitySlider.onValueChanged.AddListener(HandleSensitivityChanged);

            if (_view.FullscreenModeDropdown != null)
                _view.FullscreenModeDropdown.onValueChanged.AddListener(HandleFullscreenModeChanged);

            if (_view.ResolutionDropdown != null)
                _view.ResolutionDropdown.onValueChanged.AddListener(HandleResolutionChanged);

            if (_view.VSyncToggle != null)
                _view.VSyncToggle.onValueChanged.AddListener(HandleVSyncChanged);

            if (_view.CloseButton != null)
                _view.CloseButton.onClick.AddListener(HandleCloseClicked);
        }

        private void UnsubscribeEvents()
        {
            if (_view.MasterVolumeSlider != null)
                _view.MasterVolumeSlider.onValueChanged.RemoveListener(HandleMasterVolumeChanged);

            if (_view.SfxVolumeSlider != null)
                _view.SfxVolumeSlider.onValueChanged.RemoveListener(HandleSfxVolumeChanged);

            if (_view.SensitivitySlider != null)
                _view.SensitivitySlider.onValueChanged.RemoveListener(HandleSensitivityChanged);

            if (_view.FullscreenModeDropdown != null)
                _view.FullscreenModeDropdown.onValueChanged.RemoveListener(HandleFullscreenModeChanged);

            if (_view.ResolutionDropdown != null)
                _view.ResolutionDropdown.onValueChanged.RemoveListener(HandleResolutionChanged);

            if (_view.VSyncToggle != null)
                _view.VSyncToggle.onValueChanged.RemoveListener(HandleVSyncChanged);

            if (_view.CloseButton != null)
                _view.CloseButton.onClick.RemoveListener(HandleCloseClicked);
        }

        private void HandleMasterVolumeChanged(float value)
        {
            _settingsService?.SetMasterVolume(value);
            UpdateMasterVolumeLabel(value);
        }

        private void HandleSfxVolumeChanged(float value)
        {
            _settingsService?.SetSfxVolume(value);
            UpdateSfxVolumeLabel(value);
        }

        private void HandleSensitivityChanged(float value)
        {
            _settingsService?.SetAimSensitivity(value);
            UpdateSensitivityLabel(value);
        }

        private void HandleFullscreenModeChanged(int modeIndex)
        {
            _settingsService?.SetFullscreenMode(modeIndex);
        }

        private void HandleResolutionChanged(int index)
        {
            if (index >= 0 && index < _filteredResolutions.Count)
            {
                var target = _filteredResolutions[index];
                _settingsService?.SetResolution(target.width, target.height, (int)target.refreshRateRatio.value);
            }
        }

        private void HandleVSyncChanged(bool enabled)
        {
            _settingsService?.SetVSync(enabled);
        }

        private void HandleCloseClicked()
        {
            _settingsService?.SaveSettings();
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

        private void UpdateSensitivityLabel(float sensitivity)
        {
            if (_view.SensitivityValueText != null)
            {
                _view.SensitivityValueText.text = $"{sensitivity:0.0}x";
            }
        }
    }
}
