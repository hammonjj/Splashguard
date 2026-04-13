using BitBox.Library;
using BitBox.Library.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace BitBox.Toymageddon.UserInterface
{
    internal sealed class TitleScreenView
    {
        public TitleScreenView(VisualElement root)
        {
            Root = root ?? throw new System.ArgumentNullException(nameof(root));
            StartButton = Root.Require<Button>("StartButton");
            SettingsButton = Root.Require<Button>("TitleSettingsButton");
            QuitButton = Root.Require<Button>("QuitButton");
            Layout = Root.Q<VisualElement>(className: "title-layout");
            LayoutSpacer = Root.Q<VisualElement>("TitleLayoutSpacer");
            MainCard = Root.Q<VisualElement>(className: "title-main-card");
            PlaytestCard = Root.Q<VisualElement>(className: "playtest-card");
        }

        public VisualElement Root { get; }
        public Button StartButton { get; }
        public Button SettingsButton { get; }
        public Button QuitButton { get; }
        public VisualElement Layout { get; }
        public VisualElement LayoutSpacer { get; }
        public VisualElement MainCard { get; }
        public VisualElement PlaytestCard { get; }
    }

    internal sealed class JoinPromptScreenView
    {
        public JoinPromptScreenView(VisualElement root)
        {
            Root = root ?? throw new System.ArgumentNullException(nameof(root));
        }

        public VisualElement Root { get; }
    }

    internal sealed class PauseScreenView
    {
        public PauseScreenView(VisualElement root)
        {
            Root = root ?? throw new System.ArgumentNullException(nameof(root));
            ResumeButton = Root.Require<Button>("ResumeButton");
            SettingsButton = Root.Require<Button>("PauseSettingsButton");
            QuitButton = Root.Require<Button>("PauseQuitButton");
            Backdrop = Root.Require<VisualElement>("PauseBackdrop");
            BackdropShade = Root.Require<VisualElement>("PauseBackdropShade");
            Card = Root.Require<VisualElement>("PauseCard");
        }

        public VisualElement Root { get; }
        public Button ResumeButton { get; }
        public Button SettingsButton { get; }
        public Button QuitButton { get; }
        public VisualElement Backdrop { get; }
        public VisualElement BackdropShade { get; }
        public VisualElement Card { get; }
    }

    internal sealed class SettingsScreenView
    {
        public SettingsScreenView(VisualElement root)
        {
            Root = root ?? throw new System.ArgumentNullException(nameof(root));
            GeneralTabButton = Root.Require<Button>("SettingsGeneralTabButton");
            GameplayTabButton = Root.Require<Button>("SettingsGameplayTabButton");
            SoundTabButton = Root.Require<Button>("SettingsSoundTabButton");
            BackButton = Root.Require<Button>("SettingsBackButton");
            UiScaleSlider = Root.Require<Slider>("SettingsUiScaleSlider");
            UiScaleValueLabel = Root.Require<Label>("SettingsUiScaleValueLabel");
            UiScaleTitleLabel = Root.Require<Label>("SettingsUiScaleTitleLabel");
            UiScaleDescriptionLabel = Root.Require<Label>("SettingsUiScaleDescriptionLabel");
            LanguageDropdown = Root.Require<DropdownField>("SettingsLanguageDropdown");
            InvertVerticalAimToggle = Root.Require<Toggle>("SettingsInvertVerticalAimToggle");
            PlayerInvincibilityToggle = Root.Require<Toggle>("SettingsPlayerInvincibilityToggle");
            MasterVolumeSlider = Root.Require<Slider>("SettingsMasterVolumeSlider");
            MusicVolumeSlider = Root.Require<Slider>("SettingsMusicVolumeSlider");
            SfxVolumeSlider = Root.Require<Slider>("SettingsSfxVolumeSlider");
            GeneralPanel = Root.Require<VisualElement>("SettingsGeneralPanel");
            GameplayPanel = Root.Require<VisualElement>("SettingsGameplayPanel");
            SoundPanel = Root.Require<VisualElement>("SettingsSoundPanel");
        }

        public VisualElement Root { get; }
        public Button GeneralTabButton { get; }
        public Button GameplayTabButton { get; }
        public Button SoundTabButton { get; }
        public Button BackButton { get; }
        public Slider UiScaleSlider { get; }
        public Label UiScaleValueLabel { get; }
        public Label UiScaleTitleLabel { get; }
        public Label UiScaleDescriptionLabel { get; }
        public DropdownField LanguageDropdown { get; }
        public Toggle InvertVerticalAimToggle { get; }
        public Toggle PlayerInvincibilityToggle { get; }
        public Slider MasterVolumeSlider { get; }
        public Slider MusicVolumeSlider { get; }
        public Slider SfxVolumeSlider { get; }
        public VisualElement GeneralPanel { get; }
        public VisualElement GameplayPanel { get; }
        public VisualElement SoundPanel { get; }
    }

    internal sealed class LoadingScreenView
    {
        public LoadingScreenView(VisualElement root)
        {
            Root = root ?? throw new System.ArgumentNullException(nameof(root));
            ProgressBar = Root.Require<ProgressBar>("LoadingProgressBar");
            StatusLabel = Root.Require<Label>("LoadingStatusLabel");
            PercentLabel = Root.Require<Label>("LoadingPercentLabel");
        }

        public VisualElement Root { get; }
        public ProgressBar ProgressBar { get; }
        public Label StatusLabel { get; }
        public Label PercentLabel { get; }
    }
}
