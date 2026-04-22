using BitBox.Library;
using BitBox.Library.CameraUtils;
using BitBox.Library.Constants;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bitbox.Splashguard.Nautical.Crane
{
    [DisallowMultipleComponent]
    public sealed class CraneControlRig : MonoBehaviourBase
    {
        [Header("References")]
        [SerializeField] private CraneBoomController _boomController;
        [SerializeField] private CraneCableController _cableController;
        [SerializeField] private CraneGrabber _grabber;
        [SerializeField] private CameraTargetAnchors _cameraAnchors;

        [Header("Return To Rest")]
        [SerializeField, Min(0.01f)] private float _returnDuration = 1.25f;
        [SerializeField, Range(0f, 1f)] private float _returnDamping = 0.85f;

        [Header("Input")]
        [SerializeField, Range(0f, 1f)] private float _hoistInputDeadZone = 0.05f;

        private PlayerInput _controllingPlayerInput;
        private InputAction _moveAction;
        private InputAction _hoistAction;
        private InputAction _suctionAction;
        private float _returnElapsed;
        private bool _isReturningToRest;

        public bool HasControl => _controllingPlayerInput != null;
        public bool IsReturningToRest => _isReturningToRest;
        public CraneGrabber Grabber => _grabber;
        public CameraTargetAnchors CameraAnchors => _cameraAnchors;
        public Transform CameraLookAtTarget => _grabber != null ? _grabber.transform : _cameraAnchors != null ? _cameraAnchors.LookAtTarget : null;

        public void ConfigureReferences(
            CraneBoomController boomController,
            CraneCableController cableController,
            CraneGrabber grabber)
        {
            _boomController = boomController;
            _cableController = cableController;
            _grabber = grabber;
            _boomController?.CaptureRestPose();
            _cableController?.CaptureRestLength();
        }

        public void ConfigureCameraAnchors(CameraTargetAnchors cameraAnchors)
        {
            _cameraAnchors = cameraAnchors;
        }

        protected override void OnEnabled()
        {
            CacheReferences();
            _boomController?.CaptureRestPose();
            _cableController?.CaptureRestLength();
        }

        protected override void OnUpdated()
        {
            if (HasControl)
            {
                TickControlled(Time.deltaTime);
                return;
            }

            if (_isReturningToRest)
            {
                TickReturnToRest(Time.deltaTime);
            }
        }

        protected override void OnLateUpdated()
        {
            _cableController?.CacheReferences();
        }

        protected override void OnDisabled()
        {
            EndControl();
            _isReturningToRest = false;
            _grabber?.ReleaseHeldPickup();
        }

        protected override void OnDestroyed()
        {
            _grabber?.ReleaseHeldPickup();
        }

        private void OnValidate()
        {
            _returnDuration = Mathf.Max(0.01f, _returnDuration);
            _returnDamping = Mathf.Clamp01(_returnDamping);
            _hoistInputDeadZone = Mathf.Clamp01(_hoistInputDeadZone);
        }

        public void BeginControl(PlayerInput playerInput)
        {
            if (playerInput == null)
            {
                return;
            }

            CacheReferences();
            InputActionMap craneControlsMap = playerInput.actions.FindActionMap(Strings.CraneControls, throwIfNotFound: false);
            if (craneControlsMap == null)
            {
                LogWarning($"Cannot begin crane control because action map '{Strings.CraneControls}' was not found. rig={name}");
                return;
            }

            _moveAction = craneControlsMap.FindAction(Strings.MoveAction, throwIfNotFound: true);
            _hoistAction = craneControlsMap.FindAction(Strings.HoistAction, throwIfNotFound: true);
            _suctionAction = craneControlsMap.FindAction(Strings.SuctionAction, throwIfNotFound: true);
            _controllingPlayerInput = playerInput;
            _isReturningToRest = false;
            _returnElapsed = 0f;
        }

        public void EndControl()
        {
            if (!HasControl && !_isReturningToRest)
            {
                _grabber?.ReleaseHeldPickup();
                return;
            }

            _grabber?.ReleaseHeldPickup();
            _controllingPlayerInput = null;
            _moveAction = null;
            _hoistAction = null;
            _suctionAction = null;
            BeginReturnToRest();
        }

        public void BeginReturnToRest()
        {
            CacheReferences();
            _returnElapsed = 0f;
            _isReturningToRest = true;
            _boomController?.BeginReturnToRest();
            _cableController?.BeginReturnToRest();
        }

        private void TickControlled(float deltaTime)
        {
            Vector2 moveInput = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            float hoistInput = ResolveHoistInput(_hoistAction != null ? _hoistAction.ReadValue<float>() : 0f);
            bool suctionHeld = _suctionAction != null && _suctionAction.IsPressed();

            _boomController?.ApplyControlInput(moveInput, deltaTime);
            if (!Mathf.Approximately(hoistInput, 0f))
            {
                _cableController?.ApplyHoistInput(hoistInput, deltaTime);
            }

            _grabber?.SetSuctionHeld(suctionHeld);
        }

        private float ResolveHoistInput(float rawHoistInput)
        {
            return Mathf.Abs(rawHoistInput) >= _hoistInputDeadZone ? rawHoistInput : 0f;
        }

        private void TickReturnToRest(float deltaTime)
        {
            _returnElapsed += Mathf.Max(0f, deltaTime);
            float t = Mathf.Clamp01(_returnElapsed / _returnDuration);
            float smoothedT = t * t * (3f - 2f * t);

            _boomController?.EvaluateReturnToRest(smoothedT);
            _cableController?.EvaluateReturnToRest(smoothedT);
            _cableController?.DampGrabberVelocity(_returnDamping);

            if (t >= 1f)
            {
                _isReturningToRest = false;
            }
        }

        private void CacheReferences()
        {
            _boomController ??= GetComponentInChildren<CraneBoomController>(includeInactive: true);
            _cableController ??= GetComponentInChildren<CraneCableController>(includeInactive: true);
            _grabber ??= GetComponentInChildren<CraneGrabber>(includeInactive: true);
            _cameraAnchors ??= GetComponentInChildren<CameraTargetAnchors>(includeInactive: true);
        }
    }
}
