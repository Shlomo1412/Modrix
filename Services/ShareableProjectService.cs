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
    /// </summary>
    public class ShareableProjectService
    {
   private const string ModrixFileExtension = ".modrix";
        private const string ManifestFileName = "manifest.json";
        private const string ResourcesFolder = "resources";
        private const string CodeFolder = "code";
   private const string ElementsFolder = "elements";
  private const string LanguagesFolder = "languages";

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

            progress?.Report(("Creating project structure...", 20));
  var project = await CreateProjectFromShareableAsync(shareableProject, projectsBaseDirectory, progress);

 progress?.Report(("Import complete!", 100));
  return project;
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

    // Try to deserialize with polymorphic options
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
          // Parse JSON directly if deserialization returned null
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
      // Try to store raw JSON anyway
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

        /// <summary>
    /// Smart custom code collection that only includes truly modified or new files
        /// </summary>
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

        // Always include files in mod element directories
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
   ModElementsCount = shareable.ModElements.Count,
            ResourcesCount = shareable.Resources.Count,
           CustomCodeCount = shareable.CustomCode.Count,
     LanguagesCount = shareable.Languages.Count,
        CustomCodeSummary = new
 {
  NewFiles = shareable.CustomCode.Count(c => c.ModificationType == CodeModificationType.NewFile),
       ModifiedFiles = shareable.CustomCode.Count(c => c.ModificationType == CodeModificationType.Modified)
}
 };
                await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, options));

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
                    // Store raw JSON
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
  catch
      {
    // Ignore metadata parsing errors
        }
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

 return shareable;
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
                throw new InvalidDataException("Invalid .modrix file: Mod type (Forge/Fabric) is required");

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
           catch
          {
               // Skip resources that can't be restored
  }
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
           catch
          {
        // Skip elements that can't be restored
 }
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
          catch
       {
   // Skip language files that can't be restored
           }
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
      catch
         {
          // Skip code files that can't be restored
                }
 }

            await FileHashTracker.SaveProjectHashesAsync(
                project.Location,
     project.ModId,
         project.ModType,
                project.MinecraftVersion);
        }
  }
}
