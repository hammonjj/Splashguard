using BitBox.Library;
using BitBox.Library.Constants;
using UnityEngine;

namespace Bitbox
{
    public class CargoBayManager : MonoBehaviourBase
    {
        protected override void OnTriggerEntered(Collider other)
        {
            if (!other.gameObject.CompareTag(Tags.PlayerPickup))
            {
                LogInfo($"Incorrect Object: {other.gameObject.name}");
                return;
            }

            LogInfo($"Player picked up: {other.gameObject.name}");
            Destroy(other.gameObject);
        }
    }
}
