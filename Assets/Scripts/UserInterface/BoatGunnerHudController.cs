using Bitbox;
using BitBox.Library;
using BitBox.Library.Eventing;
using BitBox.Library.Constants;
using BitBox.Library.Constants.Enums;
using BitBox.Library.Eventing.GlobalEvents;
using BitBox.Library.Eventing.WeaponEvents;
using BitBox.Toymageddon.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BitBox.Toymageddon.UserInterface
{
    [DisallowMultipleComponent]
    public sealed class BoatGunnerHudController : MonoBehaviourBase
    {
        private const string HudOutputPath = "UiCanvas/ViewportRoot/GameplayHudRoot/PlayerHudOutput";
        private const string RuntimeRootName = "BoatGunnerHudRuntime";
        private const string AmmoValueName = "AmmoValue";
        private const string HeatSliderName = "HeatSlider";
        private const string HeatStatusName = "HeatStatus";
        private const string HeatFillName = "Fill";
        private const string CrosshairRootName = "CrosshairRoot";
        private const float AmmoPanelWidth = 188f;
        private const float AmmoPanelHeight = 104f;
        private const float HeatSliderHeight = 18f;
        private const float CrosshairSize = 36f;
        private const float CrosshairGap = 6f;
        private const float CrosshairArmLength = 11f;
        private const float CrosshairThickness = 2f;
        private const float CrosshairClampMargin = 18f;

        [SerializeField] private float _projectionDistance = 120f;

        private RectTransform _hudOutputRoot;
        private RectTransform _runtimeRoot;
        private RectTransform _crosshairRoot;
        private Text _ammoValueText;
        private Text _heatStatusText;
        private Slider _heatSlider;
        private Image _heatSliderBackground;
        private Image _heatFillImage;
        private PlayerInput _playerInput;
        private PlayerDataReference _playerData;
        private Font _font;
        private Canvas _canvas;
        private DeckMountedGunControl _activeGun;
        private PlayerWeaponController _activeWeaponController;
        private MessageBus _activeWeaponBus;
        private MacroSceneType _currentMacroScene = MacroSceneType.None;
        private bool _isPaused;

        protected override void OnAwakened()
        {
            CacheReferences();
            EnsureHudBuilt();
            ResetHudState();
            RefreshHudVisibility();
        }

        protected override void OnEnabled()
        {
            CacheReferences();
            EnsureHudBuilt();

            _currentMacroScene = StaticData.GameController != null
                ? StaticData.GameController.CurrentMacroScene
                : MacroSceneType.None;

            _globalMessageBus.Subscribe<PlayerEnteredBoatGunEvent>(OnPlayerEnteredBoatGun);
            _globalMessageBus.Subscribe<PlayerExitedBoatGunEvent>(OnPlayerExitedBoatGun);
            _globalMessageBus.Subscribe<WeaponAmmoChangedEvent>(OnWeaponAmmoChanged);
            _globalMessageBus.Subscribe<PauseGameEvent>(OnPauseGame);
            _globalMessageBus.Subscribe<MacroSceneLoadedEvent>(OnMacroSceneLoaded);
            _globalMessageBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);

            RefreshActiveGunReference();
            RefreshHudVisibility();
            RefreshAmmoText();
        }

        protected override void OnDisabled()
        {
            _globalMessageBus.Unsubscribe<PlayerEnteredBoatGunEvent>(OnPlayerEnteredBoatGun);
            _globalMessageBus.Unsubscribe<PlayerExitedBoatGunEvent>(OnPlayerExitedBoatGun);
            _globalMessageBus.Unsubscribe<WeaponAmmoChangedEvent>(OnWeaponAmmoChanged);
            _globalMessageBus.Unsubscribe<PauseGameEvent>(OnPauseGame);
            _globalMessageBus.Unsubscribe<MacroSceneLoadedEvent>(OnMacroSceneLoaded);
            _globalMessageBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);

            BindActiveWeaponBus(null);
            _activeGun = null;
            _activeWeaponController = null;
            SetHudActive(false);
        }

        protected override void OnUpdated()
        {
            RefreshActiveGunReference();
            RefreshHudVisibility();

            if (_runtimeRoot == null || !_runtimeRoot.gameObject.activeInHierarchy)
            {
                return;
            }

            RefreshAmmoText();
            UpdateCrosshairPosition();
        }

        private void OnPlayerEnteredBoatGun(PlayerEnteredBoatGunEvent @event)
        {
            if (!IsLocalPlayerEvent(@event.PlayerIndex))
            {
                return;
            }

            RefreshActiveGunReference();
            RefreshHudVisibility();
            RefreshAmmoText();
            UpdateCrosshairPosition();
        }

        private void OnPlayerExitedBoatGun(PlayerExitedBoatGunEvent @event)
        {
            if (!IsLocalPlayerEvent(@event.PlayerIndex))
            {
                return;
            }

            _activeGun = null;
            _activeWeaponController = null;
            BindActiveWeaponBus(null);
            RefreshHudVisibility();
            ResetHudState();
        }

        private void OnWeaponAmmoChanged(WeaponAmmoChangedEvent @event)
        {
            if (!IsLocalPlayerEvent(@event.PlayerIndex))
            {
                return;
            }

            RefreshAmmoText(@event.CurrentAmmo, @event.ClipCapacity, @event.InfiniteAmmo);
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
                _activeGun = null;
                _activeWeaponController = null;
                BindActiveWeaponBus(null);
            }

            RefreshHudVisibility();
        }

        private void OnPlayerDied(PlayerDiedEvent @event)
        {
            _activeGun = null;
            _activeWeaponController = null;
            BindActiveWeaponBus(null);
            RefreshHudVisibility();
            ResetHudState();
        }

        private void OnWeaponHeatChanged(WeaponHeatChangedEvent @event)
        {
            if (@event == null || !IsLocalPlayerEvent(@event.PlayerIndex))
            {
                return;
            }

            SetHeatSliderState(@event.HeatEnabled, @event.NormalizedHeat, @event.IsOverheated);
        }

        private void CacheReferences()
        {
            _playerInput ??= GetComponent<PlayerInput>();
            _playerData ??= GetComponent<PlayerDataReference>();
            _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (_hudOutputRoot == null)
            {
                _hudOutputRoot = transform.Find(HudOutputPath) as RectTransform;
            }

            if (_canvas == null && _hudOutputRoot != null)
            {
                _canvas = _hudOutputRoot.GetComponentInParent<Canvas>();
            }

            Assert.IsNotNull(_playerInput, $"{nameof(BoatGunnerHudController)} requires {nameof(PlayerInput)}.");
            Assert.IsNotNull(_playerData, $"{nameof(BoatGunnerHudController)} requires {nameof(PlayerDataReference)}.");
            Assert.IsNotNull(_hudOutputRoot, $"{nameof(BoatGunnerHudController)} requires a RectTransform at '{HudOutputPath}'.");
            Assert.IsNotNull(_font, $"{nameof(BoatGunnerHudController)} requires the built-in LegacyRuntime font.");
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

            GameObject runtimeRootObject = new GameObject(RuntimeRootName, typeof(RectTransform));
            runtimeRootObject.transform.SetParent(_hudOutputRoot, false);
            _runtimeRoot = runtimeRootObject.GetComponent<RectTransform>();
            _runtimeRoot.anchorMin = Vector2.zero;
            _runtimeRoot.anchorMax = Vector2.one;
            _runtimeRoot.pivot = new Vector2(0.5f, 0.5f);
            _runtimeRoot.anchoredPosition = Vector2.zero;
            _runtimeRoot.sizeDelta = Vector2.zero;

            Image ammoBackground = CreateImage("AmmoPanel", _runtimeRoot, new Color(0.04f, 0.07f, 0.1f, 0.72f));
            RectTransform ammoPanelRect = ammoBackground.rectTransform;
            ammoPanelRect.anchorMin = new Vector2(1f, 0f);
            ammoPanelRect.anchorMax = new Vector2(1f, 0f);
            ammoPanelRect.pivot = new Vector2(1f, 0f);
            ammoPanelRect.anchoredPosition = new Vector2(-24f, 24f);
            ammoPanelRect.sizeDelta = new Vector2(AmmoPanelWidth, AmmoPanelHeight);

            _ammoValueText = CreateText(AmmoValueName, ammoPanelRect, 22, TextAnchor.MiddleRight, new Color(0.96f, 0.97f, 0.98f, 0.96f));
            RectTransform ammoTextRect = _ammoValueText.rectTransform;
            ammoTextRect.anchorMin = Vector2.zero;
            ammoTextRect.anchorMax = Vector2.one;
            ammoTextRect.pivot = new Vector2(0.5f, 0.5f);
            ammoTextRect.anchoredPosition = new Vector2(0f, 24f);
            ammoTextRect.sizeDelta = new Vector2(-18f, -58f);

            _heatStatusText = CreateHeatStatusText(ammoPanelRect);
            _heatSlider = CreateHeatSlider(ammoPanelRect);

            _crosshairRoot = new GameObject(CrosshairRootName, typeof(RectTransform)).GetComponent<RectTransform>();
            _crosshairRoot.SetParent(_runtimeRoot, false);
            _crosshairRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _crosshairRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _crosshairRoot.pivot = new Vector2(0.5f, 0.5f);
            _crosshairRoot.anchoredPosition = Vector2.zero;
            _crosshairRoot.sizeDelta = new Vector2(CrosshairSize, CrosshairSize);

            CreateCrosshairArm("TopArm", new Vector2(0.5f, 1f), new Vector2(0f, -CrosshairGap), new Vector2(CrosshairThickness, CrosshairArmLength));
            CreateCrosshairArm("BottomArm", new Vector2(0.5f, 0f), new Vector2(0f, CrosshairGap), new Vector2(CrosshairThickness, CrosshairArmLength));
            CreateCrosshairArm("LeftArm", new Vector2(0f, 0.5f), new Vector2(CrosshairGap, 0f), new Vector2(CrosshairArmLength, CrosshairThickness));
            CreateCrosshairArm("RightArm", new Vector2(1f, 0.5f), new Vector2(-CrosshairGap, 0f), new Vector2(CrosshairArmLength, CrosshairThickness));

            SetHudActive(false);
            ResetHudState();
        }

        private void BindRuntimeParts()
        {
            _ammoValueText ??= _runtimeRoot.Find($"AmmoPanel/{AmmoValueName}")?.GetComponent<Text>();
            _heatStatusText ??= _runtimeRoot.Find($"AmmoPanel/{HeatStatusName}")?.GetComponent<Text>();
            _heatSlider ??= _runtimeRoot.Find($"AmmoPanel/{HeatSliderName}")?.GetComponent<Slider>();
            _heatSliderBackground ??= _runtimeRoot.Find($"AmmoPanel/{HeatSliderName}")?.GetComponent<Image>();
            _heatFillImage ??= _runtimeRoot.Find($"AmmoPanel/{HeatSliderName}/Fill Area/{HeatFillName}")?.GetComponent<Image>();
            RectTransform ammoPanel = _runtimeRoot.Find("AmmoPanel") as RectTransform;
            if (_heatStatusText == null && ammoPanel != null)
            {
                _heatStatusText = CreateHeatStatusText(ammoPanel);
            }

            if (_heatSlider == null)
            {
                if (ammoPanel != null)
                {
                    _heatSlider = CreateHeatSlider(ammoPanel);
                }
            }

            _crosshairRoot ??= _runtimeRoot.Find(CrosshairRootName) as RectTransform;
        }

        private Text CreateHeatStatusText(RectTransform parent)
        {
            Text statusText = CreateText(HeatStatusName, parent, 11, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.34f, 0.98f));
            statusText.text = "OVERHEATED - COOLING";
            statusText.fontStyle = FontStyle.Bold;
            statusText.raycastTarget = false;

            RectTransform statusRect = statusText.rectTransform;
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.anchoredPosition = new Vector2(0f, 36f);
            statusRect.sizeDelta = new Vector2(-18f, 16f);
            statusText.gameObject.SetActive(false);
            return statusText;
        }

        private Slider CreateHeatSlider(RectTransform parent)
        {
            GameObject sliderObject = new GameObject(HeatSliderName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
            sliderObject.transform.SetParent(parent, false);

            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 0f);
            sliderRect.anchorMax = new Vector2(1f, 0f);
            sliderRect.pivot = new Vector2(0.5f, 0f);
            sliderRect.anchoredPosition = new Vector2(0f, 8f);
            sliderRect.sizeDelta = new Vector2(-18f, HeatSliderHeight);

            _heatSliderBackground = sliderObject.GetComponent<Image>();
            _heatSliderBackground.color = new Color(0.02f, 0.025f, 0.025f, 0.85f);
            _heatSliderBackground.raycastTarget = false;

            RectTransform fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
            fillArea.SetParent(sliderRect, false);
            fillArea.anchorMin = Vector2.zero;
            fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = new Vector2(1f, 1f);
            fillArea.offsetMax = new Vector2(-1f, -1f);

            GameObject fillObject = new GameObject(HeatFillName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillObject.transform.SetParent(fillArea, false);
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            _heatFillImage = fillObject.GetComponent<Image>();
            _heatFillImage.color = new Color(0.92f, 0.62f, 0.14f, 0.96f);
            _heatFillImage.raycastTarget = false;

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.wholeNumbers = false;
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fillRect;
            slider.targetGraphic = _heatFillImage;
            sliderObject.SetActive(false);
            return slider;
        }

        private void CreateCrosshairArm(string objectName, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            Image image = CreateImage(objectName, _crosshairRoot, new Color(0.96f, 0.98f, 1f, 0.9f));
            RectTransform rect = image.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private void RefreshActiveGunReference()
        {
            DeckMountedGunControl nextGun;
            PlayerWeaponController nextWeaponController;
            MessageBus nextWeaponBus;

            if (_playerInput == null)
            {
                nextGun = null;
                nextWeaponController = null;
                nextWeaponBus = null;
            }
            else
            {
                nextGun = DeckMountedGunControl.TryGetActiveGun(_playerInput.playerIndex, out DeckMountedGunControl gun)
                    ? gun
                    : null;
                nextWeaponController = nextGun != null
                    ? nextGun.GetComponent<PlayerWeaponController>()
                    : null;
                nextWeaponBus = nextGun != null
                    ? nextGun.GetComponent<MessageBus>()
                    : null;
            }

            bool changed = nextGun != _activeGun
                || nextWeaponController != _activeWeaponController
                || nextWeaponBus != _activeWeaponBus;

            _activeGun = nextGun;
            _activeWeaponController = nextWeaponController;
            BindActiveWeaponBus(nextWeaponBus);

            if (changed)
            {
                RequestHeatSnapshot();
            }
        }

        private void RefreshHudVisibility()
        {
            if (_runtimeRoot == null)
            {
                return;
            }

            bool shouldShow = _activeGun != null
                && _activeWeaponController != null
                && !_isPaused
                && _currentMacroScene.IsGameplayScene();

            SetHudActive(shouldShow);

            if (!shouldShow)
            {
                ResetHudState();
            }
        }

        private void RefreshAmmoText()
        {
            if (_activeWeaponController == null)
            {
                RefreshAmmoText(0, 0, false);
                return;
            }

            RefreshAmmoText(
                _activeWeaponController.CurrentAmmo,
                _activeWeaponController.ClipCapacity,
                _activeWeaponController.InfiniteAmmo);
        }

        private void RefreshAmmoText(int currentAmmo, int clipCapacity, bool infiniteAmmo)
        {
            if (_ammoValueText == null)
            {
                return;
            }

            string currentAmmoText = infiniteAmmo ? "\u221e" : Mathf.Max(0, currentAmmo).ToString();
            _ammoValueText.text = $"AMMO {currentAmmoText} / {Mathf.Max(0, clipCapacity)}";
        }

        private void BindActiveWeaponBus(MessageBus nextBus)
        {
            if (_activeWeaponBus == nextBus)
            {
                return;
            }

            if (_activeWeaponBus != null)
            {
                _activeWeaponBus.Unsubscribe<WeaponHeatChangedEvent>(OnWeaponHeatChanged);
            }

            _activeWeaponBus = nextBus;
            if (_activeWeaponBus != null)
            {
                _activeWeaponBus.Subscribe<WeaponHeatChangedEvent>(OnWeaponHeatChanged);
            }
            else
            {
                SetHeatSliderState(false, 0f, false);
            }
        }

        private void RequestHeatSnapshot()
        {
            if (_activeWeaponBus == null || _playerInput == null)
            {
                SetHeatSliderState(false, 0f, false);
                return;
            }

            _activeWeaponBus.Publish(new WeaponHeatSnapshotRequestedEvent(_playerInput.playerIndex));
        }

        private void SetHeatSliderState(bool heatEnabled, float normalizedHeat, bool isOverheated)
        {
            if (_heatSlider == null)
            {
                return;
            }

            _heatSlider.gameObject.SetActive(heatEnabled);
            _heatSlider.SetValueWithoutNotify(Mathf.Clamp01(normalizedHeat));

            if (_heatStatusText != null)
            {
                _heatStatusText.gameObject.SetActive(heatEnabled && isOverheated);
            }

            if (_heatSliderBackground != null)
            {
                Color normalBackground = new(0.02f, 0.025f, 0.025f, 0.85f);
                Color warningBackground = new(0.2f, 0.018f, 0.012f, 0.96f);
                _heatSliderBackground.color = isOverheated ? warningBackground : normalBackground;
            }

            if (_heatFillImage != null)
            {
                Color coolColor = new(0.92f, 0.62f, 0.14f, 0.96f);
                Color hotColor = new(0.95f, 0.12f, 0.04f, 0.98f);
                Color overheatColor = new(1f, 0.02f, 0.02f, 1f);
                _heatFillImage.color = isOverheated
                    ? overheatColor
                    : Color.Lerp(coolColor, hotColor, Mathf.Clamp01(normalizedHeat));
            }
        }

        private void UpdateCrosshairPosition()
        {
            if (_crosshairRoot == null || _activeWeaponController == null || _playerData == null)
            {
                SetCrosshairActive(false);
                return;
            }

            Transform firePoint = _activeWeaponController.FirePoint;
            Camera gameplayCamera = _playerData.GameplayCamera;
            if (firePoint == null || gameplayCamera == null)
            {
                SetCrosshairActive(false);
                return;
            }

            Vector3 aimPoint = firePoint.position + firePoint.forward * Mathf.Max(1f, _projectionDistance);
            Vector3 screenPoint = gameplayCamera.WorldToScreenPoint(aimPoint);
            if (screenPoint.z <= 0f)
            {
                SetCrosshairActive(false);
                return;
            }

            Camera eventCamera = ResolveCanvasEventCamera(gameplayCamera);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _runtimeRoot,
                    new Vector2(screenPoint.x, screenPoint.y),
                    eventCamera,
                    out Vector2 localPoint))
            {
                SetCrosshairActive(false);
                return;
            }

            Rect runtimeRect = _runtimeRoot.rect;
            localPoint.x = Mathf.Clamp(localPoint.x, runtimeRect.xMin + CrosshairClampMargin, runtimeRect.xMax - CrosshairClampMargin);
            localPoint.y = Mathf.Clamp(localPoint.y, runtimeRect.yMin + CrosshairClampMargin, runtimeRect.yMax - CrosshairClampMargin);
            _crosshairRoot.anchoredPosition = localPoint;
            SetCrosshairActive(true);
        }

        private Camera ResolveCanvasEventCamera(Camera fallbackCamera)
        {
            if (_canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return _canvas.worldCamera != null
                ? _canvas.worldCamera
                : fallbackCamera;
        }

        private void ResetHudState()
        {
            RefreshAmmoText(0, 0, false);
            SetHeatSliderState(false, 0f, false);
            if (_crosshairRoot != null)
            {
                _crosshairRoot.anchoredPosition = Vector2.zero;
            }

            SetCrosshairActive(false);
        }

        private void SetHudActive(bool isActive)
        {
            if (_runtimeRoot != null && _runtimeRoot.gameObject.activeSelf != isActive)
            {
                _runtimeRoot.gameObject.SetActive(isActive);
            }
        }

        private void SetCrosshairActive(bool isActive)
        {
            if (_crosshairRoot != null && _crosshairRoot.gameObject.activeSelf != isActive)
            {
                _crosshairRoot.gameObject.SetActive(isActive);
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
