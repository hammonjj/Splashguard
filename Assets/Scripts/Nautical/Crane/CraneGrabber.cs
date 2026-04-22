using BitBox.Library;
using UnityEngine;

namespace Bitbox.Splashguard.Nautical.Crane
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CraneGrabber : MonoBehaviourBase
    {
        private const string DefaultPickupTag = "PlayerPickup";
        private const string BottomSuctionSensorName = "BottomSuctionSensor";

        [Header("References")]
        [SerializeField] private Rigidbody _grabberRigidbody;
        [SerializeField] private Collider _bottomSuctionSensor;

        [Header("Suction")]
        [SerializeField] private string _pickupTag = DefaultPickupTag;
        [SerializeField] private LayerMask _pickupLayers = ~0;
        [SerializeField, Min(0f)] private float _jointBreakForce;
        [SerializeField, Min(0f)] private float _jointBreakTorque;

        private FixedJoint _heldJoint;
        private Rigidbody _heldRigidbody;
        private CranePickupTarget _heldPickupTarget;
        private bool _missingSensorWarningLogged;

        public Rigidbody GrabberRigidbody => _grabberRigidbody;
        public Collider BottomSuctionSensor => _bottomSuctionSensor;
        public Rigidbody HeldRigidbody => _heldRigidbody;
        public bool HasHeldPickup => _heldJoint != null && _heldRigidbody != null;

        public void ConfigureReferences(Rigidbody grabberRigidbody, Collider bottomSuctionSensor)
        {
            _grabberRigidbody = grabberRigidbody;
            _bottomSuctionSensor = bottomSuctionSensor;
            CacheReferences();
        }

        protected override void OnEnabled()
        {
            CacheReferences();
        }

        protected override void OnDisabled()
        {
            ReleaseHeldPickup();
        }

        protected override void OnDestroyed()
        {
            ReleaseHeldPickup();
        }

        protected override void OnDrawnGizmos()
        {
            CacheReferences();
            if (_bottomSuctionSensor == null)
            {
                return;
            }

            Gizmos.color = HasHeldPickup ? Color.green : new Color(1f, 0.75f, 0.1f, 0.75f);
            ResolveSensorOverlapBox(
                _bottomSuctionSensor,
                out Vector3 sensorCenter,
                out Vector3 sensorHalfExtents,
                out Quaternion sensorRotation);
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(sensorCenter, sensorRotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, sensorHalfExtents * 2f);
            Gizmos.matrix = previousMatrix;
        }

        private void OnValidate()
        {
            _jointBreakForce = Mathf.Max(0f, _jointBreakForce);
            _jointBreakTorque = Mathf.Max(0f, _jointBreakTorque);
            if (string.IsNullOrWhiteSpace(_pickupTag))
            {
                _pickupTag = DefaultPickupTag;
            }
        }

        public void SetSuctionHeld(bool suctionHeld)
        {
            CacheReferences();
            if (!suctionHeld)
            {
                ReleaseHeldPickup();
                return;
            }

            if (HasHeldPickup)
            {
                return;
            }

            if (_bottomSuctionSensor == null)
            {
                LogMissingSensorWarning();
                return;
            }

            if (TryFindBestPickup(out Rigidbody pickupRigidbody, out Vector3 attachPosition, out CranePickupTarget pickupTarget))
            {
                AttachPickup(pickupRigidbody, attachPosition, pickupTarget);
            }
        }

        public void ReleaseHeldPickup()
        {
            if (_heldJoint != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_heldJoint);
                }
                else
                {
                    DestroyImmediate(_heldJoint);
                }
            }

            _heldPickupTarget?.SetGrabbedByCrane(false);
            _heldJoint = null;
            _heldRigidbody = null;
            _heldPickupTarget = null;
        }

        public bool TryFindBestPickup(
            out Rigidbody pickupRigidbody,
            out Vector3 attachPosition,
            out CranePickupTarget pickupTarget)
        {
            pickupRigidbody = null;
            attachPosition = default;
            pickupTarget = null;

            if (_bottomSuctionSensor == null)
            {
                return false;
            }

            ResolveSensorOverlapBox(
                _bottomSuctionSensor,
                out Vector3 sensorCenter,
                out Vector3 sensorHalfExtents,
                out Quaternion sensorRotation);
            Vector3 sensorAttachPosition = ResolveSensorAttachPosition();
            Collider[] candidates = Physics.OverlapBox(
                sensorCenter,
                sensorHalfExtents,
                sensorRotation,
                _pickupLayers,
                QueryTriggerInteraction.Collide);

            float bestSqrDistance = float.PositiveInfinity;
            for (int i = 0; i < candidates.Length; i++)
            {
                Collider candidate = candidates[i];
                if (!TryResolvePickup(
                        candidate,
                        out Rigidbody candidateRigidbody,
                        out Vector3 candidateAttachPosition,
                        out CranePickupTarget candidatePickupTarget))
                {
                    continue;
                }

                float sqrDistance = (candidateAttachPosition - sensorAttachPosition).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    pickupRigidbody = candidateRigidbody;
                    attachPosition = candidateAttachPosition;
                    pickupTarget = candidatePickupTarget;
                }
            }

            return pickupRigidbody != null;
        }

        public bool TryResolvePickup(
            Collider candidateCollider,
            out Rigidbody pickupRigidbody,
            out Vector3 attachPosition,
            out CranePickupTarget pickupTarget)
        {
            pickupRigidbody = null;
            attachPosition = default;
            pickupTarget = null;

            if (candidateCollider == null || _grabberRigidbody == null || _bottomSuctionSensor == null)
            {
                return false;
            }

            Rigidbody candidateRigidbody = candidateCollider.attachedRigidbody
                ?? candidateCollider.GetComponentInParent<Rigidbody>();
            if (candidateRigidbody == null || candidateRigidbody == _grabberRigidbody)
            {
                return false;
            }

            Transform taggedRoot = FindTaggedPickupRoot(candidateCollider.transform);
            if (taggedRoot == null)
            {
                return false;
            }

            CranePickupTarget candidatePickupTarget = candidateCollider.GetComponentInParent<CranePickupTarget>();
            if (!CranePickupTarget.TryResolveAttachPosition(
                    candidateCollider,
                    candidateRigidbody,
                    candidatePickupTarget,
                    ResolveSensorAttachPosition(),
                    out Vector3 candidateAttachPosition))
            {
                return false;
            }

            pickupRigidbody = candidateRigidbody;
            attachPosition = candidateAttachPosition;
            pickupTarget = candidatePickupTarget;
            return true;
        }

        private void AttachPickup(Rigidbody pickupRigidbody, Vector3 attachPosition, CranePickupTarget pickupTarget)
        {
            if (_grabberRigidbody == null || pickupRigidbody == null || _bottomSuctionSensor == null)
            {
                return;
            }

            ReleaseHeldPickup();
            Vector3 grabberAttachPosition = ResolveSensorAttachPosition();
            Vector3 grabberLocalAnchor = _grabberRigidbody.transform.InverseTransformPoint(grabberAttachPosition);
            Vector3 pickupLocalAnchor = pickupRigidbody.transform.InverseTransformPoint(attachPosition);
            SnapPickupAnchorToGrabber(pickupRigidbody, attachPosition, grabberAttachPosition);

            _heldJoint = _grabberRigidbody.gameObject.AddComponent<FixedJoint>();
            _heldJoint.connectedBody = pickupRigidbody;
            _heldJoint.autoConfigureConnectedAnchor = false;
            _heldJoint.anchor = grabberLocalAnchor;
            _heldJoint.connectedAnchor = pickupLocalAnchor;
            _heldJoint.enableCollision = false;
            _heldJoint.breakForce = _jointBreakForce > 0f ? _jointBreakForce : Mathf.Infinity;
            _heldJoint.breakTorque = _jointBreakTorque > 0f ? _jointBreakTorque : Mathf.Infinity;
            _heldRigidbody = pickupRigidbody;
            _heldPickupTarget = pickupTarget;
            _heldPickupTarget?.SetGrabbedByCrane(true);
        }

        private void CacheReferences()
        {
            _grabberRigidbody ??= GetComponent<Rigidbody>();
            if (_bottomSuctionSensor == null)
            {
                Transform sensorTransform = FindChildByName(transform, BottomSuctionSensorName);
                if (sensorTransform != null)
                {
                    _bottomSuctionSensor = sensorTransform.GetComponent<Collider>();
                }
            }

            if (_bottomSuctionSensor == null)
            {
                Collider[] colliders = GetComponentsInChildren<Collider>(includeInactive: true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null && colliders[i].isTrigger)
                    {
                        _bottomSuctionSensor = colliders[i];
                        break;
                    }
                }
            }

            if (_bottomSuctionSensor != null)
            {
                _bottomSuctionSensor.isTrigger = true;
            }
        }

        private Transform FindTaggedPickupRoot(Transform candidate)
        {
            Transform current = candidate;
            while (current != null)
            {
                if (current.tag == _pickupTag)
                {
                    return current;
                }

                current = current.parent;
            }

            return null;
        }

        private Vector3 ResolveSensorAttachPosition()
        {
            if (_bottomSuctionSensor is BoxCollider boxCollider)
            {
                return boxCollider.transform.TransformPoint(boxCollider.center);
            }

            return _bottomSuctionSensor != null ? _bottomSuctionSensor.bounds.center : transform.position;
        }

        private void LogMissingSensorWarning()
        {
            if (_missingSensorWarningLogged)
            {
                return;
            }

            _missingSensorWarningLogged = true;
            LogWarning(
                $"Crane grabber could not find a bottom suction trigger. grabber={name}, expectedChild={BottomSuctionSensorName}.");
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

        private static void ResolveSensorOverlapBox(
            Collider sensor,
            out Vector3 center,
            out Vector3 halfExtents,
            out Quaternion rotation)
        {
            if (sensor is BoxCollider boxCollider)
            {
                Transform sensorTransform = boxCollider.transform;
                center = sensorTransform.TransformPoint(boxCollider.center);
                halfExtents = Vector3.Scale(boxCollider.size, Abs(sensorTransform.lossyScale)) * 0.5f;
                rotation = sensorTransform.rotation;
                return;
            }

            Bounds bounds = sensor.bounds;
            center = bounds.center;
            halfExtents = bounds.extents;
            rotation = sensor.transform.rotation;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(
                Mathf.Abs(value.x),
                Mathf.Abs(value.y),
                Mathf.Abs(value.z));
        }

        private static void SnapPickupAnchorToGrabber(
            Rigidbody pickupRigidbody,
            Vector3 pickupAttachPosition,
            Vector3 grabberAttachPosition)
        {
            Vector3 snapDelta = grabberAttachPosition - pickupAttachPosition;
            pickupRigidbody.position += snapDelta;
            if (!pickupRigidbody.isKinematic)
            {
                pickupRigidbody.linearVelocity = Vector3.zero;
                pickupRigidbody.angularVelocity = Vector3.zero;
            }

            Physics.SyncTransforms();
        }
    }
}
