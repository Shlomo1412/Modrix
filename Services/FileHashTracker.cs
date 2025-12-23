using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Modrix.Services
{
    /// <summary>
    /// Tracks file hashes to detect modifications from template files
    /// </summary>
  public class FileHashTracker
    {
        private const string HashFileName = "modrix.hashes.json";

        /// <summary>
        /// Represents a stored file hash entry
   /// </summary>
        public class FileHashEntry
        {
     public string RelativePath { get; set; } = "";
    public string Hash { get; set; } = "";
      public DateTime CreatedAt { get; set; }
          public string OriginalTemplatePath { get; set; } = "";
        }

        /// <summary>
        /// Hash manifest for a project
        /// </summary>
        public class HashManifest
        {
   public string ProjectId { get; set; } = "";
            public DateTime CreatedAt { get; set; }
   public string ModType { get; set; } = "";
        public string MinecraftVersion { get; set; } = "";
    public Dictionary<string, FileHashEntry> FileHashes { get; set; } = new();
        }

        /// <summary>
        /// Computes a SHA256 hash of a file's content
   /// </summary>
        public static string ComputeFileHash(string filePath)
        {
  using var sha256 = SHA256.Create();
     using var stream = File.OpenRead(filePath);
        var hashBytes = sha256.ComputeHash(stream);
   return Convert.ToHexString(hashBytes);
      }

        /// <summary>
        /// Computes a SHA256 hash of content string
        /// </summary>
   public static string ComputeContentHash(string content)
   {
          using var sha256 = SHA256.Create();
var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
            return Convert.ToHexString(hashBytes);
      }

        /// <summary>
        /// Saves file hashes for a newly created project
   /// </summary>
        public static async Task SaveProjectHashesAsync(string projectPath, string modId, string modType, string minecraftVersion)
        {
 var manifest = new HashManifest
            {
  ProjectId = modId,
     CreatedAt = DateTime.UtcNow,
ModType = modType,
       MinecraftVersion = minecraftVersion
            };

            // Hash all Java files
   var javaDir = Path.Combine(projectPath, "src", "main", "java");
       if (Directory.Exists(javaDir))
            {
 foreach (var file in Directory.GetFiles(javaDir, "*.java", SearchOption.AllDirectories))
    {
    var relativePath = Path.GetRelativePath(projectPath, file).Replace('\\', '/');
     manifest.FileHashes[relativePath] = new FileHashEntry
               {
      RelativePath = relativePath,
            Hash = ComputeFileHash(file),
       CreatedAt = DateTime.UtcNow,
   OriginalTemplatePath = relativePath
   };
     }
  }

       // Hash mixin configs
            var resourcesDir = Path.Combine(projectPath, "src", "main", "resources");
      if (Directory.Exists(resourcesDir))
            {
      foreach (var pattern in new[] { "*.mixins.json", "*.accesswidener", "*.aw" })
      {
         foreach (var file in Directory.GetFiles(resourcesDir, pattern, SearchOption.AllDirectories))
   {
         var relativePath = Path.GetRelativePath(projectPath, file).Replace('\\', '/');
   manifest.FileHashes[relativePath] = new FileHashEntry
        {
    RelativePath = relativePath,
 Hash = ComputeFileHash(file),
             CreatedAt = DateTime.UtcNow,
                OriginalTemplatePath = relativePath
 };
         }
      }
    }

     // Save the manifest
     var hashFilePath = Path.Combine(projectPath, "modrix", HashFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(hashFilePath)!);
     var options = new JsonSerializerOptions { WriteIndented = true };
       await File.WriteAllTextAsync(hashFilePath, JsonSerializer.Serialize(manifest, options));
     }

        /// <summary>
    /// Loads the hash manifest for a project
        /// </summary>
     public static async Task<HashManifest?> LoadProjectHashesAsync(string projectPath)
 {
   var hashFilePath = Path.Combine(projectPath, "modrix", HashFileName);
if (!File.Exists(hashFilePath))
        return null;

   try
 {
       var json = await File.ReadAllTextAsync(hashFilePath);
 return JsonSerializer.Deserialize<HashManifest>(json);
       }
            catch
 {
      return null;
         }
  }

        /// <summary>
        /// Checks if a file has been modified from its original template
        /// </summary>
public static bool IsFileModified(string filePath, string projectPath, HashManifest? manifest)
   {
     if (manifest == null)
     return true; // Assume modified if no manifest exists

            var relativePath = Path.GetRelativePath(projectPath, filePath).Replace('\\', '/');
         
   if (!manifest.FileHashes.TryGetValue(relativePath, out var entry))
    return true; // New file, not in original manifest

            var currentHash = ComputeFileHash(filePath);
  return !string.Equals(currentHash, entry.Hash, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines if a file is a completely new file (not from template)
     /// </summary>
        public static bool IsNewFile(string filePath, string projectPath, HashManifest? manifest)
        {
    if (manifest == null)
        return false; // Can't determine without manifest

     var relativePath = Path.GetRelativePath(projectPath, filePath).Replace('\\', '/');
return !manifest.FileHashes.ContainsKey(relativePath);
        }

        /// <summary>
        /// Updates the hash for a specific file
   /// </summary>
        public static async Task UpdateFileHashAsync(string filePath, string projectPath)
        {
  var manifest = await LoadProjectHashesAsync(projectPath);
         if (manifest == null)
    return;

   var relativePath = Path.GetRelativePath(projectPath, filePath).Replace('\\', '/');
       manifest.FileHashes[relativePath] = new FileHashEntry
   {
  RelativePath = relativePath,
          Hash = ComputeFileHash(filePath),
            CreatedAt = DateTime.UtcNow,
     OriginalTemplatePath = relativePath
            };

var hashFilePath = Path.Combine(projectPath, "modrix", HashFileName);
            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(hashFilePath, JsonSerializer.Serialize(manifest, options));
        }
    }
}
