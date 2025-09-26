using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.IO.Compression;
using System.Linq;

namespace Modrix.Services
{
    public class MinecraftAssetExtractor
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly string _cacheDirectory;

        public MinecraftAssetExtractor()
        {
            _cacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Modrix",
                "MinecraftAssets"
            );
            Directory.CreateDirectory(_cacheDirectory);
        }

        public async Task<bool> ExtractAssetsForVersion(string minecraftVersion, IProgress<string>? progress = null)
        {
            try
            {
                progress?.Report($"Starting extraction for Minecraft {minecraftVersion}...");
                System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Starting extraction for {minecraftVersion}");

                // Check if assets already exist
                var versionCacheDir = Path.Combine(_cacheDirectory, minecraftVersion);
                if (Directory.Exists(versionCacheDir) && Directory.GetFiles(versionCacheDir, "*.png", SearchOption.AllDirectories).Length > 0)
                {
                    progress?.Report($"Assets for {minecraftVersion} already exist.");
                    System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Assets already exist for {minecraftVersion}");
                    return true;
                }

                // Get version manifest
                progress?.Report("Fetching version manifest...");
                System.Diagnostics.Debug.WriteLine("MinecraftAssetExtractor: Fetching version manifest");
                var versionManifest = await GetVersionManifest(minecraftVersion);
                if (versionManifest == null)
                {
                    progress?.Report($"Failed to get manifest for {minecraftVersion}");
                    System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Failed to get manifest for {minecraftVersion}");
                    return false;
                }

                // Download client jar
                progress?.Report("Downloading client JAR...");
                System.Diagnostics.Debug.WriteLine("MinecraftAssetExtractor: Downloading client JAR");
                var jarPath = await DownloadClientJar(versionManifest, versionCacheDir);
                if (string.IsNullOrEmpty(jarPath))
                {
                    progress?.Report("Failed to download client JAR");
                    System.Diagnostics.Debug.WriteLine("MinecraftAssetExtractor: Failed to download client JAR");
                    return false;
                }

                // Extract textures from JAR
                progress?.Report("Extracting textures from JAR...");
                System.Diagnostics.Debug.WriteLine("MinecraftAssetExtractor: Extracting textures from JAR");
                await ExtractTexturesFromJar(jarPath, versionCacheDir);

                progress?.Report($"Successfully extracted assets for Minecraft {minecraftVersion}");
                System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Successfully extracted assets for {minecraftVersion}");
                return true;
            }
            catch (Exception ex)
            {
                progress?.Report($"Error extracting assets: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Error extracting assets: {ex}");
                return false;
            }
        }

        private async Task<MinecraftVersionManifest?> GetVersionManifest(string version)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("MinecraftAssetExtractor: Getting main version manifest");
                
                // Get the version manifest URL from the main manifest
                var mainManifestResponse = await _httpClient.GetStringAsync("https://launchermeta.mojang.com/mc/game/version_manifest.json");
                System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Main manifest response length: {mainManifestResponse.Length}");

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var mainManifest = JsonSerializer.Deserialize<MinecraftMainManifest>(mainManifestResponse, options);
                System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Found {mainManifest?.Versions?.Length ?? 0} versions in manifest");

                var versionInfo = mainManifest?.Versions?.FirstOrDefault(v => v.Id == version);
                if (versionInfo == null)
                {
                    System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Version {version} not found in manifest");
                    return null;
                }

                System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Found version info, downloading from {versionInfo.Url}");

                // Get the specific version manifest
                var versionResponse = await _httpClient.GetStringAsync(versionInfo.Url);
                System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Version manifest response length: {versionResponse.Length}");
                
                var versionManifest = JsonSerializer.Deserialize<MinecraftVersionManifest>(versionResponse, options);
                System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Version manifest parsed, has client download: {versionManifest?.Downloads?.Client != null}");
                
                return versionManifest;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Error getting version manifest: {ex}");
                return null;
            }
        }

        private async Task<string?> DownloadClientJar(MinecraftVersionManifest manifest, string cacheDir)
        {
            try
            {
                var clientDownload = manifest.Downloads?.Client;
                if (clientDownload == null)
                {
                    System.Diagnostics.Debug.WriteLine("MinecraftAssetExtractor: No client download info found");
                    return null;
                }

                System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Client download URL: {clientDownload.Url}");
                System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Client download size: {clientDownload.Size} bytes");

                Directory.CreateDirectory(cacheDir);
                var jarPath = Path.Combine(cacheDir, "client.jar");

                if (File.Exists(jarPath))
                {
                    // Verify existing file
                    var existingHash = CalculateSha1(jarPath);
                    if (existingHash.Equals(clientDownload.Sha1, StringComparison.OrdinalIgnoreCase))
                    {
                        System.Diagnostics.Debug.WriteLine("MinecraftAssetExtractor: Using existing JAR file (hash matches)");
                        return jarPath;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("MinecraftAssetExtractor: Existing JAR hash doesn't match, redownloading");
                    }
                }

                System.Diagnostics.Debug.WriteLine("MinecraftAssetExtractor: Downloading client JAR...");
                using var response = await _httpClient.GetAsync(clientDownload.Url);
                response.EnsureSuccessStatusCode();

                using var fileStream = File.Create(jarPath);
                await response.Content.CopyToAsync(fileStream);

                System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Downloaded JAR to {jarPath}");
                return jarPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Error downloading client JAR: {ex}");
                return null;
            }
        }

        private async Task ExtractTexturesFromJar(string jarPath, string extractDir)
        {
            try
            {
                var texturesDir = Path.Combine(extractDir, "textures");
                var langDir = Path.Combine(extractDir, "lang");
                var modelsDir = Path.Combine(extractDir, "models");
                Directory.CreateDirectory(texturesDir);
                Directory.CreateDirectory(langDir);
                Directory.CreateDirectory(modelsDir);

                System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Opening JAR file: {jarPath}");
                using var archive = ZipFile.OpenRead(jarPath);
                
                var textureEntries = archive.Entries
                    .Where(e => e.FullName.StartsWith("assets/minecraft/textures/") && 
                               e.FullName.EndsWith(".png") && 
                               !string.IsNullOrEmpty(e.Name))
                    .ToList();

                var langEntries = archive.Entries
                    .Where(e => e.FullName.StartsWith("assets/minecraft/lang/") && 
                               e.FullName.EndsWith(".json") && 
                               !string.IsNullOrEmpty(e.Name))
                    .ToList();

                var modelEntries = archive.Entries
                    .Where(e => e.FullName.StartsWith("assets/minecraft/models/") && 
                               e.FullName.EndsWith(".json") && 
                               !string.IsNullOrEmpty(e.Name))
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Found {textureEntries.Count} texture files, {langEntries.Count} language files, and {modelEntries.Count} model files in JAR");

                int extractedCount = 0;
                
                // Extract textures
                foreach (var entry in textureEntries)
                {
                    try
                    {
                        var relativePath = entry.FullName.Substring("assets/minecraft/textures/".Length);
                        var outputPath = Path.Combine(texturesDir, relativePath);
                        var outputDir = Path.GetDirectoryName(outputPath);
                        
                        if (!string.IsNullOrEmpty(outputDir))
                        {
                            Directory.CreateDirectory(outputDir);
                        }

                        using var entryStream = entry.Open();
                        using var outputStream = File.Create(outputPath);
                        await entryStream.CopyToAsync(outputStream);
                        extractedCount++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Error extracting {entry.FullName}: {ex.Message}");
                    }
                }

                // Extract language files
                foreach (var entry in langEntries)
                {
                    try
                    {
                        var relativePath = entry.FullName.Substring("assets/minecraft/lang/".Length);
                        var outputPath = Path.Combine(langDir, relativePath);
                        
                        using var entryStream = entry.Open();
                        using var outputStream = File.Create(outputPath);
                        await entryStream.CopyToAsync(outputStream);
                        extractedCount++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Error extracting {entry.FullName}: {ex.Message}");
                    }
                }

                // Extract model files
                foreach (var entry in modelEntries)
                {
                    try
                    {
                        var relativePath = entry.FullName.Substring("assets/minecraft/models/".Length);
                        var outputPath = Path.Combine(modelsDir, relativePath);
                        var outputDir = Path.GetDirectoryName(outputPath);
                        
                        if (!string.IsNullOrEmpty(outputDir))
                        {
                            Directory.CreateDirectory(outputDir);
                        }

                        using var entryStream = entry.Open();
                        using var outputStream = File.Create(outputPath);
                        await entryStream.CopyToAsync(outputStream);
                        extractedCount++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Error extracting {entry.FullName}: {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Successfully extracted {extractedCount} asset files");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Error extracting assets from JAR: {ex}");
                throw;
            }
        }

        private string CalculateSha1(string filePath)
        {
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha1.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public string GetAssetsPath(string minecraftVersion)
        {
            return Path.Combine(_cacheDirectory, minecraftVersion, "textures");
        }

        public bool AreAssetsAvailable(string minecraftVersion)
        {
            var assetsPath = GetAssetsPath(minecraftVersion);
            var isAvailable = Directory.Exists(assetsPath) && Directory.GetFiles(assetsPath, "*.png", SearchOption.AllDirectories).Length > 0;
            System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Assets available for {minecraftVersion}: {isAvailable}");
            return isAvailable;
        }

        public List<string> GetAvailableVersions()
        {
            if (!Directory.Exists(_cacheDirectory)) return new List<string>();

            return Directory.GetDirectories(_cacheDirectory)
                .Select(Path.GetFileName)
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList()!
                .Select(v => v.Replace(" ", "")) // Trim spaces
                .ToList();
        }

        public string GetLanguageAssetsPath(string minecraftVersion)
        {
            return Path.Combine(_cacheDirectory, minecraftVersion, "lang");
        }

        public string GetModelsAssetsPath(string minecraftVersion)
        {
            return Path.Combine(_cacheDirectory, minecraftVersion, "models");
        }

        public bool AreLanguageAssetsAvailable(string minecraftVersion)
        {
            var langPath = GetLanguageAssetsPath(minecraftVersion);
            var isAvailable = Directory.Exists(langPath) && Directory.GetFiles(langPath, "*.json", SearchOption.TopDirectoryOnly).Length > 0;
            System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Language assets available for {minecraftVersion}: {isAvailable}");
            return isAvailable;
        }

        public bool AreModelsAssetsAvailable(string minecraftVersion)
        {
            var modelsPath = GetModelsAssetsPath(minecraftVersion);
            var isAvailable = Directory.Exists(modelsPath) && Directory.GetFiles(modelsPath, "*.json", SearchOption.AllDirectories).Length > 0;
            System.Diagnostics.Debug.WriteLine($"MinecraftAssetExtractor: Models assets available for {minecraftVersion}: {isAvailable}");
            return isAvailable;
        }
    }

    // Data models for Minecraft version manifest
    public class MinecraftMainManifest
    {
        [JsonPropertyName("versions")]
        public MinecraftVersionInfo[]? Versions { get; set; }
    }

    public class MinecraftVersionInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
        
        [JsonPropertyName("url")]
        public string Url { get; set; } = "";
    }

    public class MinecraftVersionManifest
    {
        [JsonPropertyName("downloads")]
        public MinecraftDownloads? Downloads { get; set; }
    }

    public class MinecraftDownloads
    {
        [JsonPropertyName("client")]
        public MinecraftDownloadInfo? Client { get; set; }
    }

    public class MinecraftDownloadInfo
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = "";
        
        [JsonPropertyName("sha1")]
        public string Sha1 { get; set; } = "";
        
        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
}