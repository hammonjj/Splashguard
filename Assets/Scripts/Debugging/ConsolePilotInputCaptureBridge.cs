using System.Collections.Generic;
using BitBox.Library;
using BitBox.Library.Constants;
using ConsolePilot;
using ConsolePilot.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace BitBox.Toymageddon.Debugging
{
    [DisallowMultipleComponent]
    public sealed class ConsolePilotInputCaptureBridge : MonoBehaviourBase
    {
        private const string DefaultOpenDebugPanelBindingPath = "<Keyboard>/backquote";
        private const string UnityTextInputName = "unity-text-input";
        private const string UnityTextFieldInputClassName = "unity-text-field__input";
        private const string UnityBaseTextFieldInputClassName = "unity-base-text-field__input";
        private const string ConsoleInputMirrorName = "ConsolePilotInputMirror";
        private const float InputMirrorLeftOffset = 12f;
        private static readonly Color InputBackgroundColor = new Color(0.145f, 0.165f, 0.2f, 1f);
        private static readonly Color InputTextColor = new Color(0.84f, 0.87f, 0.9f, 1f);

        [SerializeField] private ConsolePilotRuntime _consoleRuntime;

        private readonly Dictionary<PlayerInput, string> _previousActionMaps = new Dictionary<PlayerInput, string>();
        private readonly HashSet<PlayerInput> _previouslyEnabledPlayers = new HashSet<PlayerInput>();
        private InputAction _openDebugPanelAction;
        private UIDocument _uiDocument;
        private Label _inputMirrorLabel;
        private bool _wasConsoleOpen;
        private bool _hasStoredCursorState;
        private bool _cursorWasVisible;
        private CursorLockMode _cursorLockMode;

        protected override void OnAwakened()
        {
            CacheConsoleRuntime();
            CacheUiDocument();
        }

        protected override void OnEnabled()
        {
            CacheConsoleRuntime();
            CacheUiDocument();
            EnableOpenDebugPanelAction();

            if (_consoleRuntime == null)
            {
                return;
            }

            _consoleRuntime.OpenStateChanged -= OnConsoleOpenStateChanged;
            _consoleRuntime.OpenStateChanged += OnConsoleOpenStateChanged;

            if (_consoleRuntime.IsOpen)
            {
                OnConsoleOpenStateChanged(true);
            }
        }

        protected override void OnStarted()
        {
            _consoleRuntime?.Close();
        }

        protected override void OnLateUpdated()
        {
            if (_wasConsoleOpen)
            {
                SuspendNewlyJoinedPlayers();
                TextField inputField = GetConsoleInputField();
                ApplyConsoleInputVisuals(inputField);
                UpdateConsoleInputMirror(inputField);
            }
        }

        protected override void OnDisabled()
        {
            DisableOpenDebugPanelAction();

            if (_consoleRuntime != null)
            {
                _consoleRuntime.OpenStateChanged -= OnConsoleOpenStateChanged;
            }

            RestoreStateIfNeeded();
        }

        protected override void OnDestroyed()
        {
            DisposeOpenDebugPanelAction();

            if (_consoleRuntime != null)
            {
                _consoleRuntime.OpenStateChanged -= OnConsoleOpenStateChanged;
            }

            RestoreStateIfNeeded();
        }

        private void CacheConsoleRuntime()
        {
            _consoleRuntime ??= GetComponent<ConsolePilotRuntime>();
        }

        private void CacheUiDocument()
        {
            _uiDocument ??= GetComponent<UIDocument>();
        }

        private void EnableOpenDebugPanelAction()
        {
            _openDebugPanelAction ??= CreateOpenDebugPanelAction();
            _openDebugPanelAction.performed -= OnOpenDebugPanelPerformed;
            _openDebugPanelAction.performed += OnOpenDebugPanelPerformed;
            _openDebugPanelAction.Enable();
        }

        private void DisableOpenDebugPanelAction()
        {
            if (_openDebugPanelAction == null)
            {
                return;
            }

            _openDebugPanelAction.performed -= OnOpenDebugPanelPerformed;
            _openDebugPanelAction.Disable();
        }

        private void DisposeOpenDebugPanelAction()
        {
            if (_openDebugPanelAction == null)
            {
                return;
            }

            DisableOpenDebugPanelAction();
            _openDebugPanelAction.Dispose();
            _openDebugPanelAction = null;
        }

        private static InputAction CreateOpenDebugPanelAction()
        {
            return new InputAction(
                Strings.OpenDebugPanelAction,
                InputActionType.Button,
                DefaultOpenDebugPanelBindingPath);
        }

        private void OnOpenDebugPanelPerformed(InputAction.CallbackContext context)
        {
            if (!context.performed || _consoleRuntime == null)
            {
                return;
            }

            if (_consoleRuntime.IsOpen)
            {
                RemoveToggleCharacterFromConsoleInput();
                _consoleRuntime.Close();
                return;
            }

            _consoleRuntime.Open();
        }

        private void OnConsoleOpenStateChanged(bool isOpen)
        {
            if (isOpen == _wasConsoleOpen)
            {
                return;
            }

            if (isOpen)
            {
                HandleConsoleOpened();
            }
            else
            {
                HandleConsoleClosed();
            }

            _wasConsoleOpen = isOpen;
        }

        private void HandleConsoleOpened()
        {
            StoreCursorState();
            ApplyConsoleCursorState();
            SuspendAllPlayerInputs();
            FocusConsoleInput();
            UpdateConsoleInputMirror(GetConsoleInputField());
        }

        private void HandleConsoleClosed()
        {
            HideConsoleInputMirror();
            RestorePlayerInputs();
            RestoreCursorState();
        }

        private void SuspendAllPlayerInputs()
        {
            _previousActionMaps.Clear();
            _previouslyEnabledPlayers.Clear();

            IReadOnlyList<PlayerInput> playerInputs = StaticData.PlayerInputCoordinator?.PlayerInputs;
            if (playerInputs == null)
            {
                return;
            }

            for (int i = 0; i < playerInputs.Count; i++)
            {
                CaptureAndSuspendPlayerInput(playerInputs[i]);
            }
        }

        private void SuspendNewlyJoinedPlayers()
        {
            IReadOnlyList<PlayerInput> playerInputs = StaticData.PlayerInputCoordinator?.PlayerInputs;
            if (playerInputs == null)
            {
                return;
            }

            for (int i = 0; i < playerInputs.Count; i++)
            {
                PlayerInput playerInput = playerInputs[i];
                if (playerInput == null || _previousActionMaps.ContainsKey(playerInput))
                {
                    continue;
                }

                CaptureAndSuspendPlayerInput(playerInput);
            }
        }

        private void CaptureAndSuspendPlayerInput(PlayerInput playerInput)
        {
            if (playerInput == null || _previousActionMaps.ContainsKey(playerInput))
            {
                return;
            }

            _previousActionMaps[playerInput] = playerInput.currentActionMap?.name;

            if (playerInput.actions != null && playerInput.actions.enabled)
            {
                _previouslyEnabledPlayers.Add(playerInput);
            }

            playerInput.DeactivateInput();
        }

        private void RestorePlayerInputs()
        {
            foreach (KeyValuePair<PlayerInput, string> entry in _previousActionMaps)
            {
                PlayerInput playerInput = entry.Key;
                if (playerInput == null)
                {
                    continue;
                }

                if (_previouslyEnabledPlayers.Contains(playerInput))
                {
                    playerInput.ActivateInput();

                    if (!string.IsNullOrWhiteSpace(entry.Value)
                        && playerInput.actions != null
                        && playerInput.actions.FindActionMap(entry.Value, throwIfNotFound: false) != null)
                    {
                        playerInput.SwitchCurrentActionMap(entry.Value);
                    }

                    continue;
                }

                playerInput.DeactivateInput();
            }

            _previousActionMaps.Clear();
            _previouslyEnabledPlayers.Clear();
        }

        private void StoreCursorState()
        {
            if (_hasStoredCursorState)
            {
                return;
            }

            _cursorWasVisible = UnityEngine.Cursor.visible;
            _cursorLockMode = UnityEngine.Cursor.lockState;
            _hasStoredCursorState = true;
        }

        private static void ApplyConsoleCursorState()
        {
            UnityEngine.Cursor.visible = true;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
        }

        private void FocusConsoleInput()
        {
            TextField inputField = GetConsoleInputField();
            if (inputField == null)
            {
                return;
            }

            ApplyConsoleInputVisuals(inputField);
            FocusConsoleInput(inputField);
            inputField.schedule.Execute(() =>
            {
                ApplyConsoleInputVisuals(inputField);
                FocusConsoleInput(inputField);
                UpdateConsoleInputMirror(inputField);
            }).ExecuteLater(0);
            inputField.schedule.Execute(() =>
            {
                ApplyConsoleInputVisuals(inputField);
                FocusConsoleInput(inputField);
                UpdateConsoleInputMirror(inputField);
            }).ExecuteLater(1);
        }

        private static void FocusConsoleInput(TextField inputField)
        {
            inputField.Focus();

            int endIndex = inputField.value?.Length ?? 0;
            inputField.cursorIndex = endIndex;
            inputField.selectIndex = endIndex;
        }

        private TextField GetConsoleInputField()
        {
            CacheUiDocument();
            return _uiDocument != null
                ? _uiDocument.rootVisualElement.Q<TextField>(ConsolePilotView.InputName)
                : null;
        }

        private static void ApplyConsoleInputVisuals(TextField inputField)
        {
            if (inputField == null)
            {
                return;
            }

            inputField.style.backgroundColor = InputBackgroundColor;
            inputField.style.color = InputTextColor;
            inputField.MarkDirtyRepaint();

            ApplyInputElementVisuals(inputField.Q(UnityTextInputName));
            ApplyInputElementVisuals(inputField.Q(className: UnityTextFieldInputClassName));
            ApplyInputElementVisuals(inputField.Q(className: UnityBaseTextFieldInputClassName));

            inputField.Query<TextElement>().ForEach(textElement =>
            {
                textElement.style.color = InputTextColor;
            });
        }

        private void UpdateConsoleInputMirror(TextField inputField)
        {
            if (inputField == null)
            {
                return;
            }

            Label mirror = EnsureConsoleInputMirror(inputField);
            mirror.text = inputField.value ?? string.Empty;
            mirror.style.display = DisplayStyle.Flex;
            mirror.MarkDirtyRepaint();
        }

        private Label EnsureConsoleInputMirror(TextField inputField)
        {
            if (_inputMirrorLabel != null && _inputMirrorLabel.parent == inputField)
            {
                ConfigureConsoleInputMirror(_inputMirrorLabel);
                return _inputMirrorLabel;
            }

            _inputMirrorLabel = inputField.Q<Label>(ConsoleInputMirrorName);
            if (_inputMirrorLabel == null)
            {
                _inputMirrorLabel = new Label
                {
                    name = ConsoleInputMirrorName,
                    pickingMode = PickingMode.Ignore
                };
                inputField.Add(_inputMirrorLabel);
            }

            ConfigureConsoleInputMirror(_inputMirrorLabel);
            return _inputMirrorLabel;
        }

        private static void ConfigureConsoleInputMirror(Label mirror)
        {
            mirror.pickingMode = PickingMode.Ignore;
            mirror.style.position = Position.Absolute;
            mirror.style.left = InputMirrorLeftOffset;
            mirror.style.right = 4f;
            mirror.style.top = 0f;
            mirror.style.bottom = 0f;
            mirror.style.color = InputTextColor;
            mirror.style.backgroundColor = Color.clear;
            mirror.style.unityTextAlign = TextAnchor.MiddleLeft;
            mirror.style.fontSize = 13f;
            mirror.style.whiteSpace = WhiteSpace.NoWrap;
            mirror.style.overflow = Overflow.Hidden;
        }

        private void HideConsoleInputMirror()
        {
            if (_inputMirrorLabel == null)
            {
                return;
            }

            _inputMirrorLabel.style.display = DisplayStyle.None;
        }

        private static void ApplyInputElementVisuals(VisualElement inputElement)
        {
            if (inputElement == null)
            {
                return;
            }

            inputElement.style.backgroundColor = InputBackgroundColor;
            inputElement.style.color = InputTextColor;
            inputElement.MarkDirtyRepaint();
        }

        private void RemoveToggleCharacterFromConsoleInput()
        {
            TextField inputField = GetConsoleInputField();
            string text = inputField?.value;
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            int caretIndex = Mathf.Clamp(inputField.cursorIndex, 0, text.Length);
            int removeIndex = FindToggleCharacterIndex(text, caretIndex);
            if (removeIndex < 0)
            {
                return;
            }

            string nextText = text.Remove(removeIndex, 1);
            inputField.SetValueWithoutNotify(nextText);
            inputField.cursorIndex = removeIndex;
            inputField.selectIndex = removeIndex;
            UpdateConsoleInputMirror(inputField);
        }

        private static int FindToggleCharacterIndex(string text, int caretIndex)
        {
            if (caretIndex > 0 && IsToggleCharacter(text[caretIndex - 1]))
            {
                return caretIndex - 1;
            }

            if (caretIndex < text.Length && IsToggleCharacter(text[caretIndex]))
            {
                return caretIndex;
            }

            return -1;
        }

        private static bool IsToggleCharacter(char character)
        {
            return character == '`' || character == '~';
        }

        private void RestoreCursorState()
        {
            if (!_hasStoredCursorState)
            {
                return;
            }

            UnityEngine.Cursor.visible = _cursorWasVisible;
            UnityEngine.Cursor.lockState = _cursorLockMode;
            _hasStoredCursorState = false;
        }

        private void RestoreStateIfNeeded()
        {
            if (!_wasConsoleOpen)
            {
                return;
            }

            HandleConsoleClosed();
            _wasConsoleOpen = false;
        }
    }
}
