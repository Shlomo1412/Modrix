using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Modrix.Models;
using Modrix.ModElements;

namespace Modrix.Services
{
    /// <summary>
    /// Service for exporting and importing shareable .modrix project files
    /// Supports both mod projects and resource pack projects
    /// </summary>
    public class ShareableProjectService
    {
        private const string ModrixFileExtension = ".modrix";
   private const string ManifestFileName = "manifest.json";
        private const string ResourcesFolder = "resources";
     private const string CodeFolder = "code";
private const string ElementsFolder = "elements";
        private const string LanguagesFolder = "languages";
        private const string ResourcePackFolder = "resourcepack";

        // File extensions to include as resources
        private static readonly string[] TextureExtensions = { ".png", ".jpg", ".jpeg", ".gif" };
      private static readonly string[] SoundExtensions = { ".ogg", ".wav", ".mp3" };
    private static readonly string[] ModelExtensions = { ".json" };

        // Known template-generated patterns that should be excluded unless modified
        private static readonly string[] TemplateClassPatterns = {
            "Mod.java$",
            "ModClient.java$",
          "Config.java$",
    "Mixin.java$"
        };

        /// <summary>
        /// Exports a project to a .modrix shareable file
        /// </summary>
   public async Task<string> ExportProjectAsync(
     ModProjectData project,
  string outputPath,
            IProgress<(string Message, int Progress)>? progress = null)
        {
            progress?.Report(("Preparing export...", 0));

   var shareableProject = new ModrixShareableProject
   {
     FormatVersion = "1.1",
           ModrixVersion = GetModrixVersion(),
            CreatedDate = DateTime.UtcNow,
 Metadata = CreateMetadata(project)
         };

            // Check if this is a resource pack
       if (IsResourcePackProject(project))
   {
          return await ExportResourcePackAsync(project, shareableProject, outputPath, progress);
       }

   // Standard mod project export
   progress?.Report(("Collecting mod elements...", 10));
            await CollectModElementsAsync(project, shareableProject);

        progress?.Report(("Collecting resources...", 30));
            await CollectResourcesAsync(project, shareableProject);

 progress?.Report(("Detecting custom code...", 50));
            await CollectCustomCodeSmartAsync(project, shareableProject);

    progress?.Report(("Collecting language files...", 70));
        await CollectLanguageFilesAsync(project, shareableProject);

  progress?.Report(("Creating compressed archive...", 85));
            var filePath = await CreateCompressedArchiveAsync(shareableProject, outputPath, project.Name);

 progress?.Report(("Export complete!", 100));
       return filePath;
        }

     /// <summary>
        /// Exports a resource pack project
        /// </summary>
        private async Task<string> ExportResourcePackAsync(
     ModProjectData project,
            ModrixShareableProject shareableProject,
     string outputPath,
    IProgress<(string Message, int Progress)>? progress)
   {
            progress?.Report(("Collecting resource pack data...", 20));

            var rpData = new ResourcePackShareableData();

    // Read pack.mcmeta
     var packMcmetaPath = Path.Combine(project.Location, "pack.mcmeta");
            if (File.Exists(packMcmetaPath))
     {
       rpData.PackMcmeta = await File.ReadAllTextAsync(packMcmetaPath);
    try
   {
var json = JsonDocument.Parse(rpData.PackMcmeta);
            if (json.RootElement.TryGetProperty("pack", out var pack) &&
          pack.TryGetProperty("pack_format", out var format))
      {
         rpData.PackFormat = format.GetInt32();
 }
           }
      catch { }
            }

            progress?.Report(("Collecting pack icon...", 30));

            // Read pack icon
            var packIconPath = Path.Combine(project.Location, "pack.png");
            if (File.Exists(packIconPath))
            {
      var iconBytes = await File.ReadAllBytesAsync(packIconPath);
       rpData.PackIcon = Convert.ToBase64String(iconBytes);
         }

       progress?.Report(("Collecting texture overrides...", 40));

        // Collect overrides from the overrides directory
   var overridesPath = Path.Combine(project.Location, "overrides");
          if (Directory.Exists(overridesPath))
     {
  // Texture overrides
    var texturesPath = Path.Combine(overridesPath, "textures");
                if (Directory.Exists(texturesPath))
  {
    await CollectResourcePackOverridesAsync(texturesPath, "textures", rpData.TextureOverrides, true);
          }

   progress?.Report(("Collecting model overrides...", 50));

        // Model overrides
                var modelsPath = Path.Combine(overridesPath, "models");
                if (Directory.Exists(modelsPath))
        {
         await CollectResourcePackOverridesAsync(modelsPath, "models", rpData.ModelOverrides, false);
    }

 progress?.Report(("Collecting sound overrides...", 60));

              // Sound overrides
                var soundsPath = Path.Combine(overridesPath, "sounds");
      if (Directory.Exists(soundsPath))
        {
        await CollectResourcePackOverridesAsync(soundsPath, "sounds", rpData.SoundOverrides, true);
                }

           progress?.Report(("Collecting translation overrides...", 70));

       // Translation overrides
  var translationsPath = Path.Combine(overridesPath, "translations");
       if (Directory.Exists(translationsPath))
  {
await CollectResourcePackOverridesAsync(translationsPath, "translations", rpData.TranslationOverrides, false);
        }
            }

            // Also check assets/minecraft for any custom content
 var assetsMinecraftPath = Path.Combine(project.Location, "assets", "minecraft");
  if (Directory.Exists(assetsMinecraftPath))
         {
    progress?.Report(("Collecting custom assets...", 75));
    
      // Textures in assets
 var assetsTexturesPath = Path.Combine(assetsMinecraftPath, "textures");
        if (Directory.Exists(assetsTexturesPath))
     {
         await CollectResourcePackOverridesAsync(assetsTexturesPath, "assets/minecraft/textures", rpData.TextureOverrides, true);
         }

       // Lang files
   var assetsLangPath = Path.Combine(assetsMinecraftPath, "lang");
                if (Directory.Exists(assetsLangPath))
        {
  foreach (var file in Directory.GetFiles(assetsLangPath, "*.json"))
               {
       var content = await File.ReadAllTextAsync(file);
  var langCode = Path.GetFileNameWithoutExtension(file);
   shareableProject.Languages.Add(new LanguageFile
  {
      LanguageCode = langCode,
     Content = content
         });
         }
         }
     }

      shareableProject.ResourcePackData = rpData;

            progress?.Report(("Creating compressed archive...", 85));
    var filePath = await CreateCompressedArchiveAsync(shareableProject, outputPath, project.Name);

      progress?.Report(("Export complete!", 100));
            return filePath;
        }

        private async Task CollectResourcePackOverridesAsync(
          string directory,
string category,
            List<ResourcePackOverride> overrides,
            bool isBinary)
{
         if (!Directory.Exists(directory)) return;

  foreach (var file in Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories))
   {
           try
      {
   var relativePath = Path.GetRelativePath(directory, file).Replace('\\', '/');
     var subCategory = GetOverrideCategory(relativePath);

            if (isBinary)
    {
       var bytes = await File.ReadAllBytesAsync(file);
     overrides.Add(new ResourcePackOverride
   {
   RelativePath = relativePath,
        Category = subCategory,
        Content = Convert.ToBase64String(bytes),
  IsBase64 = true,
     OriginalSize = bytes.Length
                });
   }
        else
   {
          var content = await File.ReadAllTextAsync(file);
     overrides.Add(new ResourcePackOverride
      {
       RelativePath = relativePath,
     Category = subCategory,
      Content = content,
       IsBase64 = false,
        OriginalSize = content.Length
       });
    }
              }
   catch
      {
   // Skip files that can't be read
                }
            }
   }

        private string GetOverrideCategory(string relativePath)
        {
   var parts = relativePath.Split('/', '\\');
            if (parts.Length > 0)
      {
         return parts[0] switch
         {
          "block" => "Block",
     "item" => "Item",
       "entity" => "Entity",
               "gui" => "GUI",
      "models" => "Model",
        _ => "Other"
         };
      }
 return "Other";
        }

    private bool IsResourcePackProject(ModProjectData project)
        {
     return project.ModType?.Contains("Resource Pack", StringComparison.OrdinalIgnoreCase) == true;
        }

        /// <summary>
        /// Imports a .modrix shareable file and creates a new project
        /// </summary>
        public async Task<ModProjectData> ImportProjectAsync(
          string modrixFilePath,
         string projectsBaseDirectory,
IProgress<(string Message, int Progress)>? progress = null)
        {
            progress?.Report(("Reading archive...", 0));

            var shareableProject = await ReadCompressedArchiveAsync(modrixFilePath);

            progress?.Report(("Validating project data...", 10));
     ValidateShareableProject(shareableProject);

     // Check if this is a resource pack
       if (shareableProject.IsResourcePack)
            {
    progress?.Report(("Creating resource pack structure...", 20));
     return await ImportResourcePackAsync(shareableProject, projectsBaseDirectory, progress);
   }

    progress?.Report(("Creating project structure...", 20));
       var project = await CreateProjectFromShareableAsync(shareableProject, projectsBaseDirectory, progress);

        progress?.Report(("Import complete!", 100));
            return project;
     }

      /// <summary>
        /// Imports a resource pack from shareable data
        /// </summary>
        private async Task<ModProjectData> ImportResourcePackAsync(
        ModrixShareableProject shareable,
            string projectsBaseDirectory,
            IProgress<(string Message, int Progress)>? progress)
        {
      var metadata = shareable.Metadata;

          // Create unique project directory
            var projectDir = Path.Combine(projectsBaseDirectory, metadata.ModId);
  var counter = 1;
            while (Directory.Exists(projectDir))
       {
     projectDir = Path.Combine(projectsBaseDirectory, $"{metadata.ModId}_{counter}");
     counter++;
 }

    Directory.CreateDirectory(projectDir);

            var project = new ModProjectData
 {
        Name = metadata.Name,
           ModId = metadata.ModId,
     Package = metadata.Package,
                Location = projectDir,
              ModType = metadata.ModType,
      MinecraftVersion = metadata.MinecraftVersion,
    Description = metadata.Description,
              Authors = metadata.Authors,
           License = metadata.License,
      ModVersion = metadata.ModVersion,
      IncludeReadme = metadata.IncludeReadme,
      ProjectDir = projectDir
       };

progress?.Report(("Creating resource pack structure...", 30));

    // Create basic structure
            Directory.CreateDirectory(Path.Combine(projectDir, "assets", "minecraft", "textures"));
            Directory.CreateDirectory(Path.Combine(projectDir, "assets", "minecraft", "lang"));
     Directory.CreateDirectory(Path.Combine(projectDir, "overrides", "textures"));
            Directory.CreateDirectory(Path.Combine(projectDir, "overrides", "models"));
      Directory.CreateDirectory(Path.Combine(projectDir, "overrides", "sounds"));
          Directory.CreateDirectory(Path.Combine(projectDir, "overrides", "translations"));

     var rpData = shareable.ResourcePackData;

     if (rpData != null)
       {
    progress?.Report(("Restoring pack metadata...", 40));

           // Restore pack.mcmeta
           if (!string.IsNullOrEmpty(rpData.PackMcmeta))
    {
          await File.WriteAllTextAsync(Path.Combine(projectDir, "pack.mcmeta"), rpData.PackMcmeta);
}
      else
         {
         // Create default pack.mcmeta
       var packMcmeta = $@"{{
 ""pack"": {{
   ""pack_format"": {rpData.PackFormat},
    ""description"": ""{EscapeJsonString(metadata.Description ?? "Imported resource pack")}""
    }}
}}";
          await File.WriteAllTextAsync(Path.Combine(projectDir, "pack.mcmeta"), packMcmeta);
        }

              // Restore pack icon
       if (!string.IsNullOrEmpty(rpData.PackIcon))
     {
         var iconBytes = Convert.FromBase64String(rpData.PackIcon);
           await File.WriteAllBytesAsync(Path.Combine(projectDir, "pack.png"), iconBytes);
     }

     progress?.Report(("Restoring texture overrides...", 50));

  // Restore texture overrides
           await RestoreResourcePackOverridesAsync(rpData.TextureOverrides, Path.Combine(projectDir, "overrides", "textures"));

         progress?.Report(("Restoring model overrides...", 60));

    // Restore model overrides
              await RestoreResourcePackOverridesAsync(rpData.ModelOverrides, Path.Combine(projectDir, "overrides", "models"));

 progress?.Report(("Restoring sound overrides...", 70));

   // Restore sound overrides
    await RestoreResourcePackOverridesAsync(rpData.SoundOverrides, Path.Combine(projectDir, "overrides", "sounds"));

          progress?.Report(("Restoring translation overrides...", 80));

    // Restore translation overrides
       await RestoreResourcePackOverridesAsync(rpData.TranslationOverrides, Path.Combine(projectDir, "overrides", "translations"));
            }

            progress?.Report(("Restoring language files...", 85));

            // Restore language files
    var langDir = Path.Combine(projectDir, "assets", "minecraft", "lang");
      foreach (var lang in shareable.Languages)
       {
        try
                {
       var langPath = Path.Combine(langDir, $"{lang.LanguageCode}.json");
         await File.WriteAllTextAsync(langPath, lang.Content);
      }
      catch { }
            }

            progress?.Report(("Saving configuration...", 90));

            // Save modrix.config
     await File.WriteAllTextAsync(
         Path.Combine(projectDir, "modrix.config"),
        $"ModId={metadata.ModId}\n" +
  $"Name={metadata.Name}\n" +
              $"MinecraftVersion={metadata.MinecraftVersion}\n" +
                $"ModType=Resource Pack\n" +
    $"IconPath=pack.png");

            progress?.Report(("Import complete!", 100));

            return project;
        }

   private async Task RestoreResourcePackOverridesAsync(List<ResourcePackOverride> overrides, string targetDir)
   {
         foreach (var ovr in overrides)
         {
       try
       {
       var targetPath = Path.Combine(targetDir, ovr.RelativePath);
 Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            if (ovr.IsBase64)
       {
 var bytes = Convert.FromBase64String(ovr.Content);
     await File.WriteAllBytesAsync(targetPath, bytes);
          }
        else
             {
         await File.WriteAllTextAsync(targetPath, ovr.Content);
           }
           }
       catch
            {
         // Skip files that can't be restored
        }
            }
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

        /// <summary>
        /// Gets information about a .modrix file without fully importing it
        /// </summary>
        public async Task<ModrixShareableProject> GetProjectInfoAsync(string modrixFilePath)
        {
   return await ReadCompressedArchiveAsync(modrixFilePath);
        }

        private string GetModrixVersion()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
            return version?.ToString() ?? "1.0.0";
        }

        private ShareableProjectMetadata CreateMetadata(ModProjectData project)
  {
   return new ShareableProjectMetadata
            {
           Name = project.Name,
      ModId = project.ModId,
         Package = project.Package,
        ModType = project.ModType,
  MinecraftVersion = project.MinecraftVersion,
       Description = project.Description,
                Authors = project.Authors,
    License = project.License,
  ModVersion = project.ModVersion,
     IncludeReadme = project.IncludeReadme
            };
     }

        private async Task CollectModElementsAsync(ModProjectData project, ModrixShareableProject shareable)
    {
         var elementsDir = Path.Combine(project.Location, "modrix", "elements");
            if (!Directory.Exists(elementsDir)) return;

      foreach (var file in Directory.GetFiles(elementsDir, "*.json"))
            {
try
           {
 var json = await File.ReadAllTextAsync(file);

         var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var element = JsonSerializer.Deserialize<ModElementData>(json, options);

           if (element != null)
{
               shareable.ModElements.Add(new ShareableModElement
{
              Id = element.Id,
     Type = element.Type,
         Name = element.Name,
        Description = element.Description,
        Data = json
         });
           }
         else
     {
  using var doc = JsonDocument.Parse(json);
     var root = doc.RootElement;

    shareable.ModElements.Add(new ShareableModElement
     {
Id = root.TryGetProperty("Id", out var idProp) ? idProp.GetString() ?? Path.GetFileNameWithoutExtension(file) : Path.GetFileNameWithoutExtension(file),
Type = root.TryGetProperty("Type", out var typeProp) ? typeProp.GetString() ?? "unknown" : "unknown",
       Name = root.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() ?? "" : "",
Description = root.TryGetProperty("Description", out var descProp) ? descProp.GetString() ?? "" : "",
       Data = json
  });
        }
  }
      catch
        {
         try
           {
      var json2 = await File.ReadAllTextAsync(file);
        using var doc = JsonDocument.Parse(json2);
      var root = doc.RootElement;

                shareable.ModElements.Add(new ShareableModElement
   {
               Id = root.TryGetProperty("Id", out var idProp) ? idProp.GetString() ?? Path.GetFileNameWithoutExtension(file) : Path.GetFileNameWithoutExtension(file),
     Type = root.TryGetProperty("Type", out var typeProp) ? typeProp.GetString() ?? "unknown" : "unknown",
  Name = root.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() ?? "" : "",
     Description = root.TryGetProperty("Description", out var descProp) ? descProp.GetString() ?? "" : "",
        Data = json2
       });
                    }
  catch
   {
           // Skip truly invalid files
  }
             }
            }
     }

        private async Task CollectResourcesAsync(ModProjectData project, ModrixShareableProject shareable)
        {
  var assetsDir = Path.Combine(project.Location, "src", "main", "resources", "assets", project.ModId);
            if (!Directory.Exists(assetsDir)) return;

         var texturesDir = Path.Combine(assetsDir, "textures");
      if (Directory.Exists(texturesDir))
    {
       await CollectResourceFilesAsync(texturesDir, assetsDir, "texture", shareable.Resources);
        }

     var soundsDir = Path.Combine(assetsDir, "sounds");
 if (Directory.Exists(soundsDir))
         {
await CollectResourceFilesAsync(soundsDir, assetsDir, "sound", shareable.Resources);
            }

     var modelsDir = Path.Combine(assetsDir, "models");
       if (Directory.Exists(modelsDir))
{
          await CollectResourceFilesAsync(modelsDir, assetsDir, "model", shareable.Resources);
    }

            var iconPath = Path.Combine(project.Location, "src", "main", "resources", "assets", project.ModId, "icon.png");
if (File.Exists(iconPath))
      {
           var bytes = await File.ReadAllBytesAsync(iconPath);
             shareable.Resources.Add(new EmbeddedResource
 {
          RelativePath = "icon.png",
      ResourceType = "icon",
         Content = Convert.ToBase64String(bytes),
          OriginalSize = bytes.Length
      });
            }

            var rootIconPath = Path.Combine(project.Location, "src", "main", "resources", "icon.png");
        if (File.Exists(rootIconPath) && !shareable.Resources.Any(r => r.ResourceType == "icon"))
            {
              var bytes = await File.ReadAllBytesAsync(rootIconPath);
                shareable.Resources.Add(new EmbeddedResource
  {
     RelativePath = "icon.png",
     ResourceType = "icon",
Content = Convert.ToBase64String(bytes),
          OriginalSize = bytes.Length
 });
      }
        }

   private async Task CollectResourceFilesAsync(
  string directory,
       string baseDirectory,
  string resourceType,
        List<EmbeddedResource> resources)
        {
            var extensions = resourceType switch
       {
                "texture" => TextureExtensions,
  "sound" => SoundExtensions,
        "model" => ModelExtensions,
              _ => Array.Empty<string>()
      };

      foreach (var file in Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories))
            {
            var ext = Path.GetExtension(file).ToLowerInvariant();
   if (!extensions.Contains(ext)) continue;

          try
    {
     var bytes = await File.ReadAllBytesAsync(file);
            var relativePath = Path.GetRelativePath(baseDirectory, file);

              resources.Add(new EmbeddedResource
          {
     RelativePath = relativePath.Replace('\\', '/'),
  ResourceType = resourceType,
   Content = Convert.ToBase64String(bytes),
                 OriginalSize = bytes.Length
        });
  }
        catch
                {
         // Skip files that can't be read
           }
       }
        }

        private async Task CollectCustomCodeSmartAsync(ModProjectData project, ModrixShareableProject shareable)
        {
          var manifest = await FileHashTracker.LoadProjectHashesAsync(project.Location);

            var javaDir = Path.Combine(project.Location, "src", "main", "java");
         if (Directory.Exists(javaDir))
      {
        foreach (var file in Directory.GetFiles(javaDir, "*.java", SearchOption.AllDirectories))
    {
     try
           {
   var content = await File.ReadAllTextAsync(file);
          var relativePath = Path.GetRelativePath(project.Location, file);

      var modType = DetermineModificationType(file, content, project, manifest);

              if (modType == CodeModificationType.Unmodified)
      {
          var normalizedPath = relativePath.Replace('\\', '/').ToLowerInvariant();
    bool isModElementGenerated = normalizedPath.Contains("/item/") ||
         normalizedPath.Contains("/block/") ||
       normalizedPath.Contains("/entity/") ||
         normalizedPath.Contains("/init/");

         if (!isModElementGenerated)
        continue;

       modType = CodeModificationType.NewFile;
           }

       shareable.CustomCode.Add(new CustomCodeFile
           {
    RelativePath = relativePath.Replace('\\', '/'),
             Content = content,
    FileType = "java",
        IsCustomFile = modType == CodeModificationType.NewFile,
   ModificationType = modType,
  OriginalTemplatePath = modType == CodeModificationType.Modified
   ? GetTemplatePathForFile(relativePath, project)
          : null
            });
 }
          catch
    {
             // Skip files that can't be read
   }
             }
            }

            var clientJavaDir = Path.Combine(project.Location, "src", "client", "java");
 if (Directory.Exists(clientJavaDir))
   {
    foreach (var file in Directory.GetFiles(clientJavaDir, "*.java", SearchOption.AllDirectories))
    {
         try
           {
        var content = await File.ReadAllTextAsync(file);
     var relativePath = Path.GetRelativePath(project.Location, file);

            var modType = DetermineModificationType(file, content, project, manifest);

      if (modType == CodeModificationType.Unmodified)
            {
     var normalizedPath = relativePath.Replace('\\', '/').ToLowerInvariant();
               bool isModElementGenerated = normalizedPath.Contains("/item/") ||
         normalizedPath.Contains("/block/") ||
  normalizedPath.Contains("/entity/") ||
    normalizedPath.Contains("/init/");

              if (!isModElementGenerated)
             continue;

             modType = CodeModificationType.NewFile;
              }

          shareable.CustomCode.Add(new CustomCodeFile
  {
               RelativePath = relativePath.Replace('\\', '/'),
  Content = content,
           FileType = "java",
                IsCustomFile = modType == CodeModificationType.NewFile,
         ModificationType = modType
   });
   }
           catch
      {
          // Skip files that can't be read
           }
       }
    }

       var resourcesDir = Path.Combine(project.Location, "src", "main", "resources");
       if (Directory.Exists(resourcesDir))
            {
      foreach (var pattern in new[] { "*.mixins.json", "*.accesswidener", "*.aw" })
     {
 foreach (var file in Directory.GetFiles(resourcesDir, pattern, SearchOption.AllDirectories))
    {
try
               {
var content = await File.ReadAllTextAsync(file);
        var relativePath = Path.GetRelativePath(project.Location, file);

            bool isModified = manifest == null || FileHashTracker.IsFileModified(file, project.Location, manifest);

   if (!isModified)
            continue;

     shareable.CustomCode.Add(new CustomCodeFile
       {
    RelativePath = relativePath.Replace('\\', '/'),
      Content = content,
       FileType = Path.GetExtension(file).TrimStart('.'),
       IsCustomFile = manifest != null && FileHashTracker.IsNewFile(file, project.Location, manifest),
             ModificationType = manifest != null && FileHashTracker.IsNewFile(file, project.Location, manifest)
           ? CodeModificationType.NewFile
             : CodeModificationType.Modified
                 });
         }
           catch
          {
  // Skip files that can't be read
  }
     }
                }
    }
        }

        private CodeModificationType DetermineModificationType(
            string filePath,
   string content,
   ModProjectData project,
     FileHashTracker.HashManifest? manifest)
        {
            if (manifest != null)
            {
if (FileHashTracker.IsNewFile(filePath, project.Location, manifest))
 return CodeModificationType.NewFile;

        if (FileHashTracker.IsFileModified(filePath, project.Location, manifest))
      return CodeModificationType.Modified;

       return CodeModificationType.Unmodified;
       }

            return DetermineModificationTypeByHeuristics(filePath, content, project);
        }

        private CodeModificationType DetermineModificationTypeByHeuristics(
        string filePath,
   string content,
            ModProjectData project)
        {
 var fileName = Path.GetFileName(filePath);

   if (ContainsCustomCodeMarkers(content))
      return CodeModificationType.Modified;

 bool isTemplateFile = TemplateClassPatterns.Any(pattern =>
       Regex.IsMatch(fileName, pattern, RegexOptions.IgnoreCase));

    if (!isTemplateFile)
            {
                return CodeModificationType.NewFile;
    }

   if (IsModifiedFromTemplate(content, project))
          return CodeModificationType.Modified;

  return CodeModificationType.Unmodified;
        }

        private bool ContainsCustomCodeMarkers(string content)
        {
            var markers = new[]
            {
                "// Custom code",
       "// User modified",
   "// User added",
  "/* Custom */",
     "// TODO:",
  "// FIXME:",
          "@Custom",
  "// Added by user",
                "// My code",
    "// Custom implementation"
            };

       return markers.Any(marker => content.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsModifiedFromTemplate(string content, ModProjectData project)
      {
            var lines = content.Split('\n');
      var importLines = lines.Where(l => l.Trim().StartsWith("import ")).ToList();
       var customImports = importLines.Where(l =>
                !l.Contains("com.mojang") &&
     !l.Contains("net.minecraft") &&
    !l.Contains("net.minecraftforge") &&
      !l.Contains("net.fabricmc") &&
       !l.Contains("org.slf4j") &&
            !l.Contains(project.Package)
     ).ToList();

        if (customImports.Any())
       return true;

  var addedFunctionalityIndicators = new[]
       {
     "new Block(",
      "new Item(",
      "registry.register(",
             ".register(",
       "event.getRegistry()",
  "CreativeModeTabs.",
            };

foreach (var pattern in addedFunctionalityIndicators)
            {
                if (content.Contains(pattern))
        return true;
    }

            var nonEmptyLines = lines.Count(l => !string.IsNullOrWhiteSpace(l) && !l.Trim().StartsWith("//"));
      if (nonEmptyLines > 100)
  return true;

   return false;
}

      private string? GetTemplatePathForFile(string relativePath, ModProjectData project)
        {
            return relativePath
    .Replace(project.Package.Replace('.', '/'), "com/example")
 .Replace($"{project.ModId}Mod", "ExampleMod")
   .Replace(project.ModId, "modid");
        }

        private async Task CollectLanguageFilesAsync(ModProjectData project, ModrixShareableProject shareable)
        {
      var langDir = Path.Combine(project.Location, "src", "main", "resources", "assets", project.ModId, "lang");
            if (!Directory.Exists(langDir)) return;

      foreach (var file in Directory.GetFiles(langDir, "*.json"))
          {
          try
     {
       var content = await File.ReadAllTextAsync(file);
   var langCode = Path.GetFileNameWithoutExtension(file);

      shareable.Languages.Add(new LanguageFile
    {
      LanguageCode = langCode,
    Content = content
        });
          }
  catch
       {
        // Skip files that can't be read
      }
     }
        }

        private async Task<string> CreateCompressedArchiveAsync(
     ModrixShareableProject shareable,
            string outputDirectory,
    string projectName)
        {
       var safeName = string.Join("_", projectName.Split(Path.GetInvalidFileNameChars()));
    var fileName = $"{safeName}{ModrixFileExtension}";
        var filePath = Path.Combine(outputDirectory, fileName);

            var counter = 1;
            while (File.Exists(filePath))
          {
       fileName = $"{safeName}_{counter}{ModrixFileExtension}";
      filePath = Path.Combine(outputDirectory, fileName);
             counter++;
   }

  var tempDir = Path.Combine(Path.GetTempPath(), $"modrix_export_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

  try
 {
                var manifestPath = Path.Combine(tempDir, ManifestFileName);
     var options = new JsonSerializerOptions { WriteIndented = true };

  var manifest = new
       {
    shareable.FormatVersion,
         shareable.ModrixVersion,
           shareable.CreatedDate,
     shareable.Metadata,
       IsResourcePack = shareable.IsResourcePack,
       ModElementsCount = shareable.ModElements.Count,
      ResourcesCount = shareable.Resources.Count,
                 CustomCodeCount = shareable.CustomCode.Count,
   LanguagesCount = shareable.Languages.Count,
    ResourcePackOverrides = shareable.ResourcePackData != null ? new
               {
             Textures = shareable.ResourcePackData.TextureOverrides.Count,
 Models = shareable.ResourcePackData.ModelOverrides.Count,
Sounds = shareable.ResourcePackData.SoundOverrides.Count,
          Translations = shareable.ResourcePackData.TranslationOverrides.Count
    } : null,
        CustomCodeSummary = new
         {
        NewFiles = shareable.CustomCode.Count(c => c.ModificationType == CodeModificationType.NewFile),
      ModifiedFiles = shareable.CustomCode.Count(c => c.ModificationType == CodeModificationType.Modified)
             }
   };
                await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, options));

     // Write mod elements (for mod projects)
            if (shareable.ModElements.Any())
                {
         var elementsDir = Path.Combine(tempDir, ElementsFolder);
         Directory.CreateDirectory(elementsDir);
        foreach (var element in shareable.ModElements)
      {
       var elementPath = Path.Combine(elementsDir, $"{element.Id}.json");
      await File.WriteAllTextAsync(elementPath, element.Data);
   }
           }

   // Write resources (for mod projects)
     if (shareable.Resources.Any())
      {
               var resourcesDir = Path.Combine(tempDir, ResourcesFolder);
  foreach (var resource in shareable.Resources)
         {
          var resourcePath = Path.Combine(resourcesDir, resource.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(resourcePath)!);
                 var bytes = Convert.FromBase64String(resource.Content);
 await File.WriteAllBytesAsync(resourcePath, bytes);
        }
            }

        // Write custom code (for mod projects)
     if (shareable.CustomCode.Any())
       {
     var codeDir = Path.Combine(tempDir, CodeFolder);
   Directory.CreateDirectory(codeDir);

      var codeMetaPath = Path.Combine(codeDir, "_metadata.json");
       var codeMetadata = shareable.CustomCode.Select(c => new
      {
              c.RelativePath,
      c.FileType,
      c.IsCustomFile,
 ModificationType = c.ModificationType.ToString(),
 c.OriginalTemplatePath
              });
        await File.WriteAllTextAsync(codeMetaPath, JsonSerializer.Serialize(codeMetadata, options));

  foreach (var code in shareable.CustomCode)
         {
      var codePath = Path.Combine(codeDir, code.RelativePath);
   Directory.CreateDirectory(Path.GetDirectoryName(codePath)!);
  await File.WriteAllTextAsync(codePath, code.Content);
          }
            }

                // Write language files
        if (shareable.Languages.Any())
         {
         var langDir = Path.Combine(tempDir, LanguagesFolder);
  Directory.CreateDirectory(langDir);
         foreach (var lang in shareable.Languages)
     {
    var langPath = Path.Combine(langDir, $"{lang.LanguageCode}.json");
   await File.WriteAllTextAsync(langPath, lang.Content);
  }
    }

  // Write resource pack data
            if (shareable.ResourcePackData != null)
         {
    var rpDir = Path.Combine(tempDir, ResourcePackFolder);
           Directory.CreateDirectory(rpDir);

     // Write resource pack metadata
 var rpMeta = new
           {
   shareable.ResourcePackData.PackFormat,
       shareable.ResourcePackData.PackMcmeta
         };
          await File.WriteAllTextAsync(Path.Combine(rpDir, "metadata.json"), JsonSerializer.Serialize(rpMeta, options));

         // Write pack icon
 if (!string.IsNullOrEmpty(shareable.ResourcePackData.PackIcon))
          {
          var iconBytes = Convert.FromBase64String(shareable.ResourcePackData.PackIcon);
            await File.WriteAllBytesAsync(Path.Combine(rpDir, "pack.png"), iconBytes);
   }

        // Write overrides
             await WriteResourcePackOverridesAsync(shareable.ResourcePackData.TextureOverrides, Path.Combine(rpDir, "textures"));
       await WriteResourcePackOverridesAsync(shareable.ResourcePackData.ModelOverrides, Path.Combine(rpDir, "models"));
          await WriteResourcePackOverridesAsync(shareable.ResourcePackData.SoundOverrides, Path.Combine(rpDir, "sounds"));
     await WriteResourcePackOverridesAsync(shareable.ResourcePackData.TranslationOverrides, Path.Combine(rpDir, "translations"));
   }

           if (File.Exists(filePath))
 File.Delete(filePath);

    ZipFile.CreateFromDirectory(tempDir, filePath, CompressionLevel.Optimal, false);

     return filePath;
            }
          finally
      {
        try
 {
                    Directory.Delete(tempDir, true);
}
           catch
           {
   // Ignore cleanup errors
                }
 }
 }

        private async Task WriteResourcePackOverridesAsync(List<ResourcePackOverride> overrides, string targetDir)
        {
if (!overrides.Any()) return;

            Directory.CreateDirectory(targetDir);
       foreach (var ovr in overrides)
      {
         try
    {
     var targetPath = Path.Combine(targetDir, ovr.RelativePath);
  Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        if (ovr.IsBase64)
          {
            var bytes = Convert.FromBase64String(ovr.Content);
     await File.WriteAllBytesAsync(targetPath, bytes);
          }
        else
            {
      await File.WriteAllTextAsync(targetPath, ovr.Content);
 }
     }
                catch { }
            }
 }

        private async Task<ModrixShareableProject> ReadCompressedArchiveAsync(string filePath)
        {
      var tempDir = Path.Combine(Path.GetTempPath(), $"modrix_import_{Guid.NewGuid():N}");

       try
     {
   ZipFile.ExtractToDirectory(filePath, tempDir);

        var shareable = new ModrixShareableProject();

 var manifestPath = Path.Combine(tempDir, ManifestFileName);
     if (File.Exists(manifestPath))
       {
      var manifestJson = await File.ReadAllTextAsync(manifestPath);
         var manifestDoc = JsonDocument.Parse(manifestJson);
       var root = manifestDoc.RootElement;

   shareable.FormatVersion = root.TryGetProperty("FormatVersion", out var fv) ? fv.GetString() ?? "1.0" : "1.0";
  shareable.ModrixVersion = root.TryGetProperty("ModrixVersion", out var mv) ? mv.GetString() ?? "1.0.0" : "1.0.0";
      shareable.CreatedDate = root.TryGetProperty("CreatedDate", out var cd) ? cd.GetDateTime() : DateTime.UtcNow;

     if (root.TryGetProperty("Metadata", out var metadata))
     {
 shareable.Metadata = JsonSerializer.Deserialize<ShareableProjectMetadata>(metadata.GetRawText()) ?? new();
   }
  }

        // Read mod elements
     var elementsDir = Path.Combine(tempDir, ElementsFolder);
         if (Directory.Exists(elementsDir))
      {
              foreach (var file in Directory.GetFiles(elementsDir, "*.json"))
                {
  var json = await File.ReadAllTextAsync(file);
              try
               {
   var element = JsonSerializer.Deserialize<ModElementData>(json);
           if (element != null)
       {
    shareable.ModElements.Add(new ShareableModElement
        {
            Id = element.Id,
       Type = element.Type,
    Name = element.Name,
 Description = element.Description,
     Data = json
                   });
 }
        }
        catch
        {
       using var doc = JsonDocument.Parse(json);
           var root = doc.RootElement;
        shareable.ModElements.Add(new ShareableModElement
            {
                 Id = root.TryGetProperty("Id", out var idProp) ? idProp.GetString() ?? Path.GetFileNameWithoutExtension(file) : Path.GetFileNameWithoutExtension(file),
    Type = root.TryGetProperty("Type", out var typeProp) ? typeProp.GetString() ?? "unknown" : "unknown",
          Name = root.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() ?? "" : "",
          Description = root.TryGetProperty("Description", out var descProp) ? descProp.GetString() ?? "" : "",
     Data = json
         });
        }
     }
       }

            // Read resources
      var resourcesDir = Path.Combine(tempDir, ResourcesFolder);
  if (Directory.Exists(resourcesDir))
         {
               foreach (var file in Directory.GetFiles(resourcesDir, "*.*", SearchOption.AllDirectories))
        {
             var bytes = await File.ReadAllBytesAsync(file);
           var relativePath = Path.GetRelativePath(resourcesDir, file);
            var resourceType = DetermineResourceType(file);

 shareable.Resources.Add(new EmbeddedResource
       {
     RelativePath = relativePath.Replace('\\', '/'),
  ResourceType = resourceType,
        Content = Convert.ToBase64String(bytes),
        OriginalSize = bytes.Length
           });
        }
         }

  // Read custom code
                var codeDir = Path.Combine(tempDir, CodeFolder);
          if (Directory.Exists(codeDir))
         {
     Dictionary<string, (bool IsCustom, CodeModificationType ModType, string? OriginalPath)>? codeMetadata = null;
       var codeMetaPath = Path.Combine(codeDir, "_metadata.json");
       if (File.Exists(codeMetaPath))
{
           try
  {
         var metaJson = await File.ReadAllTextAsync(codeMetaPath);
       var metaArray = JsonSerializer.Deserialize<JsonElement[]>(metaJson);
         if (metaArray != null)
    {
              codeMetadata = new();
         foreach (var item in metaArray)
  {
      var path = item.GetProperty("RelativePath").GetString() ?? "";
            var isCustom = item.TryGetProperty("IsCustomFile", out var ic) && ic.GetBoolean();
      var modTypeStr = item.TryGetProperty("ModificationType", out var mt) ? mt.GetString() : "Unknown";
  var modType = Enum.TryParse<CodeModificationType>(modTypeStr, out var parsed) ? parsed : CodeModificationType.Unknown;
   var origPath = item.TryGetProperty("OriginalTemplatePath", out var op) ? op.GetString() : null;
           codeMetadata[path] = (isCustom, modType, origPath);
     }
     }
       }
              catch { }
           }

    foreach (var file in Directory.GetFiles(codeDir, "*.*", SearchOption.AllDirectories))
   {
          if (Path.GetFileName(file) == "_metadata.json")
           continue;

         var content = await File.ReadAllTextAsync(file);
      var relativePath = Path.GetRelativePath(codeDir, file).Replace('\\', '/');

                var isCustom = true;
   var modType = CodeModificationType.Unknown;
           string? origPath = null;

               if (codeMetadata?.TryGetValue(relativePath, out var meta) == true)
      {
           isCustom = meta.IsCustom;
        modType = meta.ModType;
origPath = meta.OriginalPath;
         }

  shareable.CustomCode.Add(new CustomCodeFile
    {
            RelativePath = relativePath,
         Content = content,
    FileType = Path.GetExtension(file).TrimStart('.'),
IsCustomFile = isCustom,
           ModificationType = modType,
          OriginalTemplatePath = origPath
    });
        }
         }

                // Read languages
   var langDir = Path.Combine(tempDir, LanguagesFolder);
  if (Directory.Exists(langDir))
     {
        foreach (var file in Directory.GetFiles(langDir, "*.json"))
           {
        var content = await File.ReadAllTextAsync(file);
  var langCode = Path.GetFileNameWithoutExtension(file);

        shareable.Languages.Add(new LanguageFile
         {
             LanguageCode = langCode,
                 Content = content
  });
        }
          }

                // Read resource pack data
     var rpDir = Path.Combine(tempDir, ResourcePackFolder);
      if (Directory.Exists(rpDir))
                {
           var rpData = new ResourcePackShareableData();

        // Read metadata
          var rpMetaPath = Path.Combine(rpDir, "metadata.json");
                    if (File.Exists(rpMetaPath))
          {
     try
  {
         var metaJson = await File.ReadAllTextAsync(rpMetaPath);
       var metaDoc = JsonDocument.Parse(metaJson);
          var root = metaDoc.RootElement;

                rpData.PackFormat = root.TryGetProperty("PackFormat", out var pf) ? pf.GetInt32() : 0;
    rpData.PackMcmeta = root.TryGetProperty("PackMcmeta", out var pm) ? pm.GetString() : null;
           }
            catch { }
    }

 // Read pack icon
    var iconPath = Path.Combine(rpDir, "pack.png");
              if (File.Exists(iconPath))
  {
           var iconBytes = await File.ReadAllBytesAsync(iconPath);
    rpData.PackIcon = Convert.ToBase64String(iconBytes);
         }

                 // Read overrides
        await ReadResourcePackOverridesAsync(Path.Combine(rpDir, "textures"), rpData.TextureOverrides, true);
   await ReadResourcePackOverridesAsync(Path.Combine(rpDir, "models"), rpData.ModelOverrides, false);
    await ReadResourcePackOverridesAsync(Path.Combine(rpDir, "sounds"), rpData.SoundOverrides, true);
      await ReadResourcePackOverridesAsync(Path.Combine(rpDir, "translations"), rpData.TranslationOverrides, false);

      shareable.ResourcePackData = rpData;
          }

           return shareable;
            }
     finally
          {
          try
     {
       Directory.Delete(tempDir, true);
  }
  catch { }
         }
        }

   private async Task ReadResourcePackOverridesAsync(string sourceDir, List<ResourcePackOverride> overrides, bool isBinary)
     {
            if (!Directory.Exists(sourceDir)) return;

    foreach (var file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            {
         try
       {
           var relativePath = Path.GetRelativePath(sourceDir, file).Replace('\\', '/');

   if (isBinary)
        {
  var bytes = await File.ReadAllBytesAsync(file);
           overrides.Add(new ResourcePackOverride
         {
             RelativePath = relativePath,
        Category = GetOverrideCategory(relativePath),
       Content = Convert.ToBase64String(bytes),
         IsBase64 = true,
             OriginalSize = bytes.Length
           });
             }
       else
 {
      var content = await File.ReadAllTextAsync(file);
               overrides.Add(new ResourcePackOverride
     {
            RelativePath = relativePath,
  Category = GetOverrideCategory(relativePath),
           Content = content,
           IsBase64 = false,
           OriginalSize = content.Length
          });
            }
       }
    catch { }
        }
      }

        private string DetermineResourceType(string filePath)
        {
 var ext = Path.GetExtension(filePath).ToLowerInvariant();
var fileName = Path.GetFileName(filePath).ToLowerInvariant();

            if (fileName == "icon.png") return "icon";
    if (TextureExtensions.Contains(ext)) return "texture";
      if (SoundExtensions.Contains(ext)) return "sound";
            if (filePath.Contains("models", StringComparison.OrdinalIgnoreCase)) return "model";
    return "other";
        }

        private void ValidateShareableProject(ModrixShareableProject shareable)
    {
            if (shareable.Metadata == null)
         throw new InvalidDataException("Invalid .modrix file: Missing project metadata");

            if (string.IsNullOrWhiteSpace(shareable.Metadata.Name))
   throw new InvalidDataException("Invalid .modrix file: Project name is required");

  if (string.IsNullOrWhiteSpace(shareable.Metadata.ModId))
        throw new InvalidDataException("Invalid .modrix file: Mod ID is required");

       if (string.IsNullOrWhiteSpace(shareable.Metadata.ModType))
            throw new InvalidDataException("Invalid .modrix file: Mod type is required");

 if (string.IsNullOrWhiteSpace(shareable.Metadata.MinecraftVersion))
  throw new InvalidDataException("Invalid .modrix file: Minecraft version is required");
        }

        private async Task<ModProjectData> CreateProjectFromShareableAsync(
      ModrixShareableProject shareable,
         string projectsBaseDirectory,
     IProgress<(string Message, int Progress)>? progress)
        {
            var metadata = shareable.Metadata;

      var projectDir = Path.Combine(projectsBaseDirectory, metadata.ModId);
       var counter = 1;
     while (Directory.Exists(projectDir))
            {
      projectDir = Path.Combine(projectsBaseDirectory, $"{metadata.ModId}_{counter}");
         counter++;
         }

  Directory.CreateDirectory(projectDir);

    var project = new ModProjectData
            {
  Name = metadata.Name,
       ModId = metadata.ModId,
        Package = metadata.Package,
          Location = projectDir,
                ModType = metadata.ModType,
                MinecraftVersion = metadata.MinecraftVersion,
  Description = metadata.Description,
      Authors = metadata.Authors,
       License = metadata.License,
   ModVersion = metadata.ModVersion,
    IncludeReadme = metadata.IncludeReadme,
    ProjectDir = projectDir
      };

            progress?.Report(("Restoring resources...", 40));
        await RestoreResourcesAsync(shareable, project);

     progress?.Report(("Restoring mod elements...", 60));
     await RestoreModElementsAsync(shareable, project);

progress?.Report(("Restoring language files...", 80));
        await RestoreLanguageFilesAsync(shareable, project);

            return project;
        }

      private async Task RestoreResourcesAsync(ModrixShareableProject shareable, ModProjectData project)
     {
     var assetsDir = Path.Combine(project.Location, "src", "main", "resources", "assets", project.ModId);

            foreach (var resource in shareable.Resources)
 {
          try
        {
   var targetPath = resource.ResourceType == "icon" && resource.RelativePath == "icon.png"
     ? Path.Combine(assetsDir, "icon.png")
        : Path.Combine(assetsDir, resource.RelativePath);

   Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
           var bytes = Convert.FromBase64String(resource.Content);
  await File.WriteAllBytesAsync(targetPath, bytes);
  }
         catch { }
      }
        }

 private async Task RestoreModElementsAsync(ModrixShareableProject shareable, ModProjectData project)
        {
            var elementsDir = Path.Combine(project.Location, "modrix", "elements");
       Directory.CreateDirectory(elementsDir);

      foreach (var element in shareable.ModElements)
  {
      try
                {
        var elementPath = Path.Combine(elementsDir, $"{element.Id}.json");
      await File.WriteAllTextAsync(elementPath, element.Data);
 }
             catch { }
   }
      }

     private async Task RestoreLanguageFilesAsync(ModrixShareableProject shareable, ModProjectData project)
        {
    var langDir = Path.Combine(project.Location, "src", "main", "resources", "assets", project.ModId, "lang");
        Directory.CreateDirectory(langDir);

            foreach (var lang in shareable.Languages)
    {
        try
                {
  var langPath = Path.Combine(langDir, $"{lang.LanguageCode}.json");
await File.WriteAllTextAsync(langPath, lang.Content);
      }
       catch { }
            }
        }

        /// <summary>
        /// Restores custom code files to a project after template generation
        /// </summary>
        public async Task RestoreCustomCodeAsync(ModrixShareableProject shareable, ModProjectData project)
        {
            foreach (var code in shareable.CustomCode)
  {
        try
                {
         var targetPath = Path.Combine(project.Location, code.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
 await File.WriteAllTextAsync(targetPath, code.Content);
    }
    catch { }
         }

   await FileHashTracker.SaveProjectHashesAsync(
        project.Location,
    project.ModId,
    project.ModType,
                project.MinecraftVersion);
        }
    }
}
