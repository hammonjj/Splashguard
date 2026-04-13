#if UNITY_EDITOR
using System;
using BitBox.Library.Constants.Enums;
using BitBox.Toymageddon.Debugging;
using UnityEditor;
using UnityEngine;
using Bitbox.Toymageddon.Debugging.Editor;

namespace Bitbox.Toymageddon.Debugging.Editor.DebugLauncher
{
    public sealed class DebugLauncherWindow : EditorWindow
    {
        private const float LaunchButtonWidth = 160f;
        private const float LaunchButtonHeight = 32f;

        [MenuItem("Tools/Debug Controls/Debug Launcher")]
        public static void ShowWindow()
        {
            var window = GetWindow<DebugLauncherWindow>();
            window.titleContent = new GUIContent("Debug Launcher");
            window.minSize = new Vector2(320f, 190f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(12f);
            DrawSceneSelector();
            EditorGUILayout.Space(8f);
            bool hasValidInputSelection = DrawInputSelector();
            EditorGUILayout.Space(8f);
            DrawWeaponSelector();
            EditorGUILayout.Space(4f);
            DrawInfiniteAmmoToggle();
            GUILayout.FlexibleSpace();
            DrawLaunchButton(hasValidInputSelection);
            EditorGUILayout.Space(12f);
        }

        private static void DrawSceneSelector()
        {
            DebugControlsWindowState state = DebugControlsWindowState.instance;
            MacroSceneType[] supportedScenes = DebugLaunchService.SupportedSceneOptions;
            int currentIndex = Array.IndexOf(supportedScenes, state.DebugLauncherScene);
            Assert.IsTrue(currentIndex >= 0, $"Selected debug launcher scene '{state.DebugLauncherScene}' is not supported.");

            string[] optionLabels = Array.ConvertAll(supportedScenes, scene => scene.ToString());
            EditorGUI.BeginChangeCheck();
            int selectedIndex = EditorGUILayout.Popup("Scene", currentIndex, optionLabels);
            if (!EditorGUI.EndChangeCheck() || selectedIndex == currentIndex)
            {
                return;
            }

            state.DebugLauncherScene = supportedScenes[selectedIndex];
            state.SaveState();
        }

        private static bool DrawInputSelector()
        {
            DebugControlsWindowState state = DebugControlsWindowState.instance;
            var inputOptions = DebugLaunchService.GetAvailableInputOptions();
            string[] optionLabels = new string[inputOptions.Count + 1];
            optionLabels[0] = "<Select Input>";

            int selectedIndex = 0;
            for (int index = 0; index < inputOptions.Count; index++)
            {
                optionLabels[index + 1] = inputOptions[index].DisplayName;
                if (string.Equals(inputOptions[index].SelectionId, state.DebugLauncherInputSelectionId, StringComparison.Ordinal))
                {
                    selectedIndex = index + 1;
                }
            }

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup("Input", selectedIndex, optionLabels);
            if (EditorGUI.EndChangeCheck())
            {
                state.DebugLauncherInputSelectionId = newIndex == 0
                    ? string.Empty
                    : inputOptions[newIndex - 1].SelectionId;
                state.SaveState();
                selectedIndex = newIndex;
            }

            if (inputOptions.Count == 0)
            {
                EditorGUILayout.HelpBox("No supported launch inputs are currently available.", MessageType.Warning);
                return false;
            }

            if (selectedIndex == 0)
            {
                EditorGUILayout.HelpBox("Select the input device to use for the debug launch player.", MessageType.Info);
                return false;
            }

            return true;
        }

        private static void DrawWeaponSelector()
        {
            DebugWeaponType[] weaponTypes = (DebugWeaponType[])Enum.GetValues(typeof(DebugWeaponType));
            int currentIndex = Array.IndexOf(weaponTypes, DebugContext.RequestedWeaponType);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            string[] optionLabels = Array.ConvertAll(weaponTypes, GetWeaponTypeLabel);
            EditorGUI.BeginChangeCheck();
            int selectedIndex = EditorGUILayout.Popup("Weapon Type", currentIndex, optionLabels);
            if (!EditorGUI.EndChangeCheck() || selectedIndex == currentIndex)
            {
                return;
            }

            DebugContext.RequestedWeaponType = weaponTypes[selectedIndex];
        }

        private static void DrawInfiniteAmmoToggle()
        {
            EditorGUI.BeginChangeCheck();
            bool infiniteAmmo = EditorGUILayout.Toggle("Infinite Ammo", DebugContext.InfiniteAmmo);
            if (EditorGUI.EndChangeCheck())
            {
                DebugContext.InfiniteAmmo = infiniteAmmo;
            }
        }

        private static void DrawLaunchButton(bool hasValidInputSelection)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!DebugLaunchService.CanLaunch || !hasValidInputSelection))
                {
                    if (GUILayout.Button("Launch", GUILayout.Width(LaunchButtonWidth), GUILayout.Height(LaunchButtonHeight)))
                    {
                        DebugLaunchService.Launch(
                            DebugControlsWindowState.instance.DebugLauncherScene,
                            DebugControlsWindowState.instance.DebugLauncherInputSelectionId);
                        GUIUtility.ExitGUI();
                    }
                }

                GUILayout.FlexibleSpace();
            }
        }

        private static string GetWeaponTypeLabel(DebugWeaponType weaponType)
        {
            return weaponType switch
            {
                DebugWeaponType.GatlingGun => "Gatling Gun",
                _ => weaponType.ToString()
            };
        }
    }
}
#endif
