using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TiktokStreakSaver.Services;

public class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

public class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("assets")]
    public GitHubAsset[] Assets { get; set; } = [];
}

public class UpdateInfo
{
    public bool HasUpdate { get; set; }
    public string LatestVersion { get; set; } = string.Empty;
    public string Changelog { get; set; } = string.Empty;
    public string ReleaseUrl { get; set; } = string.Empty;
    public string? ApkDownloadUrl { get; set; }
}

/// <summary>
/// Checks GitHub releases for updates (Jon2G/TiktokStreakSaver).
/// </summary>
public class UpdateService
{
    private static readonly HttpClient HttpClient = new();
    private const string RepoOwner = "Jon2G";
    private const string RepoName = "TiktokStreakSaver";
    private static readonly string ApiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

    static UpdateService()
    {
        HttpClient.DefaultRequestHeaders.Add("User-Agent", "TiktokStreakSaver/1.0");
        HttpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            var release = await HttpClient.GetFromJsonAsync<GitHubRelease>(ApiUrl);
            if (release == null || string.IsNullOrEmpty(release.TagName))
                return null;

            string remoteVersionStr = release.TagName.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                ? release.TagName.Substring(1)
                : release.TagName;

            if (Version.TryParse(remoteVersionStr, out var remoteVersion) &&
                Version.TryParse(AppInfo.Current.VersionString, out var localVersion))
            {
                var apkAsset = release.Assets.FirstOrDefault(a =>
                    a.Name.StartsWith("StreakSaver-", StringComparison.OrdinalIgnoreCase) &&
                    a.Name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                    ?? release.Assets.FirstOrDefault(a =>
                        a.Name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase));

                return new UpdateInfo
                {
                    HasUpdate = remoteVersion > localVersion,
                    LatestVersion = remoteVersionStr,
                    Changelog = release.Body,
                    ReleaseUrl = release.HtmlUrl,
                    ApkDownloadUrl = apkAsset?.BrowserDownloadUrl
                };
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> DownloadApkAsync(string url, string version, IProgress<double> progress, CancellationToken cancellationToken = default)
    {
        try
        {
            string fileName = $"StreakSaver-{version}.apk";
            string destPath = Path.Combine(FileSystem.CacheDirectory, fileName);

            using var downloadClient = new HttpClient();
            downloadClient.DefaultRequestHeaders.Add("User-Agent", "TiktokStreakSaver/1.0");
            downloadClient.Timeout = TimeSpan.FromMinutes(10);

            using var response = await downloadClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1;
            long downloadedBytes = 0;

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            byte[] buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                downloadedBytes += bytesRead;
                if (totalBytes > 0)
                    progress.Report((double)downloadedBytes / totalBytes);
            }

            return destPath;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetChangelogForVersionAsync(string version)
    {
        try
        {
            string tag = version.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? version : $"v{version}";
            string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/tags/{tag}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "TiktokStreakSaver/1.0");

            using var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var release = await response.Content.ReadFromJsonAsync<GitHubRelease>();
            return string.IsNullOrWhiteSpace(release?.Body) ? null : release.Body;
        }
        catch
        {
            return null;
        }
    }
}
