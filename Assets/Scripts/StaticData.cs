using BitBox.Library.Input;
using BitBox.Toymageddon;
using UnityEngine.InputSystem;

namespace BitBox.Library
{
    public sealed class PendingPlayerJoinRequest
    {
        public string ControlScheme { get; set; }
        public InputDevice PairWithDevice { get; set; }
        public string SourceControlPath { get; set; }
    }

    public static class StaticData
    {
        public static GameController GameController { get; set; }
        public static PlayerCoordinator PlayerInputCoordinator { get; set; }
        public static PendingPlayerJoinRequest PendingInitialJoinRequest { get; set; }
    }
}
