using Bitbox;
using BitBox.Library;
using BitBox.Library.Constants;
using BitBox.Library.Constants.Enums;
using BitBox.Library.Eventing.GlobalEvents;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BitBox.Toymageddon.UserInterface
{
    [DisallowMultipleComponent]
    public sealed class HelmHudController : MonoBehaviourBase
    {
        private const string HudOutputPath = "UiCanvas/ViewportRoot/GameplayHudRoot/PlayerHudOutput";
        private const string RuntimeRootName = "HelmHudRuntime";
        private const string SpeedValueName = "SpeedValue";
        private const string PositiveFillName = "PositiveFill";
        private const string NegativeFillName = "NegativeFill";
        private const float PanelWidth = 168f;
        private const float PanelHeight = 136f;
        private const float ThrottleTrackHeight = 96f;
        private const float ThrottleTrackWidth = 14f;
        private const float ThrottleFillWidth = 8f;

        private RectTransform _hudOutputRoot;
        private RectTransform _runtimeRoot;
        private Text _speedValueText;
        private RectTransform _positiveFill;
        private RectTransform _negativeFill;
        private PlayerInput _playerInput;
        private Font _font;
        private HelmControl _activeHelm;
        private MacroSceneType _currentMacroScene = MacroSceneType.None;
        private bool _isPaused;

        protected override void OnAwakened()
        {
            CacheReferences();
            EnsureHudBuilt();
            ResetTelemetry();
            RefreshHudVisibility();
        }

        protected override void OnEnabled()
        {
            CacheReferences();
            EnsureHudBuilt();

            _currentMacroScene = StaticData.GameController != null
                ? StaticData.GameController.CurrentMacroScene
                : MacroSceneType.None;

            _globalMessageBus.Subscribe<PlayerEnteredHelmEvent>(OnPlayerEnteredHelm);
            _globalMessageBus.Subscribe<PlayerExitedHelmEvent>(OnPlayerExitedHelm);
            _globalMessageBus.Subscribe<PauseGameEvent>(OnPauseGame);
            _globalMessageBus.Subscribe<MacroSceneLoadedEvent>(OnMacroSceneLoaded);
            _globalMessageBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);

            RefreshActiveHelmReference();
            RefreshHudVisibility();
            RefreshTelemetry();
        }

        protected override void OnDisabled()
        {
            _globalMessageBus.Unsubscribe<PlayerEnteredHelmEvent>(OnPlayerEnteredHelm);
            _globalMessageBus.Unsubscribe<PlayerExitedHelmEvent>(OnPlayerExitedHelm);
            _globalMessageBus.Unsubscribe<PauseGameEvent>(OnPauseGame);
            _globalMessageBus.Unsubscribe<MacroSceneLoadedEvent>(OnMacroSceneLoaded);
            _globalMessageBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);

            _activeHelm = null;
            SetHudActive(false);
        }

        protected override void OnUpdated()
        {
            RefreshActiveHelmReference();
            RefreshHudVisibility();

            if (_runtimeRoot == null || !_runtimeRoot.gameObject.activeInHierarchy)
            {
                return;
            }

            RefreshTelemetry();
        }

        private void OnPlayerEnteredHelm(PlayerEnteredHelmEvent @event)
        {
            if (!IsLocalPlayerEvent(@event.PlayerIndex))
            {
                return;
            }

            RefreshActiveHelmReference();
            RefreshHudVisibility();
            RefreshTelemetry();
        }

        private void OnPlayerExitedHelm(PlayerExitedHelmEvent @event)
        {
            if (!IsLocalPlayerEvent(@event.PlayerIndex))
            {
                return;
            }

            _activeHelm = null;
            RefreshHudVisibility();
            ResetTelemetry();
        }

        private void OnPauseGame(PauseGameEvent @event)
        {
            _isPaused = @event.IsPaused;
            RefreshHudVisibility();
        }

        private void OnMacroSceneLoaded(MacroSceneLoadedEvent @event)
        {
            _currentMacroScene = @event.SceneType;
            _isPaused = false;

            if (!@event.SceneType.IsGameplayScene())
            {
                _activeHelm = null;
            }

            RefreshHudVisibility();
        }

        private void OnPlayerDied(PlayerDiedEvent @event)
        {
            _activeHelm = null;
            RefreshHudVisibility();
            ResetTelemetry();
        }

        private void CacheReferences()
        {
            _playerInput ??= GetComponent<PlayerInput>();
            _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (_hudOutputRoot == null)
            {
                _hudOutputRoot = transform.Find(HudOutputPath) as RectTransform;
            }

            Assert.IsNotNull(_playerInput, $"{nameof(HelmHudController)} requires {nameof(PlayerInput)}.");
            Assert.IsNotNull(_hudOutputRoot, $"{nameof(HelmHudController)} requires a RectTransform at '{HudOutputPath}'.");
            Assert.IsNotNull(_font, $"{nameof(HelmHudController)} requires the built-in LegacyRuntime font.");
        }

        private void EnsureHudBuilt()
        {
            if (_hudOutputRoot == null)
            {
                return;
            }

            _runtimeRoot ??= _hudOutputRoot.Find(RuntimeRootName) as RectTransform;
            if (_runtimeRoot != null)
            {
                BindRuntimeParts();
                return;
            }

            GameObject runtimeRootObject = CreateUiObject(RuntimeRootName, _hudOutputRoot);
            _runtimeRoot = runtimeRootObject.GetComponent<RectTransform>();
            _runtimeRoot.anchorMin = new Vector2(1f, 0f);
            _runtimeRoot.anchorMax = new Vector2(1f, 0f);
            _runtimeRoot.pivot = new Vector2(1f, 0f);
            _runtimeRoot.anchoredPosition = new Vector2(-24f, 24f);
            _runtimeRoot.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            Image background = runtimeRootObject.GetComponent<Image>();
            background.color = new Color(0.04f, 0.07f, 0.1f, 0.72f);
            background.raycastTarget = false;

            Text speedCaption = CreateText("SpeedCaption", _runtimeRoot, 14, TextAnchor.UpperLeft, new Color(0.78f, 0.84f, 0.9f, 0.7f));
            RectTransform speedCaptionRect = speedCaption.rectTransform;
            speedCaptionRect.anchorMin = new Vector2(0f, 1f);
            speedCaptionRect.anchorMax = new Vector2(0f, 1f);
            speedCaptionRect.pivot = new Vector2(0f, 1f);
            speedCaptionRect.anchoredPosition = new Vector2(18f, -16f);
            speedCaptionRect.sizeDelta = new Vector2(84f, 18f);
            speedCaption.text = "SPEED";

            _speedValueText = CreateText(SpeedValueName, _runtimeRoot, 42, TextAnchor.MiddleLeft, new Color(0.96f, 0.97f, 0.98f, 0.96f));
            RectTransform speedValueRect = _speedValueText.rectTransform;
            speedValueRect.anchorMin = new Vector2(0f, 1f);
            speedValueRect.anchorMax = new Vector2(0f, 1f);
            speedValueRect.pivot = new Vector2(0f, 1f);
            speedValueRect.anchoredPosition = new Vector2(18f, -42f);
            speedValueRect.sizeDelta = new Vector2(90f, 52f);

            Image track = CreateImage("ThrottleTrack", _runtimeRoot, new Color(0.28f, 0.34f, 0.39f, 0.9f));
            RectTransform trackRect = track.rectTransform;
            trackRect.anchorMin = new Vector2(1f, 0.5f);
            trackRect.anchorMax = new Vector2(1f, 0.5f);
            trackRect.pivot = new Vector2(1f, 0.5f);
            trackRect.anchoredPosition = new Vector2(-18f, 0f);
            trackRect.sizeDelta = new Vector2(ThrottleTrackWidth, ThrottleTrackHeight);
            track.raycastTarget = false;

            Image neutralLine = CreateImage("NeutralLine", trackRect, new Color(0.88f, 0.92f, 0.96f, 0.52f));
            RectTransform neutralLineRect = neutralLine.rectTransform;
            neutralLineRect.anchorMin = new Vector2(0f, 0.5f);
            neutralLineRect.anchorMax = new Vector2(1f, 0.5f);
            neutralLineRect.pivot = new Vector2(0.5f, 0.5f);
            neutralLineRect.anchoredPosition = Vector2.zero;
            neutralLineRect.sizeDelta = new Vector2(0f, 2f);
            neutralLine.raycastTarget = false;

            _positiveFill = CreateImage(PositiveFillName, trackRect, new Color(0.36f, 0.85f, 0.95f, 0.96f)).rectTransform;
            _positiveFill.anchorMin = new Vector2(0.5f, 0.5f);
            _positiveFill.anchorMax = new Vector2(0.5f, 0.5f);
            _positiveFill.pivot = new Vector2(0.5f, 0f);
            _positiveFill.anchoredPosition = Vector2.zero;
            _positiveFill.sizeDelta = new Vector2(ThrottleFillWidth, 0f);

            _negativeFill = CreateImage(NegativeFillName, trackRect, new Color(0.36f, 0.85f, 0.95f, 0.96f)).rectTransform;
            _negativeFill.anchorMin = new Vector2(0.5f, 0.5f);
            _negativeFill.anchorMax = new Vector2(0.5f, 0.5f);
            _negativeFill.pivot = new Vector2(0.5f, 1f);
            _negativeFill.anchoredPosition = Vector2.zero;
            _negativeFill.sizeDelta = new Vector2(ThrottleFillWidth, 0f);

            SetHudActive(false);
            ResetTelemetry();
        }

        private void BindRuntimeParts()
        {
            _speedValueText ??= _runtimeRoot.Find(SpeedValueName)?.GetComponent<Text>();
            _positiveFill ??= _runtimeRoot.Find($"ThrottleTrack/{PositiveFillName}") as RectTransform;
            _negativeFill ??= _runtimeRoot.Find($"ThrottleTrack/{NegativeFillName}") as RectTransform;
        }

        private void RefreshActiveHelmReference()
        {
            if (_playerInput == null)
            {
                _activeHelm = null;
                return;
            }

            _activeHelm = HelmControl.TryGetActiveHelm(_playerInput.playerIndex, out HelmControl helm)
                ? helm
                : null;
        }

        private void RefreshHudVisibility()
        {
            if (_runtimeRoot == null)
            {
                return;
            }

            bool shouldShow = _activeHelm != null
                && !_isPaused
                && _currentMacroScene.IsGameplayScene();

            SetHudActive(shouldShow);

            if (!shouldShow)
            {
                ResetTelemetry();
            }
        }

        private void RefreshTelemetry()
        {
            if (_speedValueText == null || _positiveFill == null || _negativeFill == null)
            {
                return;
            }

            float speed = _activeHelm != null
                ? Mathf.Abs(_activeHelm.SignedForwardSpeed)
                : 0f;

            _speedValueText.text = Mathf.RoundToInt(speed).ToString();

            float throttle = _activeHelm != null
                ? Mathf.Clamp(_activeHelm.ThrottleSettingNormalized, -1f, 1f)
                : 0f;

            float halfTrackHeight = ThrottleTrackHeight * 0.5f;
            _positiveFill.sizeDelta = new Vector2(ThrottleFillWidth, Mathf.Max(0f, throttle) * halfTrackHeight);
            _negativeFill.sizeDelta = new Vector2(ThrottleFillWidth, Mathf.Max(0f, -throttle) * halfTrackHeight);
        }

        private void ResetTelemetry()
        {
            if (_speedValueText != null)
            {
                _speedValueText.text = "0";
            }

            if (_positiveFill != null)
            {
                _positiveFill.sizeDelta = new Vector2(ThrottleFillWidth, 0f);
            }

            if (_negativeFill != null)
            {
                _negativeFill.sizeDelta = new Vector2(ThrottleFillWidth, 0f);
            }
        }

        private void SetHudActive(bool isActive)
        {
            if (_runtimeRoot != null && _runtimeRoot.gameObject.activeSelf != isActive)
            {
                _runtimeRoot.gameObject.SetActive(isActive);
            }
        }

        private bool IsLocalPlayerEvent(int playerIndex)
        {
            return _playerInput != null && _playerInput.playerIndex == playerIndex;
        }

        private GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(parent, false);
            return child;
        }

        private Image CreateImage(string objectName, Transform parent, Color color)
        {
            GameObject child = CreateUiObject(objectName, parent);
            Image image = child.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text CreateText(string objectName, Transform parent, int fontSize, TextAnchor alignment, Color color)
        {
            GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            child.transform.SetParent(parent, false);

            Text text = child.GetComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }
    }
}
