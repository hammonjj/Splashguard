using BitBox.Library;
using BitBox.Library.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace BitBox.Toymageddon.UserInterface
{
    internal sealed class FrontendUiBuilder
    {
        private const string FrontendRootResourcePath = "Frontend/FrontendRoot";
        private const string TitleScreenResourcePath = "Frontend/TitleScreen";
        private const string JoinPromptScreenResourcePath = "Frontend/JoinPromptScreen";
        private const string PauseScreenResourcePath = "Frontend/PauseScreen";
        private const string SettingsScreenResourcePath = "Frontend/SettingsScreen";
        private const string LoadingScreenResourcePath = "Frontend/LoadingScreen";

        public FrontendUiRuntime Build(UIDocument uiDocument)
        {
            if (uiDocument == null)
            {
                throw new System.ArgumentNullException(nameof(uiDocument));
            }

            VisualTreeAsset frontendRootAsset = Resources.Load<VisualTreeAsset>(FrontendRootResourcePath);
            VisualTreeAsset titleScreenAsset = Resources.Load<VisualTreeAsset>(TitleScreenResourcePath);
            VisualTreeAsset joinPromptScreenAsset = Resources.Load<VisualTreeAsset>(JoinPromptScreenResourcePath);
            VisualTreeAsset pauseScreenAsset = Resources.Load<VisualTreeAsset>(PauseScreenResourcePath);
            VisualTreeAsset settingsScreenAsset = Resources.Load<VisualTreeAsset>(SettingsScreenResourcePath);
            VisualTreeAsset loadingScreenAsset = Resources.Load<VisualTreeAsset>(LoadingScreenResourcePath);

            Assert.IsNotNull(frontendRootAsset, $"Missing VisualTreeAsset resource at '{FrontendRootResourcePath}'.");
            Assert.IsNotNull(titleScreenAsset, $"Missing VisualTreeAsset resource at '{TitleScreenResourcePath}'.");
            Assert.IsNotNull(joinPromptScreenAsset, $"Missing VisualTreeAsset resource at '{JoinPromptScreenResourcePath}'.");
            Assert.IsNotNull(pauseScreenAsset, $"Missing VisualTreeAsset resource at '{PauseScreenResourcePath}'.");
            Assert.IsNotNull(settingsScreenAsset, $"Missing VisualTreeAsset resource at '{SettingsScreenResourcePath}'.");
            Assert.IsNotNull(loadingScreenAsset, $"Missing VisualTreeAsset resource at '{LoadingScreenResourcePath}'.");

            VisualElement documentRoot = uiDocument.rootVisualElement;
            documentRoot.Clear();
            documentRoot.style.flexGrow = 1f;
            documentRoot.style.position = Position.Relative;
            documentRoot.style.left = 0f;
            documentRoot.style.top = 0f;
            documentRoot.style.right = 0f;
            documentRoot.style.bottom = 0f;
            documentRoot.pickingMode = PickingMode.Ignore;
            frontendRootAsset.CloneTree(documentRoot);

            VisualElement frontendRootElement = documentRoot.Require<VisualElement>("FrontendRoot");
            VisualElement baseLayerElement = frontendRootElement.Require<VisualElement>("BaseLayer");
            VisualElement overlayLayerElement = frontendRootElement.Require<VisualElement>("OverlayLayer");
            StretchToFill(frontendRootElement);
            StretchToFill(baseLayerElement);
            StretchToFill(overlayLayerElement);
            frontendRootElement.pickingMode = PickingMode.Ignore;
            baseLayerElement.pickingMode = PickingMode.Ignore;
            overlayLayerElement.pickingMode = PickingMode.Ignore;

            var screenHost = new ToolkitScreenHost(baseLayerElement, overlayLayerElement);

            VisualElement titleScreenRoot = titleScreenAsset.CloneTree();
            VisualElement joinPromptScreenRoot = joinPromptScreenAsset.CloneTree();
            VisualElement pauseScreenRoot = pauseScreenAsset.CloneTree();
            VisualElement settingsScreenRoot = settingsScreenAsset.CloneTree();
            VisualElement loadingScreenRoot = loadingScreenAsset.CloneTree();

            StretchToFill(titleScreenRoot);
            StretchToFill(joinPromptScreenRoot);
            StretchToFill(pauseScreenRoot);
            StretchToFill(settingsScreenRoot);
            StretchToFill(loadingScreenRoot);

            screenHost.RegisterBaseScreen(FrontendUiScreenIds.Title, titleScreenRoot);
            screenHost.RegisterBaseScreen(FrontendUiScreenIds.JoinPrompt, joinPromptScreenRoot);
            screenHost.RegisterOverlay(FrontendUiScreenIds.Pause, pauseScreenRoot);
            screenHost.RegisterOverlay(FrontendUiScreenIds.Settings, settingsScreenRoot);
            screenHost.RegisterOverlay(FrontendUiScreenIds.Loading, loadingScreenRoot);
            screenHost.HideAllBaseScreens();
            screenHost.ClearOverlays();

            return new FrontendUiRuntime(
                uiDocument,
                screenHost,
                frontendRootElement,
                baseLayerElement,
                overlayLayerElement,
                new TitleScreenView(titleScreenRoot),
                new JoinPromptScreenView(joinPromptScreenRoot),
                new PauseScreenView(pauseScreenRoot),
                new SettingsScreenView(settingsScreenRoot),
                new LoadingScreenView(loadingScreenRoot));
        }

        private static void StretchToFill(VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            element.style.position = Position.Absolute;
            element.style.left = 0f;
            element.style.top = 0f;
            element.style.right = 0f;
            element.style.bottom = 0f;
        }
    }
}
