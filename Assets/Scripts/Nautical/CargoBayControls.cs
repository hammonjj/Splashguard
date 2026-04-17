using BitBox.Library;
using UnityEngine;

namespace Bitbox
{
    public class CargoBayControls : MonoBehaviourBase
    {
        [SerializeField] private GameObject _portDoor;
        [SerializeField] private GameObject _starboardDoor;

        protected override void OnEnabled()
        {
        }

        protected override void OnDisabled()
        {
        }
    }
}
