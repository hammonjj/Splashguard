namespace BitBox.Library.Eventing.DebugEvents
{
    public class InfiniteAmmoEvent
    {
        public bool IsEnabled;

        public InfiniteAmmoEvent()
        {
        }

        public InfiniteAmmoEvent(bool isEnabled)
        {
            IsEnabled = isEnabled;
        }
    }
}
