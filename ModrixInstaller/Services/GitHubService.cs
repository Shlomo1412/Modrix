using System.Net.Http.Json;
using System.Net.Http;
using ModrixInstaller.Models;
using System.IO;

namespace ModrixInstaller.Services;

public interface IGitHubService
{
    Task<List<GitHubRelease>> GetReleasesAsync();
    Task DownloadModrixAsync(GitHubAsset asset, string destinationPath, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);
}

public class GitHubService : IGitHubService
{
    private readonly HttpClient _httpClient;
    private const string GitHubApiUrl = "https://api.github.com/repos/Shlomo1412/Modrix/releases";

    public GitHubService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ModrixInstaller/1.0");
    }

    public async Task<List<GitHubRelease>> GetReleasesAsync()
    {
        try
        {
            var releases = await _httpClient.GetFromJsonAsync<List<GitHubRelease>>(GitHubApiUrl);
            return releases?.Where(r => !r.IsDraft && r.ModrixAsset != null).ToList() ?? new List<GitHubRelease>();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Failed to fetch releases from GitHub: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"An error occurred while fetching releases: {ex.Message}", ex);
        }
    }

    public async Task DownloadModrixAsync(GitHubAsset asset, string destinationPath, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var totalBytesRead = 0L;
            var buffer = new byte[8192];
            var isMoreToRead = true;

            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);

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
                    var progressPercentage = (double)totalBytesRead / totalBytes * 100;
                    progress.Report(new DownloadProgress
                    {
                        BytesReceived = totalBytesRead,
                        TotalBytesToReceive = totalBytes,
                        PercentageComplete = (int)progressPercentage
                    });
                }
            }
            while (isMoreToRead);
        }
        catch (OperationCanceledException)
        {
            // Clean up partial download
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }
            throw;
        }
        catch (Exception ex)
        {
            // Clean up partial download
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }
            throw new InvalidOperationException($"Failed to download Modrix: {ex.Message}", ex);
        }
    }
}

public class DownloadProgress
{
    public long BytesReceived { get; set; }
    public long TotalBytesToReceive { get; set; }
    public int PercentageComplete { get; set; }
}