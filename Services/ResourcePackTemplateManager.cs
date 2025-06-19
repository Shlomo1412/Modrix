using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Modrix.Models;
using Wpf.Ui.Controls;

namespace Modrix.Services
{
    public class ResourcePackTemplateManager
    {
        private static readonly Dictionary<string, int> PackFormats = new()
        {
            {"1.21.5", 22},
            {"1.21.4", 22},
            {"1.20.6", 21},
            {"1.20.4", 20},
            {"1.20.1", 18},
            {"1.19.4", 13},
            {"1.18.2", 9},
            {"1.17.1", 8},
            {"1.16.5", 7},
            {"1.15.2", 6},
            {"1.13.2", 4}
        };

        public async Task FullSetup(ModProjectData data, IProgress<(string Message, int Progress)> progress)
        {
            try
            {
                progress.Report(("Creating resource pack structure...", 10));
                await CreateResourcePackStructure(data.Location);

                progress.Report(("Generating pack metadata...", 30));
                await CreatePackMeta(data);

                progress.Report(("Creating default assets...", 50));
                await CreateAssetsStructure(data);

                progress.Report(("Copying icon...", 70));
                await CopyIconAsync(data);

                if (data.IncludeReadme)
                {
                    progress.Report(("Creating README...", 85));
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

                progress.Report(("Resource pack created!", 100));
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Resource pack setup failed: {ex.Message}", "Error",
                //    MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private async Task CreateResourcePackStructure(string location)
        {
            Directory.CreateDirectory(location);
            Directory.CreateDirectory(Path.Combine(location, "assets"));
            Directory.CreateDirectory(Path.Combine(location, "assets", "minecraft")); // Default namespace
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
            // Create mod-specific namespace
            var namespacePath = Path.Combine(data.Location, "assets", data.ModId);
            Directory.CreateDirectory(namespacePath);

            // Create example texture directory
            var texturesPath = Path.Combine(namespacePath, "textures");
            Directory.CreateDirectory(texturesPath);

            // Create example item texture
            var itemPath = Path.Combine(texturesPath, "item");
            Directory.CreateDirectory(itemPath);

            // Create placeholder file
            await File.WriteAllTextAsync(
                Path.Combine(itemPath, "example_item.png.txt"),
                "Place your custom item textures here");
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

## Installation
1. Copy the entire folder to your resource packs directory:
   - Windows: `%appdata%\.minecraft\resourcepacks`
   - macOS: `~/Library/Application Support/minecraft/resourcepacks`
   - Linux: `~/.minecraft/resourcepacks`

2. Enable the pack in Minecraft's resource pack menu

## Customization Guide
- Place custom textures in `assets/{data.ModId}/textures/`
- Add custom sounds in `assets/{data.ModId}/sounds/`
- Modify block models in `assets/{data.ModId}/models/block/`
- Edit language files in `assets/{data.ModId}/lang/`

## Building Custom Assets
Use tools like:
- [Blockbench](https://www.blockbench.net/) for 3D models
- [Paint.NET](https://www.getpaint.net/) or [GIMP](https://www.gimp.org/) for textures
- [Resource Pack Workbench](https://www.curseforge.com/minecraft/mc-mods/resource-pack-workbench) for in-game editing

## Support
For help with resource pack creation, visit:
- [Minecraft Wiki](https://minecraft.fandom.com/wiki/Resource_Pack)
- [Resource Pack Discord](https://discord.gg/resourcepacks)
";

            await File.WriteAllTextAsync(
                Path.Combine(data.Location, "README.md"),
                readmeContent);
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