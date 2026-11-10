using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Gameplay.UI
{
    [DisallowMultipleComponent]
    public sealed class LevelSelectModalUI : MonoBehaviour
    {
        [Header("Estructura Modal")]
        [SerializeField] private GameObject _modalRoot;
        [SerializeField] private Transform _buttonsContainer;
        [SerializeField] private Button _roomButtonTemplate;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TextMeshProUGUI _titleText;

        private readonly List<Button> _instantiatedButtons = new List<Button>();
        private int _selectedRoomIndex;

        public event Action<int> OnRoomSelected;

        private void Awake()
        {
            if (_modalRoot == null)
            {
                _modalRoot = gameObject;
            }

            if (_roomButtonTemplate != null)
            {
                _roomButtonTemplate.gameObject.SetActive(false);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Hide);
            }
        }

        public void Populate(int totalRooms, int currentSelected = 0)
        {
            _selectedRoomIndex = currentSelected;

            if (_buttonsContainer == null || _roomButtonTemplate == null)
            {
                return;
            }

            // Recycle or create buttons
            while (_instantiatedButtons.Count < totalRooms)
            {
                var newBtn = Instantiate(_roomButtonTemplate, _buttonsContainer);
                _instantiatedButtons.Add(newBtn);
            }

            for (int i = 0; i < _instantiatedButtons.Count; i++)
            {
                var btn = _instantiatedButtons[i];
                if (i < totalRooms)
                {
                    btn.gameObject.SetActive(true);
                    int roomIdx = i;

                    var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        tmp.text = $"<b>{i + 1}</b>";
                    }

                    // Visual highlight for currently selected
                    var img = btn.GetComponent<Image>();
                    if (img != null)
                    {
                        img.color = (i == _selectedRoomIndex)
                            ? new Color(0f, 0.95f, 0.6f, 1f)
                            : new Color(0f, 0.85f, 1f, 0.9f);
                    }

                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() =>
                    {
                        _selectedRoomIndex = roomIdx;
                        OnRoomSelected?.Invoke(roomIdx);
                    });
                }
                else
                {
                    btn.gameObject.SetActive(false);
                }
            }
        }

        public void Show()
        {
            if (_modalRoot != null)
            {
                _modalRoot.SetActive(true);
            }
        }

        public void Hide()
        {
            if (_modalRoot != null)
            {
                _modalRoot.SetActive(false);
            }
        }
    }
}
