using Sirenix.OdinInspector;
using UnityEngine;

namespace BitBox.Library.CameraUtils
{
    [DisallowMultipleComponent]
    public class CameraTargetAnchors : MonoBehaviourBase
    {
        [SerializeField, Required] private Transform _trackingTarget;
        [SerializeField] private Transform _lookAtTarget;

        public Transform TrackingTarget => _trackingTarget != null ? _trackingTarget : transform;
        public Transform LookAtTarget => _lookAtTarget != null ? _lookAtTarget : TrackingTarget;

        public void ConfigureTargets(Transform trackingTarget, Transform lookAtTarget)
        {
            _trackingTarget = trackingTarget;
            _lookAtTarget = lookAtTarget;
        }
    }
}
