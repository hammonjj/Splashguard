using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace BitBox.Library.UI.Toolkit
{
    public sealed class ToolkitScreenHost
    {
        private readonly VisualElement _baseLayer;
        private readonly VisualElement _overlayLayer;
        private readonly Dictionary<string, VisualElement> _baseScreens = new Dictionary<string, VisualElement>(StringComparer.Ordinal);
        private readonly Dictionary<string, VisualElement> _overlayScreens = new Dictionary<string, VisualElement>(StringComparer.Ordinal);
        private readonly List<string> _overlayStack = new List<string>();

        public ToolkitScreenHost(VisualElement baseLayer, VisualElement overlayLayer)
        {
            _baseLayer = baseLayer ?? throw new ArgumentNullException(nameof(baseLayer));
            _overlayLayer = overlayLayer ?? throw new ArgumentNullException(nameof(overlayLayer));
            _baseLayer.pickingMode = PickingMode.Ignore;
            _overlayLayer.pickingMode = PickingMode.Ignore;
        }

        public string ActiveBaseScreenId { get; private set; }

        public void RegisterBaseScreen(string id, VisualElement screen)
        {
            RegisterScreen(id, screen, _baseLayer, _baseScreens);
        }

        public void RegisterOverlay(string id, VisualElement screen)
        {
            RegisterScreen(id, screen, _overlayLayer, _overlayScreens);
        }

        public void ShowBaseScreen(string id)
        {
            foreach (KeyValuePair<string, VisualElement> pair in _baseScreens)
            {
                SetVisible(pair.Value, pair.Key == id);
            }

            ActiveBaseScreenId = id;
        }

        public void HideAllBaseScreens()
        {
            foreach (VisualElement screen in _baseScreens.Values)
            {
                SetVisible(screen, false);
            }

            ActiveBaseScreenId = null;
        }

        public void PushOverlay(string id)
        {
            if (!_overlayScreens.TryGetValue(id, out VisualElement screen))
            {
                throw new InvalidOperationException($"Overlay '{id}' is not registered.");
            }

            SetVisible(screen, true);
            screen.BringToFront();

            _overlayStack.Remove(id);
            _overlayStack.Add(id);
        }

        public bool HideOverlay(string id)
        {
            if (!_overlayScreens.TryGetValue(id, out VisualElement screen))
            {
                return false;
            }

            SetVisible(screen, false);
            _overlayStack.Remove(id);
            return true;
        }

        public void PopOverlay()
        {
            if (_overlayStack.Count == 0)
            {
                return;
            }

            string top = _overlayStack[_overlayStack.Count - 1];
            HideOverlay(top);
        }

        public void ClearOverlays()
        {
            foreach (VisualElement screen in _overlayScreens.Values)
            {
                SetVisible(screen, false);
            }

            _overlayStack.Clear();
        }

        public bool IsOverlayVisible(string id)
        {
            return _overlayStack.Contains(id);
        }

        public bool TryGetBaseScreen(string id, out VisualElement screen)
        {
            return _baseScreens.TryGetValue(id, out screen);
        }

        public bool TryGetOverlay(string id, out VisualElement screen)
        {
            return _overlayScreens.TryGetValue(id, out screen);
        }

        private static void RegisterScreen(
            string id,
            VisualElement screen,
            VisualElement layer,
            IDictionary<string, VisualElement> registry
        )
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Screen id cannot be null or whitespace.", nameof(id));
            }

            if (screen == null)
            {
                throw new ArgumentNullException(nameof(screen));
            }

            registry.Add(id, screen);
            screen.style.display = DisplayStyle.None;
            layer.Add(screen);
        }

        private static void SetVisible(VisualElement screen, bool isVisible)
        {
            screen.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            screen.pickingMode = isVisible ? PickingMode.Position : PickingMode.Ignore;
        }
    }
}
