using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Tolk screen reader bridge for NVDA, JAWS, and supported fallback drivers.
    /// </summary>
    public static class ScreenReader
    {
        private static bool _available;
        private static bool _initialized;
        private static string _lastSpokenForDuplicateCheck;
        private static float _activationDuplicateSuppressUntil;
        [DllImport("Tolk.dll")]
        private static extern void Tolk_Load();

        [DllImport("Tolk.dll")]
        private static extern void Tolk_Unload();

        [DllImport("Tolk.dll")]
        private static extern bool Tolk_HasSpeech();

        [DllImport("Tolk.dll", CharSet = CharSet.Unicode)]
        private static extern bool Tolk_Output(string text, bool interrupt);

        [DllImport("Tolk.dll")]
        private static extern bool Tolk_Silence();

        [DllImport("Tolk.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr Tolk_DetectScreenReader();

        /// <summary>
        /// Initializes the native Tolk bridge.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            try
            {
                Tolk_Load();
                _available = Tolk_HasSpeech();
                Main.Log.LogInfo(_available ? "Tolk initialized with speech support." : "Tolk loaded, but no speech driver reported speech support.");

                IntPtr screenReaderName = Tolk_DetectScreenReader();
                if (screenReaderName != IntPtr.Zero)
                {
                    Main.Log.LogInfo("Detected screen reader: " + Marshal.PtrToStringUni(screenReaderName));
                }
            }
            catch (Exception ex)
            {
                _available = false;
                Main.Log.LogError("Failed to initialize Tolk: " + ex.Message);
            }

            _initialized = true;
        }

        /// <summary>
        /// Announces text through the active screen reader.
        /// </summary>
        public static void Say(string text, bool interrupt = true)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (ShouldSuppressActivationDuplicate(text, interrupt))
            {
                DebugLogger.LogScreenReader("[suppressed duplicate] " + text);
                return;
            }

            RememberSpokenText(text, interrupt);
            DebugLogger.LogScreenReader(text);

            if (!_available)
            {
                Main.Log.LogInfo("[SR unavailable] " + text);
                return;
            }

            try
            {
                Tolk_Output(text, interrupt);
            }
            catch (Exception ex)
            {
                Main.Log.LogWarning("ScreenReader.Say failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Queues text after current speech.
        /// </summary>
        public static void SayQueued(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            RememberSpokenText(text, interrupt: false);
            DebugLogger.LogScreenReader("[queued] " + text);
            if (!_available)
            {
                Main.Log.LogInfo("[SR unavailable queued] " + text);
                return;
            }

            try
            {
                Tolk_Output(text, false);
            }
            catch (Exception ex)
            {
                Main.Log.LogWarning("ScreenReader.SayQueued failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Pumps queued speech messages.
        /// </summary>
        public static void Update()
        {
        }

        /// <summary>
        /// Stops current speech.
        /// </summary>
        public static void Stop()
        {
            if (!_available)
            {
                return;
            }

            try
            {
                Tolk_Silence();
            }
            catch
            {
            }
        }

        /// <summary>
        /// Suppresses the next activation announcement if it repeats the current focus.
        /// </summary>
        public static void SuppressDuplicateActivationSpeech(float seconds = 0.9f)
        {
            _activationDuplicateSuppressUntil = Mathf.Max(_activationDuplicateSuppressUntil, Time.unscaledTime + seconds);
        }

        private static bool ShouldSuppressActivationDuplicate(string text, bool interrupt)
        {
            if (!interrupt || Time.unscaledTime > _activationDuplicateSuppressUntil)
            {
                return false;
            }

            string current = NormalizeForDuplicateCheck(text);
            if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(_lastSpokenForDuplicateCheck))
            {
                return false;
            }

            return string.Equals(current, _lastSpokenForDuplicateCheck, StringComparison.OrdinalIgnoreCase);
        }

        private static void RememberSpokenText(string text, bool interrupt)
        {
            if (!interrupt)
            {
                return;
            }

            _lastSpokenForDuplicateCheck = NormalizeForDuplicateCheck(text);
        }

        private static string NormalizeForDuplicateCheck(string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? ""
                : text.Trim().TrimEnd('.', '!', '?', ':', ';').Trim();
        }

        /// <summary>
        /// Shuts down Tolk.
        /// </summary>
        public static void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            try
            {
                Tolk_Unload();
            }
            catch
            {
            }

            _available = false;
            _initialized = false;
        }

        /// <summary>
        /// True when Tolk loaded and reports speech support.
        /// </summary>
        public static bool IsAvailable
        {
            get { return _available; }
        }

    }
}
