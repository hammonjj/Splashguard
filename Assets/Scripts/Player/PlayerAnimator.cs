using System.Linq;
using BitBox.Library;
using BitBox.Library.Eventing;
using BitBox.Library.Eventing.PlayerEvents;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bitbox
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MessageBus))]
    [RequireComponent(typeof(PlayerDataReference))]
    public sealed class PlayerAnimator : MonoBehaviourBase
    {
        private const string LocomotionSpeedParameterName = "LocomotionSpeed";
        private const string JumpParameterName = "Jump";
        private const string IsGroundedParameterName = "IsGrounded";
        private const string VerticalVelocityParameterName = "VerticalVelocity";
        private const float LocomotionDampTime = 0.08f;
        private const float IdleLocomotionThreshold = 0.01f;
        private const float RunningLocomotionThreshold = 0.99f;
        private const float AnimatorSnapshotIntervalSeconds = 1f;

        [SerializeField, Required] private Animator _animator;

        private MessageBus _localMessageBus;
        private PlayerDataReference _playerDataReference;
        private PlayerInput _playerInput;
        private int _locomotionSpeedParameterHash;
        private int _jumpParameterHash;
        private int _isGroundedParameterHash;
        private int _verticalVelocityParameterHash;
        private int _receivedLocomotionEventCount;
        private float _nextAnimatorSnapshotTime;

        protected override void OnAwakened()
        {
            _locomotionSpeedParameterHash = Animator.StringToHash(LocomotionSpeedParameterName);
            _jumpParameterHash = Animator.StringToHash(JumpParameterName);
            _isGroundedParameterHash = Animator.StringToHash(IsGroundedParameterName);
            _verticalVelocityParameterHash = Animator.StringToHash(VerticalVelocityParameterName);
            CacheReferences();
            RebindAnimator();
            ResetAnimationState();
            LogInfo(
                $"Animator initialized. animator={_animator.name}, controller={_animator.runtimeAnimatorController.name}, avatar={_animator.avatar?.name ?? "None"}, isHuman={_animator.isHuman}, applyRootMotion={_animator.applyRootMotion}, controllerClips={DescribeControllerClips()}.");
        }

        protected override void OnEnabled()
        {
            CacheReferences();
            RebindAnimator();
            _localMessageBus.Subscribe<PlayerLocomotionAnimationEvent>(OnPlayerLocomotionAnimation);
            ResetAnimationState();
            LogInfo(
                $"Animator subscribed to local locomotion events. subscribers={_localMessageBus.GetSubscriberCount<PlayerLocomotionAnimationEvent>()}, currentClips={DescribeCurrentClips()}, controllerClips={DescribeControllerClips()}, currentStateHash={_animator.GetCurrentAnimatorStateInfo(0).shortNameHash}.");
        }

        protected override void OnDisabled()
        {
            if (_localMessageBus != null)
            {
                _localMessageBus.Unsubscribe<PlayerLocomotionAnimationEvent>(OnPlayerLocomotionAnimation);
            }

            ResetAnimationState();
        }

        protected override void OnUpdated()
        {
            if (!Application.isPlaying || Time.unscaledTime < _nextAnimatorSnapshotTime)
            {
                return;
            }

            _nextAnimatorSnapshotTime = Time.unscaledTime + AnimatorSnapshotIntervalSeconds;

            //AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            // LogInfo(
            //     $"Animator snapshot. initialized={_animator.isInitialized}, enabled={_animator.enabled}, controller={_animator.runtimeAnimatorController?.name ?? "None"}, locomotionParam={_animator.GetFloat(_locomotionSpeedParameterHash):F2}, inTransition={_animator.IsInTransition(0)}, currentStateHash={stateInfo.shortNameHash}, normalizedTime={stateInfo.normalizedTime:F2}, clips={DescribeCurrentClips()}, controllerClips={DescribeControllerClips()}, receivedLocomotionEvents={_receivedLocomotionEventCount}.");
        }

        private void CacheReferences()
        {
            _localMessageBus ??= GetComponent<MessageBus>();
            _playerDataReference ??= GetComponent<PlayerDataReference>();
            _playerInput ??= GetComponent<PlayerInput>();

            Assert.IsNotNull(_localMessageBus, $"{nameof(PlayerAnimator)} requires a local {nameof(MessageBus)}.");
            Assert.IsNotNull(_playerDataReference, $"{nameof(PlayerAnimator)} requires a {nameof(PlayerDataReference)}.");
            Assert.IsNotNull(_playerInput, $"{nameof(PlayerAnimator)} requires a {nameof(PlayerInput)}.");
            Assert.IsNotNull(_animator, $"{nameof(PlayerAnimator)} requires an {nameof(Animator)} reference.");
            Assert.IsNotNull(_animator.avatar, $"{nameof(PlayerAnimator)} requires an Avatar on the assigned Animator.");
            Assert.IsNotNull(_animator.runtimeAnimatorController, $"{nameof(PlayerAnimator)} requires an Animator Controller.");
            Assert.IsFalse(_animator.applyRootMotion, $"{nameof(PlayerAnimator)} requires root motion to remain disabled.");
            Assert.IsTrue(_animator.avatar.isValid, $"{nameof(PlayerAnimator)} requires a valid Animator Avatar.");
            Assert.AreEqual(
                _playerDataReference.VisualFacingTarget,
                _animator.transform,
                $"{nameof(PlayerAnimator)} requires the visual facing target to match the model Animator transform.");
            Assert.IsTrue(
                HasAnimatorParameter(_locomotionSpeedParameterHash, AnimatorControllerParameterType.Float),
                $"{nameof(PlayerAnimator)} requires a float Animator parameter named '{LocomotionSpeedParameterName}'.");
            Assert.IsTrue(
                HasAnimatorParameter(_jumpParameterHash, AnimatorControllerParameterType.Trigger),
                $"{nameof(PlayerAnimator)} requires a trigger Animator parameter named '{JumpParameterName}'.");
            Assert.IsTrue(
                HasAnimatorParameter(_isGroundedParameterHash, AnimatorControllerParameterType.Bool),
                $"{nameof(PlayerAnimator)} requires a bool Animator parameter named '{IsGroundedParameterName}'.");
            Assert.IsTrue(
                HasAnimatorParameter(_verticalVelocityParameterHash, AnimatorControllerParameterType.Float),
                $"{nameof(PlayerAnimator)} requires a float Animator parameter named '{VerticalVelocityParameterName}'.");
        }

        private void OnPlayerLocomotionAnimation(PlayerLocomotionAnimationEvent @event)
        {
            _receivedLocomotionEventCount++;
            var locomotionNormalized = Mathf.Clamp01(@event.LocomotionNormalized);
            if (locomotionNormalized <= IdleLocomotionThreshold)
            {
                locomotionNormalized = 0f;
            }
            else if (locomotionNormalized >= RunningLocomotionThreshold)
            {
                locomotionNormalized = 1f;
            }

            _animator.SetBool(_isGroundedParameterHash, @event.IsGrounded);
            _animator.SetFloat(_verticalVelocityParameterHash, @event.VerticalVelocity);
            if (@event.JumpStartedThisFrame)
            {
                _animator.SetTrigger(_jumpParameterHash);
            }

            if (GetLocomotionBucket(locomotionNormalized) is 0 or 2)
            {
                _animator.SetFloat(_locomotionSpeedParameterHash, locomotionNormalized);
                return;
            }

            _animator.SetFloat(_locomotionSpeedParameterHash, locomotionNormalized, LocomotionDampTime, Time.deltaTime);
        }

        private bool HasAnimatorParameter(int parameterHash, AnimatorControllerParameterType parameterType)
        {
            AnimatorControllerParameter[] parameters = _animator.parameters;
            for (int index = 0; index < parameters.Length; index++)
            {
                if (parameters[index].nameHash == parameterHash
                    && parameters[index].type == parameterType)
                {
                    return true;
                }
            }

            return false;
        }

        private void ResetAnimationState()
        {
            if (_animator == null)
            {
                return;
            }

            _animator.ResetTrigger(_jumpParameterHash);
            _animator.SetBool(_isGroundedParameterHash, true);
            _animator.SetFloat(_locomotionSpeedParameterHash, 0f);
            _animator.SetFloat(_verticalVelocityParameterHash, 0f);
        }

        private void RebindAnimator()
        {
            if (_animator == null)
            {
                return;
            }

            _animator.Rebind();
            _animator.Update(0f);
        }

        private Transform VisualFacingTarget => _playerDataReference.VisualFacingTarget;

        private string DescribeCurrentClips()
        {
            AnimatorClipInfo[] currentClips = _animator.GetCurrentAnimatorClipInfo(0);
            if (currentClips == null || currentClips.Length == 0)
            {
                return "[None]";
            }

            return $"[{string.Join(", ", currentClips.Select(clipInfo => clipInfo.clip != null ? clipInfo.clip.name : "null"))}]";
        }

        private string DescribeControllerClips()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null)
            {
                return "[None]";
            }

            AnimationClip[] controllerClips = _animator.runtimeAnimatorController.animationClips;
            if (controllerClips == null || controllerClips.Length == 0)
            {
                return "[None]";
            }

            return $"[{string.Join(", ", controllerClips.Select(clip => clip != null ? clip.name : "null"))}]";
        }

        private bool HasControllerClipNamed(string clipName)
        {
            AnimationClip[] controllerClips = _animator.runtimeAnimatorController.animationClips;
            return controllerClips != null && controllerClips.Any(clip => clip != null && clip.name == clipName);
        }

        private static int GetLocomotionBucket(float locomotionNormalized)
        {
            if (locomotionNormalized <= IdleLocomotionThreshold)
            {
                return 0;
            }

            return locomotionNormalized >= RunningLocomotionThreshold
                ? 2
                : 1;
        }
    }
}
