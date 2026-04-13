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
}
