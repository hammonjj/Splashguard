using System;
using System.Collections.Generic;
using BitBox.Library.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BitBox.Toymageddon.UserInterface
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(Button))]
    public sealed class CharacterSelectionOptionSelector : MonoBehaviour
    {
        private const string LabelObjectName = "Label";
        private const string ValueObjectName = "Value";
        private const string LeftArrowObjectName = "LeftArrow";
        private const string RightArrowObjectName = "RightArrow";
        private const float SelectorWidth = 320f;
        private const float SelectorHeight = 40f;
        private const float LabelWidth = 146f;
        private const float ValueWidth = 110f;

        private static readonly Color NormalBackgroundColor = new(0.09019608f, 0.12156863f, 0.16078432f, 0.96f);
        private static readonly Color FocusedBackgroundColor = new(0.19607843f, 0.37254903f, 0.24705882f, 1f);
        private static readonly Color DisabledBackgroundColor = new(0.09019608f, 0.12156863f, 0.16078432f, 0.42f);
        private static readonly Color ForegroundColor = new(0.9529412f, 0.95686275f, 0.96862745f, 1f);
        private static readonly Color FocusedForegroundColor = new(1f, 1f, 1f, 1f);
        private static readonly Color AccentColor = new(0.36078432f, 0.85882354f, 0.50980395f, 1f);
        private static readonly Color FocusedAccentColor = new(0.8666667f, 1f, 0.8f, 1f);
        private static readonly Color DisabledForegroundColor = new(0.9529412f, 0.95686275f, 0.96862745f, 0.45f);
        private static readonly Color FocusOutlineColor = new(0.84705883f, 1f, 0.7607843f, 0.95f);
        private static readonly Vector2 FocusOutlineDistance = new Vector2(2.5f, -2.5f);
        private static readonly Vector3 FocusedScale = new Vector3(1.02f, 1.02f, 1f);

        private readonly List<string> _optionKeys = new List<string>();

        private Button _button;
        private Image _backgroundImage;
        private CharacterSelectionSelectableHighlight _focusHighlight;
        private TextMeshProUGUI _label;
        private TextMeshProUGUI _valueLabel;
        private TextMeshProUGUI _leftArrowLabel;
        private TextMeshProUGUI _rightArrowLabel;
        private string _labelKey = string.Empty;
        private int _selectedIndex;

        public event Action<int> SelectionChanged;

        public Button Button => _button;

        public int GetSelectedIndex()
        {
            return _selectedIndex;
        }

        public void SetLabelKey(string labelKey)
        {
            _labelKey = labelKey ?? string.Empty;
            EnsureVisualTree();
            ApplyVisualState();
        }

        public void SetOptionKeys(IReadOnlyList<string> optionKeys)
        {
            _optionKeys.Clear();
            if (optionKeys != null)
            {
                for (int i = 0; i < optionKeys.Count; i++)
                {
                    _optionKeys.Add(optionKeys[i] ?? string.Empty);
                }
            }

            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, _optionKeys.Count - 1));
            EnsureVisualTree();
            ApplyVisualState();
        }

        public void SetSelectionIndex(int index)
        {
            EnsureVisualTree();
            int clampedIndex = Mathf.Clamp(index, 0, Mathf.Max(0, _optionKeys.Count - 1));
            if (_selectedIndex == clampedIndex)
            {
                ApplyVisualState();
                return;
            }

            _selectedIndex = clampedIndex;
            ApplyVisualState();
            SelectionChanged?.Invoke(_selectedIndex);
        }

        public void StepLeft()
        {
            if (_optionKeys.Count == 0)
            {
                return;
            }

            int nextIndex = _selectedIndex - 1;
            if (nextIndex < 0)
            {
                nextIndex = _optionKeys.Count - 1;
            }

            SetSelectionIndex(nextIndex);
        }

        public void StepRight()
        {
            if (_optionKeys.Count == 0)
            {
                return;
            }

            SetSelectionIndex((_selectedIndex + 1) % _optionKeys.Count);
        }

        public void SetInteractable(bool isInteractable)
        {
            EnsureVisualTree();
            _button.interactable = isInteractable;
            ApplyVisualState();
        }

        public void RefreshLocalizedText()
        {
            EnsureVisualTree();
            ApplyVisualState();
        }

        private void Awake()
        {
            EnsureVisualTree();
            ApplyVisualState();
        }

        private void OnEnable()
        {
            EnsureVisualTree();
            ApplyVisualState();
        }

        private void Reset()
        {
            EnsureVisualTree();
            ApplyVisualState();
        }

        private void EnsureVisualTree()
        {
            RectTransform rectTransform = GetComponent<RectTransform>();
            if (rectTransform.sizeDelta.x <= 0f || rectTransform.sizeDelta.y <= 0f)
            {
                rectTransform.sizeDelta = new Vector2(SelectorWidth, SelectorHeight);
            }

            _backgroundImage = GetComponent<Image>();
            _backgroundImage.color = NormalBackgroundColor;
            _backgroundImage.raycastTarget = true;

            _button = GetComponent<Button>();
            _button.targetGraphic = _backgroundImage;
            _button.transition = Selectable.Transition.None;

            _focusHighlight = GetComponent<CharacterSelectionSelectableHighlight>();
            if (_focusHighlight == null)
            {
                _focusHighlight = gameObject.AddComponent<CharacterSelectionSelectableHighlight>();
            }

            _focusHighlight.Configure(
                _backgroundImage,
                NormalBackgroundColor,
                FocusedBackgroundColor,
                DisabledBackgroundColor,
                FocusOutlineColor,
                FocusOutlineDistance,
                FocusedScale);

            _label = EnsureTextChild(LabelObjectName, string.Empty, TextAlignmentOptions.MidlineLeft, 14f);
            ConfigureRect(_label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(LabelWidth, 20f));

            _leftArrowLabel = EnsureTextChild(LeftArrowObjectName, "<", TextAlignmentOptions.Center, 20f);
            ConfigureRect(_leftArrowLabel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-144f, 0f), new Vector2(18f, 20f));

            _valueLabel = EnsureTextChild(ValueObjectName, string.Empty, TextAlignmentOptions.Center, 13f);
            ConfigureRect(_valueLabel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-78f, 0f), new Vector2(ValueWidth, 20f));

            _rightArrowLabel = EnsureTextChild(RightArrowObjectName, ">", TextAlignmentOptions.Center, 20f);
            ConfigureRect(_rightArrowLabel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-18f, 0f), new Vector2(18f, 20f));
        }

        private void ApplyVisualState()
        {
            string displayValueKey = _optionKeys.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _optionKeys.Count
                ? _optionKeys[_selectedIndex]
                : string.Empty;
            bool isFocused = _focusHighlight != null && _focusHighlight.IsFocused && _button != null && _button.interactable;
            Color foregroundColor = _button != null && _button.interactable
                ? isFocused
                    ? FocusedForegroundColor
                    : ForegroundColor
                : DisabledForegroundColor;
            Color arrowColor = _button != null && _button.interactable
                ? isFocused
                    ? FocusedAccentColor
                    : AccentColor
                : DisabledForegroundColor;

            _focusHighlight?.RefreshVisualState();

            if (_label != null)
            {
                _label.text = string.IsNullOrEmpty(_labelKey) ? string.Empty : GameText.Get(_labelKey);
                _label.color = foregroundColor;
            }

            if (_valueLabel != null)
            {
                _valueLabel.text = string.IsNullOrEmpty(displayValueKey) ? string.Empty : GameText.Get(displayValueKey);
                _valueLabel.color = foregroundColor;
            }

            if (_leftArrowLabel != null)
            {
                _leftArrowLabel.color = arrowColor;
            }

            if (_rightArrowLabel != null)
            {
                _rightArrowLabel.color = arrowColor;
            }
        }

        private TextMeshProUGUI EnsureTextChild(string childName, string defaultText, TextAlignmentOptions alignment, float fontSize)
        {
            Transform existingChild = transform.Find(childName);
            GameObject childObject;
            if (existingChild == null)
            {
                childObject = new GameObject(childName, typeof(RectTransform));
                childObject.transform.SetParent(transform, false);
            }
            else
            {
                childObject = existingChild.gameObject;
            }

            TextMeshProUGUI text = childObject.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = childObject.AddComponent<TextMeshProUGUI>();
            }

            text.text = defaultText;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static void ConfigureRect(
            RectTransform rectTransform,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
        }
    }
}
