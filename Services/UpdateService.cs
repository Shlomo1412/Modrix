using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Modrix.Services;

public interface IUpdateService
{
    Task<UpdateInfo?> CheckForUpdatesAsync();
    Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo updateInfo, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);
    string GetCurrentVersion();
    bool IsUpdateAvailable(UpdateInfo updateInfo);
}

public class UpdateService : IUpdateService
{
    private readonly HttpClient _httpClient;
    private const string GitHubApiUrl = "https://api.github.com/repos/Shlomo1412/Modrix/releases";
    private const string UserAgent = "Modrix/1.0";

    public UpdateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
    }

    public string GetCurrentVersion()
    {
        // First try to read from version.txt file
        var currentDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName ?? Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrEmpty(currentDirectory))
        {
            var versionFilePath = Path.Combine(currentDirectory, "version.txt");
            if (File.Exists(versionFilePath))
            {
                try
                {
                    var versionFromFile = File.ReadAllText(versionFilePath).Trim();
                    if (!string.IsNullOrEmpty(versionFromFile))
                    {
                        return versionFromFile;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to read version.txt: {ex.Message}");
                }
            }
        }

        // Fallback to assembly version
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version?.ToString(3) ?? "1.0.0";
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            var currentVersion = GetCurrentVersion();
            
            // Get all releases from GitHub
            var allReleases = await _httpClient.GetFromJsonAsync<List<GitHubRelease>>(GitHubApiUrl);
            if (allReleases == null || !allReleases.Any()) return null;

            // Filter out drafts and prereleases, and ensure they have Modrix asset
            var validReleases = allReleases
                .Where(r => !r.IsDraft && !r.IsPrerelease)
                .Where(r => r.Assets?.Any(a => a.Name.Equals("Modrix.exe", StringComparison.OrdinalIgnoreCase)) == true)
                .OrderByDescending(r => r.PublishedAt)
                .ToList();

            if (!validReleases.Any()) return null;

            // Find the current version in the list
            var currentReleaseIndex = validReleases.FindIndex(r => 
                string.Equals(r.TagName, currentVersion, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.TagName?.TrimStart('v'), currentVersion.TrimStart('v'), StringComparison.OrdinalIgnoreCase));

            UpdateInfo? latestUpdate = null;

            if (currentReleaseIndex == -1)
            {
                // Current version not found in releases, assume it's older than all releases
                // Return the latest release
                var latestRelease = validReleases.First();
                latestUpdate = CreateUpdateInfo(latestRelease);
            }
            else if (currentReleaseIndex > 0)
            {
                // There are newer releases available
                var newerRelease = validReleases.First(); // The most recent one
                latestUpdate = CreateUpdateInfo(newerRelease);
            }
            // If currentReleaseIndex == 0, we're already on the latest version

            return latestUpdate;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to check for updates: {ex.Message}");
            return null;
        }
    }

    private static UpdateInfo CreateUpdateInfo(GitHubRelease release)
    {
        var modrixAsset = release.Assets?.FirstOrDefault(a => 
            a.Name.Equals("Modrix.exe", StringComparison.OrdinalIgnoreCase));

        if (modrixAsset == null)
            throw new InvalidOperationException("Release does not contain Modrix.exe asset");

        return new UpdateInfo
        {
            Version = release.TagName?.TrimStart('v') ?? "Unknown",
            ReleaseNotes = release.Body ?? "No release notes available.",
            DownloadUrl = modrixAsset.DownloadUrl,
            FileSize = modrixAsset.Size,
            PublishedAt = release.PublishedAt,
            IsPrerelease = release.IsPrerelease
        };
    }

    public bool IsUpdateAvailable(UpdateInfo updateInfo)
    {
        try
        {
            var currentVersionString = GetCurrentVersion().TrimStart('v');
            var availableVersionString = updateInfo.Version.TrimStart('v');
            
            // Try parsing as semantic versions
            if (Version.TryParse(currentVersionString, out var currentVersion) && 
                Version.TryParse(availableVersionString, out var availableVersion))
            {
                return availableVersion > currentVersion;
            }
            
            // Fallback to string comparison
            return !string.Equals(currentVersionString, availableVersionString, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo updateInfo, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            progress?.Report(new DownloadProgress { Message = "Preparing update...", PercentageComplete = 0 });

            // Create temporary directory for update
            var tempDir = Path.Combine(Path.GetTempPath(), "ModrixUpdate");
            Directory.CreateDirectory(tempDir);

            var updateExePath = Path.Combine(tempDir, "Modrix_Update.exe");
            var currentExePath = Process.GetCurrentProcess().MainModule?.FileName ?? 
                                Environment.ProcessPath ?? 
                                Assembly.GetExecutingAssembly().Location;

            var currentDirectory = Path.GetDirectoryName(currentExePath) ?? "";
            var versionFilePath = Path.Combine(currentDirectory, "version.txt");

            progress?.Report(new DownloadProgress { Message = "Downloading update...", PercentageComplete = 10 });

            // Download the update
            using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var totalBytesRead = 0L;
            var buffer = new byte[8192];

            using var fileStream = new FileStream(updateExePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var isMoreToRead = true;
            do
            {
                var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (bytesRead == 0)
                {
                    isMoreToRead = false;
                    continue;
                }

                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                totalBytesRead += bytesRead;

                if (progress != null && totalBytes != -1L)
                {
                    var progressPercentage = 10 + (int)((double)totalBytesRead / totalBytes * 70); // 10-80%
                    progress.Report(new DownloadProgress
                    {
                        Message = $"Downloading... {FormatBytes(totalBytesRead)} / {FormatBytes(totalBytes)}",
                        BytesReceived = totalBytesRead,
                        TotalBytesToReceive = totalBytes,
                        PercentageComplete = progressPercentage
                    });
                }
            }
            while (isMoreToRead);

            progress?.Report(new DownloadProgress { Message = "Preparing installation...", PercentageComplete = 85 });

            // Create update script that also updates the version.txt file
            var scriptPath = Path.Combine(tempDir, "update.bat");
            // Store the full tag name (with or without 'v' prefix) as it appears in the release
            var versionToWrite = updateInfo.Version.StartsWith("v") ? updateInfo.Version : updateInfo.Version;
            var script = $@"
@echo off
echo Waiting for Modrix to close...
timeout /t 3 /nobreak > nul

echo Backing up current version...
if exist ""{currentExePath}.backup"" del ""{currentExePath}.backup""
if exist ""{currentExePath}"" (
    ren ""{currentExePath}"" ""{Path.GetFileName(currentExePath)}.backup""
)

echo Installing update...
copy /y ""{updateExePath}"" ""{currentExePath}""

echo Updating version information...
echo {versionToWrite} > ""{versionFilePath}""

echo Starting Modrix...
start """" ""{currentExePath}""

echo Cleaning up...
timeout /t 2 /nobreak > nul
rmdir /s /q ""{tempDir}""
";

            await File.WriteAllTextAsync(scriptPath, script, cancellationToken);

            progress?.Report(new DownloadProgress { Message = "Installing update...", PercentageComplete = 95 });

            // Launch update script and exit current application
            var startInfo = new ProcessStartInfo
            {
                FileName = scriptPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(startInfo);

            progress?.Report(new DownloadProgress { Message = "Update complete! Restarting...", PercentageComplete = 100 });

            // Give time for progress to show, then exit
            await Task.Delay(1000, cancellationToken);
            
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update installation failed: {ex.Message}");
            return false;
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

// Data models for GitHub API
public class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("published_at")]
    public DateTime PublishedAt { get; set; }

    [JsonPropertyName("prerelease")]
    public bool IsPrerelease { get; set; }

    [JsonPropertyName("draft")]
    public bool IsDraft { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = new();
}

public class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string DownloadUrl { get; set; } = string.Empty;
}

public class UpdateInfo
{
    public string Version { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime PublishedAt { get; set; }
    public bool IsPrerelease { get; set; }

    public string FormattedSize
    {
        get
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = FileSize;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    public string FormattedDate => PublishedAt.ToString("MMM dd, yyyy");
}

public class DownloadProgress
{
    public string Message { get; set; } = string.Empty;
    public long BytesReceived { get; set; }
    public long TotalBytesToReceive { get; set; }
    public int PercentageComplete { get; set; }
}