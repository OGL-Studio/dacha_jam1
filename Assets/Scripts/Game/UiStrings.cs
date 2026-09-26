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

        /// <summary>Placement confirmation, e.g. "Поставлено: IDS (-60)".</summary>
        public const string PlacedFormat = "Поставлено: {0} (-{1})";

        /// <summary>Link confirmation, e.g. "Связь создана (-15)".</summary>
        public const string LinkCreatedFormat = "Связь создана (-{0})";

        /// <summary>Upgrade confirmation, e.g. "Апгрейд: Firewall ур.2 (-80)".</summary>
        public const string UpgradedFormat = "Апгрейд: {0} ур.2 (-{1})";

        // --- Night phase: terminal (Story 004, acceptance criteria 1-8) ---

        public const string TerminalTitle = "ТЕРМИНАЛ";

        /// <summary>The always-visible reminder of the terminal's own controls (criterion 8).</summary>
        public const string TerminalHint = "Enter — выполнить · ↑/↓ — история · Esc — снять фокус";

        /// <summary>Prefix of the echoed input line, e.g. "&gt; isolate srv-1".</summary>
        public const string TerminalEchoFormat = "> {0}";

        /// <summary>Line printed when the terminal opens for the night.</summary>
        public const string TerminalReady = "Смена началась. Введите help.";

        public const string TerminalHelpHeader = "Команды:";

        /// <summary>One row of the help listing: usage line and description.</summary>
        public const string TerminalHelpRowFormat = "  {0} — {1}";

        public const string CommandDescriptionHelp = "список команд";
        public const string CommandDescriptionIsolate = "обрубить все связи ноды на время";
        public const string CommandDescriptionScan = "раскрыть скрытые пакеты";
        public const string CommandDescriptionPatch = "вернуть ноду в сеть и снять изоляцию";
        public const string CommandDescriptionUnknown = "без описания";

        public const string TerminalUnknownCommandFormat = "Неизвестная команда: «{0}». Введите help.";
        public const string TerminalUnknownNodeFormat = "Нет такой ноды: «{0}». Имена подписаны на карте.";
        public const string TerminalMissingArgumentFormat = "Нужно имя ноды: {0}";
        public const string TerminalTooManyArgumentsFormat = "Лишние аргументы. Формат: {0}";
        public const string TerminalCooldownFormat = "{0}: кулдаун, осталось {1:0.0} с.";
        public const string TerminalIsolateAppliedFormat = "{0} изолирована на {1:0.#} с — пакеты ищут обход.";
        public const string TerminalIsolateRefusedFormat = "{0} изолировать нельзя.";
        public const string TerminalScanAppliedFormat = "Скан: скрытые пакеты видны {0:0.#} с.";
        public const string TerminalPatchAppliedFormat = "{0} восстановлена и снова в сети.";
        public const string TerminalPatchRefusedFormat = "{0} восстановить не удалось.";
        public const string TerminalInternalErrorFormat = "Сбой терминала: {0}";

        // --- Story 005: misfires, the out-of-control end state, shutdown --all ---

        public const string CommandDescriptionShutdown = "вырубить всю сеть — конец смены";

        /// <summary>
        /// Prefix printed before a successful command that landed on the wrong node (Story 005
        /// acceptance criterion 4: «об этом пишется в лог»). Arguments: intended node, node actually
        /// hit.
        /// </summary>
        public const string TerminalMisfireFormat = "СБОЙ: команда ушла не туда — вместо {0} сработало на {1}. ";

        public const string TerminalLockedOutOfControlFormat = "{0} больше не отвечает. Сеть вне контроля — осталась только «shutdown --all».";
        public const string TerminalShutdownUnavailable = "Вырубать пока нечего: сеть ещё под контролем.";
        public const string TerminalInvalidArgumentFormat = "Так нельзя. Формат: {0}";
        public const string TerminalShutdownApplied = "Сеть выключена. Смена закончена.";

        // --- Story 006: title screen, letters, endings ---
        // Chrome only. Every *narrative* string - the prologue, the letters, the timed night lines,
        // the two ending texts - lives in NightShift.Core.Data.StoryLibrary, per the story's
        // implementation note ("texts in a data file, not in UI code").

        /// <summary>The game's name on the title screen.</summary>
        public const string TitleGameName = "НОЧНАЯ СМЕНА";

        /// <summary>Line under the menu entries on the title screen, telling the player what input the game expects.</summary>
        public const string TitleHint = "Мышь — строить · Терминал — команды · F4 — ускорение · подробнее — «Справка»";

        /// <summary>Header of the between-days letter screen, e.g. "ПОЧТА — УТРО ДНЯ 3".</summary>
        public const string LetterScreenTitleFormat = "ПОЧТА — УТРО ДНЯ {0}";

        /// <summary>Counter of the letter being read, e.g. "Письмо 1 из 2".</summary>
        public const string LetterCounterFormat = "Письмо {0} из {1}";

        /// <summary>From-field label of a letter.</summary>
        public const string LetterFromFormat = "От: {0}";

        /// <summary>Subject-field label of a letter.</summary>
        public const string LetterSubjectFormat = "Тема: {0}";

        /// <summary>Advances to the next unread letter.</summary>
        public const string LetterNextButton = "Следующее письмо";

        /// <summary>Closes the last letter and hands the day over to the player.</summary>
        public const string LetterCloseButton = "К монтажу";

        /// <summary>Title of the victory ending screen (<c>shutdown --all</c> on the final night).</summary>
        public const string EndingTitle = "СЕТЬ ОТКЛЮЧЕНА";

        /// <summary>Footer under the ending text.</summary>
        public const string EndingFooter = "Ночная смена — конец.";

        /// <summary>Restarts the campaign from the title screen, from the defeat or the ending screen.</summary>
        public const string RestartButton = "Заново";

        // --- Main menu, settings and help (player-requested polish pass) ---
        // The menu is also the title screen, so the game's name and the prologue above are shared with
        // it; only the entries and the two sibling screens are new here.

        /// <summary>First menu entry once a shift is under way: closes the menu and goes back to play.</summary>
        public const string MenuResumeButton = "Продолжить";

        /// <summary>Opens the settings screen.</summary>
        public const string MenuSettingsButton = "Настройки";

        /// <summary>Opens the help screen.</summary>
        public const string MenuHelpButton = "Справка";

        /// <summary>Leaves the game.</summary>
        public const string MenuQuitButton = "Выход";

        /// <summary>Returns from the settings or the help screen to the main menu.</summary>
        public const string BackButton = "Назад";

        /// <summary>Hint under the entries when the menu was opened from inside a running shift.</summary>
        public const string MenuHintInGame = "Esc — вернуться в игру";

        /// <summary>Hint on the settings and the help screens.</summary>
        public const string MenuBackHint = "Esc — назад";

        /// <summary>Discoverability line on the day panel: how the menu is reached during play.</summary>
        public const string MenuEscHint = "Esc — меню";

        // --- Settings ---

        public const string SettingsTitle = "НАСТРОЙКИ";
        public const string SettingsWindowSizeHeader = "РАЗМЕР ОКНА";
        public const string SettingsModeHeader = "РЕЖИМ ЭКРАНА";

        /// <summary>One window-size option, e.g. "1280×720".</summary>
        public const string SettingsSizeFormat = "{0}×{1}";

        public const string SettingsModeWindowed = "В окне";
        public const string SettingsModeFullscreen = "Во весь экран";

        /// <summary>Readout of the active choice, e.g. "Сейчас: 1600×900, в окне".</summary>
        public const string SettingsCurrentFormat = "Сейчас: {0}×{1} — {2}";

        /// <summary>
        /// Warning that the setting cannot be judged from the editor. Kept honest on purpose:
        /// <c>Screen.SetResolution</c> is a no-op against the editor's Game view.
        /// </summary>
        public const string SettingsNote = "Размер применяется сразу и запоминается. В редакторе Unity размер задаёт окно Game — проверяйте в собранной игре.";

        // --- Help ---
        // Every line below was read back off the code that implements it: DayBuildController for the
        // day, TerminalView and TerminalCommandProcessor for the night. If a control changes, this is
        // the file that has to change with it.

        public const string HelpTitle = "СПРАВКА";

        public const string HelpGoalHeader = "ЦЕЛЬ";

        /// <summary>The goal of the campaign, in two lines.</summary>
        public static readonly string[] HelpGoalLines =
        {
            "Пять ночей вы держите сеть. Ядро должно дожить до утра: как только его целостность падает до нуля — вас увольняют.",
            "Днём вы строите защиту на кредиты компании, ночью вмешиваетесь из терминала.",
        };

        public const string HelpDayHeader = "ДЕНЬ — МОНТАЖ";

        /// <summary>Day-phase controls, as <see cref="DayBuildController"/> actually reads them.</summary>
        public static readonly string[] HelpDayLines =
        {
            "ЛКМ по товару в магазине — выбрать, затем ЛКМ по пустой клетке — поставить ноду.",
            "ПКМ — отменить выбор товара.",
            "Протянуть от ноды к ноде (зажать ЛКМ на одной, отпустить на другой) — связь.",
            "ПКМ по связи — снять её и вернуть часть кредитов.",
            "ЛКМ по ноде защиты — выбрать её; кнопка апгрейда в панели — улучшить до ур.2.",
            "«Начать смену» — уйти в ночь.",
            "Esc — главное меню.",
        };

        public const string HelpNightHeader = "НОЧЬ — ТЕРМИНАЛ";

        /// <summary>Night-phase controls and the command set from <see cref="TerminalCommandProcessor"/>.</summary>
        public static readonly string[] HelpNightLines =
        {
            "help — список команд.",
            "isolate <нода> — обрубить все связи ноды на время.",
            "scan — раскрыть скрытые пакеты.",
            "patch <нода> — вернуть ноду в сеть и снять изоляцию.",
            "shutdown --all — вырубить сеть и закончить смену; доступна только когда сеть уже вне контроля.",
            "Enter — выполнить, ↑/↓ — история команд.",
            "Esc — снять фокус с поля ввода. Ночью Esc не открывает меню: поле ввода забирает его себе.",
            "Имена нод подписаны на карте.",
        };

        /// <summary>The two controls that work in both phases.</summary>
        public const string HelpFooterLine = "F4 — ускорение ×1/×4 · колесо мыши над логом — прокрутка";

        /// <summary>Russian description of a terminal command, keyed by its command word.</summary>
        public static string GetCommandDescription(string commandName)
        {
            switch (commandName)
            {
                case TerminalCommandProcessor.HelpCommand: return CommandDescriptionHelp;
                case TerminalCommandProcessor.IsolateCommand: return CommandDescriptionIsolate;
                case TerminalCommandProcessor.ScanCommand: return CommandDescriptionScan;
                case TerminalCommandProcessor.PatchCommand: return CommandDescriptionPatch;
                case TerminalCommandProcessor.ShutdownCommand: return CommandDescriptionShutdown;
                default: return CommandDescriptionUnknown;
            }
        }

        /// <summary>
        /// The single Russian line a <see cref="TerminalCommandResult"/> prints, or null when the
        /// result needs more than one line (<see cref="TerminalResultCode.Help"/>) or none at all
        /// (<see cref="TerminalResultCode.Empty"/>).
        /// </summary>
        /// <remarks>
        /// <c>NightShift.Core</c> holds no display text - it returns a
        /// <see cref="TerminalResultCode"/> plus the node name and the seconds involved, and this is
        /// where that becomes a sentence. That is why the terminal, unlike the day panel's
        /// <see cref="RejectedFormat"/>, never shows the player an English string from Core.
        /// </remarks>
        /// <param name="result">Outcome returned by <see cref="TerminalCommandProcessor.Execute"/>.</param>
        /// <param name="usage">
        /// The command's usage line from <see cref="TerminalCommandInfo.Usage"/>, quoted by the two
        /// argument errors. Passed in rather than rebuilt here so the command table stays in Core and
        /// this file cannot print a form the parser does not accept.
        /// </param>
        public static string FormatTerminalResult(TerminalCommandResult result, string usage)
        {
            if (result == null)
            {
                return null;
            }

            string usageLine = string.IsNullOrEmpty(usage) ? result.CommandName : usage;

            // Story 005 criterion 4: a misfire is the same sentence as the success it really was,
            // preceded by the admission that it hit the wrong node.
            string misfirePrefix = result.Misfired
                ? string.Format(TerminalMisfireFormat, result.IntendedNodeName, result.NodeName)
                : string.Empty;

            switch (result.Code)
            {
                case TerminalResultCode.UnknownCommand:
                    return string.Format(TerminalUnknownCommandFormat, result.CommandName);
                case TerminalResultCode.UnknownNode:
                    return string.Format(TerminalUnknownNodeFormat, result.Argument);
                case TerminalResultCode.MissingArgument:
                    return string.Format(TerminalMissingArgumentFormat, usageLine);
                case TerminalResultCode.TooManyArguments:
                    return string.Format(TerminalTooManyArgumentsFormat, usageLine);
                case TerminalResultCode.OnCooldown:
                    return string.Format(TerminalCooldownFormat, result.CommandName, result.Seconds);
                case TerminalResultCode.IsolateApplied:
                    return misfirePrefix + string.Format(TerminalIsolateAppliedFormat, result.NodeName, result.Seconds);
                case TerminalResultCode.IsolateRefused:
                    return string.Format(TerminalIsolateRefusedFormat, result.NodeName);
                case TerminalResultCode.ScanApplied:
                    return string.Format(TerminalScanAppliedFormat, result.Seconds);
                case TerminalResultCode.PatchApplied:
                    return misfirePrefix + string.Format(TerminalPatchAppliedFormat, result.NodeName);
                case TerminalResultCode.PatchRefused:
                    return string.Format(TerminalPatchRefusedFormat, result.NodeName);
                case TerminalResultCode.InternalError:
                    return string.Format(TerminalInternalErrorFormat, result.Diagnostic);
                case TerminalResultCode.CommandLockedOutOfControl:
                    return string.Format(TerminalLockedOutOfControlFormat, result.CommandName);
                case TerminalResultCode.ShutdownUnavailable:
                    return TerminalShutdownUnavailable;
                case TerminalResultCode.InvalidArgument:
                    return string.Format(TerminalInvalidArgumentFormat, usageLine);
                case TerminalResultCode.ShutdownApplied:
                    return TerminalShutdownApplied;
                default:
                    return null;
            }
        }

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
