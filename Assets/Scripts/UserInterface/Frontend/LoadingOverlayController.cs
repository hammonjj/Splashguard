using BitBox.Library.UI.Toolkit;
using UnityEngine;

namespace BitBox.Toymageddon.UserInterface
{
    internal sealed class LoadingOverlayController
    {
        private readonly LoadingScreenView _view;
        private readonly ToolkitScreenHost _screenHost;

        public LoadingOverlayController(LoadingScreenView view, ToolkitScreenHost screenHost)
        {
            _view = view ?? throw new System.ArgumentNullException(nameof(view));
            _screenHost = screenHost ?? throw new System.ArgumentNullException(nameof(screenHost));
        }

        public bool IsVisible => _screenHost.IsOverlayVisible(FrontendUiScreenIds.Loading);

        public void SetProgress(float normalizedProgress, string statusText)
        {
            float clampedProgress = Mathf.Clamp01(normalizedProgress);
            int percent = Mathf.RoundToInt(clampedProgress * 100f);

            _view.ProgressBar.value = percent;
            _view.StatusLabel.text = string.IsNullOrWhiteSpace(statusText) ? "Loading..." : statusText;
            _view.PercentLabel.text = $"{percent}%";
        }

        public void Show()
        {
            _screenHost.PushOverlay(FrontendUiScreenIds.Loading);
        }

        public void Hide()
        {
            _screenHost.HideOverlay(FrontendUiScreenIds.Loading);
        }
    }
}
