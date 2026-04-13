using System;
using BitBox.Library;
using BitBox.Library.Eventing.GlobalEvents;
using BitBox.Library.Localization;
using BitBox.Library.UI;
using BitBox.Toymageddon.Debugging;
using BitBox.Toymageddon.Localization;
using UnityEngine;
using UnityEngine.Audio;

namespace BitBox.Toymageddon.Settings
{
    public sealed class GameSettingsService : MonoBehaviourBase
    {
        private const string MasterVolumePlayerPrefsKey = "Toymageddon.Settings.Audio.Master";
        private const string MusicVolumePlayerPrefsKey = "Toymageddon.Settings.Audio.Music";
        private const string SfxVolumePlayerPrefsKey = "Toymageddon.Settings.Audio.Sfx";
        private const string InvertVerticalAimPlayerPrefsKey = "Toymageddon.Settings.Gameplay.InvertVerticalAim";
        private const float MixerSilentDb = -80f;
        public const float UnfilteredMusicCutoffHz = 22000f;
        internal const string UiScalePlayerPrefsKey = "Toymageddon.Settings.Ui.Scale";
        public const bool DefaultInvertVerticalAim = false;

        [SerializeField] private AudioMixer _audioMixer;
        [SerializeField] private string _masterVolumeParameter = "MasterVolume";
        [SerializeField] private string _musicVolumeParameter = "MusicVolume";
        [SerializeField] private string _sfxVolumeParameter = "SfxVolume";
        [SerializeField] private string _musicLowPassCutoffParameter = string.Empty;
        [SerializeField, Range(0f, 1f)] private float _defaultMasterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _defaultMusicVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _defaultSfxVolume = 1f;
        [SerializeField, Range(UiScaleSettings.MinimumScale, UiScaleSettings.MaximumScale)]
        private float _defaultUiScale = UiScaleSettings.DefaultScale;
        [SerializeField] private bool _defaultInvertVerticalAim = DefaultInvertVerticalAim;
        [SerializeField, Min(0.01f)] private float _pauseAudioTransitionDurationSeconds = 0.2f;
        [SerializeField, Range(0f, 1f)] private float _pauseMusicVolumeMultiplier = 0.42f;
        [SerializeField, Range(120f, UnfilteredMusicCutoffHz)] private float _pauseMusicLowPassCutoffHz = 1200f;

        private GameSettingsSnapshot _currentSettings;
        private float _pauseAudioBlendCurrent;
        private float _pauseAudioBlendTarget;

        public static GameSettingsService Instance { get; private set; }

        public event Action<GameSettingsSnapshot> SettingsChanged;

        public GameSettingsSnapshot CurrentSettings => _currentSettings;
        public float CurrentPauseMusicLowPassCutoffHz => Mathf.Lerp(
            UnfilteredMusicCutoffHz,
            Mathf.Clamp(_pauseMusicLowPassCutoffHz, 120f, UnfilteredMusicCutoffHz),
            _pauseAudioBlendCurrent);

        protected override void OnAwakened()
        {
            if (Instance != null && Instance != this)
            {
                LogWarning("Duplicate GameSettingsService detected. The new instance was disabled.");
                enabled = false;
                return;
            }

            Instance = this;
            UiScaleRuntime.SetScaleResolver(() => UiScaleSettings.ResolveCurrentScale());
            ResolveAudioMixerFromSource();
            _currentSettings = LoadCurrentSettings();
            _pauseAudioBlendCurrent = 0f;
            _pauseAudioBlendTarget = 0f;
            ApplyResolvedAudioMixerSettings();
        }

        protected override void OnEnabled()
        {
            GameText.LanguageChanged += OnLanguageChanged;
            _globalMessageBus.Subscribe<PauseGameEvent>(OnPauseGame);
            NotifySettingsChanged();
        }

        protected override void OnUpdated()
        {
            UpdatePauseAudioTransition();
        }

        protected override void OnDisabled()
        {
            GameText.LanguageChanged -= OnLanguageChanged;
            _globalMessageBus?.Unsubscribe<PauseGameEvent>(OnPauseGame);
            _pauseAudioBlendCurrent = 0f;
            _pauseAudioBlendTarget = 0f;
            ApplyResolvedAudioMixerSettings();
        }

        protected override void OnDestroyed()
        {
            if (Instance == this)
            {
                Instance = null;
                UiScaleRuntime.SetScaleResolver(null);
            }
        }

        public void SetMasterVolume(float normalizedVolume)
        {
            float clampedVolume = Mathf.Clamp01(normalizedVolume);
            if (Mathf.Approximately(_currentSettings.MasterVolume01, clampedVolume))
            {
                return;
            }

            _currentSettings = _currentSettings.WithAudio(masterVolume01: clampedVolume);
            PlayerPrefs.SetFloat(MasterVolumePlayerPrefsKey, clampedVolume);
            PlayerPrefs.Save();
            ApplyResolvedAudioMixerSettings();
            NotifySettingsChanged();
        }

        public void SetMusicVolume(float normalizedVolume)
        {
            float clampedVolume = Mathf.Clamp01(normalizedVolume);
            if (Mathf.Approximately(_currentSettings.MusicVolume01, clampedVolume))
            {
                return;
            }

            _currentSettings = _currentSettings.WithAudio(musicVolume01: clampedVolume);
            PlayerPrefs.SetFloat(MusicVolumePlayerPrefsKey, clampedVolume);
            PlayerPrefs.Save();
            ApplyResolvedAudioMixerSettings();
            NotifySettingsChanged();
        }

        public void SetSfxVolume(float normalizedVolume)
        {
            float clampedVolume = Mathf.Clamp01(normalizedVolume);
            if (Mathf.Approximately(_currentSettings.SfxVolume01, clampedVolume))
            {
                return;
            }

            _currentSettings = _currentSettings.WithAudio(sfxVolume01: clampedVolume);
            PlayerPrefs.SetFloat(SfxVolumePlayerPrefsKey, clampedVolume);
            PlayerPrefs.Save();
            ApplyResolvedAudioMixerSettings();
            NotifySettingsChanged();
        }

        public void SetLanguage(string languageId)
        {
            string normalizedLanguageId = NormalizeLanguageId(languageId);
            if (string.Equals(_currentSettings.LanguageId, normalizedLanguageId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (LocalizationManager.Instance != null)
            {
                LocalizationManager.SetCurrentLanguage(normalizedLanguageId);
                return;
            }

            DebugContext.RequestedLanguageId = normalizedLanguageId;
            GameText.SetLanguage(normalizedLanguageId);
        }

        public void SetUiScale(float uiScale)
        {
            float clampedUiScale = UiScaleSettings.Clamp(uiScale);
            if (Mathf.Approximately(_currentSettings.UiScale, clampedUiScale))
            {
                return;
            }

            _currentSettings = _currentSettings.WithUiScale(clampedUiScale);
            PlayerPrefs.SetFloat(UiScalePlayerPrefsKey, clampedUiScale);
            PlayerPrefs.Save();
            NotifySettingsChanged();
        }

        public void SetInvertVerticalAim(bool invertVerticalAim)
        {
            if (_currentSettings.InvertVerticalAim == invertVerticalAim)
            {
                return;
            }

            _currentSettings = _currentSettings.WithGameplay(invertVerticalAim);
            PlayerPrefs.SetInt(InvertVerticalAimPlayerPrefsKey, invertVerticalAim ? 1 : 0);
            PlayerPrefs.Save();
            NotifySettingsChanged();
        }

        private void OnLanguageChanged(string languageId)
        {
            string normalizedLanguageId = NormalizeLanguageId(languageId);
            if (string.Equals(_currentSettings.LanguageId, normalizedLanguageId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _currentSettings = _currentSettings.WithLanguage(normalizedLanguageId);
            NotifySettingsChanged();
        }

        private GameSettingsSnapshot LoadCurrentSettings()
        {
            float masterVolume = PlayerPrefs.GetFloat(MasterVolumePlayerPrefsKey, _defaultMasterVolume);
            float musicVolume = PlayerPrefs.GetFloat(MusicVolumePlayerPrefsKey, _defaultMusicVolume);
            float sfxVolume = PlayerPrefs.GetFloat(SfxVolumePlayerPrefsKey, _defaultSfxVolume);
            float uiScale = PlayerPrefs.GetFloat(UiScalePlayerPrefsKey, _defaultUiScale);
            bool invertVerticalAim = PlayerPrefs.GetInt(
                InvertVerticalAimPlayerPrefsKey,
                _defaultInvertVerticalAim ? 1 : 0) != 0;
            string languageId = ResolveCurrentLanguageId();

            return new GameSettingsSnapshot(
                Mathf.Clamp01(masterVolume),
                Mathf.Clamp01(musicVolume),
                Mathf.Clamp01(sfxVolume),
                languageId,
                UiScaleSettings.Clamp(uiScale),
                invertVerticalAim);
        }

        private void OnPauseGame(PauseGameEvent @event)
        {
            _pauseAudioBlendTarget = @event.IsPaused ? 1f : 0f;

            if (_pauseAudioTransitionDurationSeconds <= 0f)
            {
                _pauseAudioBlendCurrent = _pauseAudioBlendTarget;
                ApplyResolvedAudioMixerSettings();
            }
        }

        private void UpdatePauseAudioTransition()
        {
            if (Mathf.Approximately(_pauseAudioBlendCurrent, _pauseAudioBlendTarget))
            {
                return;
            }

            float transitionDuration = Mathf.Max(0.01f, _pauseAudioTransitionDurationSeconds);
            float step = Time.unscaledDeltaTime / transitionDuration;
            _pauseAudioBlendCurrent = Mathf.MoveTowards(_pauseAudioBlendCurrent, _pauseAudioBlendTarget, step);
            ApplyResolvedAudioMixerSettings();
        }

        private void ApplyResolvedAudioMixerSettings()
        {
            if (_audioMixer == null)
            {
                ResolveAudioMixerFromSource();
            }

            if (_audioMixer == null)
            {
                LogWarning("GameSettingsService could not resolve an AudioMixer. Audio settings will persist but will not apply live.");
                return;
            }

            ApplyMixerParameter(_masterVolumeParameter, _currentSettings.MasterVolume01);
            ApplyMixerParameter(_musicVolumeParameter, ResolveEffectiveMusicVolume01());
            ApplyMixerParameter(_sfxVolumeParameter, _currentSettings.SfxVolume01);
        }

        private void ApplyMixerParameter(string parameterName, float normalizedVolume)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            float mixerVolume = normalizedVolume <= 0f
                ? MixerSilentDb
                : Mathf.Log10(Mathf.Clamp(normalizedVolume, 0.0001f, 1f)) * 20f;

            _audioMixer.SetFloat(parameterName, mixerVolume);
        }

        private float ResolveEffectiveMusicVolume01()
        {
            float pauseVolumeMultiplier = Mathf.Lerp(1f, Mathf.Clamp01(_pauseMusicVolumeMultiplier), _pauseAudioBlendCurrent);
            return Mathf.Clamp01(_currentSettings.MusicVolume01 * pauseVolumeMultiplier);
        }

        private void ResolveAudioMixerFromSource()
        {
            if (_audioMixer != null)
            {
                return;
            }

            var audioSource = GetComponent<AudioSource>();
            if (audioSource != null && audioSource.outputAudioMixerGroup != null)
            {
                _audioMixer = audioSource.outputAudioMixerGroup.audioMixer;
            }
        }

        private static string ResolveCurrentLanguageId()
        {
            string currentLanguageId = string.IsNullOrWhiteSpace(GameText.CurrentLanguageId)
                ? DebugContext.RequestedLanguageId
                : GameText.CurrentLanguageId;

            return NormalizeLanguageId(currentLanguageId);
        }

        private static string NormalizeLanguageId(string languageId)
        {
            string normalizedLanguageId = LocalizationTable.NormalizeLanguageId(languageId);
            return string.IsNullOrWhiteSpace(normalizedLanguageId)
                ? LocalizationTable.EnglishLanguageId
                : normalizedLanguageId;
        }

        private void NotifySettingsChanged()
        {
            SettingsChanged?.Invoke(_currentSettings);
        }
    }
}
