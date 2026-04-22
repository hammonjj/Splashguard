namespace BitBox.Library.Eventing.GlobalEvents
{
    public class InputManagerReadyEvent
    {
        public InputManagerReadyEvent()
        {
        }
    }

    public sealed class PlayerEnteredHelmEvent
    {
        public int PlayerIndex { get; }

        public PlayerEnteredHelmEvent(int playerIndex)
        {
            PlayerIndex = playerIndex;
        }
    }

    public sealed class PlayerExitedHelmEvent
    {
        public int PlayerIndex { get; }

        public PlayerExitedHelmEvent(int playerIndex)
        {
            PlayerIndex = playerIndex;
        }
    }

    public sealed class PlayerEnteredBoatGunEvent
    {
        public int PlayerIndex { get; }

        public PlayerEnteredBoatGunEvent(int playerIndex)
        {
            PlayerIndex = playerIndex;
        }
    }

    public sealed class PlayerExitedBoatGunEvent
    {
        public int PlayerIndex { get; }

        public PlayerExitedBoatGunEvent(int playerIndex)
        {
            PlayerIndex = playerIndex;
        }
    }

    public sealed class PlayerEnteredCraneEvent
    {
        public int PlayerIndex { get; }

        public PlayerEnteredCraneEvent(int playerIndex)
        {
            PlayerIndex = playerIndex;
        }
    }

    public sealed class PlayerExitedCraneEvent
    {
        public int PlayerIndex { get; }

        public PlayerExitedCraneEvent(int playerIndex)
        {
            PlayerIndex = playerIndex;
        }
    }
}
