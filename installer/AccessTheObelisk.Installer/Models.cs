using System.Text.Json.Serialization;

namespace AccessTheObelisk.Installer;

internal sealed class ReleaseInfo
{
    public required string TagName { get; init; }

    public required string Name { get; init; }

    public required string Changelog { get; init; }

    public required IReadOnlyList<ReleaseAssetInfo> Assets { get; init; }

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(Name) ? TagName : $"{Name} ({TagName})";
    }
}

internal sealed class ReleaseAssetInfo
{
    public required string Name { get; init; }

    public required string DownloadUrl { get; init; }
}

internal sealed class PackageManifest
{
    [JsonPropertyName("packageId")]
    public string PackageId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("files")]
    public List<PackageFile> Files { get; set; } = new();
}

internal sealed class PackageFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = "";
}

internal sealed class InstalledPackage
{
    public string PackageId { get; set; } = "";

    public string Version { get; set; } = "";

    public List<string> Files { get; set; } = new();
}

internal sealed class GameInstallState
{
    public required string GamePath { get; init; }

    public bool IsValidGame { get; init; }

    public bool HasBepInEx { get; init; }

    public bool IsModInstalled { get; init; }

    public string InstalledVersion { get; init; } = "";

    public bool IsRussianLocalizationInstalled { get; init; }
}
