using System.Text.Json.Serialization;

namespace ModrixInstaller.Models;

public class GitHubRelease
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

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

    public GitHubAsset? ModrixAsset => Assets.FirstOrDefault(a => 
        a.Name.Equals("Modrix.exe", StringComparison.OrdinalIgnoreCase));
}

public class GitHubAsset
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("download_count")]
    public int DownloadCount { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    public string FormattedSize
    {
        get
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = Size;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}