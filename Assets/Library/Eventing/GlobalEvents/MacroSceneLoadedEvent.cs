using BitBox.Library.Constants.Enums;

namespace BitBox.Library.Eventing.GlobalEvents
{
    public class MacroSceneLoadedEvent
    {
        public MacroSceneType SceneType { get; private set; }

        public MacroSceneLoadedEvent(MacroSceneType sceneType)
        {
            SceneType = sceneType;
        }
    }

    public sealed class GameplayStartupBeganEvent
    {
        public MacroSceneType SceneType { get; }
        public int ExpectedPlayers { get; }

        public GameplayStartupBeganEvent(MacroSceneType sceneType, int expectedPlayers)
        {
            SceneType = sceneType;
            ExpectedPlayers = expectedPlayers;
        }
    }

    public sealed class GameplayStartupProgressEvent
    {
        public MacroSceneType SceneType { get; }
        public float Progress { get; }
        public string PhaseText { get; }
        public int CompletedPlayers { get; }
        public int ExpectedPlayers { get; }

        public GameplayStartupProgressEvent(
            MacroSceneType sceneType,
            float progress,
            string phaseText,
            int completedPlayers,
            int expectedPlayers)
        {
            SceneType = sceneType;
            Progress = progress;
            PhaseText = phaseText;
            CompletedPlayers = completedPlayers;
            ExpectedPlayers = expectedPlayers;
        }
    }

    public sealed class GameplayStartupCompletedEvent
    {
        public MacroSceneType SceneType { get; }
        public bool TimedOut { get; }
        public int CompletedPlayers { get; }
        public int ExpectedPlayers { get; }

        public GameplayStartupCompletedEvent(
            MacroSceneType sceneType,
            bool timedOut,
            int completedPlayers,
            int expectedPlayers)
        {
            SceneType = sceneType;
            TimedOut = timedOut;
            CompletedPlayers = completedPlayers;
            ExpectedPlayers = expectedPlayers;
        }
    }
}
