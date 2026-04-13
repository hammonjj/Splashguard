namespace BitBox.Toymageddon.Settings
{
    public readonly struct GameSettingsSnapshot
    {
        public GameSettingsSnapshot(
            float masterVolume01,
            float musicVolume01,
            float sfxVolume01,
            string languageId,
            float uiScale,
            bool invertVerticalAim)
        {
            MasterVolume01 = masterVolume01;
            MusicVolume01 = musicVolume01;
            SfxVolume01 = sfxVolume01;
            LanguageId = languageId;
            UiScale = uiScale;
            InvertVerticalAim = invertVerticalAim;
        }

        public float MasterVolume01 { get; }

        public float MusicVolume01 { get; }

        public float SfxVolume01 { get; }

        public string LanguageId { get; }

        public float UiScale { get; }

        public bool InvertVerticalAim { get; }

        public GameSettingsSnapshot WithAudio(
            float? masterVolume01 = null,
            float? musicVolume01 = null,
            float? sfxVolume01 = null)
        {
            return new GameSettingsSnapshot(
                masterVolume01 ?? MasterVolume01,
                musicVolume01 ?? MusicVolume01,
                sfxVolume01 ?? SfxVolume01,
                LanguageId,
                UiScale,
                InvertVerticalAim);
        }

        public GameSettingsSnapshot WithLanguage(string languageId)
        {
            return new GameSettingsSnapshot(
                MasterVolume01,
                MusicVolume01,
                SfxVolume01,
                languageId,
                UiScale,
                InvertVerticalAim);
        }

        public GameSettingsSnapshot WithUiScale(float uiScale)
        {
            return new GameSettingsSnapshot(
                MasterVolume01,
                MusicVolume01,
                SfxVolume01,
                LanguageId,
                uiScale,
                InvertVerticalAim);
        }

        public GameSettingsSnapshot WithGameplay(bool? invertVerticalAim = null)
        {
            return new GameSettingsSnapshot(
                MasterVolume01,
                MusicVolume01,
                SfxVolume01,
                LanguageId,
                UiScale,
                invertVerticalAim ?? InvertVerticalAim);
        }
    }
}
