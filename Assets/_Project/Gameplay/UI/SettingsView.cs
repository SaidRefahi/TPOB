using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Gameplay.UI
{
    [DisallowMultipleComponent]
    public sealed class SettingsView : MonoBehaviour
    {
        [Header("Navigation Tabs")]
        [SerializeField] private Button _graphicsTabButton;
        [SerializeField] private Button _audioTabButton;
        [SerializeField] private Button _controlsTabButton;
        [SerializeField] private GameObject _graphicsPanel;
        [SerializeField] private GameObject _audioPanel;
        [SerializeField] private GameObject _controlsPanel;
        [SerializeField] private Graphic _graphicsTabIndicator;
        [SerializeField] private Graphic _audioTabIndicator;
        [SerializeField] private Graphic _controlsTabIndicator;

        [Header("Audio")]
        [SerializeField] private Slider _masterVolumeSlider;
        [SerializeField] private TextMeshProUGUI _masterVolumeValueText;
        [SerializeField] private Slider _sfxVolumeSlider;
        [SerializeField] private TextMeshProUGUI _sfxVolumeValueText;
        [SerializeField] private Slider _musicVolumeSlider;
        [SerializeField] private TextMeshProUGUI _musicVolumeValueText;
        [SerializeField] private Toggle _muteInBackgroundToggle;

        [Header("Display / Video")]
        [SerializeField] private TMP_Dropdown _resolutionDropdown;
        [SerializeField] private TMP_Dropdown _fullscreenModeDropdown;
        [SerializeField] private TMP_Dropdown _qualityDropdown;
        [SerializeField] private TMP_Dropdown _fpsLimitDropdown;
        [SerializeField] private Toggle _vsyncToggle;

        [Header("Controls & Rebinding")]
        [SerializeField] private Slider _sensitivitySlider;
        [SerializeField] private TextMeshProUGUI _sensitivityValueText;
        [SerializeField] private Toggle _invertYToggle;
        [SerializeField] private InputActionAsset _inputActionsAsset;
        [SerializeField] private InputRebindRowUI[] _rebindRows;

        [Header("Footer Actions")]
        [SerializeField] private Button _resetDefaultsButton;
        [SerializeField] private Button _closeButton;

        public Button GraphicsTabButton => _graphicsTabButton;
        public Button AudioTabButton => _audioTabButton;
        public Button ControlsTabButton => _controlsTabButton;
        public GameObject GraphicsPanel => _graphicsPanel;
        public GameObject AudioPanel => _audioPanel;
        public GameObject ControlsPanel => _controlsPanel;
        public Graphic GraphicsTabIndicator => _graphicsTabIndicator;
        public Graphic AudioTabIndicator => _audioTabIndicator;
        public Graphic ControlsTabIndicator => _controlsTabIndicator;

        public Slider MasterVolumeSlider => _masterVolumeSlider;
        public TextMeshProUGUI MasterVolumeValueText => _masterVolumeValueText;
        public Slider SfxVolumeSlider => _sfxVolumeSlider;
        public TextMeshProUGUI SfxVolumeValueText => _sfxVolumeValueText;
        public Slider MusicVolumeSlider => _musicVolumeSlider;
        public TextMeshProUGUI MusicVolumeValueText => _musicVolumeValueText;
        public Toggle MuteInBackgroundToggle => _muteInBackgroundToggle;

        public TMP_Dropdown ResolutionDropdown => _resolutionDropdown;
        public TMP_Dropdown FullscreenModeDropdown => _fullscreenModeDropdown;
        public TMP_Dropdown QualityDropdown => _qualityDropdown;
        public TMP_Dropdown FpsLimitDropdown => _fpsLimitDropdown;
        public Toggle VSyncToggle => _vsyncToggle;

        public Slider SensitivitySlider => _sensitivitySlider;
        public TextMeshProUGUI SensitivityValueText => _sensitivityValueText;
        public Toggle InvertYToggle => _invertYToggle;
        public InputActionAsset InputActionsAsset => _inputActionsAsset;
        public InputRebindRowUI[] RebindRows => _rebindRows;

        public Button ResetDefaultsButton => _resetDefaultsButton;
        public Button CloseButton => _closeButton;

        public void SetActive(bool active) => gameObject.SetActive(active);
    }
}
