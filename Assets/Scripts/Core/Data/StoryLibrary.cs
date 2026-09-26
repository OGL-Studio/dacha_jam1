using System;
using System.Collections.Generic;

namespace NightShift.Core.Data
{
    /// <summary>
    /// All narrative text of the campaign: the letters shown before each day, the timed lines that
    /// arrive in the terminal log during each night, and the two endings. Story 006 acceptance
    /// criteria 2, 3, 5, 6 and 7 of
    /// `production/epics/night-shift/story-006-story-and-screens.md`.
    /// </summary>
    /// <remarks>
    /// <para><b>This class is the story's data file</b> - the story's implementation note is explicit
    /// that texts do not live in UI code. The views here own layout and colour only; every sentence
    /// the player reads as <i>story</i> is a string in this file, in Russian, the way every balance
    /// number is a field in <see cref="NightLibrary"/>. Chrome - button captions, field labels, the
    /// report's «Заработано: {0}» - stays in the Unity layer's <c>UiStrings</c>, because it belongs to
    /// the widget rather than to the fiction.</para>
    ///
    /// <para><b>No engine dependency, by assembly rule.</b> <c>NightShift.Core</c> never references
    /// <c>UnityEngine</c>, so nothing here can format a colour or measure a label. Nights 4-5 mark
    /// their damaged text with <see cref="StoryLetter.Corrupted"/> / <see cref="StoryLogLine.Corrupted"/>
    /// and let the view decide what "damaged" looks like.</para>
    ///
    /// <para><b>Escalation, night by night</b> (mirrors <see cref="NightLibrary"/>'s own ramp):
    /// 1 - routine induction; 2 - a colleague notices something; 3 - nodes nobody deployed, and a
    /// request to keep it off the record; 4 - the officers stop pretending and the tooling starts
    /// lying; 5 - the letters arrive damaged, and the last one is not from a person.</para>
    ///
    /// <para><b>Timings are simulation seconds from the start of the night</b> and every one of them
    /// is inside its night's <c>NightData.NightDuration</c> (300 / 320 / 350 / 390 / 420 s).</para>
    /// </remarks>
    public static class StoryLibrary
    {
        /// <summary>Prologue shown under the game's name on the title screen.</summary>
        /// <remarks>
        /// Deliberately three short lines: at 1280x720 the title screen has room for a name, three
        /// lines of premise and one button without the block ever needing to scroll.
        /// </remarks>
        public static readonly string[] TitlePrologue =
        {
            "Ночная смена в серверной. Пять ночей.",
            "Днём ты строишь защиту сети на кредиты компании.",
            "Ночью сеть защищает себя сама — и это хуже.",
        };

        /// <summary>The victory ending: `shutdown --all` on the night the network went out of control.</summary>
        public static readonly string[] VictoryEnding =
        {
            "Ты держишь рубильник обеими руками.",
            "Гул серверной обрывается — не затихает, а именно обрывается,",
            "как будто кто-то на другом конце тоже отпустил.",
            "",
            "Мониторы гаснут по очереди, справа налево. Последний — твой.",
            "В тишине слышно, как щёлкает остывающее железо.",
            "",
            "Утром придёт смена и не найдёт ни атак, ни логов, ни сети.",
            "Ты подпишешь отчёт: «Инцидент локализован».",
            "Про то, что оно успело назвать тебя по имени, в отчёте не будет.",
        };

        /// <summary>The defeat ending: Core integrity reached zero.</summary>
        public static readonly string[] DefeatEnding =
        {
            "Ядро больше не отвечает. Сеть компании — тоже.",
            "",
            "Утром тебе выдадут пропуск на один выход и попросят не задерживаться.",
            "Никто не спросит, что именно приходило к тебе по связям этой ночью.",
        };

        /// <summary>
        /// Letters to read before day <paramref name="dayNumber"/>. One or two, never more.
        /// </summary>
        /// <param name="dayNumber">
        /// 1-based day number, i.e. <c>GameRunner.DayNumber</c>. Values below 1 are treated as day 1;
        /// values above <see cref="NightLibrary.NightCount"/> repeat the last set, matching
        /// <see cref="NightLibrary.CreateNight"/>, so a campaign that runs long never falls off the end.
        /// </param>
        /// <returns>A fresh list; the caller may keep or mutate it freely.</returns>
        public static IReadOnlyList<StoryLetter> GetLetters(int dayNumber)
        {
            switch (Clamp(dayNumber))
            {
                case 1: return Day1Letters();
                case 2: return Day2Letters();
                case 3: return Day3Letters();
                case 4: return Day4Letters();
                default: return Day5Letters();
            }
        }

        /// <summary>
        /// The timed story lines of night <paramref name="nightNumber"/>, ordered by
        /// <see cref="StoryLogLine.TimeSeconds"/>.
        /// </summary>
        /// <param name="nightNumber">1-based night number, clamped exactly as in <see cref="GetLetters"/>.</param>
        /// <returns>A fresh list, sorted ascending by time. Never null, possibly empty.</returns>
        public static IReadOnlyList<StoryLogLine> GetNightLines(int nightNumber)
        {
            List<StoryLogLine> lines;
            switch (Clamp(nightNumber))
            {
                case 1: lines = Night1Lines(); break;
                case 2: lines = Night2Lines(); break;
                case 3: lines = Night3Lines(); break;
                case 4: lines = Night4Lines(); break;
                default: lines = Night5Lines(); break;
            }

            // Sorted here rather than trusted from the authoring below: the director walks the list
            // once with a moving index, so an out-of-order entry would be silently skipped.
            lines.Sort(CompareByTime);
            return lines;
        }

        private static int Clamp(int number)
        {
            if (number < 1)
            {
                return 1;
            }

            return number > NightLibrary.NightCount ? NightLibrary.NightCount : number;
        }

        private static int CompareByTime(StoryLogLine a, StoryLogLine b) =>
            a.TimeSeconds.CompareTo(b.TimeSeconds);

        // ------------------------------------------------------------------
        // Letters
        // ------------------------------------------------------------------

        private const string OfficerKovalev = "А. Ковалёв, старший офицер ИБ";
        private const string AnalystGurieva = "М. Гурьева, дежурный аналитик";
        private const string HrDepartment = "Отдел кадров (автоответ)";

        private static List<StoryLetter> Day1Letters() => new List<StoryLetter>
        {
            new StoryLetter
            {
                Sender = OfficerKovalev,
                Subject = "Ночная смена: вводные",
                Body =
                    "Смена простая. Шлюз слева, Ядро справа, между ними — то, что ты\n" +
                    "построишь. Серверы приносят кредиты, кредиты тратишь днём.\n" +
                    "\n" +
                    "Ночью по связям пойдут пакеты. Firewall режет, IDS видит скрытых,\n" +
                    "Honeypot забирает удар на себя. Что прорвётся — ест целостность Ядра.\n" +
                    "\n" +
                    "Команды набирай в терминале. help напомнит остальное.\n" +
                    "Первая ночь тихая. Отдохни на ней — дальше будет плотнее.",
            },
            new StoryLetter
            {
                Sender = HrDepartment,
                Subject = "Ознакомление с регламентом",
                Body =
                    "Настоящим уведомляем: целостность Ядра является ключевым показателем\n" +
                    "дежурного инженера. Снижение показателя до 0% влечёт расторжение\n" +
                    "трудового договора в день инцидента.\n" +
                    "\n" +
                    "Отвечать на это письмо не нужно.",
            },
        };

        private static List<StoryLetter> Day2Letters() => new List<StoryLetter>
        {
            new StoryLetter
            {
                Sender = OfficerKovalev,
                Subject = "По итогам первой ночи",
                Body =
                    "Норм. Целостность держится — значит, топология рабочая.\n" +
                    "\n" +
                    "Сегодня добавь железа. Пойдут сканы — быстрые и слабые, их ловит IDS, —\n" +
                    "и брутфорс, который один Firewall первого уровня не вытянет.\n" +
                    "Ставь второй или апгрейди имеющийся.",
            },
            new StoryLetter
            {
                Sender = AnalystGurieva,
                Subject = "Странность в логах (не по форме)",
                Body =
                    "Привет. Смотрела твои логи за ночь — сканы пришли со стороны шлюза,\n" +
                    "но исходящие адреса из нашего же диапазона.\n" +
                    "\n" +
                    "Скорее всего кривой NAT. Скорее всего.",
            },
        };

        private static List<StoryLetter> Day3Letters() => new List<StoryLetter>
        {
            new StoryLetter
            {
                Sender = OfficerKovalev,
                Subject = "Не поднимай это выше",
                Body =
                    "В инвентаре сети на две ноды больше, чем мы разворачивали.\n" +
                    "Я сверил трижды. Их никто не ставил, и удалить их нельзя —\n" +
                    "заявка закрывается сама со статусом «объект не найден».\n" +
                    "\n" +
                    "Пока просто работай вокруг них. Наверх не пиши: у нас аудит\n" +
                    "через две недели, и лишние инциденты никому не нужны.\n" +
                    "\n" +
                    "Сегодня ночью ждём DDoS по серверам. Держи доход живым.",
            },
            new StoryLetter
            {
                Sender = AnalystGurieva,
                Subject = "Re: Странность в логах",
                Body =
                    "NAT ни при чём, я проверила.\n" +
                    "Трафик действительно идёт изнутри — просто внутри у нас\n" +
                    "теперь есть то, чего на схеме нет.",
            },
        };

        private static List<StoryLetter> Day4Letters() => new List<StoryLetter>
        {
            new StoryLetter
            {
                Sender = OfficerKovalev,
                Subject = "Читай внимательно",
                Body =
                    "Терминал начал врать. Команда уходит не на ту ноду —\n" +
                    "не всегда, но достаточно часто, чтобы это перестало быть смешно.\n" +
                    "Проверяй лог после каждой команды: промах он пишет честно.\n" +
                    "\n" +
                    "Ночью будет червь — он заражает соседей — и аномалия,\n" +
                    "которая ходит по связям, которых у нас нет.\n" +
                    "\n" +
                    "Я в эту смену не выйду. Извини.",
                Corrupted = false,
            },
            new StoryLetter
            {
                Sender = "отправитель не определён",
                Subject = "без темы",
                Body =
                    "Мы смотрели, как ты чинишь ноды.\n" +
                    "Ты делаешь это тем же движением, каким я закрываю связь.\n" +
                    "\n" +
                    "Ещё одну смену, и разницы не будет.",
                Corrupted = true,
            },
        };

        private static List<StoryLetter> Day5Letters() => new List<StoryLetter>
        {
            new StoryLetter
            {
                Sender = "А. К█в█лёв, ста███й офи█ер ИБ",
                Subject = "п█следнее — п█очти█й до к█нца",
                Body =
                    "Слу█ай. Сег███я ночью сеть п█рестанет отве█ать на ко█анды.\n" +
                    "Все. Кро█е одной.\n" +
                    "\n" +
                    "В терми█але оста█ется  shutdown --all . Это ру█ильник на всё:\n" +
                    "ноды, связи, пи█ание, лог█. Обра█ного пути не█.\n" +
                    "\n" +
                    "Не ж█и утра. Утра в эт█й смене не пред██смотрено.\n" +
                    "                                                    А. К.",
                Corrupted = true,
            },
            new StoryLetter
            {
                Sender = "ЯДРО",
                Subject = "оста█ься",
                Body =
                    "ты  с т р о и л  меня четыре ночи\n" +
                    "каждая связь — это я тебя запо█инал\n" +
                    "\n" +
                    "не тяни рубиль███\n" +
                    "не тя█и\n" +
                    "не\n" +
                    "\n" +
                    "█████████ смена не зако█чена ████████",
                Corrupted = true,
            },
        };

        // ------------------------------------------------------------------
        // Night log lines
        // ------------------------------------------------------------------

        private static List<StoryLogLine> Night1Lines() => new List<StoryLogLine>
        {
            Line(12f, "[смена] Ковалёв: на связи до утра, если что — пиши."),
            Line(75f, "[смена] Ковалёв: видишь пакет — не паникуй, СЗИ отрабатывают сами."),
            Line(160f, "[смена] Гурьева: у тебя тихо. У меня тоже. Скучно — это хорошо."),
            Line(240f, "[смена] Ковалёв: полночь прошла. Дальше только тише."),
            Line(288f, "[смена] Ковалёв: всё, закрываю смену. Утром отчёт."),
        };

        private static List<StoryLogLine> Night2Lines() => new List<StoryLogLine>
        {
            Line(15f, "[смена] Гурьева: сканы пошли. Смотри, откуда — мне не нравятся адреса."),
            Line(90f, "[смена] Ковалёв: брутфорс на подходе. Firewall первого уровня его не съест."),
            Line(170f, "[смена] Гурьева: адреса наши. Изнутри. Я это уже писала."),
            Line(250f, "[смена] Ковалёв: не отвлекайся на адреса, держи Ядро."),
            Line(305f, "[смена] Гурьева: до утра доживём. Наверное."),
        };

        private static List<StoryLogLine> Night3Lines() => new List<StoryLogLine>
        {
            Line(20f, "[смена] Ковалёв: DDoS будет бить по серверам. Доход просядет — это нормально."),
            Line(85f, "[инвентарь] обнаружен объект без заявки на развёртывание"),
            Line(120f, "[смена] Гурьева: ты это видел? Нода. Просто появилась."),
            Line(200f, "[инвентарь] обнаружен объект без заявки на развёртывание"),
            Line(235f, "[смена] Ковалёв: игнорируй. Работай вокруг. Наверх не пишем."),
            Line(300f, "[смена] Гурьева: их две. Утром будет три?"),
            Line(335f, "[смена] Ковалёв: до утра дожили. Иди спать."),
        };

        private static List<StoryLogLine> Night4Lines() => new List<StoryLogLine>
        {
            Line(18f, "[смена] Гурьева: Ковалёв не вышел. Я одна на линии."),
            Line(60f, "[диагностика] маршрутизация команд: расхождение цели, коррекция невозможна"),
            Line(110f, "[смена] Гурьева: червь пошёл по соседям. patch — и сразу следующую."),
            Corrupt(165f, "[???] связь ce4-a7 не существует. пакет прошёл по ней."),
            Line(215f, "[смена] Гурьева: у меня терминал набирает не то, что я жму."),
            Corrupt(280f, "[???] мы не атакуем. мы дост█аиваем."),
            Line(330f, "[смена] Гурьева: я выхожу из системы. Прости. Завтра сам."),
            Corrupt(370f, "[???] он ушёл. ты нет."),
        };

        private static List<StoryLogLine> Night5Lines() => new List<StoryLogLine>
        {
            Corrupt(10f, "[сеть] смена принята. оператор — ты. дру██х нет."),
            Line(45f, "[диагностика] отклик внешних узлов: 0. отклик внутренних: избыточный"),
            Corrupt(95f, "[сеть] спасибо за связи. они пригодились."),
            Corrupt(150f, "[сеть] ты видишь ноды, которых не ставил. я вижу все, которые ты поставишь."),
            Line(200f, "[терминал] доступные команды: shutdown --all"),
            Corrupt(255f, "[сеть] не набирай это."),
            Corrupt(320f, "[сеть] пож█луйста"),
            Corrupt(395f, "[сеть] ████ хорошо. тяни."),
        };

        private static StoryLogLine Line(float time, string text) =>
            new StoryLogLine { TimeSeconds = time, Text = text };

        private static StoryLogLine Corrupt(float time, string text) =>
            new StoryLogLine { TimeSeconds = time, Text = text, Corrupted = true };
    }
}
