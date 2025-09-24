using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Net.Http;
using System.Text.Json;
using Modrix.Models;
using Wpf.Ui.Controls;

namespace Modrix.Services
{
    public class ResourcePackTemplateManager
    {
        private static readonly Dictionary<string, int> PackFormats = new()
        {
            {"1.21.5", 34},
            {"1.21.4", 34},
            {"1.21.3", 34},
            {"1.21.2", 34},
            {"1.21.1", 34},
            {"1.21", 34},
            {"1.20.6", 32},
            {"1.20.5", 32},
            {"1.20.4", 32},
            {"1.20.3", 22},
            {"1.20.2", 18},
            {"1.20.1", 18},
            {"1.19.4", 13},
            {"1.19.3", 13},
            {"1.19.2", 13},
            {"1.18.2", 9},
            {"1.18.1", 8},
            {"1.17.1", 8},
            {"1.16.5", 7},
            {"1.15.2", 6},
            {"1.14.4", 5},
            {"1.13.2", 4}
        };

        /// <summary>
        /// Creates a complete ResourcePack project with all necessary files and structure
        /// </summary>
        public async Task FullSetup(ModProjectData data, IProgress<(string Message, int Progress)> progress)
        {
            try
            {
                progress?.Report(("Creating resource pack structure...", 10));
                await CreateResourcePackStructure(data.Location);

                progress?.Report(("Generating pack metadata...", 20));
                await CreatePackMeta(data);

                progress?.Report(("Setting up assets structure...", 30));
                await CreateAssetsStructure(data);

                progress?.Report(("Copying icon...", 50));
                await CopyIconAsync(data);

                if (data.IncludeReadme)
                {
                    progress?.Report(("Creating README...", 70));
                    await CreateReadmeFile(data);
                }

                progress?.Report(("Saving configuration...", 85));
                await SaveProjectConfiguration(data);

                progress?.Report(("Creating default overrides...", 95));
                await CreateDefaultOverrides(data);

                progress?.Report(("Resource pack created successfully!", 100));
            }
            catch (Exception ex)
            {
                throw new Exception($"Resource pack setup failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Reads an existing ResourcePack project and loads its configuration
        /// </summary>
        public async Task<ModProjectData?> ReadResourcePack(string projectPath)
        {
            try
            {
                var configPath = Path.Combine(projectPath, "modrix.config");
                if (!File.Exists(configPath))
                    return null;

                var configContent = await File.ReadAllTextAsync(configPath);
                var config = ParseConfig(configContent);

                // Validate it's a ResourcePack project
                if (!config.TryGetValue("ModType", out var modType) || modType != "Resource Pack")
                    return null;

                var packMetaPath = Path.Combine(projectPath, "pack.mcmeta");
                if (!File.Exists(packMetaPath))
                    return null;

                // Parse pack.mcmeta for additional information
                var packMeta = await File.ReadAllTextAsync(packMetaPath);
                var packInfo = ParsePackMeta(packMeta);

                var projectData = new ModProjectData
                {
                    Name = config.GetValueOrDefault("Name", "Unknown Resource Pack"),
                    ModId = config.GetValueOrDefault("ModId", "unknown"),
                    Location = projectPath,
                    ModType = "Resource Pack",
                    MinecraftVersion = config.GetValueOrDefault("MinecraftVersion", "1.20.1"),
                    Description = packInfo.GetValueOrDefault("description", ""),
                    Authors = config.GetValueOrDefault("Authors", ""),
                    License = config.GetValueOrDefault("License", "All Rights Reserved"),
                    Version = config.GetValueOrDefault("Version", "1.0.0"),
                    IconPath = GetIconPath(projectPath),
                    Package = "", // ResourcePacks don't use packages
                    IncludeReadme = File.Exists(Path.Combine(projectPath, "README.md"))
                };

                return projectData;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to read resource pack at {projectPath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets all available texture overrides for a specific Minecraft version
        /// </summary>
        public async Task<List<string>> GetAvailableTextures(string minecraftVersion)
        {
            // This would ideally fetch from Minecraft assets or a cached database
            // For now, return common texture paths
            return new List<string>
            {
                "block/stone",
                "block/dirt", 
                "block/grass_block_top",
                "block/grass_block_side",
                "block/oak_log",
                "block/oak_log_top",
                "block/oak_planks",
                "item/diamond",
                "item/iron_ingot",
                "item/gold_ingot",
                "item/stick",
                "item/apple",
                "entity/pig/pig",
                "entity/cow/cow",
                "gui/options_background",
                "gui/widgets"
            };
        }

        /// <summary>
        /// Gets all translation keys for a specific Minecraft version
        /// </summary>
        public async Task<Dictionary<string, string>> GetAvailableTranslations(string minecraftVersion, string language = "en_us")
        {
            // This would ideally fetch from Minecraft assets or a cached database
            // For now, return common translation keys
            return new Dictionary<string, string>
            {
                ["block.minecraft.stone"] = "Stone",
                ["block.minecraft.dirt"] = "Dirt",
                ["block.minecraft.grass_block"] = "Grass Block",
                ["item.minecraft.diamond"] = "Diamond",
                ["item.minecraft.iron_ingot"] = "Iron Ingot",
                ["item.minecraft.stick"] = "Stick",
                ["gui.done"] = "Done",
                ["gui.cancel"] = "Cancel",
                ["menu.game"] = "Game Menu",
                ["menu.options"] = "Options",
                ["options.video"] = "Video Settings"
            };
        }

        private async Task CreateResourcePackStructure(string location)
        {
            Directory.CreateDirectory(location);
            Directory.CreateDirectory(Path.Combine(location, "assets"));
            Directory.CreateDirectory(Path.Combine(location, "assets", "minecraft"));
            Directory.CreateDirectory(Path.Combine(location, "assets", "minecraft", "textures"));
            Directory.CreateDirectory(Path.Combine(location, "assets", "minecraft", "textures", "block"));
            Directory.CreateDirectory(Path.Combine(location, "assets", "minecraft", "textures", "item"));
            Directory.CreateDirectory(Path.Combine(location, "assets", "minecraft", "textures", "entity"));
            Directory.CreateDirectory(Path.Combine(location, "assets", "minecraft", "textures", "gui"));
            Directory.CreateDirectory(Path.Combine(location, "assets", "minecraft", "lang"));
            Directory.CreateDirectory(Path.Combine(location, "assets", "minecraft", "models"));
            Directory.CreateDirectory(Path.Combine(location, "assets", "minecraft", "models", "block"));
            Directory.CreateDirectory(Path.Combine(location, "assets", "minecraft", "models", "item"));
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
            // Create example lang file
            var langDir = Path.Combine(data.Location, "assets", "minecraft", "lang");
            Directory.CreateDirectory(langDir);
            
            var exampleLang = @"{
    ""_comment"": ""This is an example language override file"",
    ""block.minecraft.stone"": ""Custom Stone Name"",
    ""item.minecraft.diamond"": ""Shiny Diamond""
}";
            
            await File.WriteAllTextAsync(
                Path.Combine(langDir, "en_us.json"),
                exampleLang);

            // Create example texture placeholder
            var texturesDir = Path.Combine(data.Location, "assets", "minecraft", "textures", "block");
            await File.WriteAllTextAsync(
                Path.Combine(texturesDir, "_example.txt"),
                "Place your custom block textures here. Remove this file when adding actual textures.");

            var itemTexturesDir = Path.Combine(data.Location, "assets", "minecraft", "textures", "item");
            await File.WriteAllTextAsync(
                Path.Combine(itemTexturesDir, "_example.txt"),
                "Place your custom item textures here. Remove this file when adding actual textures.");
        }

        private async Task CopyIconAsync(ModProjectData data)
        {
            if (string.IsNullOrEmpty(data.IconPath) || !File.Exists(data.IconPath)) 
                return;

            try
            {
                var destPath = Path.Combine(data.Location, "pack.png");

                // Direct PNG copy
                if (data.IconPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(data.IconPath, destPath, true);
                    return;
                }

                // Convert non-PNG images to PNG
                BitmapFrame bitmapFrame;
                using (var stream = new FileStream(data.IconPath, FileMode.Open, FileAccess.Read))
                {
                    var decoder = BitmapDecoder.Create(
                        stream,
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.Default);
                    
                    bitmapFrame = decoder.Frames[0];
                }

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(bitmapFrame);

                using (var fileStream = new FileStream(destPath, FileMode.Create))
                {
                    encoder.Save(fileStream);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to copy icon: {ex.Message}", ex);
            }
        }

        private async Task CreateReadmeFile(ModProjectData data)
        {
            var readmeContent = $@"# {data.Name} Resource Pack

## Description
{data.Description ?? "No description provided"}

## Pack Information
- **Pack ID**: `{data.ModId}`
- **Minecraft Version**: {data.MinecraftVersion}
- **Authors**: {data.Authors ?? "Not specified"}
- **License**: {data.License ?? "Not specified"}
- **Version**: {data.Version}

## Installation
1. Download the resource pack
2. Copy the entire folder to your resource packs directory:
   - **Windows**: `%appdata%\.minecraft\resourcepacks`
   - **macOS**: `~/Library/Application Support/minecraft/resourcepacks`
   - **Linux**: `~/.minecraft/resourcepacks`
3. Enable the pack in Minecraft's Resource Packs menu

## Features
This resource pack includes custom overrides for:
- Textures
- Language translations
- Models (if applicable)

## Customization
- **Textures**: Place custom PNG files in `assets/minecraft/textures/`
- **Language**: Edit JSON files in `assets/minecraft/lang/`
- **Models**: Add JSON model files in `assets/minecraft/models/`

## Tools & Resources
- **Texture Editors**: [Paint.NET](https://www.getpaint.net/), [GIMP](https://www.gimp.org/), [Aseprite](https://www.aseprite.org/)
- **Model Editors**: [Blockbench](https://www.blockbench.net/)
- **Documentation**: [Minecraft Wiki Resource Pack Guide](https://minecraft.fandom.com/wiki/Resource_Pack)

## Created with Modrix
This resource pack was created using [Modrix](https://github.com/Shlomo1412/Modrix) - A powerful Minecraft mod development IDE.
";

            await File.WriteAllTextAsync(
                Path.Combine(data.Location, "README.md"),
                readmeContent);
        }

        private async Task SaveProjectConfiguration(ModProjectData data)
        {
            var configContent = $@"ModId={data.ModId}
Name={data.Name}
ModType=Resource Pack
MinecraftVersion={data.MinecraftVersion}
Description={data.Description ?? ""}
Authors={data.Authors ?? ""}
License={data.License ?? "All Rights Reserved"}
Version={data.Version}
IconPath=pack.png
IncludeReadme={data.IncludeReadme}
";

            await File.WriteAllTextAsync(
                Path.Combine(data.Location, "modrix.config"),
                configContent);
        }

        private async Task CreateDefaultOverrides(ModProjectData data)
        {
            // Create overrides tracking file
            var overridesDir = Path.Combine(data.Location, ".modrix");
            Directory.CreateDirectory(overridesDir);

            var overridesInfo = new
            {
                created = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                version = data.MinecraftVersion,
                overrides = new
                {
                    textures = new string[0],
                    translations = new[] { "en_us.json" },
                    models = new string[0]
                }
            };

            await File.WriteAllTextAsync(
                Path.Combine(overridesDir, "overrides.json"),
                JsonSerializer.Serialize(overridesInfo, new JsonSerializerOptions { WriteIndented = true }));
        }

        private Dictionary<string, string> ParseConfig(string configContent)
        {
            var config = new Dictionary<string, string>();
            var lines = configContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    config[parts[0].Trim()] = parts[1].Trim();
                }
            }
            
            return config;
        }

        private Dictionary<string, string> ParsePackMeta(string packMetaContent)
        {
            try
            {
                using var doc = JsonDocument.Parse(packMetaContent);
                var pack = doc.RootElement.GetProperty("pack");
                
                var result = new Dictionary<string, string>();
                if (pack.TryGetProperty("description", out var desc))
                    result["description"] = desc.GetString() ?? "";
                if (pack.TryGetProperty("pack_format", out var format))
                    result["pack_format"] = format.GetInt32().ToString();
                
                return result;
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        private string? GetIconPath(string projectPath)
        {
            var iconPath = Path.Combine(projectPath, "pack.png");
            return File.Exists(iconPath) ? iconPath : null;
        }

        private string EscapeJsonString(string input)
        {
            return input.Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\n", "\\n")
                        .Replace("\r", "\\r")
                        .Replace("\t", "\\t");
        }
    }
}