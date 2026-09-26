using System;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// Owns the menu stack - main menu, settings, help - the one Escape key that navigates it, and the
    /// two latches that keep the game from being played underneath it. Covers the player's requests
    /// «Главное меню» plus the navigation between it, the settings and the help screen.
    /// </summary>
    /// <remarks>
    /// <para><b>Why a controller and not three self-driving views.</b> Three views each polling Escape
    /// would race each other and, worse, race <see cref="TerminalView"/>, which already owns Escape
    /// during the night. One component reading the key and one field holding which screen is up makes
    /// the rule statable in a sentence, which is the next paragraph.</para>
    ///
    /// <para><b>The Escape rule.</b> Settings or help are up -&gt; back to the menu. The menu is up and a
    /// shift is under way -&gt; resume. Nothing is up -&gt; open the menu, <i>but only during
    /// <see cref="GamePhase.Day"/></i>. That last clause is the whole of how this avoids fighting the
    /// terminal: during <see cref="GamePhase.Night"/> this component ignores Escape entirely, so the
    /// key means exactly one thing - blur the terminal field - and keeps meaning it however many times
    /// it is pressed. <see cref="GameRunner.TextInputActive"/> is checked as well, so even a focused
    /// field during the day (there is none today) could not be overridden.</para>
    ///
    /// <para><b>Two latches, both already existing.</b>
    /// <see cref="DayBuildController.ModalOpen"/> stops the map being edited under the menu - the map is
    /// picked from the legacy <see cref="Input"/> class, which UI Toolkit hit-testing never sees, so a
    /// pickable panel alone does not stop it. <see cref="GameRunner.TextInputActive"/> stops the F4
    /// hotkey and <see cref="LetterView"/>'s Enter from firing behind the menu. Both are only raised
    /// when they were already down (see <see cref="CanOpenFromPlay"/>), which is what makes lowering
    /// them on close correct rather than a guess: the menu never opens on top of the mail screen, so it
    /// can never clear a latch the mail screen was relying on.</para>
    ///
    /// <para><b>No time scale games.</b> The menu opens only during the day, and the simulation is
    /// ticked by <see cref="GameRunner"/> only while a night is active, so nothing advances while the
    /// menu is up. There is nothing to pause.</para>
    /// </remarks>
    public sealed class MenuController : MonoBehaviour
    {
        /// <summary>Which screen of the stack is up. Named to avoid shadowing <see cref="UnityEngine.Screen"/>.</summary>
        private enum MenuScreen
        {
            None = 0,
            Menu = 1,
            Settings = 2,
            Help = 3,
        }

        private GameRunner _runner;
        private DayBuildController _buildController;
        private MainMenuView _menu;
        private SettingsView _settings;
        private HelpView _help;
        private Action _onStartGame;
        private MenuScreen _screen = MenuScreen.None;

        /// <summary>True while any screen of the menu stack is up.</summary>
        public bool IsOpen => _screen != MenuScreen.None;

        /// <summary>
        /// Wires the three screens together and, optionally, opens the menu as the game's first frame.
        /// </summary>
        /// <param name="runner">Phase machine and keyboard-ownership flag.</param>
        /// <param name="buildController">
        /// Raised and lowered through <see cref="DayBuildController.ModalOpen"/> so the map cannot be
        /// edited behind the menu. May be null in a harness that has no build controller.
        /// </param>
        /// <param name="menu">The main menu screen.</param>
        /// <param name="settings">The settings screen.</param>
        /// <param name="help">The help screen.</param>
        /// <param name="onStartGame">
        /// Raises day 1. Invoked exactly once, by the menu's first entry, and only while
        /// <see cref="GameRunner.DayNumber"/> is still 0 - after that the same entry resumes instead.
        /// An <see cref="Action"/> rather than a <see cref="GameRunner"/> call so the menu layer cannot
        /// drive the phase machine by accident.
        /// </param>
        /// <param name="showAtStart">
        /// True on the normal launch path, where the menu is the title screen. False under
        /// <see cref="GameBootstrap.SkipDayArg"/>, where an automated capture cannot press a button.
        /// </param>
        public void Initialize(
            GameRunner runner,
            DayBuildController buildController,
            MainMenuView menu,
            SettingsView settings,
            HelpView help,
            Action onStartGame,
            bool showAtStart)
        {
            _runner = runner;
            _buildController = buildController;
            _menu = menu;
            _settings = settings;
            _help = help;
            _onStartGame = onStartGame;

            if (showAtStart)
            {
                Open();
            }
        }

        private void OnDestroy()
        {
            if (IsOpen)
            {
                SetLatches(false);
            }
        }

        /// <summary>Opens the main menu. Does nothing when a screen of the stack is already up.</summary>
        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            SetLatches(true);
            ShowMenu();
        }

        /// <summary>Closes the whole stack and hands the screen back to the game.</summary>
        public void Close()
        {
            if (_menu != null)
            {
                _menu.Hide();
            }

            if (_settings != null)
            {
                _settings.Hide();
            }

            if (_help != null)
            {
                _help.Hide();
            }

            bool wasOpen = IsOpen;
            _screen = MenuScreen.None;

            if (wasOpen)
            {
                SetLatches(false);
            }
        }

        /// <summary>True once day 1 has been raised, i.e. the first entry resumes rather than starts.</summary>
        private bool GameInProgress => _runner != null && _runner.DayNumber > 0;

        /// <summary>
        /// Whether Escape may open the menu right now: only during the day, only when no text field
        /// owns the keyboard, and only when no other modal (the morning mail) is already up.
        /// </summary>
        private bool CanOpenFromPlay
        {
            get
            {
                if (_runner == null || _runner.Phase != GamePhase.Day || _runner.TextInputActive)
                {
                    return false;
                }

                return _buildController == null || !_buildController.ModalOpen;
            }
        }

        private void Update()
        {
            if (_runner == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleEscape();
                return;
            }

            // Enter and Space activate the first entry, but only on the title screen. Once a shift is
            // under way they are deliberately dead here: legacy Input is global and LetterView reads
            // the same keys, so an Enter that closed the menu would also turn the page of the letter
            // sitting behind it.
            if (_screen != MenuScreen.Menu || GameInProgress)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                HandlePrimary();
            }
        }

        private void HandleEscape()
        {
            switch (_screen)
            {
                case MenuScreen.Settings:
                case MenuScreen.Help:
                    ShowMenu();
                    return;

                case MenuScreen.Menu:
                    // There is nothing to go back to before day 1: the menu is the game's floor.
                    if (GameInProgress)
                    {
                        Close();
                    }

                    return;

                default:
                    if (CanOpenFromPlay)
                    {
                        Open();
                    }

                    return;
            }
        }

        private void ShowMenu()
        {
            if (_settings != null)
            {
                _settings.Hide();
            }

            if (_help != null)
            {
                _help.Hide();
            }

            if (_menu != null)
            {
                _menu.Show(GameInProgress);
            }

            _screen = MenuScreen.Menu;
        }

        private void ShowSettings()
        {
            if (_menu != null)
            {
                _menu.Hide();
            }

            if (_settings != null)
            {
                _settings.Show();
            }

            _screen = MenuScreen.Settings;
        }

        private void ShowHelp()
        {
            if (_menu != null)
            {
                _menu.Hide();
            }

            if (_help != null)
            {
                _help.Show();
            }

            _screen = MenuScreen.Help;
        }

        /// <summary>
        /// The first entry: starts day 1 the first time, resumes every time after that.
        /// </summary>
        /// <remarks>
        /// <see cref="Close"/> runs before <see cref="_onStartGame"/> so the latches are already down
        /// when <see cref="GameRunner.BeginDay"/> raises the phase event - <see cref="LetterView"/>
        /// takes <see cref="DayBuildController.ModalOpen"/> for itself inside that event, and would
        /// otherwise have it cleared out from under it a line later.
        /// </remarks>
        private void HandlePrimary()
        {
            bool starting = !GameInProgress;
            Close();

            if (starting)
            {
                _onStartGame?.Invoke();
            }
        }

        /// <summary>
        /// Leaves the game.
        /// </summary>
        /// <remarks>
        /// <see cref="Application.Quit"/> does nothing in the Unity editor - it only closes a built
        /// player. The entry is therefore unverifiable from a play-mode session and has to be checked
        /// in a build.
        /// </remarks>
        private void HandleQuit()
        {
            Application.Quit();
        }

        /// <summary>
        /// The menu's outgoing actions, as delegates, so <see cref="MainMenuView"/> is handed plain
        /// <see cref="Action"/>s and never sees this class or its siblings.
        /// </summary>
        /// <remarks>
        /// Method groups rather than lambdas in <see cref="GameBootstrap"/>: the bootstrap wires the
        /// object graph, and which screen follows which is this component's rule, not the graph's.
        /// </remarks>
        public Action PrimaryAction => HandlePrimary;

        /// <summary>Opens the settings screen. For <see cref="MainMenuView"/>'s «Настройки» entry.</summary>
        public Action SettingsAction => ShowSettings;

        /// <summary>Opens the help screen. For <see cref="MainMenuView"/>'s «Справка» entry.</summary>
        public Action HelpAction => ShowHelp;

        /// <summary>Leaves the game. For <see cref="MainMenuView"/>'s «Выход» entry.</summary>
        public Action QuitAction => HandleQuit;

        /// <summary>Returns to the main menu. For the settings and help screens' «Назад» buttons.</summary>
        public Action BackAction => ShowMenu;

        private void SetLatches(bool held)
        {
            if (_buildController != null)
            {
                _buildController.ModalOpen = held;
            }

            if (_runner != null)
            {
                _runner.TextInputActive = held;
            }
        }
    }
}
