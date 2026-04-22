using BitBox.Library;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Bitbox.Splashguard.Nautical.Crane
{
    public static class CraneCableUtility
    {
        public const float MinimumCableLengthFloor = 0.001f;
        private const float MinimumProjectionDistance = 0.0001f;

        public static Vector3 ApplyExponentialDamping(Vector3 value, float dampingPerSecond, float deltaTime)
        {
            if (dampingPerSecond <= 0f || deltaTime <= 0f)
            {
                return value;
            }

            return value * Mathf.Exp(-dampingPerSecond * deltaTime);
        }

        public static float ClampCableLength(float cableLength, float minimumCableLength, float maximumCableLength)
        {
            if (minimumCableLength > maximumCableLength)
            {
                (minimumCableLength, maximumCableLength) = (maximumCableLength, minimumCableLength);
            }

            return Mathf.Clamp(
                cableLength,
                Mathf.Max(MinimumCableLengthFloor, minimumCableLength),
                Mathf.Max(MinimumCableLengthFloor, maximumCableLength));
        }

        public static float CalculateTautCableLength(Vector3 cableAnchorPosition, Vector3 grabberPosition, float slack)
        {
            return Mathf.Max(
                MinimumCableLengthFloor,
                Vector3.Distance(cableAnchorPosition, grabberPosition) + Mathf.Max(0f, slack));
        }

        public static bool TryProjectToCableLength(
            Vector3 cableAnchorPosition,
            Vector3 grabberPosition,
            float cableLength,
            float tolerance,
            out Vector3 projectedGrabberPosition,
            out Vector3 cableDirection,
            out float actualDistance)
        {
            Vector3 anchorToGrabber = grabberPosition - cableAnchorPosition;
            actualDistance = anchorToGrabber.magnitude;
            float clampedCableLength = Mathf.Max(MinimumCableLengthFloor, cableLength);
            if (actualDistance <= MinimumProjectionDistance)
            {
                projectedGrabberPosition = grabberPosition;
                cableDirection = Vector3.down;
                return false;
            }

            cableDirection = anchorToGrabber / actualDistance;
            if (actualDistance <= clampedCableLength + Mathf.Max(0f, tolerance))
            {
                projectedGrabberPosition = grabberPosition;
                return false;
            }

            projectedGrabberPosition = cableAnchorPosition + cableDirection * clampedCableLength;
            return true;
        }

        public static float ApplyHoistInput(
            float currentCableLength,
            float hoistInput,
            float hoistSpeed,
            float deltaTime,
            float minimumCableLength,
            float maximumCableLength)
        {
            if (deltaTime <= 0f || hoistSpeed <= 0f)
            {
                return ClampCableLength(currentCableLength, minimumCableLength, maximumCableLength);
            }

            return ClampCableLength(
                currentCableLength - hoistInput * hoistSpeed * deltaTime,
                minimumCableLength,
                maximumCableLength);
        }
    }

    [DisallowMultipleComponent]
    public sealed class CraneCableController : MonoBehaviourBase
    {
        [Header("References")]
        [SerializeField] private Transform _cableAnchor;
        [SerializeField] private Rigidbody _cableAnchorRigidbody;
        [SerializeField] private Rigidbody _grabberRigidbody;
        [SerializeField] private ConfigurableJoint _cableJoint;
        [SerializeField] private LineRenderer _cableRenderer;

        [Header("Cable")]
        [SerializeField, Min(CraneCableUtility.MinimumCableLengthFloor)] private float _minimumCableLength = 0.75f;
        [SerializeField, Min(CraneCableUtility.MinimumCableLengthFloor)] private float _maximumCableLength = 8f;
        [SerializeField, Min(CraneCableUtility.MinimumCableLengthFloor)] private float _defaultCableLength = 3f;
        [SerializeField] private bool _captureDefaultLengthFromCurrentDistance = true;
        [SerializeField] private bool _useCapturedLengthAsMinimum = true;
        [SerializeField, Min(0f)] private float _defaultCableSlack = 0f;
        [SerializeField, Min(0f)] private float _hoistMetersPerSecond = 3f;
        [SerializeField, Range(0f, 1f)] private float _returnVelocityDamping = 0.85f;
        [SerializeField, Min(0f)] private float _cableConstraintTolerance = 0.005f;

        [Header("Swing Stabilization")]
        [SerializeField, Min(0f)] private float _linearLimitSpring = 0f;
        [SerializeField, Min(0f)] private float _linearLimitDamper = 0f;
        [SerializeField, Min(0f)] private float _swingDampingPerSecond = 0.35f;
        [SerializeField, Min(0f)] private float _angularSwingDampingPerSecond = 0.55f;

        [Header("Diagnostics")]
        [SerializeField] private bool _logCableDiagnostics = true;
        [SerializeField, Min(0.1f)] private float _cableDiagnosticIntervalSeconds = 0.5f;

        [ShowInInspector, ReadOnly] private float _currentCableLength;
        private float _returnStartCableLength;
        private float _nextCableDiagnosticTime;
        private float _nextHoistDiagnosticTime;

        public Transform CableAnchor => _cableAnchor;
        public Rigidbody GrabberRigidbody => _grabberRigidbody;
        public ConfigurableJoint CableJoint => _cableJoint;
        public float CurrentCableLength => _currentCableLength;
        public float DefaultCableLength => _defaultCableLength;
        public float MinimumCableLength => _minimumCableLength;
        public float MaximumCableLength => _maximumCableLength;

        public void ConfigureReferences(
            Transform cableAnchor,
            Rigidbody cableAnchorRigidbody,
            Rigidbody grabberRigidbody,
            ConfigurableJoint cableJoint,
            LineRenderer cableRenderer)
        {
            _cableAnchor = cableAnchor;
            _cableAnchorRigidbody = cableAnchorRigidbody;
            _grabberRigidbody = grabberRigidbody;
            _cableJoint = cableJoint;
            _cableRenderer = cableRenderer;
            CacheReferences();
            CaptureRestLength();
            ApplyCableLength(_currentCableLength);
        }

        protected override void OnEnabled()
        {
            CacheReferences();
            CaptureRestLength();
            ApplyCableLength(_currentCableLength);
            UpdateCableRenderer();
        }

        protected override void OnLateUpdated()
        {
            UpdateCableRenderer();
        }

        protected override void OnFixedUpdated()
        {
            DampGrabberSwing(Time.fixedDeltaTime);
            EnforceCableLength();
        }

        private void OnValidate()
        {
            _minimumCableLength = Mathf.Max(CraneCableUtility.MinimumCableLengthFloor, _minimumCableLength);
            _maximumCableLength = Mathf.Max(CraneCableUtility.MinimumCableLengthFloor, _maximumCableLength);
            if (_minimumCableLength > _maximumCableLength)
            {
                (_minimumCableLength, _maximumCableLength) = (_maximumCableLength, _minimumCableLength);
            }

            _defaultCableLength = CraneCableUtility.ClampCableLength(
                _defaultCableLength,
                _minimumCableLength,
                _maximumCableLength);
            _hoistMetersPerSecond = Mathf.Max(0f, _hoistMetersPerSecond);
            _linearLimitSpring = Mathf.Max(0f, _linearLimitSpring);
            _linearLimitDamper = Mathf.Max(0f, _linearLimitDamper);
            _swingDampingPerSecond = Mathf.Max(0f, _swingDampingPerSecond);
            _angularSwingDampingPerSecond = Mathf.Max(0f, _angularSwingDampingPerSecond);
            _defaultCableSlack = Mathf.Max(0f, _defaultCableSlack);
            _cableConstraintTolerance = Mathf.Max(0f, _cableConstraintTolerance);
            _cableDiagnosticIntervalSeconds = Mathf.Max(0.1f, _cableDiagnosticIntervalSeconds);
        }

        public void ApplyHoistInput(float hoistInput, float deltaTime)
        {
            float previousCableLength = _currentCableLength;
            ApplyCableLength(CraneCableUtility.ApplyHoistInput(
                _currentCableLength,
                hoistInput,
                _hoistMetersPerSecond,
                deltaTime,
                _minimumCableLength,
                _maximumCableLength));
            LogHoistDiagnostic(previousCableLength, hoistInput, deltaTime);
        }

        public void CaptureRestLength()
        {
            float restLength = _defaultCableLength;
            if (_captureDefaultLengthFromCurrentDistance && _cableAnchor != null && _grabberRigidbody != null)
            {
                restLength = CraneCableUtility.CalculateTautCableLength(
                    _cableAnchor.position,
                    _grabberRigidbody.position,
                    _defaultCableSlack);
            }

            restLength = CraneCableUtility.ClampCableLength(restLength, _minimumCableLength, _maximumCableLength);
            if (_useCapturedLengthAsMinimum)
            {
                _minimumCableLength = Mathf.Min(restLength, _maximumCableLength);
            }

            _defaultCableLength = CraneCableUtility.ClampCableLength(
                restLength,
                _minimumCableLength,
                _maximumCableLength);
            _currentCableLength = _defaultCableLength;
            LogCableSnapshot("capture-rest-length");
        }

        public void BeginReturnToRest()
        {
            _returnStartCableLength = _currentCableLength;
        }

        public void EvaluateReturnToRest(float normalizedTime)
        {
            float t = Mathf.Clamp01(normalizedTime);
            ApplyCableLength(Mathf.Lerp(_returnStartCableLength, _defaultCableLength, t));
            DampGrabberVelocity(_returnVelocityDamping);
        }

        public void ApplyCableLength(float cableLength)
        {
            CacheReferences();
            _currentCableLength = CraneCableUtility.ClampCableLength(
                cableLength,
                _minimumCableLength,
                _maximumCableLength);

            if (_cableJoint != null)
            {
                SoftJointLimit limit = _cableJoint.linearLimit;
                limit.limit = _currentCableLength;
                _cableJoint.linearLimit = limit;
            }

            UpdateCableRenderer();
        }

        public void DampGrabberVelocity(float damping)
        {
            if (_grabberRigidbody == null)
            {
                return;
            }

            float clampedDamping = Mathf.Clamp01(damping);
            _grabberRigidbody.linearVelocity *= clampedDamping;
            _grabberRigidbody.angularVelocity *= clampedDamping;
        }

        public void DampGrabberSwing(float deltaTime)
        {
            if (_grabberRigidbody == null)
            {
                return;
            }

            _grabberRigidbody.linearVelocity = CraneCableUtility.ApplyExponentialDamping(
                _grabberRigidbody.linearVelocity,
                _swingDampingPerSecond,
                deltaTime);
            _grabberRigidbody.angularVelocity = CraneCableUtility.ApplyExponentialDamping(
                _grabberRigidbody.angularVelocity,
                _angularSwingDampingPerSecond,
                deltaTime);
        }

        public void CacheReferences()
        {
            _cableAnchor ??= transform;
            if (_cableAnchorRigidbody == null && _cableAnchor != null)
            {
                _cableAnchorRigidbody = _cableAnchor.GetComponent<Rigidbody>();
                if (_cableAnchorRigidbody == null)
                {
                    _cableAnchorRigidbody = _cableAnchor.gameObject.AddComponent<Rigidbody>();
                }
            }

            if (_cableAnchorRigidbody != null)
            {
                _cableAnchorRigidbody.isKinematic = true;
                _cableAnchorRigidbody.useGravity = false;
            }

            if (_grabberRigidbody == null)
            {
                CraneGrabber grabber = GetComponentInChildren<CraneGrabber>(includeInactive: true);
                if (grabber != null)
                {
                    _grabberRigidbody = grabber.GetComponent<Rigidbody>();
                }
            }

            if (_grabberRigidbody == null)
            {
                return;
            }

            if (_cableJoint == null)
            {
                _cableJoint = _grabberRigidbody.GetComponent<ConfigurableJoint>();
                if (_cableJoint == null)
                {
                    _cableJoint = _grabberRigidbody.gameObject.AddComponent<ConfigurableJoint>();
                }
            }

            ConfigureCableJoint();
            _cableRenderer ??= GetComponentInChildren<LineRenderer>(includeInactive: true);
        }

        private void ConfigureCableJoint()
        {
            if (_cableJoint == null)
            {
                return;
            }

            _cableJoint.connectedBody = _cableAnchorRigidbody;
            _cableJoint.autoConfigureConnectedAnchor = false;
            _cableJoint.anchor = Vector3.zero;
            _cableJoint.connectedAnchor = Vector3.zero;
            _cableJoint.xMotion = ConfigurableJointMotion.Limited;
            _cableJoint.yMotion = ConfigurableJointMotion.Limited;
            _cableJoint.zMotion = ConfigurableJointMotion.Limited;
            _cableJoint.angularXMotion = ConfigurableJointMotion.Free;
            _cableJoint.angularYMotion = ConfigurableJointMotion.Free;
            _cableJoint.angularZMotion = ConfigurableJointMotion.Free;
            _cableJoint.projectionMode = JointProjectionMode.PositionAndRotation;
            _cableJoint.projectionDistance = Mathf.Max(CraneCableUtility.MinimumCableLengthFloor, _cableConstraintTolerance);
            _cableJoint.enableCollision = false;
            SoftJointLimitSpring limitSpring = _cableJoint.linearLimitSpring;
            limitSpring.spring = _linearLimitSpring;
            limitSpring.damper = _linearLimitDamper;
            _cableJoint.linearLimitSpring = limitSpring;
        }

        private void EnforceCableLength()
        {
            if (_cableAnchor == null || _grabberRigidbody == null)
            {
                return;
            }

            if (!CraneCableUtility.TryProjectToCableLength(
                    _cableAnchor.position,
                    _grabberRigidbody.position,
                    _currentCableLength,
                    _cableConstraintTolerance,
                    out Vector3 projectedGrabberPosition,
                    out Vector3 cableDirection,
                    out float actualDistance))
            {
                return;
            }

            LogCableStretchDiagnostic("fixed-constraint", actualDistance);
            _grabberRigidbody.position = projectedGrabberPosition;
            float outwardVelocity = Vector3.Dot(_grabberRigidbody.linearVelocity, cableDirection);
            if (outwardVelocity > 0f)
            {
                _grabberRigidbody.linearVelocity -= cableDirection * outwardVelocity;
            }
        }

        private void LogHoistDiagnostic(float previousCableLength, float hoistInput, float deltaTime)
        {
            if (!_logCableDiagnostics
                || !Application.isPlaying
                || Mathf.Abs(hoistInput) <= 0.001f
                || Time.time < _nextHoistDiagnosticTime)
            {
                return;
            }

            _nextHoistDiagnosticTime = Time.time + Mathf.Max(0.1f, _cableDiagnosticIntervalSeconds);
            LogInfo(
                $"Cable hoist input applied. input={hoistInput:0.###}, dt={deltaTime:0.###}, length={previousCableLength:0.###}->{_currentCableLength:0.###}, min={_minimumCableLength:0.###}, max={_maximumCableLength:0.###}, anchor={DescribeTransform(_cableAnchor)}, grabber={DescribeRigidbody(_grabberRigidbody)}.");
        }

        private void LogCableStretchDiagnostic(string reason, float actualDistance)
        {
            if (!_logCableDiagnostics || !Application.isPlaying || Time.time < _nextCableDiagnosticTime)
            {
                return;
            }

            _nextCableDiagnosticTime = Time.time + Mathf.Max(0.1f, _cableDiagnosticIntervalSeconds);
            // LogWarning(
            //     $"Cable distance exceeded configured length. reason={reason}, configured={_currentCableLength:0.###}, actual={actualDistance:0.###}, excess={actualDistance - _currentCableLength:0.###}, tolerance={_cableConstraintTolerance:0.###}, jointLimit={(_cableJoint != null ? _cableJoint.linearLimit.limit : 0f):0.###}, spring={_linearLimitSpring:0.###}, damper={_linearLimitDamper:0.###}, anchor={DescribeTransform(_cableAnchor)}, grabber={DescribeRigidbody(_grabberRigidbody)}.");
        }

        private void LogCableSnapshot(string reason)
        {
            if (!_logCableDiagnostics || !Application.isPlaying)
            {
                return;
            }

            float actualDistance = _cableAnchor != null && _grabberRigidbody != null
                ? Vector3.Distance(_cableAnchor.position, _grabberRigidbody.position)
                : 0f;
            LogInfo(
                $"Cable snapshot [{reason}]. current={_currentCableLength:0.###}, default={_defaultCableLength:0.###}, min={_minimumCableLength:0.###}, max={_maximumCableLength:0.###}, actual={actualDistance:0.###}, captureCurrent={_captureDefaultLengthFromCurrentDistance}, capturedAsMinimum={_useCapturedLengthAsMinimum}, anchor={DescribeTransform(_cableAnchor)}, grabber={DescribeRigidbody(_grabberRigidbody)}.");
        }

        private static string DescribeTransform(Transform target)
        {
            return target != null ? $"{target.name}@{target.position}" : "None";
        }

        private static string DescribeRigidbody(Rigidbody target)
        {
            return target != null ? $"{target.name}@{target.position}, velocity={target.linearVelocity}" : "None";
        }

        private void UpdateCableRenderer()
        {
            if (_cableRenderer == null || _cableAnchor == null || _grabberRigidbody == null)
            {
                return;
            }

            _cableRenderer.positionCount = 2;
            _cableRenderer.useWorldSpace = true;
            _cableRenderer.SetPosition(0, _cableAnchor.position);
            _cableRenderer.SetPosition(1, _grabberRigidbody.position);
        }
    }
}
