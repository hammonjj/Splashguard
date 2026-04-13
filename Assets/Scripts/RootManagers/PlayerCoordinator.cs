using System.Text;
using System.Collections;
using System.Collections.Generic;
using BitBox.Library;
using BitBox.Library.Constants;
using BitBox.Library.Constants.Enums;
using BitBox.Library.Eventing;
using BitBox.Library.Eventing.GlobalEvents;
using BitBox.Library.Eventing.SceneEvents;
using BitBox.Toymageddon.Debugging;
using Bitbox.Toymageddon;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BitBox.Library.Input
{
    public class PlayerCoordinator : MonoBehaviourBase
    {
        public IReadOnlyList<PlayerInput> PlayerInputs => _playerInputs.AsReadOnly();

        [ShowInInspector, ReadOnly] private List<PlayerInput> _playerInputs = new List<PlayerInput>();
        [ShowInInspector, ReadOnly] private List<PlayerInputDebugInfo> _playerInputDebugInfo = new List<PlayerInputDebugInfo>();

        private readonly HashSet<int> _readyPlayerIndices = new HashSet<int>();
        private PlayerInputManager _playerInputManager;

        protected override void OnAwakened()
        {
            StaticData.PlayerInputCoordinator = this;
            _playerInputManager = GetComponent<PlayerInputManager>();

            LogInfo(
                $"PlayerInputManager configured. joiningEnabled={_playerInputManager.joiningEnabled}, joinBehavior={_playerInputManager.joinBehavior}, " +
                $"splitScreen={_playerInputManager.splitScreen}, maintainAspectRatio={_playerInputManager.maintainAspectRatioInSplitScreen}, " +
                $"fixedScreens={_playerInputManager.fixedNumberOfSplitScreens}, splitScreenArea={FormatRect(_playerInputManager.splitScreenArea)}, " +
                $"notificationBehavior={_playerInputManager.notificationBehavior}, maxPlayers={_playerInputManager.maxPlayerCount}");

            _playerInputManager.onPlayerJoined += OnPlayerJoined;
            _globalMessageBus.Publish(new InputManagerReadyEvent());
            QueueLegacyDebugStartModeJoinRequest();
            ConsumePendingInitialJoinRequest();
        }

        protected override void OnEnabled()
        {
            _globalMessageBus.Subscribe<MacroSceneLoadedEvent>(OnMacroSceneLoaded);
            int beforeCount = _globalMessageBus.GetSubscriberCount<PlayerReadyEvent>();
            int beforeUnreadyCount = _globalMessageBus.GetSubscriberCount<PlayerUnreadyEvent>();
            _globalMessageBus.Subscribe<PlayerReadyEvent>(OnPlayerReady);
            _globalMessageBus.Subscribe<PlayerUnreadyEvent>(OnPlayerUnready);
            int afterCount = _globalMessageBus.GetSubscriberCount<PlayerReadyEvent>();
            int afterUnreadyCount = _globalMessageBus.GetSubscriberCount<PlayerUnreadyEvent>();
            LogInfo(
                "Subscribed to CharacterSelection readiness events on the global bus. " +
                $"readySubscribers {beforeCount} -> {afterCount}, unreadySubscribers {beforeUnreadyCount} -> {afterUnreadyCount}.");
        }

        private void QueueLegacyDebugStartModeJoinRequest()
        {
            if (StaticData.PendingInitialJoinRequest != null)
            {
                return;
            }

            if (DebugContext.RequestedStartMode == StartUpMode.TitleMenu)
            {
                return;
            }

            StaticData.PendingInitialJoinRequest =
                DebugContext.CreateKeyboardMousePendingJoinRequest("DebugStartMode");

            LogWarning(
                $"Debug StartUpMode is set to {DebugContext.RequestedStartMode} - queueing Keyboard&Mouse input for testing.");
        }

        private void ActivateCharacterSelectionUiInput(PlayerInput input)
        {
            if (input == null)
            {
                return;
            }

            string previousMap = input.currentActionMap?.name ?? "None";
            bool actionsWereEnabled = input.actions != null && input.actions.enabled;

            input.SwitchCurrentActionMap(Strings.UIMap);
            input.ActivateInput();

            LogInfo(
                $"Activated CharacterSelection UI input for player {input.playerIndex}. " +
                $"map {previousMap} -> {input.currentActionMap?.name ?? "None"}, actionsEnabled {actionsWereEnabled} -> {input.actions.enabled}");
        }

        private void ActivateGameplayInput(PlayerInput input)
        {
            if (input == null)
            {
                return;
            }

            string previousMap = input.currentActionMap?.name ?? "None";
            bool actionsWereEnabled = input.actions != null && input.actions.enabled;

            input.actions.Enable();
            input.SwitchCurrentActionMap(Strings.ThirdPersonControls);
            input.ActivateInput();

            LogInfo(
                $"Activated gameplay input for player {input.playerIndex}. " +
                $"map {previousMap} -> {input.currentActionMap?.name ?? "None"}, actionsEnabled {actionsWereEnabled} -> {input.actions.enabled}");
        }

        private void ConsumePendingInitialJoinRequest()
        {
            PendingPlayerJoinRequest pendingJoinRequest = StaticData.PendingInitialJoinRequest;
            if (pendingJoinRequest == null)
            {
                return;
            }

            StaticData.PendingInitialJoinRequest = null;

            try
            {
                if (pendingJoinRequest.PairWithDevice == null)
                {
                    LogWarning(
                        $"Pending initial join request could not be consumed because no pairing device was available. source={pendingJoinRequest.SourceControlPath ?? "None"}");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(pendingJoinRequest.ControlScheme))
                {
                    _playerInputManager.JoinPlayer(
                        controlScheme: pendingJoinRequest.ControlScheme,
                        pairWithDevice: pendingJoinRequest.PairWithDevice
                    );
                }
                else
                {
                    _playerInputManager.JoinPlayer(pairWithDevice: pendingJoinRequest.PairWithDevice);
                }

                LogInfo(
                    $"Consumed pending initial join request. controlScheme={pendingJoinRequest.ControlScheme ?? "None"}, " +
                    $"pairDevice={pendingJoinRequest.PairWithDevice.displayName}, source={pendingJoinRequest.SourceControlPath ?? "None"}");
            }
            catch (System.Exception exception)
            {
                LogError($"Failed to consume pending initial join request: {exception.Message}");
            }
        }

        private void OnMacroSceneLoaded(MacroSceneLoadedEvent @event)
        {
            if (@event.SceneType != MacroSceneType.CharacterSelection
                && @event.SceneType != MacroSceneType.TitleMenu)
            {
                return;
            }

            _readyPlayerIndices.Clear();
            if (@event.SceneType == MacroSceneType.CharacterSelection
                || @event.SceneType == MacroSceneType.TitleMenu)
            {
                GameData.ClearCharacterSelectionSession();
            }

            if (_playerInputManager != null)
            {
                _playerInputManager.EnableJoining();
            }

            LogInfo(
                $"Reset CharacterSelection readiness for scene {@event.SceneType}. joiningEnabled={_playerInputManager?.joiningEnabled ?? false}, " +
                $"playerCount={PlayerInputs.Count}, characterSelectionEntries={GameData.CharacterSelectionData.Count}");
        }

        private void OnPlayerReady(PlayerReadyEvent @event)
        {
            bool added = _readyPlayerIndices.Add(@event.PlayerIndex);
            LogInfo(
                $"Received PlayerReadyEvent for player {@event.PlayerIndex}. added={added}, readyPlayers={_readyPlayerIndices.Count}/{PlayerInputs.Count}");
            LogAllPlayerViewportState($"player_ready_{@event.PlayerIndex}");

            if (!added || PlayerInputs.Count == 0 || _readyPlayerIndices.Count < PlayerInputs.Count)
            {
                return;
            }

            LogInfo("All joined players are ready. Publishing AllPlayersReadyEvent and loading HubWorld.");
            _playerInputManager.DisableJoining();
            _globalMessageBus.Publish(new AllPlayersReadyEvent());
            _globalMessageBus.Publish(new LoadMacroSceneEvent(MacroSceneType.HubWorld));
        }

        private void OnPlayerUnready(PlayerUnreadyEvent @event)
        {
            bool removed = _readyPlayerIndices.Remove(@event.PlayerIndex);
            LogInfo(
                $"Received PlayerUnreadyEvent for player {@event.PlayerIndex}. removed={removed}, readyPlayers={_readyPlayerIndices.Count}/{PlayerInputs.Count}");
        }

        private void OnPlayerJoined(PlayerInput input)
        {
            LogInfo($"Player {input.playerIndex + 1} joined with device: {input.devices[0].displayName}");
            Assert.IsFalse(
                _playerInputs.Contains(input),
                $"PlayerInput for player {input.playerIndex + 1} already exists.");

            _playerInputs.Add(input);
            input.gameObject.name = $"PlayerInput_{input.playerIndex}";
            input.gameObject.transform.SetParent(gameObject.transform);

            MacroSceneType currentMacroScene = StaticData.GameController != null
                ? StaticData.GameController.CurrentMacroScene
                : MacroSceneType.None;

            if (currentMacroScene == MacroSceneType.CharacterSelection)
            {
                ActivateCharacterSelectionUiInput(input);
            }
            else if (currentMacroScene.IsGameplayScene())
            {
                ActivateGameplayInput(input);
            }

            RefreshInspectorInputState();

            LogInfo($"Player {input.playerIndex} joined with scheme: {input.currentControlScheme} - {input.name}");
            if (currentMacroScene == MacroSceneType.CharacterSelection
                && input.currentActionMap == null)
            {
                LogWarning(
                    $"Player {input.playerIndex} joined during CharacterSelection with no active action map. UI submit/click actions may not fire until a UI map is activated.");
            }

            LogAllPlayerViewportState($"player_joined_immediate_{input.playerIndex}");
            StartCoroutine(LogViewportStateAfterFrames(1, $"player_joined_after_1_frame_{input.playerIndex}"));
            StartCoroutine(LogViewportStateAfterFrames(10, $"player_joined_after_10_frames_{input.playerIndex}"));
        }

        protected override void OnDisabled()
        {
            _playerInputManager.onPlayerJoined -= OnPlayerJoined;
            _globalMessageBus?.Unsubscribe<MacroSceneLoadedEvent>(OnMacroSceneLoaded);
            _globalMessageBus?.Unsubscribe<PlayerReadyEvent>(OnPlayerReady);
            _globalMessageBus?.Unsubscribe<PlayerUnreadyEvent>(OnPlayerUnready);
        }

        protected override void OnUpdated()
        {
            RefreshInspectorInputState();
        }

        private void RefreshInspectorInputState()
        {
            if (_playerInputs.Count == _playerInputDebugInfo.Count)
            {
                return;
            }

            _playerInputDebugInfo.Clear();

            foreach (var playerInput in _playerInputs)
            {
                if (playerInput == null)
                {
                    continue;
                }

                _playerInputDebugInfo.Add(new PlayerInputDebugInfo
                {
                    PlayerInput = playerInput,
                    ActiveInputMap = playerInput.currentActionMap?.name ?? "None"
                });
            }
        }

        private IEnumerator LogViewportStateAfterFrames(int frames, string context)
        {
            while (frames-- > 0)
            {
                yield return null;
            }

            LogAllPlayerViewportState(context);
        }

        private void LogAllPlayerViewportState(string context)
        {
            LogInfo(
                $"Split-screen diagnostic [{context}]. playerCount={_playerInputs.Count}, joiningEnabled={_playerInputManager.joiningEnabled}, " +
                $"splitScreen={_playerInputManager.splitScreen}, splitScreenArea={FormatRect(_playerInputManager.splitScreenArea)}");

            for (int i = 0; i < _playerInputs.Count; i++)
            {
                var playerInput = _playerInputs[i];
                if (playerInput == null)
                {
                    LogWarning($"Split-screen diagnostic [{context}] player slot {i} has a null PlayerInput reference.");
                    continue;
                }

                LogInfo(DescribePlayerInput(context, playerInput));
            }

            ValidateSplitScreenState(context);
        }

        private void ValidateSplitScreenState(string context)
        {
            if (_playerInputs.Count <= 1)
            {
                return;
            }

            int nullCameraCount = 0;
            int fullScreenCameraCount = 0;
            Dictionary<string, int> rectCounts = new Dictionary<string, int>();

            foreach (var playerInput in _playerInputs)
            {
                if (playerInput == null)
                {
                    continue;
                }

                var camera = ResolveCamera(playerInput);
                if (camera == null)
                {
                    nullCameraCount++;
                    continue;
                }

                if (Mathf.Approximately(camera.rect.x, 0f)
                    && Mathf.Approximately(camera.rect.y, 0f)
                    && Mathf.Approximately(camera.rect.width, 1f)
                    && Mathf.Approximately(camera.rect.height, 1f))
                {
                    fullScreenCameraCount++;
                }

                string rectKey = FormatRect(camera.rect);
                rectCounts.TryGetValue(rectKey, out int count);
                rectCounts[rectKey] = count + 1;

                var canvas = playerInput.GetComponentInChildren<Canvas>(true);
                if (canvas != null && canvas.worldCamera != null && canvas.worldCamera != camera)
                {
                    LogWarning(
                        $"Split-screen diagnostic [{context}] player {playerInput.playerIndex} has canvas camera mismatch. " +
                        $"canvasCamera={canvas.worldCamera.name}, playerCamera={camera.name}");
                }

                var viewportRoot = playerInput.transform.Find("UiCanvas/ViewportRoot") as RectTransform;
                if (viewportRoot == null)
                {
                    continue;
                }

                if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    if (!Approximately(viewportRoot.anchorMin, Vector2.zero)
                        || !Approximately(viewportRoot.anchorMax, Vector2.one))
                    {
                        LogWarning(
                            $"Split-screen diagnostic [{context}] player {playerInput.playerIndex} viewport root should stretch to the full canvas for ScreenSpaceCamera. " +
                            $"viewportRootMin={FormatVector2(viewportRoot.anchorMin)}, viewportRootMax={FormatVector2(viewportRoot.anchorMax)}, " +
                            $"cameraRect={FormatRect(camera.rect)}");
                    }
                }
                else if (!Approximately(viewportRoot.anchorMin, camera.rect.min)
                         || !Approximately(viewportRoot.anchorMax, camera.rect.max))
                {
                    LogWarning(
                        $"Split-screen diagnostic [{context}] player {playerInput.playerIndex} viewport root does not match camera rect. " +
                        $"viewportRootMin={FormatVector2(viewportRoot.anchorMin)}, viewportRootMax={FormatVector2(viewportRoot.anchorMax)}, " +
                        $"cameraRect={FormatRect(camera.rect)}");
                }
            }

            if (nullCameraCount > 0)
            {
                LogWarning($"Split-screen diagnostic [{context}] {nullCameraCount} players do not have an assigned camera.");
            }

            if (fullScreenCameraCount > 0)
            {
                LogWarning(
                    $"Split-screen diagnostic [{context}] {fullScreenCameraCount}/{_playerInputs.Count} player cameras are still full-screen.");
            }

            StringBuilder duplicateRects = new StringBuilder();
            foreach (var pair in rectCounts)
            {
                if (pair.Value <= 1)
                {
                    continue;
                }

                if (duplicateRects.Length > 0)
                {
                    duplicateRects.Append("; ");
                }

                duplicateRects.Append($"{pair.Key} x{pair.Value}");
            }

            if (duplicateRects.Length > 0)
            {
                LogWarning($"Split-screen diagnostic [{context}] duplicate camera rects detected: {duplicateRects}");
            }
        }

        private string DescribePlayerInput(string context, PlayerInput playerInput)
        {
            Camera camera = ResolveCamera(playerInput);
            Canvas canvas = playerInput.GetComponentInChildren<Canvas>(true);
            CanvasScaler canvasScaler = playerInput.GetComponentInChildren<CanvasScaler>(true);
            RectTransform viewportRoot = playerInput.transform.Find("UiCanvas/ViewportRoot") as RectTransform;
            string devices = FormatDevices(playerInput);

            return
                $"Split-screen diagnostic [{context}] playerIndex={playerInput.playerIndex}, object={playerInput.name}, splitScreenIndex={playerInput.splitScreenIndex}, " +
                $"scheme={playerInput.currentControlScheme}, actionMap={playerInput.currentActionMap?.name ?? "None"}, devices=[{devices}], " +
                $"camera={(camera != null ? camera.name : "None")}, cameraRect={(camera != null ? FormatRect(camera.rect) : "None")}, " +
                $"cameraPixelRect={(camera != null ? FormatRect(camera.pixelRect) : "None")}, targetDisplay={(camera != null ? camera.targetDisplay.ToString() : "None")}, " +
                $"canvas={(canvas != null ? canvas.name : "None")}, canvasCamera={(canvas?.worldCamera != null ? canvas.worldCamera.name : "None")}, " +
                $"canvasTargetDisplay={(canvas != null ? canvas.targetDisplay.ToString() : "None")}, scaleFactor={(canvasScaler != null ? canvasScaler.scaleFactor.ToString("F2") : "None")}, " +
                $"viewportRootMin={(viewportRoot != null ? FormatVector2(viewportRoot.anchorMin) : "None")}, viewportRootMax={(viewportRoot != null ? FormatVector2(viewportRoot.anchorMax) : "None")}";
        }

        private static Camera ResolveCamera(PlayerInput playerInput)
        {
            return playerInput.camera != null
                ? playerInput.camera
                : playerInput.GetComponentInChildren<Camera>(true);
        }

        private static string FormatDevices(PlayerInput playerInput)
        {
            if (playerInput.devices.Count == 0)
            {
                return "None";
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < playerInput.devices.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(playerInput.devices[i].displayName);
            }

            return builder.ToString();
        }

        private static string FormatRect(Rect rect)
        {
            return $"({rect.x:F2}, {rect.y:F2}, {rect.width:F2}, {rect.height:F2})";
        }

        private static string FormatVector2(Vector2 value)
        {
            return $"({value.x:F2}, {value.y:F2})";
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);
        }
    }
}
