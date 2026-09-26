namespace NightShift.Game
{
    /// <summary>
    /// Every player-facing string in the Unity layer, in one place. All texts are Russian, as
    /// `design/game-brief.md` requires.
    /// </summary>
    /// <remarks>
    /// Centralised so Story 006 (styled screens) and any later localisation pass have a single file
    /// to touch instead of hunting through the views.
    /// </remarks>
    public static class UiStrings
    {
        // --- HUD (Story 002 acceptance criterion 4) ---

        public const string NightFormat = "Ночь {0}";
        public const string CreditsFormat = "Кредиты: {0}";
        public const string CoreIntegrityFormat = "Ядро: {0}/{1}";
        public const string TimeRemainingFormat = "До утра: {0:00}:{1:00}";
        public const string SpeedFormat = "Скорость: x{0}";

        // --- Shift report (Story 002 acceptance criterion 5) ---

        public const string ReportTitle = "СМЕНА ЗАВЕРШЕНА";
        public const string ReportNightFormat = "Ночь: {0}";
        public const string ReportEarnedFormat = "Заработано: {0}";
        public const string ReportBlockedFormat = "Заблокировано: {0}";
        public const string ReportLeakedFormat = "Пропущено: {0}";
        public const string ReportDissipatedFormat = "Рассеялось: {0}";
        public const string ReportDamageFormat = "Урон Ядру: {0}";
        public const string ReportIntegrityFormat = "Целостность Ядра: {0}/{1}";

        // --- Defeat ---

        public const string GameOverTitle = "ВЫ УВОЛЕНЫ";
        public const string GameOverBody = "Целостность Ядра упала до нуля.";
    }
}
