namespace BitBox.Library.Eventing.GlobalEvents
{
    public class PauseGameEvent
    {
        public const int NoPlayerIndex = -1;

        public bool IsPaused { get; }
        public int InitiatingPlayerIndex { get; }

        public PauseGameEvent(bool isPaused, int initiatingPlayerIndex = NoPlayerIndex)
        {
            IsPaused = isPaused;
            InitiatingPlayerIndex = initiatingPlayerIndex;
        }
    }
}
