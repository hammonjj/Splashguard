namespace BitBox.Library.Eventing.SceneEvents
{
    public sealed class PlayerUnreadyEvent
    {
        public int PlayerIndex { get; }

        public PlayerUnreadyEvent(int playerIndex)
        {
            PlayerIndex = playerIndex;
        }
    }
}
