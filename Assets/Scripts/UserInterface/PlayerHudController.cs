using BitBox.Library;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BitBox.Toymageddon.UserInterface
{
    [DisallowMultipleComponent]
    public sealed class PlayerHudController : MonoBehaviourBase
    {
        private const string HudOutputName = "PlayerHudOutput";
        private const string RuntimeRootName = "PlayerHudRuntime";

        private RectTransform _hudOutputRoot;
        private RectTransform _runtimeRoot;
        private Text _playerLabel;
        private PlayerInput _playerInput;
        private Font _font;

        protected override void OnAwakened()
        {
            CacheReferences();
            EnsureHudBuilt();
            RefreshLabel();
        }

        protected override void OnEnabled()
        {
            CacheReferences();
            EnsureHudBuilt();
            SetHudActive(true);
            RefreshLabel();
        }

        protected override void OnDisabled()
        {
            SetHudActive(false);
        }

        public void ResetHudState()
        {
            RefreshLabel();
        }

        private void CacheReferences()
        {
            _playerInput ??= GetComponentInParent<PlayerInput>(true);
            _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (_hudOutputRoot == null && transform.parent != null)
            {
                _hudOutputRoot = transform.parent.Find(HudOutputName) as RectTransform;
            }
        }

        private void EnsureHudBuilt()
        {
            if (_hudOutputRoot == null)
            {
                return;
            }

            if (_runtimeRoot == null)
            {
                _runtimeRoot = _hudOutputRoot.Find(RuntimeRootName) as RectTransform;
            }

            if (_runtimeRoot != null)
            {
                _playerLabel ??= _runtimeRoot.GetComponentInChildren<Text>(true);
                return;
            }

            GameObject runtimeRootObject = new GameObject(RuntimeRootName, typeof(RectTransform), typeof(Image));
            runtimeRootObject.transform.SetParent(_hudOutputRoot, false);
            _runtimeRoot = runtimeRootObject.GetComponent<RectTransform>();
            _runtimeRoot.anchorMin = new Vector2(0f, 1f);
            _runtimeRoot.anchorMax = new Vector2(1f, 1f);
            _runtimeRoot.pivot = new Vector2(0.5f, 1f);
            _runtimeRoot.anchoredPosition = new Vector2(0f, -24f);
            _runtimeRoot.sizeDelta = new Vector2(0f, 92f);

            Image shellFrame = runtimeRootObject.GetComponent<Image>();
            shellFrame.color = new Color(0.05f, 0.09f, 0.12f, 0.42f);
            shellFrame.raycastTarget = false;
            shellFrame.type = Image.Type.Sliced;

            GameObject playerLabelObject = new GameObject("PlayerLabel", typeof(RectTransform), typeof(Text));
            playerLabelObject.transform.SetParent(_runtimeRoot, false);

            RectTransform labelRect = playerLabelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = new Vector2(24f, 0f);
            labelRect.sizeDelta = new Vector2(240f, 0f);

            _playerLabel = playerLabelObject.GetComponent<Text>();
            _playerLabel.font = _font;
            _playerLabel.fontSize = 24;
            _playerLabel.alignment = TextAnchor.MiddleLeft;
            _playerLabel.color = new Color(0.96f, 0.97f, 0.98f, 0.92f);
            _playerLabel.raycastTarget = false;
        }

        private void SetHudActive(bool isActive)
        {
            if (_runtimeRoot != null)
            {
                _runtimeRoot.gameObject.SetActive(isActive);
            }
        }

        private void RefreshLabel()
        {
            if (_playerLabel == null)
            {
                return;
            }

            int playerNumber = _playerInput != null ? _playerInput.playerIndex + 1 : 1;
            _playerLabel.text = $"PLAYER {playerNumber}";
        }
    }
}
