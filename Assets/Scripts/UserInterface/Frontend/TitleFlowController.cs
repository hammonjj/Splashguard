using System;
using BitBox.Library;
using BitBox.Library.Constants;
using BitBox.Library.Localization;
using BitBox.Library.UI.Toolkit;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UIElements;

namespace BitBox.Toymageddon.UserInterface
{
    internal sealed class TitleFlowController
    {
        private const float JoinPromptInputDebounceSeconds = 0.2f;
        private const float TitleLayoutMinimumWideWidth = 1180f;
        private const float TitleLayoutGapMinWidth = 220f;
        private const float TitleLayoutGapMaxWidth = 360f;

        private readonly FrontendUiRuntime _runtime;
        private readonly UiBindingScope _bindings;
        private readonly Action _onOpenSettingsRequested;
        private readonly Action<PendingPlayerJoinRequest> _onJoinPromptAccepted;
        private readonly Action<string> _logInfo;
        private bool _isAwaitingJoinPromptInput;
        private float _joinPromptInputAcceptTime;

        public TitleFlowController(
            FrontendUiRuntime runtime,
            UiBindingScope bindings,
            Action onOpenSettingsRequested,
            Action<PendingPlayerJoinRequest> onJoinPromptAccepted,
            Action<string> logInfo)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            _onOpenSettingsRequested = onOpenSettingsRequested ?? throw new ArgumentNullException(nameof(onOpenSettingsRequested));
            _onJoinPromptAccepted = onJoinPromptAccepted ?? throw new ArgumentNullException(nameof(onJoinPromptAccepted));
            _logInfo = logInfo ?? throw new ArgumentNullException(nameof(logInfo));
        }

        internal bool IsAwaitingJoinPromptInput => _isAwaitingJoinPromptInput;

        public void Initialize()
        {
            _bindings.BindButton(_runtime.TitleScreen.StartButton, OnStartClicked);
            _bindings.BindButton(_runtime.TitleScreen.SettingsButton, _onOpenSettingsRequested);
            _bindings.BindButton(_runtime.TitleScreen.QuitButton, OnQuitClicked);

            _runtime.TitleScreen.Root.RegisterCallback<GeometryChangedEvent>(OnTitleGeometryChanged);
            _bindings.Register(() => _runtime.TitleScreen.Root.UnregisterCallback<GeometryChangedEvent>(OnTitleGeometryChanged));

            if (_runtime.TitleScreen.Layout != null)
            {
                _runtime.TitleScreen.Layout.RegisterCallback<GeometryChangedEvent>(OnTitleLayoutGeometryChanged);
                _bindings.Register(() => _runtime.TitleScreen.Layout.UnregisterCallback<GeometryChangedEvent>(OnTitleLayoutGeometryChanged));
            }

            _runtime.UiDocument.rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnFrontendRootGeometryChanged);
            _bindings.Register(() => _runtime.UiDocument.rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnFrontendRootGeometryChanged));

            _runtime.FrontendRootElement.RegisterCallback<GeometryChangedEvent>(OnFrontendRootGeometryChanged);
            _bindings.Register(() => _runtime.FrontendRootElement.UnregisterCallback<GeometryChangedEvent>(OnFrontendRootGeometryChanged));

            _runtime.BaseLayerElement.RegisterCallback<GeometryChangedEvent>(OnFrontendRootGeometryChanged);
            _bindings.Register(() => _runtime.BaseLayerElement.UnregisterCallback<GeometryChangedEvent>(OnFrontendRootGeometryChanged));
        }

        public void Tick(float currentTime)
        {
            if (!CanAcceptJoinInput(currentTime))
            {
                return;
            }

            if (!TryConsumeJoinPromptInput(out InputDevice device, out string controlPath))
            {
                return;
            }

            PendingPlayerJoinRequest pendingJoinRequest = CreatePendingJoinRequest(device, controlPath);
            _isAwaitingJoinPromptInput = false;
            _logInfo(
                $"Join prompt accepted input from '{controlPath}'. " +
                $"Pending join controlScheme={pendingJoinRequest.ControlScheme ?? "None"}, pairDevice={pendingJoinRequest.PairWithDevice?.displayName ?? "None"}.");
            _onJoinPromptAccepted(pendingJoinRequest);
        }

        public void ShowTitle()
        {
            _isAwaitingJoinPromptInput = false;
            _runtime.ScreenHost.ShowBaseScreen(FrontendUiScreenIds.Title);
            UpdateTitleLayoutSpacing();
            FocusTitleDefault();
        }

        public void Hide()
        {
            _isAwaitingJoinPromptInput = false;
        }

        public bool HandleCancel()
        {
            if (_runtime.ScreenHost.ActiveBaseScreenId != FrontendUiScreenIds.JoinPrompt)
            {
                return false;
            }

            _logInfo("Frontend cancel routed from join prompt back to title.");
            ReturnToTitleFromJoinPrompt();
            return true;
        }

        public void RefreshLocalizedText()
        {
            _runtime.TitleScreen.StartButton.text = GameText.Get("ui.start");
            _runtime.TitleScreen.SettingsButton.text = GameText.Get("ui.settings");
            _runtime.TitleScreen.QuitButton.text = GameText.Get("ui.quit");
        }

        public void FocusTitleDefault()
        {
            UiFocusUtility.FocusFirstFocusable(_runtime.TitleScreen.Root, _runtime.TitleScreen.StartButton.name);
        }

        public void RestoreFocus()
        {
            if (_runtime.ScreenHost.ActiveBaseScreenId == FrontendUiScreenIds.Title)
            {
                FocusTitleDefault();
            }
        }

        internal void ShowJoinPromptForTesting(float currentTime)
        {
            ShowJoinPrompt(currentTime);
        }

        internal bool CanAcceptJoinInputForTesting(float currentTime)
        {
            return CanAcceptJoinInput(currentTime);
        }

        private void OnStartClicked()
        {
            ShowJoinPrompt(Time.unscaledTime);
            _logInfo("Displayed join prompt screen. Awaiting any input before loading HubWorld.");
        }

        private void OnQuitClicked()
        {
            _logInfo("Quit button clicked from front-end UI.");
            Application.Quit();
        }

        private void ShowJoinPrompt(float currentTime)
        {
            _runtime.ScreenHost.ShowBaseScreen(FrontendUiScreenIds.JoinPrompt);
            _isAwaitingJoinPromptInput = true;
            _joinPromptInputAcceptTime = currentTime + JoinPromptInputDebounceSeconds;
        }

        private bool CanAcceptJoinInput(float currentTime)
        {
            return _isAwaitingJoinPromptInput
                && _runtime.ScreenHost.ActiveBaseScreenId == FrontendUiScreenIds.JoinPrompt
                && currentTime >= _joinPromptInputAcceptTime;
        }

        private void ReturnToTitleFromJoinPrompt()
        {
            _isAwaitingJoinPromptInput = false;
            _runtime.ScreenHost.ShowBaseScreen(FrontendUiScreenIds.Title);
            FocusTitleDefault();
        }

        private void OnTitleGeometryChanged(GeometryChangedEvent @event)
        {
            _runtime.TitleScreen.Root.schedule.Execute(() =>
            {
                VisualElement titleCard = _runtime.TitleScreen.Root.Q(className: "title-card");
                Rect screenBounds = _runtime.TitleScreen.Root.worldBound;
                Rect cardBounds = titleCard?.worldBound ?? default;

                _logInfo(
                    $"Title layout diagnostics. screenBounds={screenBounds}, cardBounds={cardBounds}, " +
                    $"screenResolvedWidth={_runtime.TitleScreen.Root.resolvedStyle.width:F2}, screenResolvedHeight={_runtime.TitleScreen.Root.resolvedStyle.height:F2}, " +
                    $"cardResolvedWidth={(titleCard != null ? titleCard.resolvedStyle.width : 0f):F2}, " +
                    $"cardResolvedHeight={(titleCard != null ? titleCard.resolvedStyle.height : 0f):F2}");
            }).ExecuteLater(0);
        }

        private void OnFrontendRootGeometryChanged(GeometryChangedEvent @event)
        {
            UpdateTitleLayoutSpacing();
            _runtime.UiDocument.rootVisualElement.schedule.Execute(LogFrontendRootSnapshot).ExecuteLater(0);
        }

        private void OnTitleLayoutGeometryChanged(GeometryChangedEvent @event)
        {
            UpdateTitleLayoutSpacing();
        }

        private void UpdateTitleLayoutSpacing()
        {
            if (_runtime.TitleScreen.Layout == null
                || _runtime.TitleScreen.LayoutSpacer == null
                || _runtime.TitleScreen.MainCard == null
                || _runtime.TitleScreen.PlaytestCard == null)
            {
                return;
            }

            _runtime.TitleScreen.Layout.schedule.Execute(() =>
            {
                if (_runtime.ScreenHost.ActiveBaseScreenId != FrontendUiScreenIds.Title)
                {
                    return;
                }

                float layoutWidth = _runtime.TitleScreen.Layout.resolvedStyle.width;
                float mainCardWidth = ResolveLayoutWidth(_runtime.TitleScreen.MainCard, 460f);
                float playtestCardWidth = ResolveLayoutWidth(_runtime.TitleScreen.PlaytestCard, 440f);
                float desiredGapWidth = Mathf.Clamp(layoutWidth * 0.18f, TitleLayoutGapMinWidth, TitleLayoutGapMaxWidth);
                bool useWideGap = layoutWidth >= Mathf.Max(TitleLayoutMinimumWideWidth, mainCardWidth + playtestCardWidth + desiredGapWidth);

                float appliedGapWidth = useWideGap ? desiredGapWidth : 0f;
                _runtime.TitleScreen.LayoutSpacer.style.display = useWideGap ? DisplayStyle.Flex : DisplayStyle.None;
                _runtime.TitleScreen.LayoutSpacer.style.width = appliedGapWidth;
                _runtime.TitleScreen.LayoutSpacer.style.minWidth = appliedGapWidth;
                _runtime.TitleScreen.LayoutSpacer.style.maxWidth = appliedGapWidth;
            }).ExecuteLater(0);
        }

        private void LogFrontendRootSnapshot()
        {
            Rect documentBounds = _runtime.UiDocument.rootVisualElement.worldBound;
            Rect frontendBounds = _runtime.FrontendRootElement.worldBound;
            Rect baseLayerBounds = _runtime.BaseLayerElement.worldBound;
            Rect overlayLayerBounds = _runtime.OverlayLayerElement.worldBound;
            Vector2 panelSize = _runtime.UiDocument.rootVisualElement.panel?.visualTree.layout.size ?? Vector2.zero;

            _logInfo(
                $"Frontend root geometry. documentBounds={documentBounds}, frontendBounds={frontendBounds}, " +
                $"baseLayerBounds={baseLayerBounds}, overlayLayerBounds={overlayLayerBounds}, panelSize={panelSize}, " +
                $"documentResolved=({_runtime.UiDocument.rootVisualElement.resolvedStyle.width:F2}, {_runtime.UiDocument.rootVisualElement.resolvedStyle.height:F2}), " +
                $"frontendResolved=({_runtime.FrontendRootElement.resolvedStyle.width:F2}, {_runtime.FrontendRootElement.resolvedStyle.height:F2}), " +
                $"baseResolved=({_runtime.BaseLayerElement.resolvedStyle.width:F2}, {_runtime.BaseLayerElement.resolvedStyle.height:F2})");
        }

        private static float ResolveLayoutWidth(VisualElement element, float fallbackWidth)
        {
            if (element == null)
            {
                return fallbackWidth;
            }

            float resolvedWidth = element.resolvedStyle.width;
            return resolvedWidth > 0f ? resolvedWidth : fallbackWidth;
        }

        private static bool TryConsumeJoinPromptInput(out InputDevice device, out string controlPath)
        {
            foreach (InputDevice inputDevice in InputSystem.devices)
            {
                if (inputDevice == null || !inputDevice.enabled)
                {
                    continue;
                }

                foreach (InputControl control in inputDevice.allControls)
                {
                    if (control is not ButtonControl button || button.synthetic)
                    {
                        continue;
                    }

                    if (!button.wasPressedThisFrame)
                    {
                        continue;
                    }

                    device = button.device;
                    controlPath = button.path;
                    return true;
                }
            }

            device = null;
            controlPath = null;
            return false;
        }

        private static PendingPlayerJoinRequest CreatePendingJoinRequest(InputDevice device, string controlPath)
        {
            if (device is Keyboard || device is Mouse)
            {
                return new PendingPlayerJoinRequest
                {
                    ControlScheme = Strings.KeyboardControlScheme,
                    PairWithDevice = (InputDevice)Keyboard.current ?? Mouse.current,
                    SourceControlPath = controlPath
                };
            }

            if (device is Gamepad)
            {
                return new PendingPlayerJoinRequest
                {
                    ControlScheme = Strings.GamepadControlScheme,
                    PairWithDevice = device,
                    SourceControlPath = controlPath
                };
            }

            return new PendingPlayerJoinRequest
            {
                ControlScheme = null,
                PairWithDevice = device,
                SourceControlPath = controlPath
            };
        }
    }
}
