using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Gameplay.UI
{
    [DisallowMultipleComponent]
    public sealed class UIButtonPolish : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        [SerializeField] private Graphic _accentBar;
        [SerializeField] private Outline _borderOutline;
        [SerializeField] private RectTransform _labelRect;
        [SerializeField] private Color _normalAccentColor = new Color(0f, 0.94f, 1f, 0.55f);
        [SerializeField] private Color _hoverAccentColor = new Color(0f, 0.98f, 1f, 1f);
        [SerializeField] private float _hoverScale = 1.03f;
        [SerializeField] private float _pressScale = 0.96f;
        [SerializeField] private float _tweenDuration = 0.14f;

        private RectTransform _rectTransform;
        private Button _button;
        private Vector3 _initialScale = Vector3.one;
        private Vector3 _initialLabelLocalPos;
        private bool _isHovered;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _button = GetComponent<Button>();

            if (_rectTransform != null)
            {
                _initialScale = _rectTransform.localScale;
            }

            if (_labelRect != null)
            {
                _initialLabelLocalPos = _labelRect.localPosition;
            }

            ApplyIdleVisualsImmediate();
        }

        private void OnEnable()
        {
            _isHovered = false;
            ApplyIdleVisualsImmediate();
        }

        private void OnDisable()
        {
            KillTweens();
            ApplyIdleVisualsImmediate();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_button != null && !_button.interactable)
            {
                return;
            }

            _isHovered = true;
            KillTweens();

            if (_rectTransform != null)
            {
                _rectTransform.DOScale(_initialScale * _hoverScale, _tweenDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
            }

            if (_accentBar != null)
            {
                _accentBar.color = _hoverAccentColor;
            }

            if (_borderOutline != null)
            {
                _borderOutline.effectColor = _hoverAccentColor;
            }

            if (_labelRect != null)
            {
                _labelRect.DOLocalMoveX(_initialLabelLocalPos.x + 5f, _tweenDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            KillTweens();

            if (_rectTransform != null)
            {
                _rectTransform.DOScale(_initialScale, _tweenDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
            }

            if (_accentBar != null)
            {
                _accentBar.color = _normalAccentColor;
            }

            if (_borderOutline != null)
            {
                _borderOutline.effectColor = _normalAccentColor;
            }

            if (_labelRect != null)
            {
                _labelRect.DOLocalMoveX(_initialLabelLocalPos.x, _tweenDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_button != null && !_button.interactable)
            {
                return;
            }

            if (_rectTransform != null)
            {
                _rectTransform.DOKill();
                _rectTransform.DOScale(_initialScale * _pressScale, 0.08f)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_rectTransform != null)
            {
                _rectTransform.DOKill();
                float targetMult = _isHovered ? _hoverScale : 1f;
                _rectTransform.DOScale(_initialScale * targetMult, 0.12f)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true);
            }
        }

        private void ApplyIdleVisualsImmediate()
        {
            if (_rectTransform != null)
            {
                _rectTransform.localScale = _initialScale;
            }

            if (_accentBar != null)
            {
                _accentBar.color = _normalAccentColor;
            }

            if (_borderOutline != null)
            {
                _borderOutline.effectColor = _normalAccentColor;
            }

            if (_labelRect != null)
            {
                _labelRect.localPosition = _initialLabelLocalPos;
            }
        }

        private void KillTweens()
        {
            if (_rectTransform != null)
            {
                _rectTransform.DOKill();
            }

            if (_labelRect != null)
            {
                _labelRect.DOKill();
            }
        }
    }
}
