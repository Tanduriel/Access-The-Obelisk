using Microsoft.Win32;

namespace AccessTheObelisk.Installer;

internal static class GameLocator
{
    public static string? FindGameDirectory()
    {
        foreach (string candidate in GetCandidates())
        {
            if (IsGameDirectory(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static bool IsGameDirectory(string path)
    {
        return File.Exists(Path.Combine(path, "AcrossTheObelisk.exe"))
            && Directory.Exists(Path.Combine(path, "AcrossTheObelisk_Data"));
    }

    private static IEnumerable<string> GetCandidates()
    {
        yield return @"D:\Across.the.Obelisk.v1.7.5.1";

        foreach (string steamLibrary in GetSteamLibraries())
        {
            yield return Path.Combine(steamLibrary, "steamapps", "common", "Across the Obelisk");
            yield return Path.Combine(steamLibrary, "steamapps", "common", "AcrossTheObelisk");
        }

        yield return @"C:\Program Files (x86)\Steam\steamapps\common\Across the Obelisk";
        yield return @"C:\Program Files\Steam\steamapps\common\Across the Obelisk";
        yield return @"D:\SteamLibrary\steamapps\common\Across the Obelisk";
        yield return @"E:\SteamLibrary\steamapps\common\Across the Obelisk";
    }

    private static IEnumerable<string> GetSteamLibraries()
    {
        string? steamPath = GetSteamPathFromRegistry();
        if (string.IsNullOrWhiteSpace(steamPath))
        {
            yield break;
        }

        yield return steamPath;

        string libraryFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFile))
        {
            yield break;
        }

        foreach (string line in File.ReadLines(libraryFile))
        {
            string trimmed = line.Trim();
            if (!trimmed.Contains("\"path\"", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] parts = trimmed.Split('"', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4)
            {
                yield return parts[3].Replace(@"\\", @"\");
            }
        }
    }

    private static string? GetSteamPathFromRegistry()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        return key?.GetValue("SteamPath") as string;
    }
}
