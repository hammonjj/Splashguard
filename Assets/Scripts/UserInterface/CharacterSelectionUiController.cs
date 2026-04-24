using Bitbox.Toymageddon;
using BitBox.Library;
using BitBox.Library.Constants;
using BitBox.Library.Constants.Enums;
using BitBox.Library.Eventing.GlobalEvents;
using BitBox.Library.Eventing.SceneEvents;
using BitBox.Library.Localization;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BitBox.Toymageddon.UserInterface
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(MultiplayerEventSystem))]
    [RequireComponent(typeof(InputSystemUIInputModule))]
    public sealed class CharacterSelectionUiController : MonoBehaviourBase
    {
        [SerializeField, Required] private GameObject _characterSelectionContainer;

        [ShowInInspector, ReadOnly] private bool _isReady;

        private PlayerInput _playerInput;
        private MultiplayerEventSystem _multiplayerEventSystem;
        private Button _readyButton;
        private Image _cardSurface;
        private TextMeshProUGUI _titleLabel;
        private TextMeshProUGUI _subtitleLabel;
        private TextMeshProUGUI _readyButtonLabel;
        private InputAction _uiSubmitAction;
        private InputAction _uiCancelAction;
        private bool _ignoreReadyInputUntilRelease;
        private bool _wasReadyInputPressed;

        private static readonly Color DefaultCardColor = new(0.07058824f, 0.09411765f, 0.1254902f, 0.92f);
        private static readonly Color ReadyCardColor = new(0.10588235f, 0.16862746f, 0.13725491f, 0.96f);

        protected override void OnAwakened()
        {
            CacheReferences();
            SyncVisibilityFromCurrentScene();
        }

        protected override void OnEnabled()
        {
            CacheReferences();
            BindReadyButton();
            BindCancelAction();
            BindSubmitAction();

            GameText.LanguageChanged += OnLanguageChanged;
            _globalMessageBus.Subscribe<MacroSceneLoadedEvent>(OnMacroSceneLoaded);
            _globalMessageBus.Subscribe<AllPlayersReadyEvent>(OnAllPlayersReady);

            SyncVisibilityFromCurrentScene();
            RefreshLocalizedText();
        }

        protected override void OnDisabled()
        {
            GameText.LanguageChanged -= OnLanguageChanged;
            _globalMessageBus.Unsubscribe<MacroSceneLoadedEvent>(OnMacroSceneLoaded);
            _globalMessageBus.Unsubscribe<AllPlayersReadyEvent>(OnAllPlayersReady);

            if (_readyButton != null)
            {
                _readyButton.onClick.RemoveListener(PlayerReady);
            }

            if (_uiCancelAction != null)
            {
                _uiCancelAction.performed -= OnUiCancelPerformed;
            }
        }

        protected override void OnUpdated()
        {
            if (CurrentMacroScene != MacroSceneType.CharacterSelection
                || _characterSelectionContainer == null
                || !_characterSelectionContainer.activeInHierarchy
                || _isReady)
            {
                return;
            }

            bool isReadyInputPressed = IsReadyInputPressed();

            if (_ignoreReadyInputUntilRelease)
            {
                _wasReadyInputPressed = isReadyInputPressed;

                if (!isReadyInputPressed)
                {
                    _ignoreReadyInputUntilRelease = false;
                }

                return;
            }

            bool isNewReadyPress = isReadyInputPressed && !_wasReadyInputPressed;
            _wasReadyInputPressed = isReadyInputPressed;

            if (!isNewReadyPress)
            {
                return;
            }

            PlayerReady();
        }

        public void PlayerReady()
        {
            if (_isReady || CurrentMacroScene != MacroSceneType.CharacterSelection)
            {
                return;
            }

            GameData.EnsureCharacterSelectionEntry(GetPlayerIndex());
            ApplyReadyState(true);
            _globalMessageBus.Publish(new PlayerReadyEvent(GetPlayerIndex()));
        }

        private void OnMacroSceneLoaded(MacroSceneLoadedEvent @event)
        {
            if (@event.SceneType == MacroSceneType.CharacterSelection)
            {
                ShowCharacterSelection();
                return;
            }

            HideCharacterSelection();
        }

        private void OnAllPlayersReady(AllPlayersReadyEvent @event)
        {
            HideCharacterSelection();
        }

        private void OnLanguageChanged(string languageId)
        {
            RefreshLocalizedText();
        }

        private void ShowCharacterSelection()
        {
            GameData.EnsureCharacterSelectionEntry(GetPlayerIndex());
            _isReady = false;
            _ignoreReadyInputUntilRelease = true;
            _wasReadyInputPressed = IsReadyInputPressed();

            if (_characterSelectionContainer != null)
            {
                _characterSelectionContainer.SetActive(true);
            }

            ActivateUiInput();
            ApplyReadyState(false);
            SelectReadyButton();
            RefreshLocalizedText();
        }

        private void HideCharacterSelection()
        {
            _isReady = false;
            _ignoreReadyInputUntilRelease = true;
            _wasReadyInputPressed = false;

            if (_characterSelectionContainer != null)
            {
                _characterSelectionContainer.SetActive(false);
            }
        }

        private void SyncVisibilityFromCurrentScene()
        {
            if (CurrentMacroScene == MacroSceneType.CharacterSelection)
            {
                ShowCharacterSelection();
                return;
            }

            HideCharacterSelection();
        }

        private MacroSceneType CurrentMacroScene =>
            StaticData.GameController != null
                ? StaticData.GameController.CurrentMacroScene
                : MacroSceneType.None;

        private void CacheReferences()
        {
            _playerInput ??= GetComponent<PlayerInput>();
            _multiplayerEventSystem ??= GetComponent<MultiplayerEventSystem>();
            _characterSelectionContainer ??= CharacterSelectionUiRuntimeBuilder.EnsureBuilt(transform);

            if (_characterSelectionContainer == null)
            {
                return;
            }

            _readyButton ??= _characterSelectionContainer.transform.Find("Panel/CardSurface/Ready")?.GetComponent<Button>();
            _cardSurface ??= _characterSelectionContainer.transform.Find("Panel/CardSurface")?.GetComponent<Image>();
            _titleLabel ??= _characterSelectionContainer.transform.Find("Panel/CardSurface/Title")?.GetComponent<TextMeshProUGUI>();
            _subtitleLabel ??= _characterSelectionContainer.transform.Find("Panel/CardSurface/Subtitle")?.GetComponent<TextMeshProUGUI>();
            _readyButtonLabel ??= _readyButton != null
                ? _readyButton.GetComponentInChildren<TextMeshProUGUI>(true)
                : null;
        }

        private void BindReadyButton()
        {
            if (_readyButton == null)
            {
                return;
            }

            _readyButton.onClick.RemoveListener(PlayerReady);
            _readyButton.onClick.AddListener(PlayerReady);
        }

        private void BindCancelAction()
        {
            if (_playerInput?.actions == null)
            {
                return;
            }

            InputActionMap uiMap = _playerInput.actions.FindActionMap(Strings.UIMap, false);
            if (uiMap == null)
            {
                return;
            }

            if (_uiCancelAction != null)
            {
                _uiCancelAction.performed -= OnUiCancelPerformed;
            }

            _uiCancelAction = uiMap.FindAction("Cancel", false);
            if (_uiCancelAction != null)
            {
                _uiCancelAction.performed += OnUiCancelPerformed;
            }
        }

        private void BindSubmitAction()
        {
            _uiSubmitAction = null;

            if (_playerInput?.actions == null)
            {
                return;
            }

            InputActionMap uiMap = _playerInput.actions.FindActionMap(Strings.UIMap, false);
            if (uiMap == null)
            {
                return;
            }

            _uiSubmitAction = uiMap.FindAction("Submit", false);
        }

        private void OnUiCancelPerformed(InputAction.CallbackContext context)
        {
            if (CurrentMacroScene != MacroSceneType.CharacterSelection
                || _characterSelectionContainer == null
                || !_characterSelectionContainer.activeInHierarchy)
            {
                return;
            }

            if (_isReady)
            {
                UnreadyPlayer();
                return;
            }

            GameData.ClearCharacterSelectionSession();
            _globalMessageBus.Publish(new LoadMacroSceneEvent(MacroSceneType.TitleMenu));
        }

        private void UnreadyPlayer()
        {
            if (!_isReady)
            {
                return;
            }

            ApplyReadyState(false);
            _globalMessageBus.Publish(new PlayerUnreadyEvent(GetPlayerIndex()));
            ActivateUiInput();
            _ignoreReadyInputUntilRelease = true;
            _wasReadyInputPressed = IsReadyInputPressed();
            SelectReadyButton();
        }

        private bool IsReadyInputPressed()
        {
            if (_uiSubmitAction != null && _uiSubmitAction.IsPressed())
            {
                return true;
            }

            if (_playerInput == null)
            {
                return false;
            }

            foreach (InputDevice device in _playerInput.devices)
            {
                switch (device)
                {
                    case Keyboard keyboard when keyboard.enterKey.isPressed
                                               || keyboard.numpadEnterKey.isPressed
                                               || keyboard.spaceKey.isPressed:
                        return true;
                    case Gamepad gamepad when gamepad.buttonSouth.isPressed:
                        return true;
                }
            }

            return false;
        }

        private void ActivateUiInput()
        {
            if (_playerInput == null)
            {
                return;
            }

            _playerInput.actions.Enable();
            _playerInput.SwitchCurrentActionMap(Strings.UIMap);
            _playerInput.ActivateInput();
        }

        private void SelectReadyButton()
        {
            if (_multiplayerEventSystem == null || _readyButton == null || !_readyButton.IsInteractable())
            {
                return;
            }

            _multiplayerEventSystem.firstSelectedGameObject = _readyButton.gameObject;
            _multiplayerEventSystem.SetSelectedGameObject(_readyButton.gameObject);
        }

        private void ApplyReadyState(bool isReady)
        {
            _isReady = isReady;

            if (_cardSurface != null)
            {
                _cardSurface.color = isReady ? ReadyCardColor : DefaultCardColor;
            }

            if (_readyButton != null)
            {
                _readyButton.interactable = !isReady;
            }

            RefreshLocalizedText();
        }

        private void RefreshLocalizedText()
        {
            if (_titleLabel != null)
            {
                _titleLabel.text = GameText.Get(_isReady
                    ? CharacterSelectionLocalizationKeys.TitleReady
                    : CharacterSelectionLocalizationKeys.TitleDefault);
            }

            if (_subtitleLabel != null)
            {
                _subtitleLabel.text = GameText.Get(_isReady
                    ? CharacterSelectionLocalizationKeys.SubtitleReady
                    : CharacterSelectionLocalizationKeys.SubtitleDefault);
            }

            if (_readyButtonLabel != null)
            {
                _readyButtonLabel.text = GameText.Get(_isReady
                    ? CharacterSelectionLocalizationKeys.ReadyButtonReady
                    : CharacterSelectionLocalizationKeys.ReadyButtonDefault);
            }
        }

        private int GetPlayerIndex()
        {
            return _playerInput != null
                ? _playerInput.playerIndex
                : 0;
        }
    }

    public static class CharacterSelectionUiRuntimeBuilder
    {
        private const string ViewportRootPath = "UiCanvas/ViewportRoot";
        private const string ContainerObjectName = "CharacterSelectionRoot";
        private const string PanelObjectName = "Panel";
        private const string CardSurfaceObjectName = "CardSurface";
        private const string TitleObjectName = "Title";
        private const string SubtitleObjectName = "Subtitle";
        private const string ReadyButtonObjectName = "Ready";
        private const string ReadyButtonLabelObjectName = "Label";

        private static readonly Color OverlayColor = new(0f, 0f, 0f, 0f);
        private static readonly Color CardColor = new(0.07f, 0.09f, 0.13f, 0.92f);
        private static readonly Color ReadyButtonColor = new(0.15f, 0.56f, 0.33f, 0.98f);
        private static readonly Color TitleColor = new(0.95f, 0.96f, 0.97f, 1f);
        private static readonly Color SubtitleColor = new(0.82f, 0.86f, 0.9f, 1f);

        public static GameObject EnsureBuilt(Transform playerRoot)
        {
            if (playerRoot == null)
            {
                return null;
            }

            Transform viewportRoot = playerRoot.Find(ViewportRootPath);
            if (viewportRoot == null)
            {
                return null;
            }

            Transform existingContainer = viewportRoot.Find(ContainerObjectName);
            if (existingContainer != null)
            {
                return existingContainer.gameObject;
            }

            int uiLayer = viewportRoot.gameObject.layer;

            GameObject container = CreateUiObject(ContainerObjectName, viewportRoot, uiLayer);
            RectTransform containerRect = container.GetComponent<RectTransform>();
            StretchToFill(containerRect);

            Image overlayImage = container.AddComponent<Image>();
            overlayImage.color = OverlayColor;
            overlayImage.raycastTarget = false;

            GameObject panel = CreateUiObject(PanelObjectName, container.transform, uiLayer);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(420f, 280f);
            panelRect.anchoredPosition = Vector2.zero;

            GameObject cardSurface = CreateUiObject(CardSurfaceObjectName, panel.transform, uiLayer);
            RectTransform cardRect = cardSurface.GetComponent<RectTransform>();
            StretchToFill(cardRect);

            Image cardImage = cardSurface.AddComponent<Image>();
            cardImage.color = CardColor;
            cardImage.raycastTarget = true;

            Outline cardOutline = cardSurface.AddComponent<Outline>();
            cardOutline.effectColor = new Color(0.9f, 1f, 0.83f, 0.18f);
            cardOutline.effectDistance = new Vector2(2f, -2f);

            TextMeshProUGUI titleLabel = CreateText(TitleObjectName, cardSurface.transform, uiLayer);
            titleLabel.alignment = TextAlignmentOptions.Center;
            titleLabel.fontSize = 28f;
            titleLabel.fontStyle = FontStyles.Bold;
            titleLabel.color = TitleColor;
            RectTransform titleRect = titleLabel.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(24f, -74f);
            titleRect.offsetMax = new Vector2(-24f, -24f);

            TextMeshProUGUI subtitleLabel = CreateText(SubtitleObjectName, cardSurface.transform, uiLayer);
            subtitleLabel.alignment = TextAlignmentOptions.Top;
            subtitleLabel.fontSize = 18f;
            subtitleLabel.textWrappingMode = TextWrappingModes.Normal;
            subtitleLabel.color = SubtitleColor;
            RectTransform subtitleRect = subtitleLabel.rectTransform;
            subtitleRect.anchorMin = new Vector2(0f, 0f);
            subtitleRect.anchorMax = new Vector2(1f, 1f);
            subtitleRect.pivot = new Vector2(0.5f, 0.5f);
            subtitleRect.offsetMin = new Vector2(28f, 88f);
            subtitleRect.offsetMax = new Vector2(-28f, -88f);

            GameObject readyButtonObject = CreateUiObject(ReadyButtonObjectName, cardSurface.transform, uiLayer);
            RectTransform readyButtonRect = readyButtonObject.GetComponent<RectTransform>();
            readyButtonRect.anchorMin = new Vector2(0.5f, 0f);
            readyButtonRect.anchorMax = new Vector2(0.5f, 0f);
            readyButtonRect.pivot = new Vector2(0.5f, 0f);
            readyButtonRect.sizeDelta = new Vector2(200f, 52f);
            readyButtonRect.anchoredPosition = new Vector2(0f, 28f);

            Image readyButtonImage = readyButtonObject.AddComponent<Image>();
            readyButtonImage.color = ReadyButtonColor;
            readyButtonImage.raycastTarget = true;

            Button readyButton = readyButtonObject.AddComponent<Button>();
            readyButton.targetGraphic = readyButtonImage;
            ColorBlock colors = readyButton.colors;
            colors.normalColor = ReadyButtonColor;
            colors.highlightedColor = new Color(0.21f, 0.68f, 0.42f, 1f);
            colors.pressedColor = new Color(0.11f, 0.44f, 0.25f, 1f);
            colors.selectedColor = new Color(0.21f, 0.68f, 0.42f, 1f);
            colors.disabledColor = new Color(0.19f, 0.23f, 0.27f, 0.7f);
            readyButton.colors = colors;
            readyButton.transition = Selectable.Transition.ColorTint;

            TextMeshProUGUI readyLabel = CreateText(ReadyButtonLabelObjectName, readyButtonObject.transform, uiLayer);
            readyLabel.alignment = TextAlignmentOptions.Center;
            readyLabel.fontSize = 22f;
            readyLabel.fontStyle = FontStyles.Bold;
            readyLabel.color = TitleColor;
            RectTransform readyLabelRect = readyLabel.rectTransform;
            StretchToFill(readyLabelRect);

            container.SetActive(false);
            return container;
        }

        private static GameObject CreateUiObject(string name, Transform parent, int layer)
        {
            GameObject gameObject = new(name, typeof(RectTransform));
            gameObject.layer = layer;
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, int layer)
        {
            GameObject gameObject = CreateUiObject(name, parent, layer);
            var text = gameObject.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            text.text = string.Empty;
            text.raycastTarget = false;
            return text;
        }

        private static void StretchToFill(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
        }
    }
}
