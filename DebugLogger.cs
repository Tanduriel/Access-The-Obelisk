namespace AccessTheObelisk
{
    /// <summary>
    /// Centralized debug logging helpers.
    /// </summary>
    public static class DebugLogger
    {
        /// <summary>
        /// Logs screen reader text when debug logging is enabled.
        /// </summary>
        public static void LogScreenReader(string text)
        {
            if (!Main.DebugMode)
            {
                return;
            }

            Main.Log.LogInfo("[SR] " + text);
        }

        /// <summary>
        /// Logs state changes when debug logging is enabled.
        /// </summary>
        public static void LogState(string description)
        {
            if (!Main.DebugMode)
            {
                return;
            }

            Main.Log.LogInfo("[STATE] " + description);
        }

        /// <summary>
        /// Logs managed and process memory when debug logging is enabled.
        /// </summary>
        public static void LogMemory(string reason)
        {
            if (!Main.DebugMode)
            {
                return;
            }

            try
            {
                long managed = System.GC.GetTotalMemory(false);
                using (System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess())
                {
                    Main.Log.LogInfo(
                        "[MEMORY] " + reason +
                        "; managed=" + FormatMegabytes(managed) +
                        "; workingSet=" + FormatMegabytes(process.WorkingSet64) +
                        "; privateBytes=" + FormatMegabytes(process.PrivateMemorySize64) +
                        "; state=" + AccessStateManager.CurrentState +
                        "; scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                }
            }
            catch (System.Exception ex)
            {
                Main.Log.LogWarning("Memory diagnostics failed: " + ex.Message);
            }
        }

        private static string FormatMegabytes(long bytes)
        {
            return (bytes / 1048576.0).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " MB";
        }
    }
}
