using BitBox.Library.UI.Toolkit;
using UnityEngine.UIElements;

namespace BitBox.Toymageddon.UserInterface
{
    internal sealed class FrontendUiRuntime
    {
        public FrontendUiRuntime(
            UIDocument uiDocument,
            ToolkitScreenHost screenHost,
            VisualElement frontendRootElement,
            VisualElement baseLayerElement,
            VisualElement overlayLayerElement,
            TitleScreenView titleScreen,
            JoinPromptScreenView joinPromptScreen,
            PauseScreenView pauseScreen,
            SettingsScreenView settingsScreen,
            LoadingScreenView loadingScreen)
        {
            UiDocument = uiDocument;
            ScreenHost = screenHost;
            FrontendRootElement = frontendRootElement;
            BaseLayerElement = baseLayerElement;
            OverlayLayerElement = overlayLayerElement;
            TitleScreen = titleScreen;
            JoinPromptScreen = joinPromptScreen;
            PauseScreen = pauseScreen;
            SettingsScreen = settingsScreen;
            LoadingScreen = loadingScreen;
        }

        public UIDocument UiDocument { get; }
        public ToolkitScreenHost ScreenHost { get; }
        public VisualElement FrontendRootElement { get; }
        public VisualElement BaseLayerElement { get; }
        public VisualElement OverlayLayerElement { get; }
        public TitleScreenView TitleScreen { get; }
        public JoinPromptScreenView JoinPromptScreen { get; }
        public PauseScreenView PauseScreen { get; }
        public SettingsScreenView SettingsScreen { get; }
        public LoadingScreenView LoadingScreen { get; }
    }
}
