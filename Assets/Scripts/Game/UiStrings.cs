using NightShift.Core;

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

        // --- Day phase: shop panel (Story 003 acceptance criteria 1, 4, 5, 6) ---

        /// <summary>Title of the day panel, e.g. "ДЕНЬ 2 — МОНТАЖ".</summary>
        public const string DayTitleFormat = "ДЕНЬ {0} — МОНТАЖ";

        public const string ShopHeader = "МАГАЗИН";

        /// <summary>Shop row: node name and its price, e.g. "Сервер — 50".</summary>
        public const string ShopItemFormat = "{0} — {1}";

        public const string NodeNameServer = "Сервер";
        public const string NodeNameFirewall = "Firewall";
        public const string NodeNameIds = "IDS";
        public const string NodeNameHoneypot = "Honeypot";
        public const string NodeNameGateway = "Шлюз";
        public const string NodeNameCore = "Ядро";

        public const string StartShiftButton = "Начать смену";
        public const string ContinueButton = "Следующий день";

        /// <summary>Upgrade button on a selected security tool, e.g. "Апгрейд до ур.2 — 80".</summary>
        public const string UpgradeButtonFormat = "Апгрейд до ур.2 — {0}";

        /// <summary>Header of the selected-node block, e.g. "Выбрано: Firewall ур.1".</summary>
        public const string SelectedNodeFormat = "Выбрано: {0} ур.{1}";

        public const string UpgradeMaxLevel = "Уже ур.2 — максимум";

        // --- Day phase: hints and rejections ---

        public const string DayHintIdle = "ЛКМ по ноде — выбрать; протяни от ноды к ноде — связь; ПКМ по связи — снять.";
        public const string DayHintPlacing = "ЛКМ по пустой клетке — поставить. ПКМ — отменить выбор.";

        /// <summary>Link price hint, e.g. "Связь: 3 кл. — 15".</summary>
        public const string LinkPriceFormat = "Связь: {0} кл. — {1}";

        /// <summary>Maximum link length readout, e.g. "Макс. длина связи: 6 кл.".</summary>
        public const string MaxLinkLengthFormat = "Макс. длина связи: {0} кл.";

        /// <summary>Wraps a rejection reason returned by <c>NightShift.Core</c>.</summary>
        /// <remarks>
        /// The reason itself comes from the simulation verbatim (the UI never re-implements a build
        /// rule, so it cannot phrase the refusal itself). Core's messages are English; giving them
        /// Russian text needs a change inside <c>NightShift.Core</c>, which Story 003 does not own.
        /// </remarks>
        public const string RejectedFormat = "Отказ: {0}";

        public const string CellOccupied = "Клетка занята.";
        public const string CellOutOfGrid = "Клик вне сетки.";
        public const string NoLinkUnderCursor = "Под курсором нет связи.";

        /// <summary>Confirmation of a removed link and its refund, e.g. "Связь снята, возврат 7".</summary>
        public const string LinkRemovedFormat = "Связь снята, возврат {0}";

        /// <summary>Shown when the link-removal shim could not bind to the simulation.</summary>
        public const string LinkRemovalUnavailable = "Снятие связей недоступно в этой сборке.";

        /// <summary>Placement confirmation, e.g. "Поставлено: IDS (-60)".</summary>
        public const string PlacedFormat = "Поставлено: {0} (-{1})";

        /// <summary>Link confirmation, e.g. "Связь создана (-15)".</summary>
        public const string LinkCreatedFormat = "Связь создана (-{0})";

        /// <summary>Upgrade confirmation, e.g. "Апгрейд: Firewall ур.2 (-80)".</summary>
        public const string UpgradedFormat = "Апгрейд: {0} ур.2 (-{1})";

        /// <summary>
        /// Russian display name of a node type. Lives here rather than in <see cref="ViewConfig"/>
        /// so that every player-visible string in the Unity layer stays in this one file.
        /// </summary>
        public static string GetNodeName(NodeType type)
        {
            switch (type)
            {
                case NodeType.Gateway: return NodeNameGateway;
                case NodeType.Server: return NodeNameServer;
                case NodeType.Core: return NodeNameCore;
                case NodeType.Firewall: return NodeNameFirewall;
                case NodeType.Ids: return NodeNameIds;
                case NodeType.Honeypot: return NodeNameHoneypot;
                default: return type.ToString();
            }
        }
    }
}
