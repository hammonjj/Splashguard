using UnityEngine;

namespace BitBox.Library.Eventing.WeaponEvents
{
    public sealed class WeaponControlAcquiredEvent
    {
        public WeaponControlAcquiredEvent(
            int playerIndex,
            GameObject playerRoot,
            GameObject weaponRoot,
            GameObject ownerRoot)
        {
            PlayerIndex = playerIndex;
            PlayerRoot = playerRoot;
            WeaponRoot = weaponRoot;
            OwnerRoot = ownerRoot;
        }

        public int PlayerIndex { get; }
        public GameObject PlayerRoot { get; }
        public GameObject WeaponRoot { get; }
        public GameObject OwnerRoot { get; }
    }

    public sealed class WeaponControlReleasedEvent
    {
        public WeaponControlReleasedEvent(int playerIndex, string reason)
        {
            PlayerIndex = playerIndex;
            Reason = reason;
        }

        public int PlayerIndex { get; }
        public string Reason { get; }
    }

    public sealed class WeaponFireInputEvent
    {
        public WeaponFireInputEvent(int playerIndex, bool isHeld)
        {
            PlayerIndex = playerIndex;
            IsHeld = isHeld;
        }

        public int PlayerIndex { get; }
        public bool IsHeld { get; }
    }
}
