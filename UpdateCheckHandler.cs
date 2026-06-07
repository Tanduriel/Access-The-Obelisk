using System;
using System.Collections;
using System.Xml;
using UnityEngine.Networking;

namespace AccessTheObelisk
{
    /// <summary>
    /// Checks GitHub Releases for a newer stable mod version after startup.
    /// </summary>
    public sealed class UpdateCheckHandler
    {
        private const string ReleasesFeedUrl = "https://github.com/tanduriel/Access-The-Obelisk/releases.atom";
        private bool _started;

        /// <summary>
        /// Starts the one-time update check without blocking the Unity main thread.
        /// </summary>
        public void Begin()
        {
            if (_started || Main.Instance == null)
            {
                return;
            }

            _started = true;
            Main.Instance.RunHandlerCoroutine(CheckForUpdate());
        }

        private static IEnumerator CheckForUpdate()
        {
            using (UnityWebRequest request = UnityWebRequest.Get(ReleasesFeedUrl))
            {
                request.timeout = 8;
                request.SetRequestHeader("User-Agent", "AccessTheObelisk/" + Main.PluginVersion);
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Main.Log.LogInfo("Update check skipped: " + request.error);
                    yield break;
                }

                string latestVersionText;
                try
                {
                    XmlDocument feed = new XmlDocument();
                    feed.LoadXml(request.downloadHandler.text);
                    XmlNamespaceManager namespaces = new XmlNamespaceManager(feed.NameTable);
                    namespaces.AddNamespace("atom", "http://www.w3.org/2005/Atom");
                    XmlNode releaseId = feed.SelectSingleNode("/atom:feed/atom:entry[1]/atom:id", namespaces);
                    latestVersionText = releaseId == null ? "" : ReleaseTagFromId(releaseId.InnerText);
                }
                catch (Exception ex)
                {
                    Main.Log.LogWarning("Update check response could not be parsed: " + ex.Message);
                    yield break;
                }

                Version latestVersion;
                if (!TryParseVersion(latestVersionText, out latestVersion))
                {
                    Main.Log.LogWarning("Update check returned an invalid release tag: " + latestVersionText);
                    yield break;
                }

                Version currentVersion;
                if (!TryParseVersion(Main.PluginVersion, out currentVersion))
                {
                    Main.Log.LogWarning("Current mod version is invalid: " + Main.PluginVersion);
                    yield break;
                }

                if (latestVersion > currentVersion)
                {
                    ScreenReader.SayQueued(Loc.Get("update_available", latestVersion.ToString(), currentVersion.ToString()));
                }
                else
                {
                    ScreenReader.SayQueued(Loc.Get("update_current", currentVersion.ToString()));
                }
            }
        }

        private static string ReleaseTagFromId(string releaseId)
        {
            int separatorIndex = (releaseId ?? "").LastIndexOf('/');
            return separatorIndex >= 0 ? releaseId.Substring(separatorIndex + 1) : releaseId;
        }

        private static bool TryParseVersion(string value, out Version version)
        {
            string normalized = (value ?? "").Trim().TrimStart('v', 'V');
            int suffixIndex = normalized.IndexOfAny(new[] { '-', '+' });
            if (suffixIndex >= 0)
            {
                normalized = normalized.Substring(0, suffixIndex);
            }

            return Version.TryParse(normalized, out version);
        }
    }
}
