using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BitBox.Toymageddon.UserInterface
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Selectable))]
    public sealed class CharacterSelectionSelectableHighlight : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        private Selectable _selectable;
        private Graphic _targetGraphic;
        private Outline _outline;
        private Color _normalColor;
        private Color _focusedColor;
        private Color _disabledColor;
        private Color _outlineColor;
        private Vector2 _outlineDistance;
        private Vector3 _focusedScale;
        private Vector3 _defaultScale = Vector3.one;
        private bool _isConfigured;
        private bool _isFocused;

        public bool IsFocused => _isFocused;

        public void Configure(
            Graphic targetGraphic,
            Color normalColor,
            Color focusedColor,
            Color disabledColor,
            Color outlineColor,
            Vector2 outlineDistance,
            Vector3 focusedScale)
        {
            _targetGraphic = targetGraphic;
            _normalColor = normalColor;
            _focusedColor = focusedColor;
            _disabledColor = disabledColor;
            _outlineColor = outlineColor;
            _outlineDistance = outlineDistance;
            _focusedScale = focusedScale;
            _isConfigured = true;

            EnsureVisuals();
            RefreshVisualState();
        }

        public void RefreshVisualState()
        {
            EnsureVisuals();
            ApplyVisualState();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _isFocused = true;
            ApplyVisualState();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _isFocused = false;
            ApplyVisualState();
        }

        private void Awake()
        {
            EnsureVisuals();
            ApplyVisualState();
        }

        private void OnEnable()
        {
            EnsureVisuals();
            ApplyVisualState();
        }

        private void EnsureVisuals()
        {
            _selectable ??= GetComponent<Selectable>();
            _targetGraphic ??= _selectable?.targetGraphic;

            if (_defaultScale == Vector3.one && transform.localScale != Vector3.zero)
            {
                _defaultScale = transform.localScale;
            }

            if (_targetGraphic == null)
            {
                return;
            }

            _outline ??= _targetGraphic.GetComponent<Outline>();
            if (_outline == null)
            {
                _outline = _targetGraphic.gameObject.AddComponent<Outline>();
            }

            _outline.useGraphicAlpha = true;
        }

        private void ApplyVisualState()
        {
            if (!_isConfigured || _selectable == null)
            {
                return;
            }

            bool isInteractable = _selectable.interactable;
            if (_targetGraphic != null)
            {
                _targetGraphic.color = !isInteractable
                    ? _disabledColor
                    : _isFocused
                        ? _focusedColor
                        : _normalColor;
            }

            if (_outline != null)
            {
                _outline.enabled = isInteractable && _isFocused;
                _outline.effectColor = _outlineColor;
                _outline.effectDistance = _outlineDistance;
            }

            transform.localScale = isInteractable && _isFocused
                ? _focusedScale
                : _defaultScale;
        }
    }
}
