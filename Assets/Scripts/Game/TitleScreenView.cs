using System;
using NightShift.Core.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace NightShift.Game
{
    /// <summary>
    /// The title screen: the game's name, the prologue from
    /// <see cref="StoryLibrary.TitlePrologue"/>, and «Начать смену». Implements Story 006 acceptance
    /// criterion 1 of `production/epics/night-shift/story-006-story-and-screens.md`.
    /// </summary>
    /// <remarks>
    /// <para><b>It is the first thing shown, and it owns the first day.</b> The bootstrap no longer
    /// calls <see cref="GameRunner.BeginDay"/> itself on the normal path - it hands that call to this
    /// view as <c>onStart</c>. Until the player presses the button no phase event has been raised, so
    /// the shop, the HUD and the terminal are all still in their built-hidden state and nothing draws
    /// behind the title.</para>
    ///
    /// <para><b>Layer picking.</b> Like <see cref="TerminalView"/>, the layer this builds into ignores
    /// picking and only the panel inside it is pickable, so the title screen cannot swallow a click
    /// meant for a lower layer once it has hidden itself. See the comment in
    /// <see cref="GameBootstrap"/> on layer creation.</para>
    ///
    /// <para><b>Keyboard as well as mouse.</b> Enter or Space starts the shift, read from the legacy
    /// <see cref="Input"/> class - <c>com.unity.inputsystem</c> is not installed in this project
    /// (<c>activeInputHandler: 0</c>), so the new Input System API would not compile here.</para>
    /// </remarks>
    public sealed class TitleScreenView : MonoBehaviour
    {
        private const int PrologueSpacingPx = 6;
        private const int BlockSpacingPx = 26;
        private const int ButtonPaddingXPx = 34;
        private const int ButtonPaddingYPx = 10;

        private ViewConfig _config;
        private Action _onStart;
        private VisualElement _panel;

        /// <summary>True while the title screen is on screen.</summary>
        public bool IsShowing { get; private set; }

        /// <summary>
        /// Builds the title screen and shows it immediately - it is the game's first frame.
        /// </summary>
        /// <param name="config">Presentation constants.</param>
        /// <param name="parent">UI Toolkit layer from <see cref="UiRoot.CreateLayer"/>.</param>
        /// <param name="onStart">
        /// Invoked once, after the screen hides itself, when the player starts the shift. An
        /// <see cref="Action"/> rather than a <see cref="GameRunner"/> reference so this view stays a
        /// pure view and cannot drive the phase machine by accident.
        /// </param>
        public void Initialize(ViewConfig config, VisualElement parent, Action onStart)
        {
            _config = config;
            _onStart = onStart;

            if (parent == null)
            {
                Debug.LogError("[NightShift] TitleScreenView.Initialize got a null UI layer; the title screen will not appear.");
                return;
            }

            BuildPanel(parent);
            Show();
        }

        /// <summary>Puts the title screen up.</summary>
        public void Show()
        {
            if (_panel == null)
            {
                return;
            }

            _panel.style.display = DisplayStyle.Flex;
            IsShowing = true;
        }

        /// <summary>Takes the title screen down without starting anything.</summary>
        public void Hide()
        {
            if (_panel != null)
            {
                _panel.style.display = DisplayStyle.None;
            }

            IsShowing = false;
        }

        private void Update()
        {
            if (!IsShowing)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                HandleStartClicked();
            }
        }

        private void HandleStartClicked()
        {
            if (!IsShowing)
            {
                return;
            }

            Hide();
            _onStart?.Invoke();
        }

        private void BuildPanel(VisualElement parent)
        {
            _panel = StoryUi.CreateOverlayPanel(parent, "title-panel", _config);

            var title = new Label { name = "title-name", text = UiStrings.TitleGameName };
            title.style.color = _config.HudTextColor;
            title.style.fontSize = _config.TitleFontSize;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.flexShrink = 0f;
            title.style.marginBottom = BlockSpacingPx;
            _panel.Add(title);

            foreach (string line in StoryLibrary.TitlePrologue)
            {
                var prologue = new Label { text = line };
                prologue.style.color = _config.StoryTextColor;
                prologue.style.fontSize = _config.TitleBodyFontSize;
                prologue.style.unityTextAlign = TextAnchor.MiddleCenter;
                prologue.style.marginBottom = PrologueSpacingPx;
                prologue.style.flexShrink = 0f;
                _panel.Add(prologue);
            }

            Button start = StoryUi.CreateButton(UiStrings.StartShiftButton, _config, ButtonPaddingXPx, ButtonPaddingYPx);
            start.name = "title-start";
            start.style.marginTop = BlockSpacingPx;
            start.clicked += HandleStartClicked;
            _panel.Add(start);

            var hint = new Label { name = "title-hint", text = UiStrings.TitleHint };
            hint.style.color = _config.TitleHintColor;
            hint.style.fontSize = _config.TerminalFontSize;
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.marginTop = BlockSpacingPx;
            hint.style.flexShrink = 0f;
            _panel.Add(hint);
        }
    }
}
