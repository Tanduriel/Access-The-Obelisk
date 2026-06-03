using System.Net.Http.Headers;
using System.Text.Json;

namespace AccessTheObelisk.Installer;

internal sealed class GitHubReleaseClient
{
    private readonly HttpClient _httpClient = new();

    public GitHubReleaseClient()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AccessTheObelisk.Installer", InstallerConstants.CurrentInstallerVersion));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(CancellationToken cancellationToken)
    {
        string url = $"https://api.github.com/repos/{InstallerConstants.GitHubOwner}/{InstallerConstants.GitHubRepository}/releases";
        using HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        List<ReleaseInfo> releases = new();

        foreach (JsonElement releaseElement in document.RootElement.EnumerateArray())
        {
            string tag = releaseElement.GetProperty("tag_name").GetString() ?? "";
            string name = releaseElement.GetProperty("name").GetString() ?? tag;
            string body = releaseElement.GetProperty("body").GetString() ?? "";
            List<ReleaseAssetInfo> assets = new();

            foreach (JsonElement assetElement in releaseElement.GetProperty("assets").EnumerateArray())
            {
                assets.Add(new ReleaseAssetInfo
                {
                    Name = assetElement.GetProperty("name").GetString() ?? "",
                    DownloadUrl = assetElement.GetProperty("browser_download_url").GetString() ?? ""
                });
            }

            releases.Add(new ReleaseInfo
            {
                TagName = tag,
                Name = name,
                Changelog = body,
                Assets = assets
            });
        }

        return releases;
    }

    public async Task DownloadFileAsync(string url, string targetPath, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        long total = response.Content.Headers.ContentLength.GetValueOrDefault();
        await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using FileStream output = File.Create(targetPath);

        byte[] buffer = new byte[81920];
        long copied = 0;
        while (true)
        {
            int read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            copied += read;

            if (total > 0)
            {
                progress?.Report((int)Math.Clamp(copied * 100 / total, 0, 100));
            }
        }

        progress?.Report(100);
    }
}
