namespace BitBox.Library.Eventing.SceneEvents
{
    public class PlayerReadyEvent
    {
        public int PlayerIndex { get; }

        public PlayerReadyEvent(int playerIndex)
        {
            PlayerIndex = playerIndex;
        }
    }
}
