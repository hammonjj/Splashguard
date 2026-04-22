using UnityEngine;

namespace Bitbox.Splashguard.Nautical.Crane
{
    public enum CranePickupAttachMode
    {
        FixedAttachPoint = 0,
        ClosestColliderPoint = 1
    }

    [DisallowMultipleComponent]
    public sealed class CranePickupTarget : MonoBehaviour
    {
        private const string AttachmentPointName = "CraneAttachmentPoint";

        [SerializeField] private CranePickupAttachMode _attachMode = CranePickupAttachMode.FixedAttachPoint;
        [SerializeField] private Transform _attachPoint;
        [SerializeField] private Rigidbody _rigidbody;

        private bool _isGrabbedByCrane;

        public CranePickupAttachMode AttachMode => _attachMode;
        public Transform AttachPoint => _attachPoint != null ? _attachPoint : transform;
        public Rigidbody Rigidbody => _rigidbody != null ? _rigidbody : GetComponentInParent<Rigidbody>();
        public bool IsGrabbedByCrane => _isGrabbedByCrane;

        public void SetGrabbedByCrane(bool isGrabbed)
        {
            _isGrabbedByCrane = isGrabbed;
        }

        private void Reset()
        {
            _rigidbody = GetComponentInParent<Rigidbody>();
            _attachPoint = FindChildByName(transform, AttachmentPointName);
        }

        private void OnValidate()
        {
            if (_rigidbody == null)
            {
                _rigidbody = GetComponentInParent<Rigidbody>();
            }

            if (_attachPoint == null)
            {
                _attachPoint = FindChildByName(transform, AttachmentPointName);
            }
        }

        public static Transform ResolveAttachPoint(Rigidbody pickupRigidbody, CranePickupTarget pickupTarget)
        {
            if (pickupTarget != null && pickupTarget.AttachPoint != null)
            {
                return pickupTarget.AttachPoint;
            }

            if (pickupRigidbody == null)
            {
                return null;
            }

            Transform namedAttachPoint = FindChildByName(pickupRigidbody.transform, AttachmentPointName);
            return namedAttachPoint != null ? namedAttachPoint : pickupRigidbody.transform;
        }

        public static bool TryResolveAttachPosition(
            Collider candidateCollider,
            Rigidbody pickupRigidbody,
            CranePickupTarget pickupTarget,
            Vector3 sensorPosition,
            out Vector3 attachPosition)
        {
            attachPosition = default;
            if (pickupTarget != null
                && pickupTarget.AttachMode == CranePickupAttachMode.ClosestColliderPoint
                && candidateCollider != null)
            {
                attachPosition = candidateCollider.ClosestPoint(sensorPosition);
                return true;
            }

            Transform attachPoint = ResolveAttachPoint(pickupRigidbody, pickupTarget);
            if (attachPoint == null)
            {
                return false;
            }

            attachPosition = attachPoint.position;
            return true;
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform nestedChild = FindChildByName(child, childName);
                if (nestedChild != null)
                {
                    return nestedChild;
                }
            }

            return null;
        }
    }
}
