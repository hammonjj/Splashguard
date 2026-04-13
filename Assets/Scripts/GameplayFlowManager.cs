

using BitBox.Library;
using BitBox.Library.Constants.Enums;
using BitBox.Library.Eventing.GlobalEvents;
using UnityEngine;

namespace BitBox.Toymageddon
{
    public class GameplayFlowManager : MonoBehaviourBase
    {
        public bool IsPaused { get; private set; }

        protected override void OnAwakened()
        {
            _globalMessageBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
            _globalMessageBus.Subscribe<QuitToMainMenuEvent>(OnQuitToMainMenu);
            _globalMessageBus.Subscribe<PauseGameEvent>(OnPauseGame);
        }

        private void OnQuitToMainMenu(QuitToMainMenuEvent @event)
        {
            LogInfo("Quitting to main menu.");
            Time.timeScale = 1f; // Reset time scale when quitting to main menu
            _globalMessageBus.Publish(new LoadMacroSceneEvent(MacroSceneType.TitleMenu));
        }

        private void OnPlayerDied(PlayerDiedEvent @event)
        {
            LogInfo("Player Died");
            IsPaused = true;
            Time.timeScale = 0f;
            OnPauseGame(new PauseGameEvent(true));
        }

        private void OnPauseGame(PauseGameEvent @event)
        {
            if (@event.IsPaused)
            {
                LogDebug("Game paused.");
                IsPaused = true;
                Time.timeScale = 0f;
            }
            else
            {
                LogDebug("Game resumed.");
                IsPaused = false;
                Time.timeScale = 1f;
            }
        }
    }
}
