using System.IO.Compression;
using System.Reflection;
using System.Text.Json;

namespace AccessTheObelisk.Installer;

internal sealed class ModInstaller
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public GameInstallState GetState(string gamePath)
    {
        bool isValidGame = GameLocator.IsGameDirectory(gamePath);
        bool hasBepInEx = Directory.Exists(Path.Combine(gamePath, "BepInEx"));
        InstalledPackage? package = ReadInstalledPackage(gamePath, InstallerConstants.ModName);
        bool hasCurrentDll = File.Exists(Path.Combine(gamePath, "BepInEx", "plugins", "AccessTheObelisk", "AccessTheObelisk.dll"));
        bool hasLegacyDll = File.Exists(Path.Combine(gamePath, "BepInEx", "plugins", "AccessTheObelisk.dll"));
        bool russianInstalled = File.Exists(Path.Combine(gamePath, "BepInEx", "plugins", "RussianTranslation", "RussianTranslation.dll"));

        return new GameInstallState
        {
            GamePath = gamePath,
            IsValidGame = isValidGame,
            HasBepInEx = hasBepInEx,
            IsModInstalled = package != null || hasCurrentDll || hasLegacyDll,
            InstalledVersion = package?.Version ?? GetDllVersion(gamePath) ?? "",
            IsRussianLocalizationInstalled = russianInstalled
        };
    }

    public void InstallPackage(string gamePath, string zipPath, string fallbackPackageId, string fallbackVersion)
    {
        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        PackageManifest manifest = ReadManifest(archive) ?? BuildManifestFromArchive(archive, fallbackPackageId, fallbackVersion);

        if (string.Equals(manifest.PackageId, InstallerConstants.ModName, StringComparison.OrdinalIgnoreCase))
        {
            RemoveLegacyModFiles(gamePath);
            UninstallPackage(gamePath, InstallerConstants.ModName);
        }

        List<string> installedFiles = new();
        foreach (PackageFile file in manifest.Files)
        {
            ZipArchiveEntry? entry = archive.GetEntry(file.Path.Replace('/', '\\')) ?? archive.GetEntry(file.Path.Replace('\\', '/'));
            if (entry == null || string.IsNullOrWhiteSpace(entry.Name))
            {
                continue;
            }

            string targetPath = GetSafeTargetPath(gamePath, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            entry.ExtractToFile(targetPath, overwrite: true);
            installedFiles.Add(NormalizeRelativePath(file.Path));
        }

        WriteInstalledPackage(gamePath, new InstalledPackage
        {
            PackageId = manifest.PackageId,
            Version = manifest.Version,
            Files = installedFiles
        });
    }

    public void InstallRussianLocalization(string gamePath, string zipPath)
    {
        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        PackageManifest manifest = BuildManifestFromArchive(archive, "RussianTranslation", "1.2.1");
        RemoveKnownRussianLocalizationFiles(gamePath);

        List<string> installedFiles = new();
        foreach (PackageFile file in manifest.Files)
        {
            ZipArchiveEntry? entry = archive.GetEntry(file.Path.Replace('/', '\\')) ?? archive.GetEntry(file.Path.Replace('\\', '/'));
            if (entry == null || string.IsNullOrWhiteSpace(entry.Name))
            {
                continue;
            }

            if (!ShouldInstallRussianLocalizationEntry(file.Path))
            {
                continue;
            }

            string targetPath = GetSafeTargetPath(gamePath, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            entry.ExtractToFile(targetPath, overwrite: true);
            installedFiles.Add(NormalizeRelativePath(file.Path));
        }

        WriteInstalledPackage(gamePath, new InstalledPackage
        {
            PackageId = "RussianTranslation",
            Version = "1.2.1",
            Files = installedFiles
        });
    }

    public void UninstallPackage(string gamePath, string packageId)
    {
        InstalledPackage? package = ReadInstalledPackage(gamePath, packageId);
        if (package == null)
        {
            if (string.Equals(packageId, InstallerConstants.ModName, StringComparison.OrdinalIgnoreCase))
            {
                RemoveLegacyModFiles(gamePath);
            }

            return;
        }

        foreach (string relativeFile in package.Files.OrderByDescending(file => file.Length))
        {
            string targetPath = GetSafeTargetPath(gamePath, relativeFile);
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }

        DeleteEmptyDirectories(gamePath, package.Files);
        string installRecord = GetInstallRecordPath(gamePath, packageId);
        if (File.Exists(installRecord))
        {
            File.Delete(installRecord);
        }
    }

    private static PackageManifest? ReadManifest(ZipArchive archive)
    {
        ZipArchiveEntry? entry = archive.GetEntry("manifest.json");
        if (entry == null)
        {
            return null;
        }

        using Stream stream = entry.Open();
        return JsonSerializer.Deserialize<PackageManifest>(stream);
    }

    private static PackageManifest BuildManifestFromArchive(ZipArchive archive, string packageId, string version)
    {
        List<PackageFile> files = archive.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            .Select(entry => new PackageFile
            {
                Path = NormalizeRelativePath(entry.FullName),
                Sha256 = ""
            })
            .Where(file => !string.Equals(file.Path, "manifest.json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new PackageManifest
        {
            PackageId = packageId,
            Name = packageId,
            Version = version,
            Files = files
        };
    }

    private static bool ShouldInstallRussianLocalizationEntry(string relativePath)
    {
        string normalized = NormalizeRelativePath(relativePath);
        return normalized.StartsWith("BepInEx/plugins/RussianTranslation/", StringComparison.OrdinalIgnoreCase);
    }

    private static void RemoveLegacyModFiles(string gamePath)
    {
        string legacyDll = Path.Combine(gamePath, "BepInEx", "plugins", "AccessTheObelisk.dll");
        if (File.Exists(legacyDll))
        {
            File.Delete(legacyDll);
        }
    }

    private static void RemoveKnownRussianLocalizationFiles(string gamePath)
    {
        string packageRecord = GetInstallRecordPath(gamePath, "RussianTranslation");
        if (File.Exists(packageRecord))
        {
            return;
        }

        string pluginDir = Path.Combine(gamePath, "BepInEx", "plugins", "RussianTranslation");
        if (Directory.Exists(pluginDir))
        {
            Directory.Delete(pluginDir, recursive: true);
        }
    }

    private static InstalledPackage? ReadInstalledPackage(string gamePath, string packageId)
    {
        string path = GetInstallRecordPath(gamePath, packageId);
        if (!File.Exists(path))
        {
            return null;
        }

        using FileStream stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<InstalledPackage>(stream);
    }

    private static void WriteInstalledPackage(string gamePath, InstalledPackage package)
    {
        string path = GetInstallRecordPath(gamePath, package.PackageId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string json = JsonSerializer.Serialize(package, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static string GetInstallRecordPath(string gamePath, string packageId)
    {
        return Path.Combine(gamePath, "BepInEx", "plugins", "AccessTheObelisk", $"{packageId}.install.json");
    }

    private static string GetSafeTargetPath(string gamePath, string relativePath)
    {
        string normalized = NormalizeRelativePath(relativePath);
        string targetPath = Path.GetFullPath(Path.Combine(gamePath, normalized.Replace('/', Path.DirectorySeparatorChar)));
        string rootPath = Path.GetFullPath(gamePath);

        if (!targetPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsafe package path: {relativePath}");
        }

        return targetPath;
    }

    private static void DeleteEmptyDirectories(string gamePath, IEnumerable<string> files)
    {
        foreach (string relativeFile in files)
        {
            string? directory = Path.GetDirectoryName(GetSafeTargetPath(gamePath, relativeFile));
            while (!string.IsNullOrWhiteSpace(directory)
                && Directory.Exists(directory)
                && !directory.Equals(gamePath, StringComparison.OrdinalIgnoreCase)
                && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
                directory = Path.GetDirectoryName(directory);
            }
        }
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    private static string? GetDllVersion(string gamePath)
    {
        string currentDll = Path.Combine(gamePath, "BepInEx", "plugins", "AccessTheObelisk", "AccessTheObelisk.dll");
        string legacyDll = Path.Combine(gamePath, "BepInEx", "plugins", "AccessTheObelisk.dll");
        string path = File.Exists(currentDll) ? currentDll : legacyDll;

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return AssemblyName.GetAssemblyName(path).Version?.ToString();
        }
        catch
        {
            return "";
        }
    }
}
