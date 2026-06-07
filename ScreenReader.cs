using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Prism screen reader bridge.
    /// </summary>
    public static class ScreenReader
    {
        private const int PrismOk = 0;
        private static bool _available;
        private static bool _initialized;
        private static IntPtr _prismContext;
        private static IntPtr _prismBackend;
        private static string _lastSpokenForDuplicateCheck;
        private static float _activationDuplicateSuppressUntil;

        [DllImport("prism.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr prism_init(IntPtr cfg);

        [DllImport("prism.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void prism_shutdown(IntPtr ctx);

        [DllImport("prism.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr prism_registry_acquire_best(IntPtr ctx);

        [DllImport("prism.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void prism_backend_free(IntPtr backend);

        [DllImport("prism.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr prism_backend_name(IntPtr backend);

        [DllImport("prism.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int prism_backend_output(IntPtr backend, byte[] text, [MarshalAs(UnmanagedType.I1)] bool interrupt);

        [DllImport("prism.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int prism_backend_speak(IntPtr backend, byte[] text, [MarshalAs(UnmanagedType.I1)] bool interrupt);

        [DllImport("prism.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int prism_backend_stop(IntPtr backend);

        [DllImport("prism.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr prism_error_string(int error);

        /// <summary>
        /// Initializes the native screen reader bridge.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            try
            {
                _available = TryInitializePrism();
            }
            catch (Exception ex)
            {
                _available = false;
                ShutdownPrism();
                Main.Log.LogError("Failed to initialize Prism: " + ex.Message);
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
                Output(text, interrupt);
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
                Output(text, interrupt: false);
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
                if (_prismBackend != IntPtr.Zero)
                {
                    int error = prism_backend_stop(_prismBackend);
                    if (error != PrismOk)
                    {
                        Main.Log.LogWarning("Prism stop failed: " + PrismError(error));
                    }
                }
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

        /// <summary>
        /// Shuts down the native screen reader bridge.
        /// </summary>
        public static void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            try
            {
                ShutdownPrism();
            }
            catch
            {
            }

            _available = false;
            _initialized = false;
        }

        /// <summary>
        /// True when a screen reader bridge loaded and reports speech support.
        /// </summary>
        public static bool IsAvailable
        {
            get { return _available; }
        }

        private static bool TryInitializePrism()
        {
            _prismContext = prism_init(IntPtr.Zero);
            if (_prismContext == IntPtr.Zero)
            {
                Main.Log.LogWarning("Prism initialization failed: context was null.");
                return false;
            }

            _prismBackend = prism_registry_acquire_best(_prismContext);
            if (_prismBackend == IntPtr.Zero)
            {
                Main.Log.LogWarning("Prism initialization failed: no usable backend was found.");
                ShutdownPrism();
                return false;
            }

            string backendName = PtrToUtf8String(prism_backend_name(_prismBackend));
            Main.Log.LogInfo("Prism initialized with backend: " + (string.IsNullOrWhiteSpace(backendName) ? "unknown" : backendName));
            return true;
        }

        private static void Output(string text, bool interrupt)
        {
            if (_prismBackend == IntPtr.Zero)
            {
                return;
            }

            byte[] utf8Text = ToNullTerminatedUtf8(text);
            int error = prism_backend_output(_prismBackend, utf8Text, interrupt);
            if (error == PrismOk)
            {
                return;
            }

            error = prism_backend_speak(_prismBackend, utf8Text, interrupt);
            if (error != PrismOk)
            {
                Main.Log.LogWarning("Prism output failed: " + PrismError(error));
            }
        }

        private static void ShutdownPrism()
        {
            if (_prismBackend != IntPtr.Zero)
            {
                try
                {
                    prism_backend_stop(_prismBackend);
                }
                catch
                {
                }

                prism_backend_free(_prismBackend);
                _prismBackend = IntPtr.Zero;
            }

            if (_prismContext != IntPtr.Zero)
            {
                prism_shutdown(_prismContext);
                _prismContext = IntPtr.Zero;
            }
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

        private static byte[] ToNullTerminatedUtf8(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            byte[] result = new byte[bytes.Length + 1];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
            return result;
        }

        private static string PrismError(int error)
        {
            string message = PtrToUtf8String(prism_error_string(error));
            return string.IsNullOrWhiteSpace(message) ? "error " + error : message + " (" + error + ")";
        }

        private static string PtrToUtf8String(IntPtr value)
        {
            if (value == IntPtr.Zero)
            {
                return "";
            }

            int length = 0;
            while (Marshal.ReadByte(value, length) != 0)
            {
                length++;
            }

            byte[] bytes = new byte[length];
            Marshal.Copy(value, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
