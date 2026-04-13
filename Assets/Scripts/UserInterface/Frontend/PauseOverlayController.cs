using System;
using System.Collections;
using System.Collections.Generic;
using BitBox.Library.Eventing.GlobalEvents;
using BitBox.Library.UI.Toolkit;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace BitBox.Toymageddon.UserInterface
{
    internal sealed class PauseOverlayController : IDisposable
    {
        private const float PauseBackdropBlurSigma = 14f;

        private readonly PauseScreenView _view;
        private readonly ToolkitScreenHost _screenHost;
        private readonly UiBindingScope _bindings;
        private readonly MonoBehaviour _host;
        private readonly GameObject _uiOwnerRoot;
        private readonly Func<IReadOnlyList<PlayerInput>> _getPlayerInputs;
        private readonly Action _onOpenSettingsRequested;
        private readonly Action<int> _onResumeRequested;
        private readonly Action<int> _onQuitToMainMenuRequested;
        private readonly Action<string> _logWarning;
        private readonly Dictionary<int, FrontendPlayerUiDriverState> _playerUiDriverStates = new Dictionary<int, FrontendPlayerUiDriverState>();

        private Coroutine _pauseBackdropCaptureCoroutine;
        private RenderTexture _pauseBackdropRenderTexture;
        private bool _pauseOwnershipApplied;
        private bool _pauseUiPending;
        private int _pauseOwningPlayerIndex = PauseGameEvent.NoPlayerIndex;

        public PauseOverlayController(
            PauseScreenView view,
            ToolkitScreenHost screenHost,
            UiBindingScope bindings,
            MonoBehaviour host,
            GameObject uiOwnerRoot,
            Func<IReadOnlyList<PlayerInput>> getPlayerInputs,
            Action onOpenSettingsRequested,
            Action<int> onResumeRequested,
            Action<int> onQuitToMainMenuRequested,
            Action<string> logWarning)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _screenHost = screenHost ?? throw new ArgumentNullException(nameof(screenHost));
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _uiOwnerRoot = uiOwnerRoot ?? throw new ArgumentNullException(nameof(uiOwnerRoot));
            _getPlayerInputs = getPlayerInputs ?? throw new ArgumentNullException(nameof(getPlayerInputs));
            _onOpenSettingsRequested = onOpenSettingsRequested ?? throw new ArgumentNullException(nameof(onOpenSettingsRequested));
            _onResumeRequested = onResumeRequested ?? throw new ArgumentNullException(nameof(onResumeRequested));
            _onQuitToMainMenuRequested = onQuitToMainMenuRequested ?? throw new ArgumentNullException(nameof(onQuitToMainMenuRequested));
            _logWarning = logWarning ?? throw new ArgumentNullException(nameof(logWarning));
        }

        public bool IsVisible => _screenHost.IsOverlayVisible(FrontendUiScreenIds.Pause);
        public bool CanReturnFromSettings => _pauseOwningPlayerIndex != PauseGameEvent.NoPlayerIndex;
        internal int OwningPlayerIndex => _pauseOwningPlayerIndex;

        public void Initialize()
        {
            _view.Backdrop.pickingMode = PickingMode.Ignore;
            _view.BackdropShade.pickingMode = PickingMode.Ignore;
            _view.Backdrop.style.scale = new Scale(new Vector2(1f, -1f));
            SetPauseCardVisible(true);
            ClearPauseBackdrop();

            _bindings.BindButton(_view.ResumeButton, () => _onResumeRequested(_pauseOwningPlayerIndex));
            _bindings.BindButton(_view.SettingsButton, _onOpenSettingsRequested);
            _bindings.BindButton(_view.QuitButton, () => _onQuitToMainMenuRequested(_pauseOwningPlayerIndex));
        }

        public void ShowForPlayer(int ownerPlayerIndex)
        {
            if (_screenHost.IsOverlayVisible(FrontendUiScreenIds.Pause) || _pauseBackdropCaptureCoroutine != null)
            {
                return;
            }

            _pauseOwningPlayerIndex = ownerPlayerIndex;
            _pauseUiPending = true;
            ApplyPauseOwnership(ownerPlayerIndex);
            BeginPauseOverlayPresentation();
        }

        public void PrepareForSettings()
        {
            if (_pauseOwningPlayerIndex == PauseGameEvent.NoPlayerIndex)
            {
                return;
            }

            ApplyPauseOwnership(_pauseOwningPlayerIndex);
            SetPauseCardVisible(false);
        }

        public bool RestoreFromSettings()
        {
            if (_pauseOwningPlayerIndex == PauseGameEvent.NoPlayerIndex)
            {
                return false;
            }

            ApplyPauseOwnership(_pauseOwningPlayerIndex);
            SetPauseCardVisible(true);
            ShowPauseOverlay();
            return true;
        }

        public void Close()
        {
            StopPauseBackdropCaptureRoutine();
            _screenHost.HideOverlay(FrontendUiScreenIds.Pause);
            ClearPauseBackdrop();
            ReleasePauseBackdropRenderTexture();
            SetPauseCardVisible(true);
            RestorePlayerUiDrivers();
            _pauseUiPending = false;
            _pauseOwningPlayerIndex = PauseGameEvent.NoPlayerIndex;
        }

        public void FocusDefault()
        {
            UiFocusUtility.FocusFirstFocusable(_view.Root, _view.ResumeButton.name);
        }

        internal void SetOwningPlayerForTesting(int ownerPlayerIndex)
        {
            _pauseOwningPlayerIndex = ownerPlayerIndex;
        }

        public void Dispose()
        {
            StopPauseBackdropCaptureRoutine();
            ClearPauseBackdrop();
            ReleasePauseBackdropRenderTexture();
            RestorePlayerUiDrivers();
        }

        private void BeginPauseOverlayPresentation()
        {
            StopPauseBackdropCaptureRoutine();
            SetPauseCardVisible(false);
            ClearPauseBackdrop();
            _pauseBackdropCaptureCoroutine = _host.StartCoroutine(CapturePauseBackdropAndShowOverlay());
        }

        private IEnumerator CapturePauseBackdropAndShowOverlay()
        {
            yield return new WaitForEndOfFrame();

            _pauseBackdropCaptureCoroutine = null;
            if (!_pauseUiPending)
            {
                yield break;
            }

            CapturePauseBackdrop();
            ShowPauseOverlay();
        }

        private void CapturePauseBackdrop()
        {
            int captureWidth = Mathf.Max(1, Screen.width);
            int captureHeight = Mathf.Max(1, Screen.height);
            EnsurePauseBackdropRenderTexture(captureWidth, captureHeight);

            if (_pauseBackdropRenderTexture == null)
            {
                ClearPauseBackdrop();
                return;
            }

            ScreenCapture.CaptureScreenshotIntoRenderTexture(_pauseBackdropRenderTexture);

            var background = new Background
            {
                renderTexture = _pauseBackdropRenderTexture
            };

            var blurFilter = new FilterFunction(FilterFunctionType.Blur);
            blurFilter.AddParameter(new FilterParameter(PauseBackdropBlurSigma));

            _view.Backdrop.style.backgroundImage = new StyleBackground(background);
            _view.Backdrop.style.filter = new StyleList<FilterFunction>(new List<FilterFunction> { blurFilter });
            _view.Backdrop.style.display = DisplayStyle.Flex;
            _view.BackdropShade.style.display = DisplayStyle.Flex;
        }

        private void EnsurePauseBackdropRenderTexture(int width, int height)
        {
            if (_pauseBackdropRenderTexture != null
                && _pauseBackdropRenderTexture.width == width
                && _pauseBackdropRenderTexture.height == height)
            {
                return;
            }

            ReleasePauseBackdropRenderTexture();
            _pauseBackdropRenderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _pauseBackdropRenderTexture.Create();
        }

        private void ShowPauseOverlay()
        {
            _view.Root.style.display = DisplayStyle.Flex;
            if (!_screenHost.IsOverlayVisible(FrontendUiScreenIds.Pause))
            {
                _screenHost.PushOverlay(FrontendUiScreenIds.Pause);
            }

            _view.Root.pickingMode = PickingMode.Position;
            SetPauseCardVisible(true);
            _view.Root.BringToFront();
            FocusDefault();
        }

        private void SetPauseCardVisible(bool isVisible)
        {
            _view.Card.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ClearPauseBackdrop()
        {
            _view.Backdrop.style.display = DisplayStyle.None;
            _view.BackdropShade.style.display = DisplayStyle.None;
            _view.Backdrop.style.backgroundImage = new StyleBackground(StyleKeyword.None);
        }

        private void StopPauseBackdropCaptureRoutine()
        {
            if (_pauseBackdropCaptureCoroutine == null)
            {
                return;
            }

            _host.StopCoroutine(_pauseBackdropCaptureCoroutine);
            _pauseBackdropCaptureCoroutine = null;
        }

        private void ReleasePauseBackdropRenderTexture()
        {
            if (_pauseBackdropRenderTexture == null)
            {
                return;
            }

            _pauseBackdropRenderTexture.Release();
            UnityEngine.Object.Destroy(_pauseBackdropRenderTexture);
            _pauseBackdropRenderTexture = null;
        }

        private void ApplyPauseOwnership(int ownerPlayerIndex)
        {
            RestorePlayerUiDrivers();

            IReadOnlyList<PlayerInput> playerInputs = _getPlayerInputs();
            if (playerInputs == null || playerInputs.Count == 0)
            {
                _logWarning("Pause ownership was requested but no player inputs are available. Shared pause will fall back to pointer-only interaction.");
                return;
            }

            foreach (PlayerInput playerInput in playerInputs)
            {
                if (playerInput == null)
                {
                    continue;
                }

                var eventSystem = playerInput.GetComponent<MultiplayerEventSystem>();
                var inputModule = playerInput.GetComponent<InputSystemUIInputModule>();
                if (eventSystem == null || inputModule == null)
                {
                    _logWarning($"Missing multiplayer UI driver components on player '{playerInput.name}'. eventSystemPresent={eventSystem != null}, inputModulePresent={inputModule != null}");
                    continue;
                }

                _playerUiDriverStates[playerInput.playerIndex] = new FrontendPlayerUiDriverState
                {
                    EventSystem = eventSystem,
                    InputModule = inputModule,
                    OriginalPlayerRoot = eventSystem.playerRoot,
                    EventSystemWasEnabled = eventSystem.enabled,
                    InputModuleWasEnabled = inputModule.enabled
                };

                bool isOwner = playerInput.playerIndex == ownerPlayerIndex;
                if (isOwner)
                {
                    eventSystem.playerRoot = _uiOwnerRoot;
                    eventSystem.enabled = true;
                    inputModule.enabled = true;
                    eventSystem.SetSelectedGameObject(null);
                }
                else
                {
                    inputModule.enabled = false;
                    eventSystem.enabled = false;
                }
            }

            _pauseOwnershipApplied = _playerUiDriverStates.Count > 0;
        }

        private void RestorePlayerUiDrivers()
        {
            if (!_pauseOwnershipApplied)
            {
                return;
            }

            foreach (FrontendPlayerUiDriverState state in _playerUiDriverStates.Values)
            {
                if (state.EventSystem != null)
                {
                    state.EventSystem.playerRoot = state.OriginalPlayerRoot;
                    state.EventSystem.SetSelectedGameObject(null);
                    state.EventSystem.enabled = state.EventSystemWasEnabled;
                }

                if (state.InputModule != null)
                {
                    state.InputModule.enabled = state.InputModuleWasEnabled;
                }
            }

            _playerUiDriverStates.Clear();
            _pauseOwnershipApplied = false;
        }
    }
}
