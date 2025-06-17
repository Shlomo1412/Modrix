using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;
using Modrix.ModElements;
using System.Text;
using System.Text.Json;
using System.Diagnostics;

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

            Debug.WriteLine($"Generating item with ModLoader: {context.ModLoader}");

            // Handle different mod loaders
            switch (context.ModLoader.ToLower())
            {
                case "forge":
                case "neoforge":
                    GenerateForgeItem(context, name, texturePath);
                    break;
                case "fabric":
                    GenerateFabricItem(context, name, texturePath);
                    break;
                default:
                    // If ModLoader isn't set correctly, try to detect from files
                    var detectedModLoader = DetectModLoaderFromFiles(context.ProjectPath);
                    Debug.WriteLine($"Mod loader not specified correctly, detected: {detectedModLoader}");
                    
                    if (detectedModLoader.ToLower() == "forge" || detectedModLoader.ToLower() == "neoforge")
                        GenerateForgeItem(context, name, texturePath);
                    else
                        GenerateFabricItem(context, name, texturePath);
                    break;
            }
        }

        private string DetectModLoaderFromFiles(string projectPath)
        {
            try
            {
                // Check for fabric.mod.json
                if (File.Exists(Path.Combine(projectPath, "src", "main", "resources", "fabric.mod.json")))
                    return "fabric";

                // Check for mods.toml
                if (File.Exists(Path.Combine(projectPath, "src", "main", "resources", "META-INF", "mods.toml")))
                {
                    // Check for NeoForge vs Forge
                    var tomlContent = File.ReadAllText(Path.Combine(projectPath, "src", "main", "resources", "META-INF", "mods.toml"));
                    if (tomlContent.Contains("neoforge") || tomlContent.Contains("NeoForge"))
                        return "neoforge";
                    return "forge";
                }

                // Check build.gradle for dependencies
                if (File.Exists(Path.Combine(projectPath, "build.gradle")))
                {
                    var gradleContent = File.ReadAllText(Path.Combine(projectPath, "build.gradle"));
                    if (gradleContent.Contains("fabric"))
                        return "fabric";
                    if (gradleContent.Contains("neoforge"))
                        return "neoforge";
                    if (gradleContent.Contains("forge"))
                        return "forge";
                }

                // Try to read from modrix.config
                var configPath = Path.Combine(projectPath, "modrix.config");
                if (File.Exists(configPath))
                {
                    var content = File.ReadAllText(configPath);
                    var index = content.IndexOf("\"ModType\": \"");
                    if (index >= 0)
                    {
                        var start = index + 12;
                        var end = content.IndexOf("\"", start);
                        if (end > start)
                        {
                            var modType = content.Substring(start, end - start).ToLower();
                            return modType; // Will be "fabric", "forge" etc.
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error detecting mod loader: {ex.Message}");
            }

            return "fabric"; // Default to Fabric if detection fails
        }

        private void GenerateForgeItem(ModElementGenerationContext context, string itemName, string texturePath)
        {
            Debug.WriteLine("Generating Forge item code");
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
            Debug.WriteLine($"Created Forge item class at {itemClassPath}");
            
            // Copy texture if needed
            HandleTexture(projectPath, texturePath, modId, itemName);
            
            // Update registry class
            UpdateForgeItemRegistry(projectPath, packageName, itemClassName);
            
            // Update language file
            UpdateLanguageFile(projectPath, modId, itemName, context.Parameters.TryGetValue("TranslationKey", out var key) ? key?.ToString() : null);
        }

        private void GenerateFabricItem(ModElementGenerationContext context, string itemName, string texturePath)
        {
            Debug.WriteLine("Generating Fabric item code");
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
            Directory.CreateDirectory(Path.GetDirectoryName(itemClassPath ?? ""));
            
            // Write item class - use proper Fabric version for the Minecraft version
            var minecraftVersion = context.MinecraftVersion ?? "1.20.x"; // Default to 1.20.x if not specified
            var itemClass = GenerateFabricItemClass(packageName, modId, itemClassName, itemName, minecraftVersion);
            File.WriteAllText(itemClassPath, itemClass);
            Debug.WriteLine($"Created Fabric item class at {itemClassPath}");
            
            // Copy texture if needed
            HandleTexture(projectPath, texturePath, modId, itemName);
            
            // Update registry class (for Fabric)
            UpdateFabricItemRegistry(projectPath, packageName, itemClassName, modId);
            
            // Update language file
            UpdateLanguageFile(projectPath, modId, itemName, context.Parameters.TryGetValue("TranslationKey", out var key) ? key?.ToString() : null);
        }

        private string GenerateForgeItemClass(string packageName, string modId, string itemClassName, string itemName)
        {
            var modClassName = FormatClassName(modId) + "Mod";
            
            return $@"package {packageName}.item;

import net.minecraft.world.item.Item;
import net.minecraft.world.item.CreativeModeTab;
import {packageName}.{modClassName};

public class {itemClassName}Item extends Item {{
    public static final String ID = ""{ToSnakeCase(itemName)}"";

    public {itemClassName}Item() {{
        super(new Item.Properties());
    }}
}}";
        }

        private string GenerateFabricItemClass(string packageName, string modId, string itemClassName, string itemName, string minecraftVersion)
        {
            var modClassName = FormatClassName(modId) + "Mod";
            
            // Different versions of Minecraft require different imports for Fabric
            if (minecraftVersion.StartsWith("1.19") || minecraftVersion.StartsWith("1.20"))
            {
                return $@"package {packageName}.item;

import net.minecraft.item.Item;
import net.minecraft.registry.Registries;
import net.minecraft.registry.Registry;
import net.minecraft.util.Identifier;
import {packageName}.{modClassName};

public class {itemClassName}Item extends Item {{
    public static final String ID = ""{ToSnakeCase(itemName)}"";
    public static final {itemClassName}Item INSTANCE = new {itemClassName}Item();

    public {itemClassName}Item() {{
        super(new Item.Settings());
    }}

    public static void register() {{
        Registry.register(Registries.ITEM, new Identifier({modClassName}.MOD_ID, ID), INSTANCE);
    }}
}}";
            }
            else if (minecraftVersion.StartsWith("1.21"))
            {
                return $@"package {packageName}.item;

import net.minecraft.item.Item;
import net.minecraft.registry.Registries;
import net.minecraft.registry.Registry;
import net.minecraft.util.Identifier;
import {packageName}.{modClassName};

public class {itemClassName}Item extends Item {{
    public static final String ID = ""{ToSnakeCase(itemName)}"";
    public static final {itemClassName}Item INSTANCE = new {itemClassName}Item();

    public {itemClassName}Item() {{
        super(new Item.Settings());
    }}

    public static void register() {{
        Registry.register(Registries.ITEM, new Identifier({modClassName}.MOD_ID, ID), INSTANCE);
    }}
}}";
            }
            else // For older versions
            {
                return $@"package {packageName}.item;

import net.minecraft.item.Item;
import net.minecraft.util.Identifier;
import net.minecraft.util.registry.Registry;
import {packageName}.{modClassName};

public class {itemClassName}Item extends Item {{
    public static final String ID = ""{ToSnakeCase(itemName)}"";
    public static final {itemClassName}Item INSTANCE = new {itemClassName}Item();

    public {itemClassName}Item() {{
        super(new Item.Settings().group(Item.Settings.MISC));
    }}

    public static void register() {{
        Registry.register(Registry.ITEM, new Identifier({modClassName}.MOD_ID, ID), INSTANCE);
    }}
}}";
            }
        }

        private void UpdateForgeItemRegistry(string projectPath, string packageName, string itemClassName)
        {
            var javaDir = FindJavaDirectory(projectPath);
            var packagePath = packageName.Replace('.', '/');
            var registryPath = Path.Combine(javaDir, packagePath, "registry", "ItemRegistry.java");
            
            // If registry file doesn't exist, create it
            if (!File.Exists(registryPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(registryPath) ?? "");
                var registryClass = GenerateForgeItemRegistryClass(packageName, itemClassName);
                File.WriteAllText(registryPath, registryClass);
                Debug.WriteLine($"Created Forge ItemRegistry at {registryPath}");
                
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
                    Debug.WriteLine($"Updated Forge ItemRegistry at {registryPath}");
                }
            }
        }

        private void UpdateFabricItemRegistry(string projectPath, string packageName, string itemClassName, string modId)
        {
            var javaDir = FindJavaDirectory(projectPath);
            if (string.IsNullOrEmpty(javaDir))
                return;
                
            var modClassName = FormatClassName(modId) + "Mod";
            var modClassPath = Path.Combine(javaDir, packageName.Replace('.', '/'), $"{modClassName}.java");
            
            // If the main mod class doesn't exist, create a simple one
            if (!File.Exists(modClassPath))
            {
                var modClassContent = GenerateFabricModClass(packageName, modId, itemClassName);
                Directory.CreateDirectory(Path.GetDirectoryName(modClassPath) ?? "");
                File.WriteAllText(modClassPath, modClassContent);
                Debug.WriteLine($"Created Fabric mod class at {modClassPath}");
                return;
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
            if (lastImportIndex < 0) 
            {
                // No imports found, add after package
                int packageEnd = content.IndexOf(';');
                if (packageEnd > 0)
                {
                    content = content.Insert(packageEnd + 2, importStatement);
                }
            }
            else
            {
                int lastImportEnd = content.IndexOf(';', lastImportIndex) + 1;
                content = content.Insert(lastImportEnd + 1, importStatement);
            }
            
            // Find onInitialize method
            int initIndex = content.IndexOf("public void onInitialize() {");
            if (initIndex == -1)
            {
                initIndex = content.IndexOf("public void onInitialize(");
            }
            
            if (initIndex != -1)
            {
                // Find the opening brace
                int openingBrace = content.IndexOf('{', initIndex);
                
                // Find the closing brace for the method
                int closeBraceIndex = FindClosingBrace(content, openingBrace);
                
                // Add registration line just before closing brace
                string registrationLine = $"\n        {itemClassName}Item.register();";
                content = content.Insert(closeBraceIndex, registrationLine);
                
                File.WriteAllText(modClassPath, content);
                Debug.WriteLine($"Updated Fabric mod class at {modClassPath}");
            }
        }

        private string GenerateFabricModClass(string packageName, string modId, string itemClassName)
        {
            var modClassName = FormatClassName(modId) + "Mod";
            
            return $@"package {packageName};

import net.fabricmc.api.ModInitializer;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import {packageName}.item.{itemClassName}Item;

public class {modClassName} implements ModInitializer {{
    public static final String MOD_ID = ""{modId}"";
    public static final Logger LOGGER = LoggerFactory.getLogger(MOD_ID);

    @Override
    public void onInitialize() {{
        LOGGER.info(""{modClassName} initializing..."");
        {itemClassName}Item.register();
    }}
}}";
        }

        private string GenerateForgeItemRegistryClass(string packageName, string firstItemClassName)
        {
            var modId = packageName.Substring(packageName.LastIndexOf('.') + 1);
            var modClassName = FormatClassName(modId) + "Mod";
            
            return $@"package {packageName}.registry;

import net.minecraft.world.item.Item;
import net.minecraftforge.registries.DeferredRegister;
import net.minecraftforge.registries.ForgeRegistries;
import net.minecraftforge.registries.RegistryObject;
import net.minecraftforge.eventbus.api.IEventBus;
import {packageName}.{modClassName};
import {packageName}.item.{firstItemClassName}Item;

public class ItemRegistry {{
    
    public static final DeferredRegister<Item> ITEMS = DeferredRegister.create(ForgeRegistries.ITEMS, 
        {modClassName}.MOD_ID);
    
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
                Debug.WriteLine($"Warning: Could not find main Forge mod class at {modClassPath}");
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
            if (lastImportIndex >= 0)
            {
                int lastImportEnd = content.IndexOf(';', lastImportIndex) + 1;
                content = content.Insert(lastImportEnd + 1, importStatement);
                
                // Find constructor or initialization method
                int constructorIndex = content.IndexOf("public " + modClassName + "(");
                if (constructorIndex != -1)
                {
                    constructorIndex += modClassName.Length + 8;
                    // Find the closing brace for the constructor
                    int closeBraceIndex = FindClosingBrace(content, constructorIndex);
                    
                    // Add registration line just before closing brace
                    string registrationLine = "\n        ItemRegistry.register(modEventBus);";
                    content = content.Insert(closeBraceIndex, registrationLine);
                    
                    File.WriteAllText(modClassPath, content);
                    Debug.WriteLine($"Updated Forge mod class at {modClassPath}");
                }
            }
        }

        private void HandleTexture(string projectPath, string texturePath, string modId, string itemName)
        {
            try
            {
                // Determine destination texture path
                var resourcesDir = FindResourcesDirectory(projectPath);
                if (resourcesDir == null)
                {
                    resourcesDir = Path.Combine(projectPath, "src", "main", "resources");
                    Directory.CreateDirectory(resourcesDir);
                }
                
                var textureDestDir = Path.Combine(resourcesDir, "assets", modId, "textures", "item");
                Directory.CreateDirectory(textureDestDir);
                
                var textureFileName = ToSnakeCase(itemName) + ".png";
                var textureDestPath = Path.Combine(textureDestDir, textureFileName);
                
                // Copy texture file
                if (File.Exists(texturePath))
                {
                    File.Copy(texturePath, textureDestPath, true);
                    Debug.WriteLine($"Copied texture from {texturePath} to {textureDestPath}");
                }
                else
                {
                    Debug.WriteLine($"Warning: Texture file not found: {texturePath}");
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
                Debug.WriteLine($"Created model JSON at {modelPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling texture: {ex.Message}");
                throw; // Rethrow to show the error to the user
            }
        }

        private void UpdateLanguageFile(string projectPath, string modId, string itemName, string? translationKey = null)
        {
            var resourcesDir = FindResourcesDirectory(projectPath);
            if (resourcesDir == null)
            {
                resourcesDir = Path.Combine(projectPath, "src", "main", "resources");
                Directory.CreateDirectory(resourcesDir);
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
                try
                {
                    // Try parsing as JSON first to handle it properly
                    var json = File.ReadAllText(langPath);
                    var langEntries = JsonSerializer.Deserialize<Dictionary<string, string>>(json, 
                        new JsonSerializerOptions { AllowTrailingCommas = true });
                    
                    if (langEntries == null)
                        langEntries = new Dictionary<string, string>();
                        
                    // Add or update the translation
                    langEntries[translationKey] = itemName;
                    
                    // Write back as nicely formatted JSON
                    var updatedJson = JsonSerializer.Serialize(langEntries, 
                        new JsonSerializerOptions { WriteIndented = true });
                    
                    File.WriteAllText(langPath, updatedJson);
                    Debug.WriteLine($"Updated language file at {langPath}");
                }
                catch
                {
                    // Fallback to simple text manipulation if JSON parsing fails
                    var content = File.ReadAllText(langPath);
                    
                    if (!content.Contains(translationKey))
                    {
                        int lastBraceIndex = content.LastIndexOf('}');
                        if (lastBraceIndex > 0)
                        {
                            var isLastEntry = !content.Substring(0, lastBraceIndex).TrimEnd().EndsWith(",");
                            string newEntry = isLastEntry ? $",\n  \"{translationKey}\": \"{itemName}\"" : $"\n  \"{translationKey}\": \"{itemName}\",";
                            content = content.Insert(lastBraceIndex, newEntry);
                            File.WriteAllText(langPath, content);
                            Debug.WriteLine($"Updated language file (text mode) at {langPath}");
                        }
                    }
                }
            }
            else
            {
                // Create new language file
                var langJson = $@"{{
  ""{translationKey}"": ""{itemName}""
}}";
                File.WriteAllText(langPath, langJson);
                Debug.WriteLine($"Created new language file at {langPath}");
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
                // Try to read from modrix.config first as most reliable source
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
                
                // Try to extract from the project path itself
                var projectName = new DirectoryInfo(projectPath).Name;
                // Guess a reasonable package name from project path
                return $"net.modrix.{projectName.ToLower()}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting package name: {ex.Message}");
            }
            
            // Could not determine package name, use default
            return "net.modrix.mod";
        }

        private string GetModIdFromProject(string projectPath)
        {
            try
            {
                // Try to read from modrix.config as most reliable source
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
                
                // Try to infer from project folder name
                var dirInfo = new DirectoryInfo(projectPath);
                return dirInfo.Name.ToLower();
            }
            catch (Exception ex) 
            {
                Debug.WriteLine($"Error getting mod ID: {ex.Message}");
            }
            
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
            
            // If directory doesn't exist but should, create it
            var mainJavaDir = Path.Combine(projectPath, "src", "main", "java");
            Directory.CreateDirectory(mainJavaDir);
            return mainJavaDir;
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
            
            // Create the standard resources directory if it doesn't exist
            var resourcesDir = Path.Combine(projectPath, "src", "main", "resources");
            Directory.CreateDirectory(resourcesDir);
            return resourcesDir;
        }

        private string FormatClassName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Default";
                
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
                return "item";
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
