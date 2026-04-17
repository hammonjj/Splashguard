using System;
using BitBox.Library;
using BitBox.Library.Constants;
using BitBox.Library.Constants.Enums;
using BitBox.Library.Localization;
using BitBox.Toymageddon.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BitBox.Toymageddon.Debugging
{
    public enum DebugEnemyMode
    {
        Normal = 0,
        Passive = 1,
        Frozen = 2
    }

    public static class DebugContext
    {
        private const string DebugStartModeKey = "Debug.StartMode";
        private const string DebugPlayerInvincibleKey = "Debug.PlayerInvincible";
        private const string DebugInfiniteAmmoKey = "Debug.InfiniteAmmo";
        private const string DebugEnemyModeKey = "Debug.EnemyMode";
        private const string DebugRequestedWeaponTypeKey = "Debug.RequestedWeaponType";
        private const string DebugLaunchTargetKey = "Debug.LaunchTarget";
        private const string DebugLaunchInputControlSchemeKey = "Debug.LaunchInputControlScheme";
        private const string DebugLaunchInputDeviceIdKey = "Debug.LaunchInputDeviceId";
        private const int InvalidDeviceId = -1;

        private static StartUpMode _requestedStartMode = StartUpMode.TitleMenu;
        private static bool _playerInvincible;
        private static bool _infiniteAmmo;
        private static DebugEnemyMode _enemyMode = DebugEnemyMode.Normal;
        private static DebugWeaponType _requestedWeaponType = DebugWeaponType.GatlingGun;
        private static string _requestedLanguageId = LocalizationTable.EnglishLanguageId;
        private static MacroSceneType _pendingDebugLaunchTarget = MacroSceneType.None;
        private static MacroSceneType _activeDebugLaunchTarget = MacroSceneType.None;
        private static string _pendingDebugLaunchInputControlScheme = string.Empty;
        private static int _pendingDebugLaunchInputDeviceId = InvalidDeviceId;

        public static StartUpMode RequestedStartMode
        {
            get
            {
#if UNITY_EDITOR
                if (EditorPrefs.HasKey(DebugStartModeKey))
                {
                    string saved = EditorPrefs.GetString(DebugStartModeKey);
                    if (System.Enum.TryParse(saved, out StartUpMode parsed))
                    {
                        _requestedStartMode = parsed;
                    }
                }
#endif
                return _requestedStartMode;
            }
            set
            {
                _requestedStartMode = value;
#if UNITY_EDITOR
                EditorPrefs.SetString(DebugStartModeKey, value.ToString());
#endif
            }
        }

        public static bool PlayerInvincible
        {
            get
            {
#if UNITY_EDITOR
                if (EditorPrefs.HasKey(DebugPlayerInvincibleKey))
                {
                    _playerInvincible = EditorPrefs.GetBool(DebugPlayerInvincibleKey);
                }
#endif
                return _playerInvincible;
            }
            set
            {
                _playerInvincible = value;
#if UNITY_EDITOR
                EditorPrefs.SetBool(DebugPlayerInvincibleKey, value);
#endif
            }
        }

        public static bool InfiniteAmmo
        {
            get
            {
#if UNITY_EDITOR
                if (EditorPrefs.HasKey(DebugInfiniteAmmoKey))
                {
                    _infiniteAmmo = EditorPrefs.GetBool(DebugInfiniteAmmoKey);
                }
#endif
                return _infiniteAmmo;
            }
            set
            {
                _infiniteAmmo = value;
#if UNITY_EDITOR
                EditorPrefs.SetBool(DebugInfiniteAmmoKey, value);
#endif
            }
        }

        public static DebugEnemyMode EnemyMode
        {
            get
            {
#if UNITY_EDITOR
                if (EditorPrefs.HasKey(DebugEnemyModeKey))
                {
                    string saved = EditorPrefs.GetString(DebugEnemyModeKey);
                    if (System.Enum.TryParse(saved, out DebugEnemyMode parsed))
                    {
                        _enemyMode = parsed;
                    }
                }
#endif
                return _enemyMode;
            }
            set
            {
                _enemyMode = value;
#if UNITY_EDITOR
                EditorPrefs.SetString(DebugEnemyModeKey, value.ToString());
#endif
            }
        }

        public static bool EnemiesPassive =>
            EnemyMode == DebugEnemyMode.Passive || EnemyMode == DebugEnemyMode.Frozen;

        public static bool EnemiesFrozen =>
            EnemyMode == DebugEnemyMode.Frozen;

        public static DebugWeaponType RequestedWeaponType
        {
            get
            {
#if UNITY_EDITOR
                if (EditorPrefs.HasKey(DebugRequestedWeaponTypeKey))
                {
                    string saved = EditorPrefs.GetString(DebugRequestedWeaponTypeKey);
                    if (System.Enum.TryParse(saved, out DebugWeaponType parsed))
                    {
                        _requestedWeaponType = parsed;
                    }
                }
#endif
                return _requestedWeaponType;
            }
            set
            {
                _requestedWeaponType = value;
#if UNITY_EDITOR
                EditorPrefs.SetString(DebugRequestedWeaponTypeKey, value.ToString());
#endif
            }
        }

        public static string RequestedLanguageId
        {
            get
            {
                string savedLanguageId = PlayerPrefs.GetString(
                    LocalizationManager.SelectedLanguagePlayerPrefsKey,
                    LocalizationTable.EnglishLanguageId);

                _requestedLanguageId = string.IsNullOrWhiteSpace(savedLanguageId)
                    ? LocalizationTable.EnglishLanguageId
                    : savedLanguageId;

                return _requestedLanguageId;
            }
            set
            {
                _requestedLanguageId = string.IsNullOrWhiteSpace(value)
                    ? LocalizationTable.EnglishLanguageId
                    : value;

                PlayerPrefs.SetString(LocalizationManager.SelectedLanguagePlayerPrefsKey, _requestedLanguageId);
                PlayerPrefs.Save();
            }
        }

        public static bool HasRequestedLanguage =>
            PlayerPrefs.HasKey(LocalizationManager.SelectedLanguagePlayerPrefsKey);

        public static bool HasPendingDebugLaunchTarget =>
            PendingDebugLaunchTarget != MacroSceneType.None;

        public static MacroSceneType PendingDebugLaunchTarget
        {
            get
            {
#if UNITY_EDITOR
                if (EditorPrefs.HasKey(DebugLaunchTargetKey))
                {
                    string saved = EditorPrefs.GetString(DebugLaunchTargetKey);
                    bool parsed = System.Enum.TryParse(saved, out MacroSceneType parsedTarget);
                    Assert.IsTrue(parsed, $"Invalid pending debug launch target '{saved}'.");
                    _pendingDebugLaunchTarget = parsedTarget;
                }
                else
                {
                    _pendingDebugLaunchTarget = MacroSceneType.None;
                }
#endif
                return _pendingDebugLaunchTarget;
            }
        }

        public static bool HasActiveDebugLaunchTarget =>
            _activeDebugLaunchTarget != MacroSceneType.None;

        public static MacroSceneType ActiveDebugLaunchTarget => _activeDebugLaunchTarget;

#if UNITY_EDITOR
        public static void ArmDebugLaunchRequest(MacroSceneType targetScene, string controlScheme, int deviceId)
        {
            Assert.IsTrue(
                targetScene == MacroSceneType.HubWorld || targetScene == MacroSceneType.Sandbox,
                $"Unsupported debug launch target '{targetScene}'.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(controlScheme), "Debug launch control scheme is required.");
            Assert.IsTrue(deviceId != InvalidDeviceId, "Debug launch input device id is required.");

            _pendingDebugLaunchTarget = targetScene;
            _pendingDebugLaunchInputControlScheme = controlScheme;
            _pendingDebugLaunchInputDeviceId = deviceId;
            EditorPrefs.SetString(DebugLaunchTargetKey, targetScene.ToString());
            EditorPrefs.SetString(DebugLaunchInputControlSchemeKey, controlScheme);
            EditorPrefs.SetInt(DebugLaunchInputDeviceIdKey, deviceId);
        }
#endif

        public static void ClearPendingDebugLaunchRequest()
        {
            _pendingDebugLaunchTarget = MacroSceneType.None;
            _pendingDebugLaunchInputControlScheme = string.Empty;
            _pendingDebugLaunchInputDeviceId = InvalidDeviceId;

#if UNITY_EDITOR
            EditorPrefs.DeleteKey(DebugLaunchTargetKey);
            EditorPrefs.DeleteKey(DebugLaunchInputControlSchemeKey);
            EditorPrefs.DeleteKey(DebugLaunchInputDeviceIdKey);
#endif
        }

        public static void SetActiveDebugLaunchTarget(MacroSceneType targetScene)
        {
            Assert.IsTrue(targetScene != MacroSceneType.None, "Active debug launch target cannot be None.");
            _activeDebugLaunchTarget = targetScene;
        }

        public static void ClearActiveDebugLaunchTarget()
        {
            _activeDebugLaunchTarget = MacroSceneType.None;
        }

        public static PendingPlayerJoinRequest CreateKeyboardMousePendingJoinRequest(string sourceControlPath)
        {
            Assert.IsNotNull(Keyboard.current, "Debug launcher requires an available keyboard device.");
            Assert.IsNotNull(Mouse.current, "Debug launcher requires an available mouse device.");

            return CreatePendingJoinRequest(Strings.KeyboardControlScheme, Keyboard.current.deviceId, sourceControlPath);
        }

        public static PendingPlayerJoinRequest CreatePendingJoinRequest(
            string controlScheme,
            int deviceId,
            string sourceControlPath)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(controlScheme), "Pending join control scheme is required.");
            Assert.IsTrue(
                string.Equals(controlScheme, Strings.KeyboardControlScheme, StringComparison.Ordinal)
                || string.Equals(controlScheme, Strings.GamepadControlScheme, StringComparison.Ordinal),
                $"Unsupported pending join control scheme '{controlScheme}'.");
            Assert.IsTrue(deviceId != InvalidDeviceId, "Pending join input device id is required.");

            InputDevice pairingDevice = InputSystem.GetDeviceById(deviceId);
            Assert.IsNotNull(pairingDevice, $"Input device with id '{deviceId}' is not currently available.");

            if (string.Equals(controlScheme, Strings.KeyboardControlScheme, StringComparison.Ordinal))
            {
                Assert.IsTrue(pairingDevice is Keyboard, "Keyboard&Mouse launch input must pair with a keyboard.");
                Assert.IsNotNull(Mouse.current, "Keyboard&Mouse launch input requires an available mouse device.");
            }
            else
            {
                Assert.IsTrue(pairingDevice is Gamepad, "Gamepad launch input must pair with a gamepad.");
            }

            return new PendingPlayerJoinRequest
            {
                ControlScheme = controlScheme,
                PairWithDevice = pairingDevice,
                SourceControlPath = sourceControlPath
            };
        }

        public static void Clear()
        {
            _requestedStartMode = StartUpMode.TitleMenu;
            _playerInvincible = false;
            _infiniteAmmo = false;
            _enemyMode = DebugEnemyMode.Normal;
            _requestedWeaponType = DebugWeaponType.GatlingGun;
            _requestedLanguageId = LocalizationTable.EnglishLanguageId;
            ClearPendingDebugLaunchRequest();
            ClearActiveDebugLaunchTarget();

#if UNITY_EDITOR
            EditorPrefs.DeleteKey(DebugStartModeKey);
            EditorPrefs.DeleteKey(DebugPlayerInvincibleKey);
            EditorPrefs.DeleteKey(DebugInfiniteAmmoKey);
            EditorPrefs.DeleteKey(DebugEnemyModeKey);
            EditorPrefs.DeleteKey(DebugRequestedWeaponTypeKey);
#endif

            PlayerPrefs.DeleteKey(LocalizationManager.SelectedLanguagePlayerPrefsKey);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeDebugLaunchSession()
        {
#if UNITY_EDITOR
            MacroSceneType pendingTarget = PendingDebugLaunchTarget;
            if (pendingTarget == MacroSceneType.None)
            {
                ClearActiveDebugLaunchTarget();
                return;
            }

            string controlScheme = ReadPendingDebugLaunchInputControlScheme();
            int deviceId = ReadPendingDebugLaunchInputDeviceId();

            ClearPendingDebugLaunchRequest();
            SetActiveDebugLaunchTarget(pendingTarget);
            StaticData.PendingInitialJoinRequest = CreatePendingJoinRequest(controlScheme, deviceId, "DebugLauncher");
#else
            ClearActiveDebugLaunchTarget();
#endif
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void RegisterEditorHooks()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            ClearPendingDebugLaunchRequest();
            ClearActiveDebugLaunchTarget();
        }

        private static string ReadPendingDebugLaunchInputControlScheme()
        {
            Assert.IsTrue(
                EditorPrefs.HasKey(DebugLaunchInputControlSchemeKey),
                "Pending debug launch input control scheme was not armed.");

            string controlScheme = EditorPrefs.GetString(DebugLaunchInputControlSchemeKey);
            Assert.IsFalse(string.IsNullOrWhiteSpace(controlScheme), "Pending debug launch input control scheme is empty.");
            _pendingDebugLaunchInputControlScheme = controlScheme;
            return _pendingDebugLaunchInputControlScheme;
        }

        private static int ReadPendingDebugLaunchInputDeviceId()
        {
            Assert.IsTrue(
                EditorPrefs.HasKey(DebugLaunchInputDeviceIdKey),
                "Pending debug launch input device id was not armed.");

            _pendingDebugLaunchInputDeviceId = EditorPrefs.GetInt(DebugLaunchInputDeviceIdKey, InvalidDeviceId);
            Assert.IsTrue(
                _pendingDebugLaunchInputDeviceId != InvalidDeviceId,
                "Pending debug launch input device id is invalid.");
            return _pendingDebugLaunchInputDeviceId;
        }
#endif
    }
}
