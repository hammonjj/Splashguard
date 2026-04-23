using System.Collections;
using BitBox.Library;
using BitBox.Library.Constants;
using BitBox.Library.Constants.Enums;
using BitBox.Library.Eventing.GlobalEvents;
using BitBox.Library.Input;
using BitBox.Toymageddon.Settings;
using Bitbox;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bitbox.Toymageddon.Audio
{
    public class BackgroundMusicManager : MonoBehaviourBase
    {
        private const float DefaultLowPassResonanceQ = 1f;

        [SerializeField, InlineEditor] private BackgroundMusicData musicData;
        [SerializeField, ReadOnly] private AudioSource _primarySource;
        [SerializeField, ReadOnly] private AudioSource _secondarySource;
        [SerializeField, ReadOnly] private AudioLowPassFilter _musicLowPassFilter;

        [Header("Volume Settings")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float titleMenuVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float hubVolume = 1f;

        [Header("Transition Settings")]
        [SerializeField, Min(0.01f)] private float transitionFadeDurationSeconds = 0.75f;

        private int _lastIndex = -1;
        private AudioSource _activeSource;
        private AudioSource _inactiveSource;
        private Coroutine _transitionCoroutine;
        private float _lastAppliedLowPassCutoffHz = -1f;
        private AudioListener _fallbackAudioListener;
        private AudioListener _activeManagedListener;
        private MacroSceneType _currentMacroScene = MacroSceneType.None;

        protected override void OnEnabled()
        {
            EnsureAudioSources();
            EnsureMusicLowPassFilter();
            EnsureFallbackAudioListener();

            _globalMessageBus.Subscribe<MacroSceneLoadedEvent>(OnMacroSceneLoaded);
            RefreshAudioListenerOwnership();
        }

        protected override void OnUpdated()
        {
            if (_currentMacroScene.IsGameplayScene()
                && (!IsManagedListenerActive() || _activeManagedListener == _fallbackAudioListener))
            {
                RefreshAudioListenerOwnership();
            }

            float targetCutoff = GameSettingsService.Instance != null
                ? GameSettingsService.Instance.CurrentPauseMusicLowPassCutoffHz
                : GameSettingsService.UnfilteredMusicCutoffHz;

            if (Mathf.Approximately(_lastAppliedLowPassCutoffHz, targetCutoff))
            {
                return;
            }

            ApplyMusicLowPassCutoff(targetCutoff);
        }

        protected override void OnDisabled()
        {
            _globalMessageBus.Unsubscribe<MacroSceneLoadedEvent>(OnMacroSceneLoaded);

            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = null;
            }

            _activeManagedListener = null;
        }

        private void OnMacroSceneLoaded(MacroSceneLoadedEvent @event)
        {
            _currentMacroScene = @event.SceneType;

            switch (@event.SceneType)
            {
                case MacroSceneType.TitleMenu:
                    PlayRandom(musicData.titleMenuTracks, "TitleMenu", titleMenuVolume);
                    break;

                case MacroSceneType.CharacterSelection:
                case MacroSceneType.HubWorld:
                case MacroSceneType.Sandbox:
                case MacroSceneType.CombatArena:
                    PlayRandom(musicData.hubTracks, @event.SceneType.ToString(), hubVolume);
                    break;
            }

            RefreshAudioListenerOwnership();
        }

        private void PlayRandom(AudioClip[] tracks, string context, float contextVolume)
        {
            if (tracks == null || tracks.Length == 0)
            {
                return;
            }

            int index;
            do
            {
                index = Random.Range(0, tracks.Length);
            } while (tracks.Length > 1 && index == _lastIndex);

            _lastIndex = index;
            AudioClip nextClip = tracks[index];
            if (nextClip == null)
            {
                return;
            }

            float finalVolume = Mathf.Clamp01(masterVolume * contextVolume);
            if (IsClipActive(nextClip))
            {
                RetargetCurrentTrack(nextClip, finalVolume);
                return;
            }

            LogInfo($"[{context}] Crossfading to track '{nextClip.name}'.");
            CrossfadeTo(nextClip, finalVolume);
        }

        private void EnsureAudioSources()
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            Assert.IsTrue(sources.Length > 0, $"{nameof(BackgroundMusicManager)} requires at least one {nameof(AudioSource)} component.");

            _primarySource = sources[0];
            ConfigureSource(_primarySource);

            if (sources.Length > 1)
            {
                _secondarySource = sources[1];
            }
            else
            {
                _secondarySource = gameObject.AddComponent<AudioSource>();
            }

            CopySourceSettings(_primarySource, _secondarySource);
            ConfigureSource(_secondarySource);

            _activeSource ??= _primarySource;
            if (_inactiveSource == null || _inactiveSource == _activeSource)
            {
                _inactiveSource = _activeSource == _primarySource ? _secondarySource : _primarySource;
            }
        }

        private void EnsureMusicLowPassFilter()
        {
            _musicLowPassFilter = GetComponent<AudioLowPassFilter>();
            if (_musicLowPassFilter == null)
            {
                _musicLowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();
            }

            _musicLowPassFilter.enabled = true;
            _musicLowPassFilter.lowpassResonanceQ = DefaultLowPassResonanceQ;
            ApplyMusicLowPassCutoff(GameSettingsService.UnfilteredMusicCutoffHz);
        }

        private void EnsureFallbackAudioListener()
        {
            _fallbackAudioListener = GetComponent<AudioListener>();
            if (_fallbackAudioListener == null)
            {
                _fallbackAudioListener = gameObject.AddComponent<AudioListener>();
            }
        }

        private void RefreshAudioListenerOwnership()
        {
            EnsureFallbackAudioListener();

            AudioListener preferredListener = _currentMacroScene.IsGameplayScene()
                ? ResolveGameplayAudioListener()
                : ResolveNonGameplayAudioListener();

            SetExclusiveAudioListener(preferredListener != null ? preferredListener : _fallbackAudioListener);
        }

        private AudioListener ResolveGameplayAudioListener()
        {
            PlayerCoordinator coordinator = StaticData.PlayerInputCoordinator;
            if (coordinator == null)
            {
                return null;
            }

            PlayerInput primaryPlayerInput = null;
            foreach (PlayerInput playerInput in coordinator.PlayerInputs)
            {
                if (playerInput == null)
                {
                    continue;
                }

                if (primaryPlayerInput == null || playerInput.playerIndex < primaryPlayerInput.playerIndex)
                {
                    primaryPlayerInput = playerInput;
                }
            }

            if (primaryPlayerInput == null)
            {
                return null;
            }

            Camera gameplayCamera = ResolveGameplayCamera(primaryPlayerInput);
            if (gameplayCamera == null)
            {
                return null;
            }

            AudioListener listener = gameplayCamera.GetComponent<AudioListener>();
            if (listener == null)
            {
                listener = gameplayCamera.gameObject.AddComponent<AudioListener>();
            }

            return listener;
        }

        private AudioListener ResolveNonGameplayAudioListener()
        {
            AudioListener[] listeners = FindAllAudioListeners();
            for (int i = 0; i < listeners.Length; i++)
            {
                AudioListener listener = listeners[i];
                if (listener == null
                    || listener == _fallbackAudioListener
                    || !listener.gameObject.activeInHierarchy
                    || listener.GetComponentInParent<PlayerInput>() != null)
                {
                    continue;
                }

                return listener;
            }

            return _fallbackAudioListener;
        }

        private void SetExclusiveAudioListener(AudioListener preferredListener)
        {
            AudioListener[] listeners = FindAllAudioListeners();
            for (int i = 0; i < listeners.Length; i++)
            {
                AudioListener listener = listeners[i];
                if (listener == null)
                {
                    continue;
                }

                listener.enabled = listener == preferredListener;
            }

            _activeManagedListener = preferredListener;
        }

        private static AudioListener[] FindAllAudioListeners()
        {
            return UnityEngine.Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }

        private static Camera ResolveGameplayCamera(PlayerInput playerInput)
        {
            if (playerInput == null)
            {
                return null;
            }

            PlayerDataReference dataReference = playerInput.GetComponent<PlayerDataReference>();
            if (dataReference != null && dataReference.GameplayCamera != null)
            {
                return dataReference.GameplayCamera;
            }

            if (playerInput.camera != null)
            {
                return playerInput.camera;
            }

            return playerInput.GetComponentInChildren<Camera>(true);
        }

        private bool IsManagedListenerActive()
        {
            return _activeManagedListener != null
                && _activeManagedListener.enabled
                && _activeManagedListener.gameObject.activeInHierarchy;
        }

        private void ApplyMusicLowPassCutoff(float cutoffHz)
        {
            if (_musicLowPassFilter == null)
            {
                return;
            }

            float clampedCutoff = Mathf.Clamp(cutoffHz, 10f, GameSettingsService.UnfilteredMusicCutoffHz);
            _musicLowPassFilter.cutoffFrequency = clampedCutoff;
            _lastAppliedLowPassCutoffHz = clampedCutoff;
        }

        private void ConfigureSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
        }

        private static void CopySourceSettings(AudioSource source, AudioSource destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            destination.outputAudioMixerGroup = source.outputAudioMixerGroup;
            destination.priority = source.priority;
            destination.panStereo = source.panStereo;
            destination.reverbZoneMix = source.reverbZoneMix;
        }

        private bool IsClipActive(AudioClip clip)
        {
            return clip != null
                && ((_activeSource != null && _activeSource.clip == clip)
                    || (_inactiveSource != null && _inactiveSource.clip == clip));
        }

        private void RetargetCurrentTrack(AudioClip clip, float targetVolume)
        {
            if (_activeSource != null && _activeSource.clip == clip)
            {
                _activeSource.volume = targetVolume;
                return;
            }

            if (_inactiveSource != null && _inactiveSource.clip == clip)
            {
                _inactiveSource.volume = targetVolume;
            }
        }

        private void CrossfadeTo(AudioClip nextClip, float targetVolume)
        {
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
            }

            _transitionCoroutine = StartCoroutine(CrossfadeRoutine(nextClip, targetVolume));
        }

        private IEnumerator CrossfadeRoutine(AudioClip nextClip, float targetVolume)
        {
            _inactiveSource.clip = nextClip;
            _inactiveSource.volume = 0f;
            _inactiveSource.Play();

            float elapsed = 0f;
            float startingActiveVolume = _activeSource != null ? _activeSource.volume : 0f;

            while (elapsed < transitionFadeDurationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = transitionFadeDurationSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / transitionFadeDurationSeconds);

                if (_activeSource != null)
                {
                    _activeSource.volume = Mathf.Lerp(startingActiveVolume, 0f, t);
                }

                _inactiveSource.volume = Mathf.Lerp(0f, targetVolume, t);
                yield return null;
            }

            if (_activeSource != null)
            {
                _activeSource.Stop();
                _activeSource.clip = null;
                _activeSource.volume = 0f;
            }

            (_activeSource, _inactiveSource) = (_inactiveSource, _activeSource);
            _transitionCoroutine = null;
        }
    }
}
