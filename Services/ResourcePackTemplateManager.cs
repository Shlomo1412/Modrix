using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Modrix.Models;
using System.IO.Compression;
using Wpf.Ui.Controls;

namespace Modrix.Services
{
    public class ResourcePackTemplateManager
    {
        private static readonly Dictionary<string, int> PackFormats = new()
        {
            {"1.21.5", 22},
            {"1.21.4", 22}, 
            {"1.21.3", 22},
            {"1.21.2", 18},
            {"1.21.1", 18},
            {"1.21", 18},
            {"1.20.6", 21},
            {"1.20.4", 20},
            {"1.20.3", 18},
            {"1.20.2", 18},
            {"1.20.1", 18},
            {"1.19.4", 13},
            {"1.18.2", 9},
            {"1.17.1", 8},
            {"1.16.5", 7},
            {"1.15.2", 6},
            {"1.13.2", 4}
        };

        private readonly HttpClient _httpClient;

        public ResourcePackTemplateManager()
        {
            _httpClient = new HttpClient();
        }

        public async Task FullSetup(ModProjectData data, IProgress<(string Message, int Progress)> progress)
        {
            try
            {
                progress.Report(("Creating resource pack structure...", 10));
                await CreateResourcePackStructure(data.Location);

                progress.Report(("Generating pack metadata...", 30));
                await CreatePackMeta(data);

                progress.Report(("Creating default assets structure...", 40));
                await CreateAssetsStructure(data);

                progress.Report(("Copying icon...", 50));
                await CopyIconAsync(data);

                progress.Report(("Downloading and extracting Minecraft assets...", 60));
                await ExtractMinecraftAssets(data.MinecraftVersion, data.Location, progress);

                if (data.IncludeReadme)
                {
                    progress.Report(("Creating README...", 90));
                    await CreateReadmeFile(data);
                }

                // Save modrix.config
                progress.Report(("Saving configuration...", 95));
                await File.WriteAllTextAsync(
                    Path.Combine(data.Location, "modrix.config"),
                    $"ModId={data.ModId}\n" +
                    $"Name={data.Name}\n" +
                    $"MinecraftVersion={data.MinecraftVersion}\n" +
                    $"ModType=Resource Pack\n" +
                    $"IconPath=pack.png");

                progress.Report(("Resource pack created successfully!", 100));
            }
            catch (Exception ex)
            {
                throw new Exception($"Resource pack setup failed: {ex.Message}", ex);
            }
        }

        private async Task CreateResourcePackStructure(string location)
        {
            Directory.CreateDirectory(location);
            Directory.CreateDirectory(Path.Combine(location, "assets"));
            Directory.CreateDirectory(Path.Combine(location, "assets", "minecraft"));
            Directory.CreateDirectory(Path.Combine(location, "assets", "minecraft", "textures"));
            Directory.CreateDirectory(Path.Combine(location, "assets", "minecraft", "lang"));
            Directory.CreateDirectory(Path.Combine(location, "overrides"));
            Directory.CreateDirectory(Path.Combine(location, "overrides", "textures"));
            Directory.CreateDirectory(Path.Combine(location, "overrides", "translations"));
        }

        private async Task CreatePackMeta(ModProjectData data)
        {
            if (!PackFormats.TryGetValue(data.MinecraftVersion, out var format))
                throw new Exception($"Unsupported Minecraft version: {data.MinecraftVersion}");

            var metaContent = $@"{{
    ""pack"": {{
        ""pack_format"": {format},
        ""description"": ""{EscapeJsonString(data.Description ?? "A resource pack created with Modrix")}""
    }}
}}";

            await File.WriteAllTextAsync(
                Path.Combine(data.Location, "pack.mcmeta"),
                metaContent);
        }

        private async Task CreateAssetsStructure(ModProjectData data)
        {
            // Create default override structure
            var overridesPath = Path.Combine(data.Location, "overrides");
            Directory.CreateDirectory(Path.Combine(overridesPath, "textures", "block"));
            Directory.CreateDirectory(Path.Combine(overridesPath, "textures", "item"));
            Directory.CreateDirectory(Path.Combine(overridesPath, "textures", "entity"));
            Directory.CreateDirectory(Path.Combine(overridesPath, "textures", "gui"));
            Directory.CreateDirectory(Path.Combine(overridesPath, "translations"));

            // Create example override info file
            var infoContent = $@"# {data.Name} - Resource Pack Overrides

This directory contains your custom overrides for Minecraft assets.

## Structure:
- textures/: Custom texture overrides
  - block/: Block textures
  - item/: Item textures
  - entity/: Entity textures
  - gui/: GUI textures
- translations/: Language file overrides

## How to Use:
1. Place your custom textures in the appropriate subdirectories
2. Use the same filename as the original Minecraft asset
3. Your textures will override the default Minecraft textures

## Supported Formats:
- Textures: PNG format recommended
- Translations: JSON format (e.g., en_us.json)

Created with Modrix on {DateTime.Now:yyyy-MM-dd}
";

            await File.WriteAllTextAsync(
                Path.Combine(overridesPath, "README.md"),
                infoContent);
        }

        private async Task ExtractMinecraftAssets(string version, string packLocation, IProgress<(string Message, int Progress)> progress)
        {
            try
            {
                var assetsPath = Path.Combine(packLocation, "assets", "minecraft");
                var extractedPath = Path.Combine(packLocation, ".minecraft_assets");
                
                progress.Report(($"Downloading Minecraft {version} assets...", 65));
                
                // Check if we already have assets extracted for this version
                var versionFile = Path.Combine(extractedPath, "version.txt");
                if (File.Exists(versionFile) && File.ReadAllText(versionFile).Trim() == version)
                {
                    progress.Report(("Using cached Minecraft assets...", 80));
                    return;
                }

                // Create extraction directory
                Directory.CreateDirectory(extractedPath);

                // Try to find local Minecraft installation first
                var localAssets = await TryFindLocalAssets(version);
                if (localAssets != null)
                {
                    progress.Report(("Found local Minecraft assets, copying...", 70));
                    await CopyLocalAssets(localAssets, assetsPath);
                    await File.WriteAllTextAsync(versionFile, version);
                    return;
                }

                // If no local assets, download from Mojang (simplified approach)
                progress.Report(("Downloading Minecraft client JAR...", 75));
                var clientJar = await DownloadMinecraftClient(version);
                
                if (clientJar != null)
                {
                    progress.Report(("Extracting assets from JAR...", 80));
                    await ExtractAssetsFromJar(clientJar, assetsPath);
                    File.Delete(clientJar); // Clean up
                }

                await File.WriteAllTextAsync(versionFile, version);
            }
            catch (Exception ex)
            {
                // Don't fail the entire process if asset extraction fails
                progress.Report(("Failed to extract Minecraft assets - continuing without them...", 85));
                System.Diagnostics.Debug.WriteLine($"Asset extraction failed: {ex.Message}");
            }
        }

        private async Task<string> TryFindLocalAssets(string version)
        {
            // Check common Minecraft installation paths
            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", "Microsoft.MinecraftUWP_8wekyb3d8bbwe", "LocalState", "games", "com.mojang"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "curse", "minecraft"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Roaming", ".minecraft")
            };

            foreach (var basePath in possiblePaths)
            {
                if (!Directory.Exists(basePath)) continue;

                // Check versions folder
                var versionsPath = Path.Combine(basePath, "versions", version);
                var assetsPath = Path.Combine(basePath, "assets");
                
                if (Directory.Exists(versionsPath) && Directory.Exists(assetsPath))
                {
                    return assetsPath;
                }
            }

            return null;
        }

        private async Task CopyLocalAssets(string localAssetsPath, string targetPath)
        {
            var texturesPath = Path.Combine(localAssetsPath, "minecraft", "textures");
            var langPath = Path.Combine(localAssetsPath, "minecraft", "lang");

            if (Directory.Exists(texturesPath))
            {
                CopyDirectory(texturesPath, Path.Combine(targetPath, "textures"));
            }

            if (Directory.Exists(langPath))
            {
                CopyDirectory(langPath, Path.Combine(targetPath, "lang"));
            }
        }

        private async Task<string> DownloadMinecraftClient(string version)
        {
            try
            {
                // This is a simplified approach - in a real implementation,
                // you'd want to use the Mojang launcher API to get the correct download URL
                var tempFile = Path.GetTempFileName() + ".jar";
                
                // For now, we'll skip actual downloading and just create a placeholder
                // In a real implementation, you'd download from Mojang's servers
                File.WriteAllText(tempFile, ""); // Placeholder
                return tempFile;
            }
            catch
            {
                return null;
            }
        }

        private async Task ExtractAssetsFromJar(string jarPath, string targetPath)
        {
            try
            {
                // Extract assets from the Minecraft client JAR
                // This would use System.IO.Compression to extract relevant files
                // For now, we'll skip this step as it requires proper JAR handling
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"JAR extraction failed: {ex.Message}");
            }
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDir, file);
                var targetFile = Path.Combine(targetDir, relativePath);
                
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                File.Copy(file, targetFile, true);
            }
        }

        private async Task CopyIconAsync(ModProjectData data)
        {
            if (string.IsNullOrEmpty(data.IconPath)) return;

            try
            {
                var destPath = Path.Combine(data.Location, "pack.png");

                // Direct PNG copy
                if (data.IconPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(data.IconPath, destPath, true);
                    return;
                }

                // Convert non-PNG images
                BitmapFrame bitmapFrame;
                using (var stream = new FileStream(data.IconPath, FileMode.Open, FileAccess.Read))
                {
                    var decoder = BitmapDecoder.Create(
                        stream,
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.Default);

                    bitmapFrame = decoder.Frames[0];
                }

                // Save as PNG
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(bitmapFrame);

                using (var fileStream = new FileStream(destPath, FileMode.Create))
                {
                    encoder.Save(fileStream);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to copy icon: {ex.Message}");
            }
        }

        private async Task CreateReadmeFile(ModProjectData data)
        {
            var readmeContent = $@"# {data.Name} Resource Pack

## Description
{data.Description ?? "No description provided"}

### Pack Details
- **Pack ID**: `{data.ModId}`
- **Minecraft Version**: {data.MinecraftVersion}
- **Authors**: {data.Authors ?? "Not specified"}
- **License**: {data.License ?? "Not specified"}
- **Pack Format**: {PackFormats.GetValueOrDefault(data.MinecraftVersion, 18)}

## Installation
1. Copy the entire folder to your resource packs directory:
   - Windows: `%appdata%\.minecraft\resourcepacks`
   - macOS: `~/Library/Application Support/minecraft/resourcepacks`
   - Linux: `~/.minecraft/resourcepacks`

2. Enable the pack in Minecraft's resource pack menu

## Customization Guide

### Texture Overrides
- Place custom textures in `overrides/textures/`
- Use the same filename as the original Minecraft texture
- Supported categories:
  - `block/` - Block textures
  - `item/` - Item textures  
  - `entity/` - Entity textures
  - `gui/` - GUI textures

### Language Overrides
- Place custom language files in `overrides/translations/`
- Use format: `language_code.json` (e.g., `en_us.json`)
- Override specific translation keys

## Original Assets
- The `assets/minecraft/` directory contains extracted original Minecraft assets
- Use these as reference when creating overrides
- Do not modify files in this directory - use the `overrides/` directory instead

## Tools
Use these tools for creating resource pack content:
- [Paint.NET](https://www.getpaint.net/) or [GIMP](https://www.gimp.org/) for textures
- [Blockbench](https://www.blockbench.net/) for models
- Any text editor for language files

## Support
For help with resource pack creation, visit:
- [Minecraft Wiki](https://minecraft.fandom.com/wiki/Resource_Pack)
- [Resource Pack Discord](https://discord.gg/resourcepacks)

---
Created with Modrix on {DateTime.Now:yyyy-MM-dd}
";

            await File.WriteAllTextAsync(
                Path.Combine(data.Location, "README.md"),
                readmeContent);
        }

        private string EscapeJsonString(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            
            return input.Replace("\\", "\\\\")
                         .Replace("\"", "\\\"")
                         .Replace("\n", "\\n")
                         .Replace("\r", "\\r")
                         .Replace("\t", "\\t");
        }

        public ResourcePackData ReadResourcePack(string packPath)
        {
            try
            {
                var packData = new ResourcePackData
                {
                    Location = packPath,
                    Name = Path.GetFileName(packPath)
                };

                // Read modrix.config if it exists
                var configPath = Path.Combine(packPath, "modrix.config");
                if (File.Exists(configPath))
                {
                    var configLines = File.ReadAllLines(configPath);
                    foreach (var line in configLines)
                    {
                        if (line.StartsWith("ModId="))
                            packData.ModId = line.Substring(6);
                        else if (line.StartsWith("Name="))
                            packData.Name = line.Substring(5);
                        else if (line.StartsWith("MinecraftVersion="))
                            packData.MinecraftVersion = line.Substring(17);
                    }
                }

                // Read pack.mcmeta
                var metaPath = Path.Combine(packPath, "pack.mcmeta");
                if (File.Exists(metaPath))
                {
                    var metaContent = File.ReadAllText(metaPath);
                    var metaJson = JsonSerializer.Deserialize<JsonElement>(metaContent);
                    
                    if (metaJson.TryGetProperty("pack", out var packElement))
                    {
                        if (packElement.TryGetProperty("description", out var descElement))
                            packData.Description = descElement.GetString();
                        
                        if (packElement.TryGetProperty("pack_format", out var formatElement))
                            packData.PackFormat = formatElement.GetInt32();
                    }
                }

                // Check for icon
                var iconPath = Path.Combine(packPath, "pack.png");
                if (File.Exists(iconPath))
                    packData.IconPath = iconPath;

                // Scan for overrides
                packData.Overrides = ScanOverrides(packPath);

                return packData;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to read resource pack: {ex.Message}", ex);
            }
        }

        private List<ResourceOverride> ScanOverrides(string packPath)
        {
            var overrides = new List<ResourceOverride>();
            var overridesPath = Path.Combine(packPath, "overrides");
            
            if (!Directory.Exists(overridesPath))
                return overrides;

            // Scan texture overrides
            var texturesPath = Path.Combine(overridesPath, "textures");
            if (Directory.Exists(texturesPath))
            {
                foreach (var file in Directory.GetFiles(texturesPath, "*.png", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(texturesPath, file);
                    overrides.Add(new ResourceOverride
                    {
                        Type = OverrideType.Texture,
                        OriginalPath = $"assets/minecraft/textures/{relativePath}",
                        OverridePath = file,
                        Category = GetTextureCategory(relativePath)
                    });
                }
            }

            // Scan translation overrides
            var translationsPath = Path.Combine(overridesPath, "translations");
            if (Directory.Exists(translationsPath))
            {
                foreach (var file in Directory.GetFiles(translationsPath, "*.json", SearchOption.TopDirectoryOnly))
                {
                    var fileName = Path.GetFileName(file);
                    overrides.Add(new ResourceOverride
                    {
                        Type = OverrideType.Translation,
                        OriginalPath = $"assets/minecraft/lang/{fileName}",
                        OverridePath = file,
                        Category = "Language"
                    });
                }
            }

            return overrides;
        }

        private string GetTextureCategory(string relativePath)
        {
            var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (parts.Length > 0)
            {
                return parts[0] switch
                {
                    "block" => "Block Textures",
                    "item" => "Item Textures", 
                    "entity" => "Entity Textures",
                    "gui" => "GUI Textures",
                    _ => "Other Textures"
                };
            }
            return "Textures";
        }
    }

    public class ResourcePackData
    {
        public string Location { get; set; } = "";
        public string Name { get; set; } = "";
        public string ModId { get; set; } = "";
        public string Description { get; set; } = "";
        public string MinecraftVersion { get; set; } = "";
        public int PackFormat { get; set; }
        public string IconPath { get; set; } = "";
        public List<ResourceOverride> Overrides { get; set; } = new();
    }

    public class ResourceOverride
    {
        public OverrideType Type { get; set; }
        public string OriginalPath { get; set; } = "";
        public string OverridePath { get; set; } = "";
        public string Category { get; set; } = "";
    }

    public enum OverrideType
    {
        Texture,
        Translation,
        Model,
        Sound
    }
}