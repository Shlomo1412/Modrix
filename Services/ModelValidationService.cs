using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Modrix.Services
{
    public class ModelValidationService
    {
        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public List<ValidationIssue> Issues { get; set; } = new();
            public List<MissingMapping> MissingMappings { get; set; } = new();
        }

        public class ValidationIssue
        {
            public string Type { get; set; } // Error, Warning
            public string Message { get; set; }
            public string FilePath { get; set; }
            public string Category { get; set; } // Syntax, Reference, Mapping
        }

        public class MissingMapping
        {
            public string ModelPath { get; set; }
            public string TexturePath { get; set; }
            public string ReferencedTexture { get; set; }
            public string SuggestedTexture { get; set; }
            public MappingType Type { get; set; }
        }

        public enum MappingType
        {
            Texture,
            Parent,
            Model
        }

        public async Task<ValidationResult> ValidateModelsAsync(string projectPath, string modId)
        {
            var result = new ValidationResult { IsValid = true };
            
            var modelsPath = Path.Combine(projectPath, "src", "main", "resources", "assets", modId, "models");
            var texturesPath = Path.Combine(projectPath, "src", "main", "resources", "assets", modId, "textures");
            
            if (!Directory.Exists(modelsPath))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Type = "Warning",
                    Message = "Models directory not found",
                    Category = "Structure"
                });
                return result;
            }

            var modelFiles = Directory.GetFiles(modelsPath, "*.json", SearchOption.AllDirectories);
            
            // Build comprehensive texture paths including all subdirectories
            var textureFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(texturesPath))
            {
                foreach (var file in Directory.GetFiles(texturesPath, "*.png", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(texturesPath, file)
                        .Replace('\\', '/')
                        .Replace(".png", "");
                    
                    // Add both the full path and variations
                    textureFiles.Add(relativePath); // e.g., "item/itemtest" or "block/stone"
                    
                    // Also add without subdirectory for compatibility
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    textureFiles.Add(fileName); // e.g., "itemtest" or "stone"
                }
            }

            // Debug: Add information about found textures as a warning so user can see what was found
            if (textureFiles.Count > 0)
            {
                var textureList = string.Join(", ", textureFiles.Take(10)); // Show first 10
                var moreCount = textureFiles.Count > 10 ? $" and {textureFiles.Count - 10} more" : "";
                result.Issues.Add(new ValidationIssue
                {
                    Type = "Info", // Changed to Info so it doesn't show as error
                    Message = $"Found {textureFiles.Count} texture variations: {textureList}{moreCount}",
                    Category = "Debug"
                });
            }
            else
            {
                result.Issues.Add(new ValidationIssue
                {
                    Type = "Warning",
                    Message = $"No texture files found in: {texturesPath}",
                    Category = "Structure"
                });
            }

            foreach (var modelFile in modelFiles)
            {
                await ValidateModelFile(modelFile, textureFiles, modId, result);
            }

            result.IsValid = !result.Issues.Any(i => i.Type == "Error");
            return result;
        }

        private async Task ValidateModelFile(string modelPath, HashSet<string> availableTextures, string modId, ValidationResult result)
        {
            try
            {
                var content = await File.ReadAllTextAsync(modelPath);
                var model = JsonSerializer.Deserialize<JsonElement>(content);

                // Validate JSON structure
                if (!model.TryGetProperty("textures", out var texturesElement))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Type = "Warning",
                        Message = "Model has no textures property",
                        FilePath = modelPath,
                        Category = "Structure"
                    });
                    return;
                }

                // Check texture references
                if (texturesElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var texture in texturesElement.EnumerateObject())
                    {
                        var texturePath = texture.Value.GetString();
                        if (string.IsNullOrEmpty(texturePath)) continue;

                        // Skip variable references (e.g., "#layer0", "#texture")
                        if (texturePath.StartsWith("#"))
                        {
                            continue;
                        }

                        // Handle different texture reference formats
                        var cleanTexturePath = CleanTexturePath(texturePath, modId);
                        
                        // Skip vanilla Minecraft textures
                        if (cleanTexturePath.StartsWith("minecraft:"))
                        {
                            continue;
                        }
                        
                        // Check if texture exists (try multiple variations)
                        bool textureExists = DoesTextureExist(cleanTexturePath, availableTextures);
                        
                        if (!textureExists)
                        {
                            var suggestedTexture = FindSimilarTexture(cleanTexturePath, availableTextures);
                            
                            result.MissingMappings.Add(new MissingMapping
                            {
                                ModelPath = modelPath,
                                TexturePath = cleanTexturePath,
                                ReferencedTexture = texturePath,
                                SuggestedTexture = suggestedTexture,
                                Type = MappingType.Texture
                            });

                            // Add debug info about what was being searched for
                            result.Issues.Add(new ValidationIssue
                            {
                                Type = "Error",
                                Message = $"Missing texture: '{texturePath}' (cleaned to: '{cleanTexturePath}') - suggested: '{suggestedTexture ?? "none"}'",
                                FilePath = modelPath,
                                Category = "Reference"
                            });
                        }
                    }
                }

                // Check parent model references
                if (model.TryGetProperty("parent", out var parentElement))
                {
                    var parentPath = parentElement.GetString();
                    if (!string.IsNullOrEmpty(parentPath) && parentPath.StartsWith(modId + ":"))
                    {
                        var modelDir = Path.GetDirectoryName(modelPath);
                        var parentModelPath = Path.Combine(modelDir, parentPath.Replace(modId + ":", "") + ".json");
                        
                        if (!File.Exists(parentModelPath))
                        {
                            result.Issues.Add(new ValidationIssue
                            {
                                Type = "Error",
                                Message = $"Missing parent model: {parentPath}",
                                FilePath = modelPath,
                                Category = "Reference"
                            });
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Type = "Error",
                    Message = $"Invalid JSON: {ex.Message}",
                    FilePath = modelPath,
                    Category = "Syntax"
                });
            }
            catch (Exception ex)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Type = "Error",
                    Message = $"Validation error: {ex.Message}",
                    FilePath = modelPath,
                    Category = "Validation"
                });
            }
        }

        private bool DoesTextureExist(string texturePath, HashSet<string> availableTextures)
        {
            if (string.IsNullOrEmpty(texturePath)) return false;
            
            // Try exact match first (case-insensitive since HashSet is created with StringComparer.OrdinalIgnoreCase)
            if (availableTextures.Contains(texturePath))
            {
                return true;
            }
            
            // Try without directory prefix (for backward compatibility)
            var fileName = Path.GetFileName(texturePath);
            if (availableTextures.Contains(fileName))
            {
                return true;
            }
            
            // Try with common prefixes if not already present
            if (!texturePath.Contains("/"))
            {
                if (availableTextures.Contains($"item/{texturePath}") || 
                    availableTextures.Contains($"block/{texturePath}"))
                {
                    return true;
                }
            }
            
            // Try case variations manually as an extra safety net
            foreach (var texture in availableTextures)
            {
                if (string.Equals(texture, texturePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                
                var textureFileName = Path.GetFileName(texture);
                if (string.Equals(textureFileName, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            return false;
        }

        private string CleanTexturePath(string texturePath, string modId)
        {
            // Remove mod namespace if present (e.g., "bruhitem:item/itemtest" -> "item/itemtest")
            if (texturePath.StartsWith(modId + ":"))
            {
                texturePath = texturePath.Substring(modId.Length + 1);
            }
            
            // Handle Minecraft's built-in texture references (don't remove these prefixes for mod textures)
            if (texturePath.StartsWith("minecraft:"))
            {
                // This is a vanilla texture reference, which is valid
                return texturePath;
            }

            // Don't remove "block/" or "item/" prefixes as they are part of the actual path structure
            // The texture files should be organized in subdirectories like textures/item/ or textures/block/
            
            return texturePath;
        }

        private string FindSimilarTexture(string targetTexture, HashSet<string> availableTextures)
        {
            if (string.IsNullOrEmpty(targetTexture)) return null;
            
            var target = targetTexture.ToLower();
            var targetFileName = Path.GetFileName(target);
            
            // First try to find exact filename matches in different directories
            var exactFileNameMatches = availableTextures
                .Where(t => Path.GetFileName(t.ToLower()) == targetFileName)
                .ToList();
            
            if (exactFileNameMatches.Any())
            {
                return exactFileNameMatches.First();
            }
            
            // Then try partial matches
            var partialMatches = availableTextures
                .Where(t => 
                {
                    var lowerT = t.ToLower();
                    return lowerT.Contains(targetFileName) || 
                           targetFileName.Contains(Path.GetFileName(lowerT)) ||
                           lowerT.Contains(target) || 
                           target.Contains(lowerT);
                })
                .OrderBy(t => 
                {
                    // Prefer matches with same filename
                    var fileName = Path.GetFileName(t.ToLower());
                    if (fileName == targetFileName) return 0;
                    if (fileName.Contains(targetFileName) || targetFileName.Contains(fileName)) return 1;
                    return 2;
                })
                .ThenBy(t => Math.Abs(t.Length - target.Length))
                .ToList();
            
            return partialMatches.FirstOrDefault();
        }
    }
}