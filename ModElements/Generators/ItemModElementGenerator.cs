using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;
using Modrix.ModElements;
using System.Text;

namespace Modrix.ModElements.Generators
{
    public class ItemModElementGenerator : IModElementGenerator
    {
        public string Name => "Item";
        public string Description => "Create a new item for your mod.";
        public string Icon => "pack://application:,,,/Resources/Icons/WorkspaceIcon.png";
        public IReadOnlyList<string> SupportedModLoaders => new[] { "Forge", "Fabric" };
        public IReadOnlyList<string> SupportedMinecraftVersions => new[] { "1.21.x", "1.20.x", "1.19.x" };

        public void Generate(ModElementGenerationContext context)
        {
            // Extract parameters
            if (!context.Parameters.TryGetValue("Name", out var nameObj) || nameObj is not string name)
            {
                throw new ArgumentException("Item name is required");
            }

            if (!context.Parameters.TryGetValue("TexturePath", out var texturePathObj) || texturePathObj is not string texturePath)
            {
                throw new ArgumentException("Item texture path is required");
            }

            // Handle different mod loaders
            switch (context.ModLoader.ToLower())
            {
                case "forge":
                    GenerateForgeItem(context, name, texturePath);
                    break;
                case "fabric":
                    GenerateFabricItem(context, name, texturePath);
                    break;
                default:
                    throw new NotSupportedException($"Mod loader '{context.ModLoader}' is not supported");
            }
        }

        private void GenerateForgeItem(ModElementGenerationContext context, string itemName, string texturePath)
        {
            var projectPath = context.ProjectPath;
            var packageName = GetPackageNameFromProject(projectPath);
            var modId = GetModIdFromProject(projectPath);
            
            // Format the class name (remove spaces, capitalize)
            var itemClassName = FormatClassName(itemName);
            
            // Prepare paths
            var packagePath = packageName.Replace('.', '/');
            var javaDir = FindJavaDirectory(projectPath);
            
            if (string.IsNullOrEmpty(javaDir))
            {
                throw new DirectoryNotFoundException("Could not find Java source directory");
            }
            
            // Create item class
            var itemClassPath = Path.Combine(javaDir, packagePath, "item", $"{itemClassName}Item.java");
            Directory.CreateDirectory(Path.GetDirectoryName(itemClassPath));
            
            // Write item class
            var itemClass = GenerateForgeItemClass(packageName, modId, itemClassName, itemName);
            File.WriteAllText(itemClassPath, itemClass);
            
            // Copy texture if needed
            HandleTexture(projectPath, texturePath, modId, itemName);
            
            // Update registry class
            UpdateForgeItemRegistry(projectPath, packageName, itemClassName);
            
            // Update language file
            UpdateLanguageFile(projectPath, modId, itemName, context.Parameters.TryGetValue("TranslationKey", out var key) ? key.ToString() : null);
        }

        private void GenerateFabricItem(ModElementGenerationContext context, string itemName, string texturePath)
        {
            var projectPath = context.ProjectPath;
            var packageName = GetPackageNameFromProject(projectPath);
            var modId = GetModIdFromProject(projectPath);
            
            // Format the class name (remove spaces, capitalize)
            var itemClassName = FormatClassName(itemName);
            
            // Prepare paths
            var packagePath = packageName.Replace('.', '/');
            var javaDir = FindJavaDirectory(projectPath);
            
            if (string.IsNullOrEmpty(javaDir))
            {
                throw new DirectoryNotFoundException("Could not find Java source directory");
            }
            
            // Create item class
            var itemClassPath = Path.Combine(javaDir, packagePath, "item", $"{itemClassName}Item.java");
            Directory.CreateDirectory(Path.GetDirectoryName(itemClassPath));
            
            // Write item class
            var itemClass = GenerateFabricItemClass(packageName, modId, itemClassName, itemName);
            File.WriteAllText(itemClassPath, itemClass);
            
            // Copy texture if needed
            HandleTexture(projectPath, texturePath, modId, itemName);
            
            // Update registry class (for Fabric)
            UpdateFabricItemRegistry(projectPath, packageName, itemClassName);
            
            // Update language file
            UpdateLanguageFile(projectPath, modId, itemName, context.Parameters.TryGetValue("TranslationKey", out var key) ? key.ToString() : null);
        }

        private string GenerateForgeItemClass(string packageName, string modId, string itemClassName, string itemName)
        {
            return $@"package {packageName}.item;

import net.minecraft.world.item.Item;
import net.minecraft.world.item.CreativeModeTab;
import {packageName}.{FormatClassName(modId)}Mod;

public class {itemClassName}Item extends Item {{
    public static final String ID = ""{ToSnakeCase(itemName)}"";

    public {itemClassName}Item() {{
        super(new Item.Properties());
    }}
}}";
        }

        private string GenerateFabricItemClass(string packageName, string modId, string itemClassName, string itemName)
        {
            return $@"package {packageName}.item;

import net.minecraft.item.Item;
import net.minecraft.registry.Registries;
import net.minecraft.registry.Registry;
import net.minecraft.util.Identifier;
import {packageName}.{FormatClassName(modId)}Mod;

public class {itemClassName}Item extends Item {{
    public static final String ID = ""{ToSnakeCase(itemName)}"";
    public static final {itemClassName}Item INSTANCE = new {itemClassName}Item();

    public {itemClassName}Item() {{
        super(new Item.Settings());
    }}

    public static void register() {{
        Registry.register(Registries.ITEM, new Identifier({FormatClassName(modId)}Mod.MOD_ID, ID), INSTANCE);
    }}
}}";
        }

        private void UpdateForgeItemRegistry(string projectPath, string packageName, string itemClassName)
        {
            var javaDir = FindJavaDirectory(projectPath);
            var packagePath = packageName.Replace('.', '/');
            var registryPath = Path.Combine(javaDir, packagePath, "registry", "ItemRegistry.java");
            
            // If registry file doesn't exist, create it
            if (!File.Exists(registryPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(registryPath));
                var registryClass = GenerateForgeItemRegistryClass(packageName, itemClassName);
                File.WriteAllText(registryPath, registryClass);
                
                // Also update main mod class to call registry
                UpdateForgeModClass(projectPath, packageName);
            }
            else
            {
                // Update existing registry
                var content = File.ReadAllText(registryPath);
                
                // Check if the item is already registered
                if (content.Contains($"{itemClassName}Item"))
                {
                    return; // Already registered
                }
                
                // Find the registration method
                int registerIndex = content.IndexOf("public static void register(IEventBus eventBus) {");
                if (registerIndex == -1)
                {
                    registerIndex = content.IndexOf("public static void register(") + 24;
                }
                
                if (registerIndex != -1)
                {
                    // Find the closing brace for the method
                    int closeBraceIndex = FindClosingBrace(content, registerIndex);
                    
                    // Add import
                    string importStatement = $"import {packageName}.item.{itemClassName}Item;\n";
                    int lastImportIndex = content.LastIndexOf("import ");
                    int lastImportEnd = content.IndexOf(';', lastImportIndex) + 1;
                    content = content.Insert(lastImportEnd + 1, importStatement);
                    
                    // Add registration line just before closing brace
                    string registrationLine = $"\n        ITEMS.register(\"{ToSnakeCase(itemClassName)}\", () -> new {itemClassName}Item());";
                    content = content.Insert(closeBraceIndex, registrationLine);
                    
                    File.WriteAllText(registryPath, content);
                }
            }
        }

        private void UpdateFabricItemRegistry(string projectPath, string packageName, string itemClassName)
        {
            var javaDir = FindJavaDirectory(projectPath);
            var modId = GetModIdFromProject(projectPath);
            var modClassName = FormatClassName(modId) + "Mod";
            var modClassPath = Path.Combine(javaDir, packageName.Replace('.', '/'), $"{modClassName}.java");
            
            if (!File.Exists(modClassPath))
            {
                return; // Can't update non-existent mod class
            }
            
            var content = File.ReadAllText(modClassPath);
            
            // Check if item is already registered
            if (content.Contains($"{itemClassName}Item"))
            {
                return; // Already registered
            }
            
            // Add import
            string importStatement = $"import {packageName}.item.{itemClassName}Item;\n";
            int lastImportIndex = content.LastIndexOf("import ");
            int lastImportEnd = content.IndexOf(';', lastImportIndex) + 1;
            content = content.Insert(lastImportEnd + 1, importStatement);
            
            // Find onInitialize method
            int initIndex = content.IndexOf("public void onInitialize() {");
            if (initIndex == -1)
            {
                initIndex = content.IndexOf("public void onInitialize(") + 26;
            }
            
            if (initIndex != -1)
            {
                // Find the closing brace for the method
                int closeBraceIndex = FindClosingBrace(content, initIndex);
                
                // Add registration line just before closing brace
                string registrationLine = $"\n        {itemClassName}Item.register();";
                content = content.Insert(closeBraceIndex, registrationLine);
                
                File.WriteAllText(modClassPath, content);
            }
        }

        private string GenerateForgeItemRegistryClass(string packageName, string firstItemClassName)
        {
            return $@"package {packageName}.registry;

import net.minecraft.world.item.Item;
import net.minecraftforge.registries.DeferredRegister;
import net.minecraftforge.registries.ForgeRegistries;
import net.minecraftforge.registries.RegistryObject;
import net.minecraftforge.eventbus.api.IEventBus;
import {packageName}.{packageName.Substring(packageName.LastIndexOf('.') + 1)};
import {packageName}.item.{firstItemClassName}Item;

public class ItemRegistry {{
    
    public static final DeferredRegister<Item> ITEMS = DeferredRegister.create(ForgeRegistries.ITEMS, 
        {packageName.Substring(packageName.LastIndexOf('.') + 1)}.MOD_ID);
    
    // Items
    public static final RegistryObject<Item> {ToUpperSnakeCase(firstItemClassName)} = 
        ITEMS.register(""{ToSnakeCase(firstItemClassName)}"", () -> new {firstItemClassName}Item());
    
    public static void register(IEventBus eventBus) {{
        ITEMS.register(eventBus);
    }}
}}";
        }

        private void UpdateForgeModClass(string projectPath, string packageName)
        {
            var javaDir = FindJavaDirectory(projectPath);
            var modId = GetModIdFromProject(projectPath);
            var modClassName = FormatClassName(modId) + "Mod";
            var modClassPath = Path.Combine(javaDir, packageName.Replace('.', '/'), $"{modClassName}.java");
            
            if (!File.Exists(modClassPath))
            {
                return; // Can't update non-existent mod class
            }
            
            var content = File.ReadAllText(modClassPath);
            
            // Check if registry is already imported and registered
            if (content.Contains("ItemRegistry") && content.Contains("ItemRegistry.register("))
            {
                return; // Already registered
            }
            
            // Add import
            string importStatement = $"import {packageName}.registry.ItemRegistry;\n";
            int lastImportIndex = content.LastIndexOf("import ");
            int lastImportEnd = content.IndexOf(';', lastImportIndex) + 1;
            content = content.Insert(lastImportEnd + 1, importStatement);
            
            // Find constructor or initialization method
            int constructorIndex = content.IndexOf("public " + modClassName + "(") + modClassName.Length + 8;
            if (constructorIndex != -1)
            {
                // Find the closing brace for the constructor
                int closeBraceIndex = FindClosingBrace(content, constructorIndex);
                
                // Add registration line just before closing brace
                string registrationLine = "\n        ItemRegistry.register(modEventBus);";
                content = content.Insert(closeBraceIndex, registrationLine);
                
                File.WriteAllText(modClassPath, content);
            }
        }

        private void HandleTexture(string projectPath, string texturePath, string modId, string itemName)
        {
            // Determine destination texture path
            var resourcesDir = FindResourcesDirectory(projectPath);
            if (resourcesDir == null)
            {
                throw new DirectoryNotFoundException("Resources directory not found");
            }
            
            var textureDestDir = Path.Combine(resourcesDir, "assets", modId, "textures", "item");
            Directory.CreateDirectory(textureDestDir);
            
            var textureFileName = ToSnakeCase(itemName) + ".png";
            var textureDestPath = Path.Combine(textureDestDir, textureFileName);
            
            // Copy texture file
            if (File.Exists(texturePath))
            {
                File.Copy(texturePath, textureDestPath, true);
            }
            
            // Create item model JSON
            var modelDir = Path.Combine(resourcesDir, "assets", modId, "models", "item");
            Directory.CreateDirectory(modelDir);
            
            var modelPath = Path.Combine(modelDir, $"{ToSnakeCase(itemName)}.json");
            var modelJson = $@"{{
  ""parent"": ""item/generated"",
  ""textures"": {{
    ""layer0"": ""{modId}:item/{ToSnakeCase(itemName)}""
  }}
}}";
            File.WriteAllText(modelPath, modelJson);
        }

        private void UpdateLanguageFile(string projectPath, string modId, string itemName, string translationKey = null)
        {
            var resourcesDir = FindResourcesDirectory(projectPath);
            if (resourcesDir == null)
            {
                return; // Can't update language file
            }
            
            var langDir = Path.Combine(resourcesDir, "assets", modId, "lang");
            Directory.CreateDirectory(langDir);
            
            var langPath = Path.Combine(langDir, "en_us.json");
            
            // Create translation key if not provided
            if (string.IsNullOrEmpty(translationKey))
            {
                translationKey = $"item.{modId}.{ToSnakeCase(itemName)}";
            }
            
            // Check if language file exists
            if (File.Exists(langPath))
            {
                // Read existing JSON
                var content = File.ReadAllText(langPath);
                
                // Simple JSON editing (could use JSON library for more complex files)
                if (content.Contains(translationKey))
                {
                    return; // Already has this translation
                }
                
                // Add new translation
                int lastBraceIndex = content.LastIndexOf('}');
                if (lastBraceIndex > 0)
                {
                    var isLastEntry = !content.Substring(0, lastBraceIndex).TrimEnd().EndsWith(",");
                    string newEntry = isLastEntry ? $",\n  \"{translationKey}\": \"{itemName}\"" : $"\n  \"{translationKey}\": \"{itemName}\",";
                    content = content.Insert(lastBraceIndex, newEntry);
                    File.WriteAllText(langPath, content);
                }
            }
            else
            {
                // Create new language file
                var langJson = $@"{{
  ""{translationKey}"": ""{itemName}""
}}";
                File.WriteAllText(langPath, langJson);
            }
        }

        public Page CreatePage()
        {
            return new Views.Pages.ItemGeneratorPage();
        }

        #region Helper Methods

        private string GetPackageNameFromProject(string projectPath)
        {
            try
            {
                // Try to read from build.gradle
                var buildGradlePath = Path.Combine(projectPath, "build.gradle");
                if (File.Exists(buildGradlePath))
                {
                    var content = File.ReadAllText(buildGradlePath);
                    var index = content.IndexOf("group = '");
                    if (index >= 0)
                    {
                        var start = index + 9;
                        var end = content.IndexOf("'", start);
                        if (end > start)
                        {
                            return content.Substring(start, end - start);
                        }
                    }
                }
                
                // Try to read from modrix.config
                var configPath = Path.Combine(projectPath, "modrix.config");
                if (File.Exists(configPath))
                {
                    var content = File.ReadAllText(configPath);
                    var index = content.IndexOf("\"Package\": \"");
                    if (index >= 0)
                    {
                        var start = index + 12;
                        var end = content.IndexOf("\"", start);
                        if (end > start)
                        {
                            return content.Substring(start, end - start);
                        }
                    }
                }

                // Fall back to scanning for main class
                var javaDir = FindJavaDirectory(projectPath);
                if (!string.IsNullOrEmpty(javaDir))
                {
                    foreach (var file in Directory.GetFiles(javaDir, "*.java", SearchOption.AllDirectories))
                    {
                        var content = File.ReadAllText(file);
                        if (content.Contains("@Mod(") || content.Contains("implements ModInitializer"))
                        {
                            var packageLine = content.Contains("package ") ? content.Substring(0, content.IndexOf(';')) : null;
                            if (!string.IsNullOrEmpty(packageLine))
                            {
                                return packageLine.Replace("package ", "").Trim();
                            }
                        }
                    }
                }
            }
            catch { }
            
            // Could not determine package name, use default
            return "com.example.mod";
        }

        private string GetModIdFromProject(string projectPath)
        {
            try
            {
                // Try to read from modrix.config
                var configPath = Path.Combine(projectPath, "modrix.config");
                if (File.Exists(configPath))
                {
                    var content = File.ReadAllText(configPath);
                    var index = content.IndexOf("\"ModId\": \"");
                    if (index >= 0)
                    {
                        var start = index + 10;
                        var end = content.IndexOf("\"", start);
                        if (end > start)
                        {
                            return content.Substring(start, end - start);
                        }
                    }
                }
                
                // Try to read from main class
                var javaDir = FindJavaDirectory(projectPath);
                if (!string.IsNullOrEmpty(javaDir))
                {
                    foreach (var file in Directory.GetFiles(javaDir, "*.java", SearchOption.AllDirectories))
                    {
                        var content = File.ReadAllText(file);
                        if (content.Contains("@Mod(") || content.Contains("implements ModInitializer"))
                        {
                            // Look for MOD_ID constant
                            var modIdIndex = content.IndexOf("MOD_ID = \"");
                            if (modIdIndex >= 0)
                            {
                                var start = modIdIndex + 10;
                                var end = content.IndexOf("\"", start);
                                if (end > start)
                                {
                                    return content.Substring(start, end - start);
                                }
                            }
                            
                            // Look for @Mod annotation
                            modIdIndex = content.IndexOf("@Mod(\"");
                            if (modIdIndex >= 0)
                            {
                                var start = modIdIndex + 6;
                                var end = content.IndexOf("\"", start);
                                if (end > start)
                                {
                                    return content.Substring(start, end - start);
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            
            // Could not determine mod ID, use default
            return "examplemod";
        }

        private string FindJavaDirectory(string projectPath)
        {
            // Common Java source directories
            var commonDirs = new[]
            {
                Path.Combine(projectPath, "src", "main", "java"),
                Path.Combine(projectPath, "java")
            };
            
            foreach (var dir in commonDirs)
            {
                if (Directory.Exists(dir))
                {
                    return dir;
                }
            }
            
            // Search for java directory
            foreach (var dir in Directory.GetDirectories(projectPath, "*", SearchOption.AllDirectories))
            {
                if (dir.EndsWith("java") && Directory.GetFiles(dir, "*.java", SearchOption.AllDirectories).Length > 0)
                {
                    return dir;
                }
            }
            
            return null;
        }

        private string FindResourcesDirectory(string projectPath)
        {
            // Common resource directories
            var commonDirs = new[]
            {
                Path.Combine(projectPath, "src", "main", "resources"),
                Path.Combine(projectPath, "resources")
            };
            
            foreach (var dir in commonDirs)
            {
                if (Directory.Exists(dir))
                {
                    return dir;
                }
            }
            
            // Search for resources directory
            foreach (var dir in Directory.GetDirectories(projectPath, "*", SearchOption.AllDirectories))
            {
                if (dir.EndsWith("resources"))
                {
                    return dir;
                }
            }
            
            return null;
        }

        private string FormatClassName(string name)
        {
            var sb = new StringBuilder();
            var parts = name.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var part in parts)
            {
                if (part.Length > 0)
                {
                    sb.Append(char.ToUpper(part[0]));
                    if (part.Length > 1)
                    {
                        sb.Append(part.Substring(1));
                    }
                }
            }
            
            return sb.ToString();
        }

        private string ToSnakeCase(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            var sb = new StringBuilder(name.Length + 5);
            sb.Append(char.ToLower(name[0]));
            
            for (int i = 1; i < name.Length; i++)
            {
                if (char.IsUpper(name[i]))
                {
                    sb.Append('_');
                    sb.Append(char.ToLower(name[i]));
                }
                else if (name[i] == ' ' || name[i] == '-')
                {
                    sb.Append('_');
                }
                else
                {
                    sb.Append(name[i]);
                }
            }
            
            return sb.ToString().ToLower().Replace("__", "_");
        }

        private string ToUpperSnakeCase(string name)
        {
            return ToSnakeCase(name).ToUpper();
        }

        private int FindClosingBrace(string content, int openingBraceIndex)
        {
            int startIndex = content.IndexOf('{', openingBraceIndex);
            if (startIndex == -1)
            {
                return content.Length - 1;
            }
            
            int count = 1;
            for (int i = startIndex + 1; i < content.Length; i++)
            {
                if (content[i] == '{')
                {
                    count++;
                }
                else if (content[i] == '}')
                {
                    count--;
                    if (count == 0)
                    {
                        return i;
                    }
                }
            }
            
            return content.Length - 1;
        }

        #endregion
    }
}
