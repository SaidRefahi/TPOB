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

        [Header("Player Sub-Tabs (Controls)")]
        [SerializeField] private Button _player1ControlsTabButton;
        [SerializeField] private Button _player2ControlsTabButton;
        [SerializeField] private Graphic _player1ControlsIndicator;
        [SerializeField] private Graphic _player2ControlsIndicator;
        [SerializeField] private GameObject _player1ControlsContainer;
        [SerializeField] private GameObject _player2ControlsContainer;
        [SerializeField] private InputRebindRowUI[] _player2RebindRows;

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

        public Button Player1ControlsTabButton
        {
            get
            {
                if (_player1ControlsTabButton == null)
                {
                    var t = transform.Find("Dialog_Frame/BodyRoot/Panel_Controls/SubTabsRow/Btn_ControlsP1");
                    if (t != null) _player1ControlsTabButton = t.GetComponent<Button>();
                }
                return _player1ControlsTabButton;
            }
        }

        public Button Player2ControlsTabButton
        {
            get
            {
                if (_player2ControlsTabButton == null)
                {
                    var t = transform.Find("Dialog_Frame/BodyRoot/Panel_Controls/SubTabsRow/Btn_ControlsP2");
                    if (t != null) _player2ControlsTabButton = t.GetComponent<Button>();
                }
                return _player2ControlsTabButton;
            }
        }

        public Graphic Player1ControlsIndicator
        {
            get
            {
                if (_player1ControlsIndicator == null && Player1ControlsTabButton != null)
                {
                    var t = Player1ControlsTabButton.transform.Find("ActiveIndicator");
                    if (t != null) _player1ControlsIndicator = t.GetComponent<Graphic>();
                }
                return _player1ControlsIndicator;
            }
        }

        public Graphic Player2ControlsIndicator
        {
            get
            {
                if (_player2ControlsIndicator == null && Player2ControlsTabButton != null)
                {
                    var t = Player2ControlsTabButton.transform.Find("ActiveIndicator");
                    if (t != null) _player2ControlsIndicator = t.GetComponent<Graphic>();
                }
                return _player2ControlsIndicator;
            }
        }

        public GameObject Player1ControlsContainer
        {
            get
            {
                if (_player1ControlsContainer == null)
                {
                    var t = transform.Find("Dialog_Frame/BodyRoot/Panel_Controls/RebindGrid_P1");
                    if (t == null) t = transform.Find("Dialog_Frame/BodyRoot/Panel_Controls/RebindGrid");
                    if (t != null) _player1ControlsContainer = t.gameObject;
                }
                return _player1ControlsContainer;
            }
        }

        public GameObject Player2ControlsContainer
        {
            get
            {
                if (_player2ControlsContainer == null)
                {
                    var t = transform.Find("Dialog_Frame/BodyRoot/Panel_Controls/RebindGrid_P2");
                    if (t != null) _player2ControlsContainer = t.gameObject;
                }
                return _player2ControlsContainer;
            }
        }

        public InputRebindRowUI[] Player2RebindRows => _player2RebindRows;

        public Button ResetDefaultsButton => _resetDefaultsButton;
        public Button CloseButton => _closeButton;

        public void SetActive(bool active) => gameObject.SetActive(active);
    }
}
