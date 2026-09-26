namespace NightShift.Core
{
    /// <summary>
    /// One e-mail from a security officer, shown between nights. Story 006 acceptance criterion 2 of
    /// `production/epics/night-shift/story-006-story-and-screens.md`: «перед каждым днём — 1-2 письма
    /// от офицеров ИБ; тон мрачнеет от ночи к ночи; к ночи 5 письма искажены».
    /// </summary>
    /// <remarks>
    /// <para><b>Pure data, no engine.</b> Lives in <c>NightShift.Core</c> next to the rest of the
    /// content so the letters can be unit-tested or re-authored without the Unity assembly, exactly
    /// as <c>NightShift.Core.Data.NightLibrary</c> holds the nights.</para>
    ///
    /// <para><b>Corruption is authored, not generated.</b> The glitched night-5 letters are written
    /// corrupted in <c>StoryLibrary</c> rather than mangled at runtime, so the ending reads the same
    /// way every playthrough and no randomness can produce an unreadable screen.
    /// <see cref="Corrupted"/> is the view's cue to tint the letter and mark the sender as unknown -
    /// it never changes the text.</para>
    /// </remarks>
    public sealed class StoryLetter
    {
        /// <summary>Who the letter is from, as the player sees it ("А. Ковалёв, старший офицер ИБ").</summary>
        public string Sender { get; set; }

        /// <summary>Subject line.</summary>
        public string Subject { get; set; }

        /// <summary>
        /// Body text. May contain <c>\n</c> line breaks; the view renders them as-is and wraps long
        /// lines, so no line here needs to be measured against the screen width.
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// True for the damaged transmissions of the last nights. The view renders these in the
        /// warning colour instead of the calm terminal green.
        /// </summary>
        public bool Corrupted { get; set; }
    }
}
