using System;
using System.Collections.Generic;
using System.Globalization;

namespace NightShift.Game
{
    /// <summary>
    /// Reads optional launch arguments from the process command line. Shared by every QA/debug hook
    /// in the Unity layer so the parsing rules (case-insensitive name, value in the next argument)
    /// are defined exactly once.
    /// </summary>
    /// <remarks>
    /// <para><b>Why the command line.</b> A built player cannot be sent a keystroke from an
    /// automated session, so anything that has to be provable from a screenshot - the debug time
    /// scale, the capture schedule - needs a non-interactive way in. Every argument here is
    /// optional and every reader returns a caller-supplied fallback, so a normal launch behaves
    /// exactly as if this class did not exist.</para>
    ///
    /// <para><b>Unity's own arguments are untouched.</b> All names used by this project start with
    /// <c>--</c> and are ignored by the player, which only reads single-dash arguments.</para>
    /// </remarks>
    public static class StartupArgs
    {
        /// <summary>Value that follows <paramref name="name"/> on the command line, or null.</summary>
        public static string ReadValue(string name) => ReadValue(Environment.GetCommandLineArgs(), name);

        /// <summary>Value that follows <paramref name="name"/> in <paramref name="args"/>, or null.</summary>
        public static string ReadValue(string[] args, string name)
        {
            if (args == null)
            {
                return null;
            }

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        /// <summary>True if <paramref name="name"/> appears on the command line at all.</summary>
        public static bool HasFlag(string name) => HasFlag(Environment.GetCommandLineArgs(), name);

        /// <summary>True if <paramref name="name"/> appears in <paramref name="args"/> at all.</summary>
        public static bool HasFlag(string[] args, string name)
        {
            if (args == null)
            {
                return false;
            }

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Float value of <paramref name="name"/>, or <paramref name="fallback"/> when absent or
        /// unparsable. Parsed invariantly so a comma-decimal OS locale cannot change the meaning of
        /// a QA command line.
        /// </summary>
        public static float ReadFloat(string name, float fallback) =>
            ReadFloat(Environment.GetCommandLineArgs(), name, fallback);

        /// <inheritdoc cref="ReadFloat(string,float)"/>
        public static float ReadFloat(string[] args, string name, float fallback)
        {
            string raw = ReadValue(args, name);
            if (raw != null
                && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                return parsed;
            }

            return fallback;
        }

        /// <summary>
        /// Comma-separated float list for <paramref name="name"/>, sorted ascending, or null when
        /// the argument is absent or contains no parsable number.
        /// </summary>
        public static List<float> ReadFloatList(string[] args, string name)
        {
            string raw = ReadValue(args, name);
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            var values = new List<float>();
            foreach (string part in raw.Split(','))
            {
                if (float.TryParse(
                        part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    values.Add(value);
                }
            }

            if (values.Count == 0)
            {
                return null;
            }

            values.Sort();
            return values;
        }
    }
}
