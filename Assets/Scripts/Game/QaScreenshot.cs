using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// Unattended screenshot capture for QA evidence. Dormant unless the player is launched with
    /// <c>--shotdir</c>, so it costs a normal play session nothing.
    /// </summary>
    /// <remarks>
    /// <para><b>Why this exists.</b> The project's coding standards require that a story changing
    /// anything player-observable is launched and observed, with the screenshot retained under
    /// <c>production/qa/evidence/</c> - a compile check is not a run. A built player cannot be
    /// driven by hand from an automated session, so it has to photograph itself.</para>
    ///
    /// <para><b>Arguments</b> (all optional, all ours rather than Unity's):</para>
    /// <list type="bullet">
    ///   <item><c>--shotdir &lt;abs dir&gt;</c> - directory to write into. Absolute: the relative
    ///   base differs between editor and player. Presence of this flag arms the capture.</item>
    ///   <item><c>--at &lt;s1,s2,...&gt;</c> - real-time seconds from first rendered frame at which
    ///   to capture. Defaults to a single shot at 5 s.</item>
    ///   <item><c>--atsim &lt;s1,s2,...&gt;</c> - <i>simulation</i> seconds into the night at which
    ///   to capture. Takes precedence over <c>--at</c>. See the note below.</item>
    ///   <item><c>--timescale &lt;f&gt;</c> - <see cref="Time.timeScale"/>, so a 300 s night can be
    ///   observed in a fraction of that. Defaults to 1.</item>
    ///   <item><c>--stayopen</c> - skip the quit after the last shot.</item>
    /// </list>
    ///
    /// <para><b>Why <c>--atsim</c> matters.</b> Everything worth photographing happens on the
    /// simulation clock, but <c>--at</c> schedules on the wall clock, and the ratio between them is
    /// <see cref="Time.timeScale"/> multiplied by the frame rate the player happens to reach. A
    /// packet is in flight for only a few seconds of each 25-second spawn interval, so a wall-clock
    /// schedule samples the night blind and can photograph an empty map all night without anything
    /// being wrong with the map. <c>--atsim 9</c> photographs the instant the night is 9 seconds
    /// old, whatever the frame rate.</para>
    ///
    /// <para><b>Capture timing.</b> <see cref="ScreenCapture.CaptureScreenshot"/> photographs the
    /// last frame <i>presented</i>, so the first capture is deliberately deferred past at least one
    /// rendered frame and the file is polled for existence before the next shot is scheduled.</para>
    /// </remarks>
    public static class QaScreenshot
    {
        private const string ShotDirArg = "--shotdir";
        private const string AtArg = "--at";
        private const string AtSimArg = "--atsim";
        private const string TimeScaleArg = "--timescale";
        private const string StayOpenArg = "--stayopen";

        private const float DefaultShotTime = 5f;

        /// <summary>
        /// Arms the capture runner when <c>--shotdir</c> is present on the command line.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            string dir = StartupArgs.ReadValue(args, ShotDirArg);
            if (string.IsNullOrEmpty(dir))
            {
                return;
            }

            float timeScale = StartupArgs.ReadFloat(args, TimeScaleArg, 1f);
            if (timeScale > 0f)
            {
                Time.timeScale = timeScale;
            }

            List<float> simTimes = StartupArgs.ReadFloatList(args, AtSimArg);
            List<float> times = simTimes ?? StartupArgs.ReadFloatList(args, AtArg) ?? new List<float> { DefaultShotTime };

            var host = new GameObject("QaScreenshotRunner");
            UnityEngine.Object.DontDestroyOnLoad(host);
            var runner = host.AddComponent<QaScreenshotRunner>();
            runner.Configure(dir, times, StartupArgs.HasFlag(args, StayOpenArg), simTimes != null);
        }
    }

    /// <summary>
    /// Captures screenshots at the configured offsets, then quits. Installed only by
    /// <see cref="QaScreenshot"/>; never add it to a scene by hand.
    /// </summary>
    public sealed class QaScreenshotRunner : MonoBehaviour
    {
        /// <summary>Frames to keep looking for the <see cref="GameRunner"/> before giving up on simulation-time scheduling.</summary>
        private const int RunnerSearchFrames = 600;

        private string _directory;
        private List<float> _times;
        private bool _stayOpen;
        private bool _useSimulationTime;

        private GameRunner _runner;
        private bool _nightSeenActive;

        /// <summary>Supplies the capture schedule. Call before the first frame.</summary>
        /// <param name="directory">Absolute directory to write PNGs into; created if absent.</param>
        /// <param name="times">Ascending offsets, in seconds, on whichever clock is selected.</param>
        /// <param name="stayOpen">When true, the player is left running after the last capture.</param>
        /// <param name="useSimulationTime">
        /// True to schedule on the night's simulation clock instead of the wall clock.
        /// </param>
        public void Configure(string directory, List<float> times, bool stayOpen, bool useSimulationTime)
        {
            _directory = directory;
            _times = times;
            _stayOpen = stayOpen;
            _useSimulationTime = useSimulationTime;
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(_directory);

            // CaptureScreenshot photographs the last presented frame, so never ask on frame zero.
            yield return null;
            yield return new WaitForEndOfFrame();

            if (_useSimulationTime)
            {
                yield return ResolveRunner();
            }

            float startedAt = Time.realtimeSinceStartup;

            for (int i = 0; i < _times.Count; i++)
            {
                float target = _times[i];
                while (!HasReached(target, startedAt))
                {
                    yield return null;
                }

                yield return new WaitForEndOfFrame();

                string path = Path.Combine(
                    _directory,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        _useSimulationTime ? "{0:00}-sim{1:000}s.png" : "{0:00}-t{1:000}s.png",
                        i + 1,
                        Mathf.RoundToInt(target)));

                ScreenCapture.CaptureScreenshot(path);
                Debug.Log("[QaScreenshot] requested " + path);

                // The write lands asynchronously; give it frames and confirm before moving on so a
                // later shot cannot overtake an earlier one's file.
                for (int waited = 0; waited < 240 && !File.Exists(path); waited++)
                {
                    yield return null;
                }

                Debug.Log(
                    "[QaScreenshot] " + (File.Exists(path) ? "wrote " : "MISSING ") + path);
            }

            if (!_stayOpen)
            {
                Debug.Log("[QaScreenshot] captures complete, quitting");
                Application.Quit(0);
            }
        }

        /// <summary>
        /// Finds the <see cref="GameRunner"/> the bootstrap created, falling back to wall-clock
        /// scheduling if it never appears.
        /// </summary>
        /// <remarks>
        /// Looks the object up by name rather than scanning every component: both this class and
        /// <see cref="GameBootstrap"/> run from <c>RuntimeInitializeOnLoadMethod</c> and their order
        /// is not defined, so the search has to tolerate the bootstrap not having run yet.
        /// </remarks>
        private IEnumerator ResolveRunner()
        {
            for (int frame = 0; frame < RunnerSearchFrames && _runner == null; frame++)
            {
                GameObject root = GameObject.Find(GameBootstrap.RootObjectName);
                if (root != null)
                {
                    _runner = root.GetComponent<GameRunner>();
                }

                if (_runner == null)
                {
                    yield return null;
                }
            }

            if (_runner == null)
            {
                _useSimulationTime = false;
                Debug.LogWarning(
                    "[QaScreenshot] --atsim was given but no GameRunner was found; " +
                    "falling back to real-time capture offsets.");
            }
        }

        /// <summary>True once the selected clock has reached <paramref name="target"/> seconds.</summary>
        private bool HasReached(float target, float startedAt)
        {
            if (!_useSimulationTime || _runner == null)
            {
                return Time.realtimeSinceStartup - startedAt >= target;
            }

            NightShift.Core.NetworkSimulation simulation = _runner.Simulation;
            if (simulation == null)
            {
                return false;
            }

            if (simulation.IsNightActive)
            {
                _nightSeenActive = true;
                return simulation.NightElapsedTime >= target;
            }

            // The night is over, so its clock will never advance again: take the remaining shots
            // now rather than hanging forever on a mark that cannot arrive. Guarded by
            // _nightSeenActive so marks are not all fired in the frames before the night starts.
            return _nightSeenActive;
        }
    }
}
