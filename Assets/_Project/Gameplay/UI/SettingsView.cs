using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Gameplay.UI
{
    [DisallowMultipleComponent]
    public sealed class SettingsView : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private Slider _masterVolumeSlider;
        [SerializeField] private TextMeshProUGUI _masterVolumeValueText;
        [SerializeField] private Slider _sfxVolumeSlider;
        [SerializeField] private TextMeshProUGUI _sfxVolumeValueText;

        [Header("Display / Video")]
        [SerializeField] private TMP_Dropdown _resolutionDropdown;
        [SerializeField] private TMP_Dropdown _fullscreenModeDropdown;
        [SerializeField] private Toggle _vsyncToggle;

        [Header("Controls")]
        [SerializeField] private Slider _sensitivitySlider;
        [SerializeField] private TextMeshProUGUI _sensitivityValueText;

        [Header("Actions")]
        [SerializeField] private Button _closeButton;

        public Slider MasterVolumeSlider => _masterVolumeSlider;
        public TextMeshProUGUI MasterVolumeValueText => _masterVolumeValueText;
        public Slider SfxVolumeSlider => _sfxVolumeSlider;
        public TextMeshProUGUI SfxVolumeValueText => _sfxVolumeValueText;

        public TMP_Dropdown ResolutionDropdown => _resolutionDropdown;
        public TMP_Dropdown FullscreenModeDropdown => _fullscreenModeDropdown;
        public Toggle VSyncToggle => _vsyncToggle;

        public Slider SensitivitySlider => _sensitivitySlider;
        public TextMeshProUGUI SensitivityValueText => _sensitivityValueText;

        public Button CloseButton => _closeButton;

        public void SetActive(bool active) => gameObject.SetActive(active);
    }
}
