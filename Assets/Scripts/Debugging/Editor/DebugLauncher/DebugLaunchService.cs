#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using BitBox.Library.Constants;
using BitBox.Library.Constants.Enums;
using BitBox.Toymageddon.Debugging;
using BitBox.Toymageddon.SceneManagement.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bitbox.Toymageddon.Debugging.Editor.DebugLauncher
{
    internal static class DebugLaunchService
    {
        internal readonly struct DebugLaunchInputOption
        {
            public DebugLaunchInputOption(string selectionId, string displayName, string controlScheme, int deviceId)
            {
                SelectionId = selectionId;
                DisplayName = displayName;
                ControlScheme = controlScheme;
                DeviceId = deviceId;
            }

            public string SelectionId { get; }
            public string DisplayName { get; }
            public string ControlScheme { get; }
            public int DeviceId { get; }
        }

        private static readonly MacroSceneType[] SupportedScenes =
        {
            MacroSceneType.HubWorld,
            MacroSceneType.Sandbox
        };

        private static bool _awaitingPlayModeTransition;

        public static MacroSceneType[] SupportedSceneOptions => SupportedScenes;

        public static bool CanLaunch =>
            !EditorApplication.isCompiling
            && !EditorApplication.isPlaying
            && !EditorApplication.isPlayingOrWillChangePlaymode;

        public static IReadOnlyList<DebugLaunchInputOption> GetAvailableInputOptions()
        {
            var options = new List<DebugLaunchInputOption>();

            if (Keyboard.current != null && Mouse.current != null)
            {
                options.Add(new DebugLaunchInputOption(
                    BuildSelectionId(Strings.KeyboardControlScheme, Keyboard.current.deviceId),
                    "Keyboard & Mouse",
                    Strings.KeyboardControlScheme,
                    Keyboard.current.deviceId));
            }

            for (int index = 0; index < Gamepad.all.Count; index++)
            {
                Gamepad gamepad = Gamepad.all[index];
                if (gamepad == null)
                {
                    continue;
                }

                string displayName = string.IsNullOrWhiteSpace(gamepad.displayName)
                    ? $"Gamepad {index + 1}"
                    : $"Gamepad {index + 1}: {gamepad.displayName}";

                options.Add(new DebugLaunchInputOption(
                    BuildSelectionId(Strings.GamepadControlScheme, gamepad.deviceId),
                    displayName,
                    Strings.GamepadControlScheme,
                    gamepad.deviceId));
            }

            return options;
        }

        public static bool HasValidInputSelection(string selectionId)
        {
            return TryGetInputOption(selectionId, out _);
        }

        [InitializeOnLoadMethod]
        private static void RegisterEditorHooks()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        public static void Launch(MacroSceneType targetScene, string inputSelectionId)
        {
            Assert.IsTrue(IsSupportedScene(targetScene), $"Unsupported debug launch target '{targetScene}'.");
            bool foundInputOption = TryGetInputOption(inputSelectionId, out DebugLaunchInputOption inputOption);
            Assert.IsTrue(foundInputOption, $"Selected debug launch input '{inputSelectionId}' is not currently available.");

            if (!CanLaunch || !PrepareForLaunch())
            {
                return;
            }

            string bootstrapScenePath = GetBootstrapScenePath();
            EditorSceneManager.OpenScene(bootstrapScenePath, OpenSceneMode.Single);

            DebugContext.ArmDebugLaunchRequest(targetScene, inputOption.ControlScheme, inputOption.DeviceId);
            _awaitingPlayModeTransition = true;
            EditorApplication.isPlaying = true;
        }

        private static void OnEditorUpdate()
        {
            if (!_awaitingPlayModeTransition || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            _awaitingPlayModeTransition = false;
            DebugContext.ClearPendingDebugLaunchRequest();
        }

        private static bool PrepareForLaunch()
        {
            return PreparePrefabStageForLaunch()
                && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        private static bool PreparePrefabStageForLaunch()
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null)
            {
                return true;
            }

            if (prefabStage.scene.isDirty)
            {
                string prefabName = Path.GetFileNameWithoutExtension(prefabStage.assetPath);
                int choice = EditorUtility.DisplayDialogComplex(
                    "Unsaved Prefab Changes",
                    $"Save changes to prefab '{prefabName}' before launching?",
                    "Save",
                    "Cancel",
                    "Don't Save");

                switch (choice)
                {
                    case 0:
                        Assert.IsNotNull(prefabStage.prefabContentsRoot,
                            "Current prefab stage has no prefab contents root to save.");
                        PrefabUtility.SavePrefabAsset(prefabStage.prefabContentsRoot);
                        break;
                    case 1:
                        return false;
                }
            }

            StageUtility.GoToMainStage();
            return true;
        }

        private static string GetBootstrapScenePath()
        {
            var config = SceneManagementBootstrap.GetOrCreateConfig();
            Assert.IsNotNull(config, "SceneManagementConfig could not be loaded for debug launch.");

            string bootstrapScenePath = config.BootstrapScene.ScenePath;
            SceneAsset bootstrapScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(bootstrapScenePath);
            Assert.IsNotNull(bootstrapScene, $"Bootstrap scene asset was not found at '{bootstrapScenePath}'.");
            return bootstrapScenePath;
        }

        private static bool IsSupportedScene(MacroSceneType targetScene)
        {
            return targetScene == MacroSceneType.HubWorld || targetScene == MacroSceneType.Sandbox;
        }

        private static bool TryGetInputOption(string selectionId, out DebugLaunchInputOption inputOption)
        {
            IReadOnlyList<DebugLaunchInputOption> options = GetAvailableInputOptions();
            for (int index = 0; index < options.Count; index++)
            {
                if (!string.Equals(options[index].SelectionId, selectionId, StringComparison.Ordinal))
                {
                    continue;
                }

                inputOption = options[index];
                return true;
            }

            inputOption = default;
            return false;
        }

        private static string BuildSelectionId(string controlScheme, int deviceId)
        {
            return $"{controlScheme}|{deviceId}";
        }
    }
}
#endif
