using Game.Core.Enums;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Gameplay.UI
{
    [DisallowMultipleComponent]
    public sealed class LobbyView : MonoBehaviour
    {
        [Header("Tarjeta Piernas (Legs)")]
        [SerializeField] private Button _selectLegsButton;
        [SerializeField] private TextMeshProUGUI _legsStatusText;
        [SerializeField] private CanvasGroup _legsCanvasGroup;
        [SerializeField] private TextMeshProUGUI _legsAbilitiesText;
        [SerializeField] private TextMeshProUGUI _legsControlsText;

        [Header("Tarjeta Torso (Torso)")]
        [SerializeField] private Button _selectTorsoButton;
        [SerializeField] private TextMeshProUGUI _torsoStatusText;
        [SerializeField] private CanvasGroup _torsoCanvasGroup;
        [SerializeField] private TextMeshProUGUI _torsoAbilitiesText;
        [SerializeField] private TextMeshProUGUI _torsoControlsText;

        [Header("Controles de Confirmación y Partida")]
        [SerializeField] private Button _readyToggleButton;
        [SerializeField] private TextMeshProUGUI _readyButtonText;
        [SerializeField] private Button _startGameButton;
        [SerializeField] private TextMeshProUGUI _matchStatusText;
        [SerializeField] private Button _leaveLobbyButton;

        [Header("Información de Sala")]
        [SerializeField] private TextMeshProUGUI _roomCodeText;

        private Canvas _canvas;
        private GraphicRaycaster _raycaster;

        public Button SelectLegsButton => _selectLegsButton;
        public TextMeshProUGUI LegsStatusText => _legsStatusText;
        public CanvasGroup LegsCanvasGroup => _legsCanvasGroup;
        public TextMeshProUGUI LegsAbilitiesText => _legsAbilitiesText;
        public TextMeshProUGUI LegsControlsText => _legsControlsText;

        public Button SelectTorsoButton => _selectTorsoButton;
        public TextMeshProUGUI TorsoStatusText => _torsoStatusText;
        public CanvasGroup TorsoCanvasGroup => _torsoCanvasGroup;
        public TextMeshProUGUI TorsoAbilitiesText => _torsoAbilitiesText;
        public TextMeshProUGUI TorsoControlsText => _torsoControlsText;

        public Button ReadyToggleButton => _readyToggleButton;
        public TextMeshProUGUI ReadyButtonText => _readyButtonText;
        public Button StartGameButton => _startGameButton;
        public TextMeshProUGUI MatchStatusText => _matchStatusText;
        public Button LeaveLobbyButton => _leaveLobbyButton;

        public TextMeshProUGUI RoomCodeText
        {
            get
            {
                if (_roomCodeText == null)
                {
                    var header = transform.Find("Background/Header");
                    if (header != null)
                    {
                        _roomCodeText = header.GetComponent<TextMeshProUGUI>();
                    }
                }
                return _roomCodeText;
            }
        }

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _raycaster = GetComponent<GraphicRaycaster>();

            // Start with canvas hidden while keeping GameObject active so Presenters/listeners stay alive
            if (_canvas != null) _canvas.enabled = false;
            if (_raycaster != null) _raycaster.enabled = false;
        }

        public void SetActive(bool active)
        {
            if (_canvas == null) _canvas = GetComponent<Canvas>();
            if (_raycaster == null) _raycaster = GetComponent<GraphicRaycaster>();

            if (_canvas != null) _canvas.enabled = active;
            if (_raycaster != null) _raycaster.enabled = active;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        public void UpdateCard(PlayerRole role, bool isOccupied, bool isReady, bool isLocal)
        {
            var cg = role == PlayerRole.Legs ? _legsCanvasGroup : _torsoCanvasGroup;
            var text = role == PlayerRole.Legs ? _legsStatusText : _torsoStatusText;
            var btn = role == PlayerRole.Legs ? _selectLegsButton : _selectTorsoButton;

            if (cg == null || text == null || btn == null) return;

            if (!isOccupied)
            {
                cg.alpha = 1f;
                btn.interactable = true;
                text.text = "<color=#888888>[ DISPONIBLE — CLIC PARA ELEGIR ]</color>";
            }
            else if (isLocal)
            {
                cg.alpha = 1f;
                btn.interactable = !isReady;
                text.text = isReady
                    ? "<color=#55FF55><b>[LISTO] TU SELECCIÓN CONFIRMADA</b></color>"
                    : "<color=#00FFFF><b>[ELEGIDO] TU SELECCIÓN (PENDIENTE DE LISTO)</b></color>";
            }
            else
            {
                // Occupied by teammate
                cg.alpha = isReady ? 0.45f : 0.8f;
                btn.interactable = false;
                text.text = isReady
                    ? "<color=#FF5555><b>[OCUPADO] CONFIRMADO POR COMPAÑERO</b></color>"
                    : "<color=#FFAA00><b>COMPAÑERO ELIGIENDO...</b></color>";
            }
        }

        public void SetReadyButtonState(bool isLocalReady, bool hasSelectedRole)
        {
            if (_readyToggleButton != null)
            {
                _readyToggleButton.interactable = hasSelectedRole;
            }

            if (_readyButtonText != null)
            {
                _readyButtonText.text = isLocalReady ? "CANCELAR LISTO" : "CONFIRMAR (LISTO)";
            }
        }

        public void SetMatchStatus(string message, Color color)
        {
            if (_matchStatusText != null)
            {
                _matchStatusText.text = message;
                _matchStatusText.color = color;
            }
        }

        public void SetStartButtonInteractable(bool isInteractable, bool isHost)
        {
            if (_startGameButton != null)
            {
                _startGameButton.gameObject.SetActive(isHost);
                _startGameButton.interactable = isInteractable;
            }
        }
    }
}
