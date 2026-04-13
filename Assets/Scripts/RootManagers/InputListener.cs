using System.Collections.Generic;
using BitBox.Library.Constants;
using BitBox.Library.Constants.Enums;
using BitBox.Library.Eventing.GlobalEvents;
using UnityEngine.InputSystem;

namespace BitBox.Library.Input
{
    public class InputListener : MonoBehaviourBase
    {
        private readonly HashSet<int> _playersUsingHelm = new();
        private readonly HashSet<int> _playersUsingBoatGun = new();

        private PlayerCoordinator _playerInputCoordinator;
        private MacroSceneType _currentMacroScene = MacroSceneType.None;

        protected override void OnEnabled()
        {
            _globalMessageBus.Subscribe<PauseGameEvent>(OnPauseGame);
            _globalMessageBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
            _globalMessageBus.Subscribe<MacroSceneLoadedEvent>(OnMacroSceneLoaded);
            _globalMessageBus.Subscribe<PlayerEnteredHelmEvent>(OnPlayerEnteredHelm);
            _globalMessageBus.Subscribe<PlayerExitedHelmEvent>(OnPlayerExitedHelm);
            _globalMessageBus.Subscribe<PlayerEnteredBoatGunEvent>(OnPlayerEnteredBoatGun);
            _globalMessageBus.Subscribe<PlayerExitedBoatGunEvent>(OnPlayerExitedBoatGun);

            _playerInputCoordinator = StaticData.PlayerInputCoordinator;
            Assert.IsNotNull(_playerInputCoordinator, "PlayerInputCoordinator is not set in StaticData.");
            _currentMacroScene = StaticData.GameController != null
                ? StaticData.GameController.CurrentMacroScene
                : MacroSceneType.None;
        }

        protected override void OnDisabled()
        {
            _globalMessageBus.Unsubscribe<PauseGameEvent>(OnPauseGame);
            _globalMessageBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
            _globalMessageBus.Unsubscribe<MacroSceneLoadedEvent>(OnMacroSceneLoaded);
            _globalMessageBus.Unsubscribe<PlayerEnteredHelmEvent>(OnPlayerEnteredHelm);
            _globalMessageBus.Unsubscribe<PlayerExitedHelmEvent>(OnPlayerExitedHelm);
            _globalMessageBus.Unsubscribe<PlayerEnteredBoatGunEvent>(OnPlayerEnteredBoatGun);
            _globalMessageBus.Unsubscribe<PlayerExitedBoatGunEvent>(OnPlayerExitedBoatGun);
        }

        private void OnMacroSceneLoaded(MacroSceneLoadedEvent @event)
        {
            _currentMacroScene = @event.SceneType;
            _playersUsingHelm.Clear();
            _playersUsingBoatGun.Clear();

            if (!@event.SceneType.IsGameplayScene())
            {
                return;
            }

            foreach (PlayerInput playerInput in _playerInputCoordinator.PlayerInputs)
            {
                ActivateThirdPersonMap(playerInput);
            }
        }

        private void OnPlayerDied(PlayerDiedEvent @event)
        {
            _playersUsingHelm.Clear();
            _playersUsingBoatGun.Clear();

            foreach (PlayerInput playerInput in _playerInputCoordinator.PlayerInputs)
            {
                ActivateUiMap(playerInput);
            }
        }

        private void OnPauseGame(PauseGameEvent @event)
        {
            foreach (PlayerInput playerInput in _playerInputCoordinator.PlayerInputs)
            {
                if (@event.IsPaused)
                {
                    ActivateUiMap(playerInput);
                    continue;
                }

                if (_currentMacroScene.IsGameplayScene())
                {
                    ActivateGameplayMap(playerInput);
                }
            }
        }

        private void OnPlayerEnteredHelm(PlayerEnteredHelmEvent @event)
        {
            _playersUsingHelm.Add(@event.PlayerIndex);
            _playersUsingBoatGun.Remove(@event.PlayerIndex);

            if (!_currentMacroScene.IsGameplayScene()
                || !TryGetPlayerInput(@event.PlayerIndex, out PlayerInput playerInput))
            {
                return;
            }

            ActivateNavalMap(playerInput);
        }

        private void OnPlayerExitedHelm(PlayerExitedHelmEvent @event)
        {
            _playersUsingHelm.Remove(@event.PlayerIndex);

            if (!_currentMacroScene.IsGameplayScene()
                || !TryGetPlayerInput(@event.PlayerIndex, out PlayerInput playerInput))
            {
                return;
            }

            ActivateThirdPersonMap(playerInput);
        }

        private void OnPlayerEnteredBoatGun(PlayerEnteredBoatGunEvent @event)
        {
            _playersUsingBoatGun.Add(@event.PlayerIndex);
            _playersUsingHelm.Remove(@event.PlayerIndex);

            if (!_currentMacroScene.IsGameplayScene()
                || !TryGetPlayerInput(@event.PlayerIndex, out PlayerInput playerInput))
            {
                return;
            }

            ActivateBoatGunnerMap(playerInput);
        }

        private void OnPlayerExitedBoatGun(PlayerExitedBoatGunEvent @event)
        {
            _playersUsingBoatGun.Remove(@event.PlayerIndex);

            if (!_currentMacroScene.IsGameplayScene()
                || !TryGetPlayerInput(@event.PlayerIndex, out PlayerInput playerInput))
            {
                return;
            }

            ActivateThirdPersonMap(playerInput);
        }

        private static void ActivateUiMap(PlayerInput playerInput)
        {
            Assert.IsNotNull(playerInput, $"{nameof(InputListener)} expected a non-null {nameof(PlayerInput)} when activating the UI map.");

            playerInput.actions.Enable();
            playerInput.SwitchCurrentActionMap(Strings.UIMap);
            playerInput.ActivateInput();
        }

        private static void ActivateThirdPersonMap(PlayerInput playerInput)
        {
            Assert.IsNotNull(playerInput, $"{nameof(InputListener)} expected a non-null {nameof(PlayerInput)} when activating the third-person map.");

            playerInput.actions.Enable();
            playerInput.SwitchCurrentActionMap(Strings.ThirdPersonControls);
            playerInput.ActivateInput();
        }

        private static void ActivateNavalMap(PlayerInput playerInput)
        {
            Assert.IsNotNull(playerInput, $"{nameof(InputListener)} expected a non-null {nameof(PlayerInput)} when activating the naval-navigation map.");

            playerInput.actions.Enable();
            playerInput.SwitchCurrentActionMap(Strings.NavalNavigation);
            playerInput.ActivateInput();
        }

        private static void ActivateBoatGunnerMap(PlayerInput playerInput)
        {
            Assert.IsNotNull(playerInput, $"{nameof(InputListener)} expected a non-null {nameof(PlayerInput)} when activating the boat-gunner map.");

            playerInput.actions.Enable();
            playerInput.SwitchCurrentActionMap(Strings.BoatGunner);
            playerInput.ActivateInput();
        }

        private void ActivateGameplayMap(PlayerInput playerInput)
        {
            if (_playersUsingBoatGun.Contains(playerInput.playerIndex))
            {
                ActivateBoatGunnerMap(playerInput);
                return;
            }

            if (_playersUsingHelm.Contains(playerInput.playerIndex))
            {
                ActivateNavalMap(playerInput);
                return;
            }

            ActivateThirdPersonMap(playerInput);
        }

        private bool TryGetPlayerInput(int playerIndex, out PlayerInput playerInput)
        {
            foreach (PlayerInput candidate in _playerInputCoordinator.PlayerInputs)
            {
                if (candidate != null && candidate.playerIndex == playerIndex)
                {
                    playerInput = candidate;
                    return true;
                }
            }

            playerInput = null;
            return false;
        }
    }
}
