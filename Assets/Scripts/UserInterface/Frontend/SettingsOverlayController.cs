using System;
using System.Collections.Generic;
using BitBox.Library.Localization;
using BitBox.Library.UI.Toolkit;
using BitBox.Toymageddon.Debugging;
using BitBox.Toymageddon.Localization;
using BitBox.Toymageddon.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace BitBox.Toymageddon.UserInterface
{
    internal enum SettingsTab
    {
        General,
        Gameplay,
        Sound
    }

    internal readonly struct SettingsNavigationResult
    {
        public SettingsNavigationResult(bool consumed, SettingsTab targetTab, VisualElement focusTarget)
        {
            Consumed = consumed;
            TargetTab = targetTab;
            FocusTarget = focusTarget;
        }

        public bool Consumed { get; }
        public SettingsTab TargetTab { get; }
        public VisualElement FocusTarget { get; }
    }

    internal sealed class SettingsOverlayController
    {
        private const string SettingsUiScaleTitleLocalizationKey = "ui.settings.general.ui_scale.title";
        private const string SettingsUiScaleDescriptionLocalizationKey = "ui.settings.general.ui_scale.description";
        private const string SettingsRowFocusedClassName = "settings-row--focused";
        private const string SettingsFocusableFocusedClassName = "settings-focusable--focused";

        private readonly SettingsScreenView _view;
        private readonly ToolkitScreenHost _screenHost;
        private readonly UiBindingScope _bindings;
        private readonly Func<GameSettingsService> _resolveGameSettingsService;
        private readonly Func<GameSettingsSnapshot> _resolveCurrentSettingsSnapshot;
        private readonly Action<bool> _onPlayerInvincibilityChanged;
        private readonly Action _onBackRequested;
        private readonly List<string> _languageIds = new List<string>();
        private VisualElement _lastFocusedElement;
        private SettingsTab _activeTab = SettingsTab.General;
        private FrontendSettingsReturnTarget _returnTarget = FrontendSettingsReturnTarget.Title;

        public SettingsOverlayController(
            SettingsScreenView view,
            ToolkitScreenHost screenHost,
            UiBindingScope bindings,
            Func<GameSettingsService> resolveGameSettingsService,
            Func<GameSettingsSnapshot> resolveCurrentSettingsSnapshot,
            Action<bool> onPlayerInvincibilityChanged,
            Action onBackRequested)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _screenHost = screenHost ?? throw new ArgumentNullException(nameof(screenHost));
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            _resolveGameSettingsService = resolveGameSettingsService ?? throw new ArgumentNullException(nameof(resolveGameSettingsService));
            _resolveCurrentSettingsSnapshot = resolveCurrentSettingsSnapshot ?? throw new ArgumentNullException(nameof(resolveCurrentSettingsSnapshot));
            _onPlayerInvincibilityChanged = onPlayerInvincibilityChanged ?? throw new ArgumentNullException(nameof(onPlayerInvincibilityChanged));
            _onBackRequested = onBackRequested ?? throw new ArgumentNullException(nameof(onBackRequested));
        }

        public bool IsVisible => _screenHost.IsOverlayVisible(FrontendUiScreenIds.Settings);
        public FrontendSettingsReturnTarget ReturnTarget => _returnTarget;
        internal SettingsTab ActiveTab => _activeTab;

        public void Initialize()
        {
            _bindings.BindButton(_view.GeneralTabButton, () => SetSettingsTab(SettingsTab.General));
            _bindings.BindButton(_view.GameplayTabButton, () => SetSettingsTab(SettingsTab.Gameplay));
            _bindings.BindButton(_view.SoundTabButton, () => SetSettingsTab(SettingsTab.Sound));
            _bindings.BindButton(_view.BackButton, _onBackRequested);

            _view.Root.RegisterCallback<NavigationMoveEvent>(OnNavigationMove);
            _bindings.Register(() => _view.Root.UnregisterCallback<NavigationMoveEvent>(OnNavigationMove));

            _view.LanguageDropdown.RegisterValueChangedCallback(OnLanguageChanged);
            _bindings.Register(() => _view.LanguageDropdown.UnregisterValueChangedCallback(OnLanguageChanged));

            _view.UiScaleSlider.RegisterValueChangedCallback(OnUiScaleChanged);
            _bindings.Register(() => _view.UiScaleSlider.UnregisterValueChangedCallback(OnUiScaleChanged));

            _view.PlayerInvincibilityToggle.RegisterValueChangedCallback(OnPlayerInvincibilityChanged);
            _bindings.Register(() => _view.PlayerInvincibilityToggle.UnregisterValueChangedCallback(OnPlayerInvincibilityChanged));

            _view.InvertVerticalAimToggle.RegisterValueChangedCallback(OnInvertVerticalAimChanged);
            _bindings.Register(() => _view.InvertVerticalAimToggle.UnregisterValueChangedCallback(OnInvertVerticalAimChanged));

            _view.MasterVolumeSlider.RegisterValueChangedCallback(OnMasterVolumeChanged);
            _bindings.Register(() => _view.MasterVolumeSlider.UnregisterValueChangedCallback(OnMasterVolumeChanged));

            _view.MusicVolumeSlider.RegisterValueChangedCallback(OnMusicVolumeChanged);
            _bindings.Register(() => _view.MusicVolumeSlider.UnregisterValueChangedCallback(OnMusicVolumeChanged));

            _view.SfxVolumeSlider.RegisterValueChangedCallback(OnSfxVolumeChanged);
            _bindings.Register(() => _view.SfxVolumeSlider.UnregisterValueChangedCallback(OnSfxVolumeChanged));

            RegisterFocusableCallbacks(_view.Root);
            RefreshLanguageChoices();
            RefreshValues(_resolveCurrentSettingsSnapshot());
            SetSettingsTab(SettingsTab.General);
        }

        public void Open(FrontendSettingsReturnTarget returnTarget)
        {
            _returnTarget = returnTarget;
            RefreshLanguageChoices();
            RefreshValues(_resolveCurrentSettingsSnapshot());
            SetSettingsTab(SettingsTab.General);
            _screenHost.PushOverlay(FrontendUiScreenIds.Settings);
            FocusDefault();
        }

        public void Close()
        {
            _screenHost.HideOverlay(FrontendUiScreenIds.Settings);
        }

        public void Hide()
        {
            _screenHost.HideOverlay(FrontendUiScreenIds.Settings);
            _returnTarget = FrontendSettingsReturnTarget.Title;
        }

        public void FocusDefault()
        {
            UiFocusUtility.FocusFirstFocusable(_view.Root, GetSettingsFocusTargetName());
        }

        public void RefreshText()
        {
            _view.UiScaleTitleLabel.text = GameText.Get(SettingsUiScaleTitleLocalizationKey);
            _view.UiScaleDescriptionLabel.text = GameText.Get(SettingsUiScaleDescriptionLocalizationKey);
        }

        public void RefreshValues(GameSettingsSnapshot settingsSnapshot)
        {
            _view.UiScaleSlider.SetValueWithoutNotify(UiScaleSettings.ScaleToSliderValue(settingsSnapshot.UiScale));
            _view.UiScaleValueLabel.text = UiScaleSettings.FormatPercentLabel(settingsSnapshot.UiScale);
            _view.InvertVerticalAimToggle.SetValueWithoutNotify(settingsSnapshot.InvertVerticalAim);
            _view.PlayerInvincibilityToggle.SetValueWithoutNotify(DebugContext.PlayerInvincible);
            _view.MasterVolumeSlider.SetValueWithoutNotify(NormalizedToSliderValue(settingsSnapshot.MasterVolume01));
            _view.MusicVolumeSlider.SetValueWithoutNotify(NormalizedToSliderValue(settingsSnapshot.MusicVolume01));
            _view.SfxVolumeSlider.SetValueWithoutNotify(NormalizedToSliderValue(settingsSnapshot.SfxVolume01));
            SetLanguageDropdownSelection(settingsSnapshot.LanguageId);
        }

        internal SettingsNavigationResult EvaluateNavigationForTesting(NavigationMoveEvent.Direction direction, VisualElement focusedElement)
        {
            return EvaluateNavigationMove(direction, focusedElement);
        }

        internal bool ApplyNavigationMoveForTesting(NavigationMoveEvent.Direction direction, VisualElement focusedElement)
        {
            return ApplyNavigationMove(EvaluateNavigationMove(direction, focusedElement), deferFocus: false);
        }

        internal void ApplyFocusStylingForTesting(VisualElement element, bool isFocused)
        {
            UpdateFocusStyling(element, isFocused);
            if (isFocused)
            {
                _lastFocusedElement = element;
            }
        }

        private void OnNavigationMove(NavigationMoveEvent @event)
        {
            if (_view.Root.panel == null)
            {
                return;
            }

            VisualElement focusedElement = ResolveNavigationElement(_view.Root.panel.focusController?.focusedElement as VisualElement);
            SettingsNavigationResult navigationResult = EvaluateNavigationMove(@event.direction, focusedElement);
            if (ApplyNavigationMove(navigationResult, deferFocus: true))
            {
                @event.StopPropagation();
            }
        }

        private bool ApplyNavigationMove(SettingsNavigationResult navigationResult, bool deferFocus)
        {
            if (!navigationResult.Consumed)
            {
                return false;
            }

            SetSettingsTab(navigationResult.TargetTab);
            FocusSettingsElement(navigationResult.FocusTarget, deferFocus);
            return true;
        }

        private SettingsNavigationResult EvaluateNavigationMove(NavigationMoveEvent.Direction direction, VisualElement focusedElement)
        {
            if ((direction == NavigationMoveEvent.Direction.Left || direction == NavigationMoveEvent.Direction.Right)
                && TryResolveSettingsTabFromTabButton(focusedElement, out SettingsTab focusedTab))
            {
                SettingsTab adjacentTab = GetAdjacentSettingsTab(focusedTab, direction);
                if (adjacentTab != focusedTab)
                {
                    return new SettingsNavigationResult(true, adjacentTab, GetSettingsTabButton(adjacentTab));
                }
            }

            if (direction == NavigationMoveEvent.Direction.Down)
            {
                SettingsTab targetTab = ResolveSettingsTabForDownNavigation(focusedElement);
                if (!IsFocusWithinSettingsPanel(focusedElement, targetTab))
                {
                    return new SettingsNavigationResult(true, targetTab, GetSettingsEntryControl(targetTab));
                }
            }

            if (direction == NavigationMoveEvent.Direction.Up)
            {
                SettingsTab sourceTab = ResolveSettingsTabForFocusedElement(focusedElement);
                if (focusedElement != null && IsFirstFocusableInSettingsPanel(focusedElement, sourceTab))
                {
                    return new SettingsNavigationResult(true, sourceTab, GetSettingsTabButton(sourceTab));
                }
            }

            return default;
        }

        private void RegisterFocusableCallbacks(VisualElement root)
        {
            if (root == null)
            {
                return;
            }

            if (root.focusable)
            {
                root.RegisterCallback<FocusInEvent>(OnFocusableFocusIn);
                root.RegisterCallback<FocusOutEvent>(OnFocusableFocusOut);
                _bindings.Register(() => root.UnregisterCallback<FocusInEvent>(OnFocusableFocusIn));
                _bindings.Register(() => root.UnregisterCallback<FocusOutEvent>(OnFocusableFocusOut));
            }

            for (int childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                RegisterFocusableCallbacks(root[childIndex]);
            }
        }

        private void OnFocusableFocusIn(FocusInEvent @event)
        {
            VisualElement element = @event.currentTarget as VisualElement;
            if (element == null)
            {
                return;
            }

            _lastFocusedElement = element;
            UpdateFocusStyling(element, isFocused: true);

            if (TryResolveSettingsTabFromTabButton(element, out SettingsTab tab) && tab != _activeTab)
            {
                SetSettingsTab(tab);
            }
        }

        private void OnFocusableFocusOut(FocusOutEvent @event)
        {
            VisualElement element = @event.currentTarget as VisualElement;
            UpdateFocusStyling(element, isFocused: false);
        }

        private void UpdateFocusStyling(VisualElement element, bool isFocused)
        {
            if (element == null)
            {
                return;
            }

            element.EnableInClassList(SettingsFocusableFocusedClassName, isFocused);
            FindContainingSettingsRow(element)?.EnableInClassList(SettingsRowFocusedClassName, isFocused);
        }

        private void OnLanguageChanged(ChangeEvent<string> @event)
        {
            int selectionIndex = _view.LanguageDropdown.choices.IndexOf(@event.newValue);
            if (selectionIndex < 0 || selectionIndex >= _languageIds.Count)
            {
                return;
            }

            string languageId = _languageIds[selectionIndex];
            GameSettingsService settingsService = _resolveGameSettingsService();
            if (settingsService != null)
            {
                settingsService.SetLanguage(languageId);
                return;
            }

            if (LocalizationManager.Instance != null)
            {
                LocalizationManager.SetCurrentLanguage(languageId);
                return;
            }

            DebugContext.RequestedLanguageId = languageId;
            GameText.SetLanguage(languageId);
        }

        private void OnPlayerInvincibilityChanged(ChangeEvent<bool> @event)
        {
            _onPlayerInvincibilityChanged(@event.newValue);
        }

        private void OnInvertVerticalAimChanged(ChangeEvent<bool> @event)
        {
            _resolveGameSettingsService()?.SetInvertVerticalAim(@event.newValue);
        }

        private void OnMasterVolumeChanged(ChangeEvent<float> @event)
        {
            _resolveGameSettingsService()?.SetMasterVolume(SliderValueToNormalized(@event.newValue));
        }

        private void OnMusicVolumeChanged(ChangeEvent<float> @event)
        {
            _resolveGameSettingsService()?.SetMusicVolume(SliderValueToNormalized(@event.newValue));
        }

        private void OnSfxVolumeChanged(ChangeEvent<float> @event)
        {
            _resolveGameSettingsService()?.SetSfxVolume(SliderValueToNormalized(@event.newValue));
        }

        private void OnUiScaleChanged(ChangeEvent<float> @event)
        {
            _resolveGameSettingsService()?.SetUiScale(UiScaleSettings.SliderValueToScale(@event.newValue));
        }

        private void RefreshLanguageChoices()
        {
            _languageIds.Clear();
            var languageLabels = new List<string>();
            IReadOnlyList<LocalizationLanguageDefinition> availableLanguages = LocalizationManager.Instance?.AvailableLanguages;

            if (availableLanguages != null)
            {
                for (int index = 0; index < availableLanguages.Count; index++)
                {
                    LocalizationLanguageDefinition language = availableLanguages[index];
                    if (language == null || string.IsNullOrWhiteSpace(language.Id))
                    {
                        continue;
                    }

                    AppendLanguageOption(language.Id, language.DropdownLabel, languageLabels);
                }
            }

            if (_languageIds.Count == 0)
            {
                AppendLanguageOption(LocalizationTable.EnglishLanguageId, "English", languageLabels);
                AppendLanguageOption(LocalizationTable.SpanishLanguageId, "Spanish", languageLabels);
            }

            _view.LanguageDropdown.choices = languageLabels;
            SetLanguageDropdownSelection(_resolveCurrentSettingsSnapshot().LanguageId);
        }

        private void AppendLanguageOption(string languageId, string languageLabel, List<string> languageLabels)
        {
            if (string.IsNullOrWhiteSpace(languageId))
            {
                return;
            }

            for (int index = 0; index < _languageIds.Count; index++)
            {
                if (string.Equals(_languageIds[index], languageId, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            _languageIds.Add(languageId);
            languageLabels.Add(string.IsNullOrWhiteSpace(languageLabel) ? languageId : languageLabel);
        }

        private void SetLanguageDropdownSelection(string languageId)
        {
            if (_languageIds.Count == 0 || _view.LanguageDropdown.choices == null || _view.LanguageDropdown.choices.Count == 0)
            {
                _view.LanguageDropdown.SetValueWithoutNotify(string.Empty);
                return;
            }

            int selectedIndex = ResolveLanguageOptionIndex(languageId);
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }

            _view.LanguageDropdown.SetValueWithoutNotify(_view.LanguageDropdown.choices[selectedIndex]);
        }

        private int ResolveLanguageOptionIndex(string languageId)
        {
            for (int index = 0; index < _languageIds.Count; index++)
            {
                if (string.Equals(_languageIds[index], languageId, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private void SetSettingsTab(SettingsTab tab)
        {
            _activeTab = tab;
            _view.GeneralPanel.style.display = tab == SettingsTab.General ? DisplayStyle.Flex : DisplayStyle.None;
            _view.GameplayPanel.style.display = tab == SettingsTab.Gameplay ? DisplayStyle.Flex : DisplayStyle.None;
            _view.SoundPanel.style.display = tab == SettingsTab.Sound ? DisplayStyle.Flex : DisplayStyle.None;
            _view.GeneralTabButton.EnableInClassList("settings-tab-button--selected", tab == SettingsTab.General);
            _view.GameplayTabButton.EnableInClassList("settings-tab-button--selected", tab == SettingsTab.Gameplay);
            _view.SoundTabButton.EnableInClassList("settings-tab-button--selected", tab == SettingsTab.Sound);
        }

        private void FocusSettingsElement(VisualElement element, bool deferFocus)
        {
            if (element == null)
            {
                return;
            }

            if (deferFocus)
            {
                _view.Root.schedule.Execute(() =>
                {
                    if (IsFocusableVisibleElement(element))
                    {
                        element.Focus();
                    }
                });
                return;
            }

            if (IsFocusableVisibleElement(element))
            {
                element.Focus();
            }
        }

        private VisualElement ResolveNavigationElement(VisualElement element)
        {
            VisualElement tabButton = FindContainingSettingsTabButton(element);
            if (tabButton != null)
            {
                return tabButton;
            }

            while (element != null && element != _view.Root)
            {
                if (element.focusable)
                {
                    return element;
                }

                element = element.parent;
            }

            return _lastFocusedElement;
        }

        private bool IsFocusWithinSettingsPanel(VisualElement element, SettingsTab tab)
        {
            VisualElement settingsPanel = GetSettingsPanel(tab);
            return element != null && settingsPanel != null && (element == settingsPanel || settingsPanel.Contains(element));
        }

        private SettingsTab ResolveSettingsTabForDownNavigation(VisualElement focusedElement)
        {
            if (TryResolveSettingsTabFromTabButton(focusedElement, out SettingsTab focusedTab))
            {
                return focusedTab;
            }

            if (TryResolveSettingsTabFromPanelContent(focusedElement, out SettingsTab panelTab))
            {
                return panelTab;
            }

            return _activeTab;
        }

        private SettingsTab ResolveSettingsTabForFocusedElement(VisualElement focusedElement)
        {
            if (TryResolveSettingsTabFromPanelContent(focusedElement, out SettingsTab panelTab))
            {
                return panelTab;
            }

            if (TryResolveSettingsTabFromTabButton(focusedElement, out SettingsTab focusedTab))
            {
                return focusedTab;
            }

            return _activeTab;
        }

        private SettingsTab GetAdjacentSettingsTab(SettingsTab tab, NavigationMoveEvent.Direction direction)
        {
            return direction switch
            {
                NavigationMoveEvent.Direction.Left => tab switch
                {
                    SettingsTab.Gameplay => SettingsTab.General,
                    SettingsTab.Sound => SettingsTab.Gameplay,
                    _ => SettingsTab.General
                },
                NavigationMoveEvent.Direction.Right => tab switch
                {
                    SettingsTab.General => SettingsTab.Gameplay,
                    SettingsTab.Gameplay => SettingsTab.Sound,
                    _ => SettingsTab.Sound
                },
                _ => tab
            };
        }

        private bool IsFirstFocusableInSettingsPanel(VisualElement element, SettingsTab tab)
        {
            VisualElement firstFocusable = GetSettingsEntryControl(tab);
            return firstFocusable != null && (firstFocusable == element || firstFocusable.Contains(element));
        }

        private VisualElement GetSettingsEntryControl(SettingsTab tab)
        {
            VisualElement preferredControl = GetPreferredSettingsEntryControl(tab);
            if (IsFocusableVisibleElement(preferredControl))
            {
                return preferredControl;
            }

            return FindFirstFocusableVisibleDescendant(GetSettingsPanel(tab));
        }

        private VisualElement GetSettingsPanel(SettingsTab tab)
        {
            return tab switch
            {
                SettingsTab.Gameplay => _view.GameplayPanel,
                SettingsTab.Sound => _view.SoundPanel,
                _ => _view.GeneralPanel
            };
        }

        private VisualElement GetSettingsTabButton(SettingsTab tab)
        {
            return tab switch
            {
                SettingsTab.Gameplay => _view.GameplayTabButton,
                SettingsTab.Sound => _view.SoundTabButton,
                _ => _view.GeneralTabButton
            };
        }

        private bool IsSettingsTabButton(VisualElement element)
        {
            return element == _view.GeneralTabButton || element == _view.GameplayTabButton || element == _view.SoundTabButton;
        }

        private bool TryResolveSettingsTabFromTabButton(VisualElement element, out SettingsTab tab)
        {
            VisualElement tabButton = FindContainingSettingsTabButton(element);
            if (tabButton == _view.GameplayTabButton)
            {
                tab = SettingsTab.Gameplay;
                return true;
            }

            if (tabButton == _view.SoundTabButton)
            {
                tab = SettingsTab.Sound;
                return true;
            }

            if (tabButton == _view.GeneralTabButton)
            {
                tab = SettingsTab.General;
                return true;
            }

            tab = _activeTab;
            return false;
        }

        private bool TryResolveSettingsTabFromPanelContent(VisualElement element, out SettingsTab tab)
        {
            if (element != null)
            {
                if (_view.GameplayPanel == element || _view.GameplayPanel.Contains(element))
                {
                    tab = SettingsTab.Gameplay;
                    return true;
                }

                if (_view.SoundPanel == element || _view.SoundPanel.Contains(element))
                {
                    tab = SettingsTab.Sound;
                    return true;
                }

                if (_view.GeneralPanel == element || _view.GeneralPanel.Contains(element))
                {
                    tab = SettingsTab.General;
                    return true;
                }
            }

            tab = _activeTab;
            return false;
        }

        private VisualElement GetPreferredSettingsEntryControl(SettingsTab tab)
        {
            return tab switch
            {
                SettingsTab.Gameplay => IsFocusableVisibleElement(_view.InvertVerticalAimToggle)
                    ? _view.InvertVerticalAimToggle
                    : _view.PlayerInvincibilityToggle,
                SettingsTab.Sound => _view.MasterVolumeSlider,
                _ => IsFocusableVisibleElement(_view.UiScaleSlider)
                    ? _view.UiScaleSlider
                    : _view.LanguageDropdown
            };
        }

        private string GetSettingsFocusTargetName()
        {
            return _activeTab switch
            {
                SettingsTab.Gameplay => _view.GameplayTabButton.name,
                SettingsTab.Sound => _view.SoundTabButton.name,
                _ => _view.GeneralTabButton.name
            };
        }

        private VisualElement FindContainingSettingsTabButton(VisualElement element)
        {
            while (element != null && element != _view.Root)
            {
                if (IsSettingsTabButton(element))
                {
                    return element;
                }

                element = element.parent;
            }

            return null;
        }

        private static VisualElement FindContainingSettingsRow(VisualElement element)
        {
            while (element != null)
            {
                if (element.ClassListContains("settings-row"))
                {
                    return element;
                }

                element = element.parent;
            }

            return null;
        }

        private static bool IsFocusableVisibleElement(VisualElement element)
        {
            return element != null
                && element.focusable
                && element.enabledInHierarchy
                && element.visible
                && element.resolvedStyle.display != DisplayStyle.None;
        }

        private static VisualElement FindFirstFocusableVisibleDescendant(VisualElement root)
        {
            if (root == null || !root.visible || root.resolvedStyle.display == DisplayStyle.None)
            {
                return null;
            }

            if (IsFocusableVisibleElement(root))
            {
                return root;
            }

            for (int childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                VisualElement focusableChild = FindFirstFocusableVisibleDescendant(root[childIndex]);
                if (focusableChild != null)
                {
                    return focusableChild;
                }
            }

            return null;
        }

        private static float SliderValueToNormalized(float sliderValue)
        {
            return Mathf.Clamp01(sliderValue / 100f);
        }

        private static float NormalizedToSliderValue(float normalizedValue)
        {
            return Mathf.Clamp01(normalizedValue) * 100f;
        }
    }
}
