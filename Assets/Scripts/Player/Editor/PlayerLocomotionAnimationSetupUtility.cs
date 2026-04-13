#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Bitbox.Player.Editor
{
    internal static class PlayerLocomotionAnimationSetupUtility
    {
        private const string MenuItemPath = "Tools/Player/Setup Locomotion Animation";
        private const string PlayerPrefabPath = "Assets/Prefabs/PlayerContainer.prefab";
        private const string ModelAssetPath = "Assets/Models/mixbot_low_v2-3-tpose.fbx";
        private const string IdleClipPath = "Assets/Animations/mixbot_low_v2-3@Idle.fbx";
        private const string WalkingClipPath = "Assets/Animations/mixbot_low_v2-3@Walking.fbx";
        private const string RunningClipPath = "Assets/Animations/mixbot_low_v2-3@Running.fbx";
        private const string JumpUpClipPath = "Assets/Animations/Jumping Up.fbx";
        private const string FallingIdleClipPath = "Assets/Animations/Falling Idle.fbx";
        private const string IdleClipName = "Idle";
        private const string WalkingClipName = "Walk";
        private const string RunningClipName = "Run";
        private const string ControllerAssetPath = "Assets/Animations/Controllers/PlayerLocomotion.controller";
        private const string LocomotionParameterName = "LocomotionSpeed";
        private const string JumpParameterName = "Jump";
        private const string IsGroundedParameterName = "IsGrounded";
        private const string VerticalVelocityParameterName = "VerticalVelocity";
        private const string ModelTransformName = "Model";

        [MenuItem(MenuItemPath)]
        private static void RunFromMenu()
        {
            RunSetup();
        }

        public static void RunFromCommandLine()
        {
            RunSetup();
        }

        private static void RunSetup()
        {
            Avatar avatar = LoadRequiredAvatar();
            ConfigureAnimationClipImport(IdleClipPath, true, true, avatar, false, false);
            ConfigureAnimationClipImport(WalkingClipPath, true, true, avatar, false, false);
            ConfigureAnimationClipImport(RunningClipPath, true, true, avatar, false, false);
            ConfigureAnimationClipImport(FallingIdleClipPath, true, false, avatar, true, true);
            ConfigureAnimationClipImport(JumpUpClipPath, false, false, avatar, true, true);

            AnimatorController controller = CreateOrUpdateController();
            UpdatePlayerPrefab(controller, avatar);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Configured player locomotion animation controller, clips, and prefab wiring.");
        }

        private static void ConfigureAnimationClipImport(
            string clipAssetPath,
            bool shouldLoop,
            bool copyAvatar,
            Avatar sourceAvatar,
            bool bakeVerticalRootMotionIntoPose,
            bool alignVerticalRootToFeet)
        {
            ModelImporter importer = AssetImporter.GetAtPath(clipAssetPath) as ModelImporter;
            Assert.IsNotNull(importer, $"Expected a {nameof(ModelImporter)} at '{clipAssetPath}'.");
            Assert.IsNotNull(sourceAvatar, $"Expected a source Avatar when configuring '{clipAssetPath}'.");

            bool dirty = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                dirty = true;
            }

            ModelImporterAvatarSetup avatarSetup = copyAvatar
                ? ModelImporterAvatarSetup.CopyFromOther
                : ModelImporterAvatarSetup.CreateFromThisModel;
            if (importer.avatarSetup != avatarSetup)
            {
                importer.avatarSetup = avatarSetup;
                dirty = true;
            }

            Avatar importerSourceAvatar = copyAvatar ? sourceAvatar : null;
            if (importer.sourceAvatar != importerSourceAvatar)
            {
                importer.sourceAvatar = importerSourceAvatar;
                dirty = true;
            }

            ModelImporterClipAnimation[] clipAnimations = importer.clipAnimations;
            if (clipAnimations == null || clipAnimations.Length == 0)
            {
                clipAnimations = importer.defaultClipAnimations;
            }

            Assert.IsTrue(clipAnimations != null && clipAnimations.Length > 0, $"No animation clips were found at '{clipAssetPath}'.");

            dirty |= importer.clipAnimations == null || importer.clipAnimations.Length == 0;
            for (int index = 0; index < clipAnimations.Length; index++)
            {
                if (clipAnimations[index].loopTime != shouldLoop)
                {
                    clipAnimations[index].loopTime = shouldLoop;
                    dirty = true;
                }

                if (clipAnimations[index].lockRootHeightY != bakeVerticalRootMotionIntoPose)
                {
                    clipAnimations[index].lockRootHeightY = bakeVerticalRootMotionIntoPose;
                    dirty = true;
                }

                bool keepOriginalPositionY = !bakeVerticalRootMotionIntoPose;
                if (clipAnimations[index].keepOriginalPositionY != keepOriginalPositionY)
                {
                    clipAnimations[index].keepOriginalPositionY = keepOriginalPositionY;
                    dirty = true;
                }

                if (clipAnimations[index].heightFromFeet != alignVerticalRootToFeet)
                {
                    clipAnimations[index].heightFromFeet = alignVerticalRootToFeet;
                    dirty = true;
                }

                if (!Mathf.Approximately(clipAnimations[index].heightOffset, 0f))
                {
                    clipAnimations[index].heightOffset = 0f;
                    dirty = true;
                }
            }

            if (!dirty)
            {
                return;
            }

            importer.clipAnimations = clipAnimations;
            importer.SaveAndReimport();
        }

        private static AnimatorController CreateOrUpdateController()
        {
            EnsureDirectoryExists(Path.GetDirectoryName(ControllerAssetPath));

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerAssetPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerAssetPath);
            }

            RebuildParameters(controller);
            RebuildBaseLayer(controller);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void RebuildParameters(AnimatorController controller)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters.ToArray())
            {
                controller.RemoveParameter(parameter);
            }

            controller.AddParameter(LocomotionParameterName, AnimatorControllerParameterType.Float);
            controller.AddParameter(JumpParameterName, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(IsGroundedParameterName, AnimatorControllerParameterType.Bool);
            controller.AddParameter(VerticalVelocityParameterName, AnimatorControllerParameterType.Float);
        }

        private static void RebuildBaseLayer(AnimatorController controller)
        {
            while (controller.layers.Length > 1)
            {
                controller.RemoveLayer(controller.layers.Length - 1);
            }

            if (controller.layers.Length == 0)
            {
                controller.AddLayer("Base Layer");
            }

            AnimatorControllerLayer baseLayer = controller.layers[0];
            baseLayer.name = "Base Layer";
            baseLayer.defaultWeight = 1f;
            baseLayer.iKPass = false;

            AnimatorStateMachine stateMachine = baseLayer.stateMachine;
            Assert.IsNotNull(stateMachine, "Animator controller base layer is missing a state machine.");

            foreach (ChildAnimatorState childState in stateMachine.states.ToArray())
            {
                stateMachine.RemoveState(childState.state);
            }

            foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines.ToArray())
            {
                stateMachine.RemoveStateMachine(childStateMachine.stateMachine);
            }

            foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions.ToArray())
            {
                stateMachine.RemoveAnyStateTransition(transition);
            }

            foreach (AnimatorTransition entryTransition in stateMachine.entryTransitions.ToArray())
            {
                stateMachine.RemoveEntryTransition(entryTransition);
            }

            controller.layers = new[] { baseLayer };

            controller.CreateBlendTreeInController("Locomotion", out BlendTree blendTree, 0);
            Assert.IsNotNull(blendTree, "Failed to create the locomotion blend tree.");

            blendTree.name = "Locomotion";
            blendTree.blendType = BlendTreeType.Simple1D;
            blendTree.blendParameter = LocomotionParameterName;
            blendTree.useAutomaticThresholds = false;
            blendTree.minThreshold = 0f;
            blendTree.maxThreshold = 1f;

            while (blendTree.children.Length > 0)
            {
                blendTree.RemoveChild(0);
            }

            blendTree.AddChild(LoadRequiredClip(IdleClipPath, IdleClipName), 0f);
            blendTree.AddChild(LoadRequiredClip(WalkingClipPath, WalkingClipName), 0.5f);
            blendTree.AddChild(LoadRequiredClip(RunningClipPath, RunningClipName), 1f);

            AnimatorState locomotionState = stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(state => state != null && state.name == "Locomotion");

            Assert.IsNotNull(locomotionState, "Failed to locate the locomotion Animator state after creating the blend tree.");
            locomotionState.writeDefaultValues = true;
            locomotionState.iKOnFeet = false;

            AnimatorState jumpUpState = CreateState(stateMachine, "JumpUp", LoadSingleClip(JumpUpClipPath), new Vector3(560f, 10f, 0f));
            AnimatorState fallingIdleState = CreateState(stateMachine, "FallingIdle", LoadSingleClip(FallingIdleClipPath), new Vector3(560f, 150f, 0f));

            CreateTransition(locomotionState, jumpUpState, false, 0f, 0.08f, transition =>
            {
                transition.AddCondition(AnimatorConditionMode.If, 0f, JumpParameterName);
            });
            CreateTransition(locomotionState, fallingIdleState, false, 0f, 0.08f, transition =>
            {
                transition.AddCondition(AnimatorConditionMode.IfNot, 0f, IsGroundedParameterName);
                transition.AddCondition(AnimatorConditionMode.Less, 0f, VerticalVelocityParameterName);
            });
            CreateTransition(jumpUpState, fallingIdleState, false, 0f, 0.05f, transition =>
            {
                transition.AddCondition(AnimatorConditionMode.IfNot, 0f, IsGroundedParameterName);
                transition.AddCondition(AnimatorConditionMode.Less, 0f, VerticalVelocityParameterName);
            });
            CreateTransition(jumpUpState, locomotionState, false, 0f, 0.08f, transition =>
            {
                transition.AddCondition(AnimatorConditionMode.If, 0f, IsGroundedParameterName);
            });
            CreateTransition(fallingIdleState, locomotionState, false, 0f, 0.08f, transition =>
            {
                transition.AddCondition(AnimatorConditionMode.If, 0f, IsGroundedParameterName);
            });

            stateMachine.defaultState = locomotionState;

            EditorUtility.SetDirty(blendTree);
            EditorUtility.SetDirty(stateMachine);
        }

        private static void UpdatePlayerPrefab(AnimatorController controller, Avatar avatar)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            try
            {
                Assert.IsNotNull(prefabRoot, $"Failed to load prefab contents for '{PlayerPrefabPath}'.");

                Transform modelTransform = prefabRoot.transform.Find(ModelTransformName);
                Assert.IsNotNull(modelTransform, $"Expected a child named '{ModelTransformName}' on '{PlayerPrefabPath}'.");

                Animator animator = modelTransform.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = modelTransform.gameObject.AddComponent<Animator>();
                }

                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                PlayerAnimator playerAnimator = prefabRoot.GetComponent<PlayerAnimator>();
                if (playerAnimator == null)
                {
                    playerAnimator = prefabRoot.AddComponent<PlayerAnimator>();
                }

                SerializedObject serializedPlayerAnimator = new SerializedObject(playerAnimator);
                serializedPlayerAnimator.FindProperty("_animator").objectReferenceValue = animator;
                serializedPlayerAnimator.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(animator);
                EditorUtility.SetDirty(playerAnimator);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static AnimationClip LoadRequiredClip(string clipAssetPath, string clipName)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(clipAssetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(asset => asset != null && asset.name == clipName);

            Assert.IsNotNull(clip, $"Animation clip '{clipName}' was not found at '{clipAssetPath}'.");
            return clip;
        }

        private static AnimationClip LoadSingleClip(string clipAssetPath)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(clipAssetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(asset => asset != null && !asset.name.StartsWith("__preview__", System.StringComparison.Ordinal));

            Assert.IsNotNull(clip, $"Animation clip was not found at '{clipAssetPath}'.");
            return clip;
        }

        private static Avatar LoadRequiredAvatar()
        {
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelAssetPath).OfType<Avatar>().FirstOrDefault();
            Assert.IsNotNull(avatar, $"Avatar asset was not found at '{ModelAssetPath}'.");
            return avatar;
        }

        private static void EnsureDirectoryExists(string directoryPath)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(directoryPath), "Animator controller directory path must be provided.");
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        private static AnimatorState CreateState(AnimatorStateMachine stateMachine, string stateName, Motion motion, Vector3 position)
        {
            AnimatorState state = stateMachine.AddState(stateName, position);
            state.motion = motion;
            state.writeDefaultValues = true;
            state.iKOnFeet = false;
            return state;
        }

        private static void CreateTransition(AnimatorState fromState, AnimatorState toState, bool hasExitTime, float exitTime, float duration, System.Action<AnimatorStateTransition> configure)
        {
            AnimatorStateTransition transition = fromState.AddTransition(toState);
            transition.hasExitTime = hasExitTime;
            transition.exitTime = exitTime;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            transition.offset = 0f;
            transition.interruptionSource = TransitionInterruptionSource.None;
            transition.orderedInterruption = true;
            transition.canTransitionToSelf = false;
            configure?.Invoke(transition);
        }
    }
}
#endif
