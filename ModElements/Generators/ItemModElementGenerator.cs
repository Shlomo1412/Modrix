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
        public string Icon => "pack://application:,,,/Resources/Icons/ItemIcon.png";
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

            if (string.IsNullOrWhiteSpace(context.ModLoader))
            {
                throw new InvalidOperationException($"ModLoader is not set in context for project {context.ProjectPath}. This is a bug in the project setup or manager.");
            }

            Debug.WriteLine($"[ItemModElementGenerator] Generating item for mod loader: {context.ModLoader}, MC Version: {context.MinecraftVersion} (project: {context.ProjectPath})");

            switch (context.ModLoader.ToLowerInvariant())
            {
                case "forge":
                case "neoforge":
                    GenerateForgeItem(context, name, texturePath);
                    break;
                case "fabric":
                    GenerateFabricItem(context, name, texturePath);
                    break;
                default:
                    throw new NotSupportedException($"Unknown or unsupported mod loader: {context.ModLoader}");
            }
        }

        private void GenerateForgeItem(ModElementGenerationContext context, string itemName, string texturePath)
        {
            Debug.WriteLine("Generating Forge item code");
            var projectPath = context.ProjectPath;
            var packageName = GetPackageNameFromProject(projectPath);
            var modId = GetModIdFromProject(projectPath);

            // Format the class name
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
            var itemClass = GenerateForgeItemClass(packageName, itemClassName);
            File.WriteAllText(itemClassPath, itemClass);
            Debug.WriteLine($"Created Forge item class at {itemClassPath}");

            // Copy texture and create model
            HandleTexture(projectPath, texturePath, modId, itemName);

            // Update registry class
            UpdateForgeItemRegistry(projectPath, packageName, itemClassName);

            // Update language file
            UpdateLanguageFile(projectPath, modId, itemName);
        }

        private void GenerateFabricItem(ModElementGenerationContext context, string itemName, string texturePath)
        {
            var minecraftVersion = context.MinecraftVersion ?? "1.20.x";
            Debug.WriteLine($"Generating Fabric item code for MC version: {minecraftVersion}");
            
            var projectPath = context.ProjectPath;
            var packageName = GetPackageNameFromProject(projectPath);
            var modId = GetModIdFromProject(projectPath);

            // Use modId as-is for class name (to match file and class exactly)
            var modClassName = modId + "Mod";
            var itemClassName = FormatClassName(itemName);

            var packagePath = packageName.Replace('.', '/');
            var javaDir = FindJavaDirectory(projectPath);
            if (string.IsNullOrEmpty(javaDir))
                throw new DirectoryNotFoundException("Could not find Java source directory");

            var modClassPath = Path.Combine(javaDir, packagePath.Replace('/', Path.DirectorySeparatorChar), $"{modClassName}.java");
            if (!File.Exists(modClassPath))
            {
                Debug.WriteLine($"Warning: Could not find main Fabric mod class at {modClassPath}. Skipping mod class update.");
                // Do not generate a new mod class file, just skip updating it
            }

            var itemDirPath = Path.Combine(javaDir, packagePath.Replace('/', Path.DirectorySeparatorChar), "item");
            Directory.CreateDirectory(itemDirPath);
            var itemClassPath = Path.Combine(itemDirPath, $"{itemClassName}Item.java");

            string version = NormalizeMinecraftVersion(minecraftVersion);
            bool isModern = IsModernFabricVersion(version);
            Debug.WriteLine($"Using modern Fabric imports: {isModern} for version {version}");

            var itemClass = GenerateFabricItemClass(packageName, modId, itemClassName, itemName, version, isModern, modClassName);
            File.WriteAllText(itemClassPath, itemClass);
            Debug.WriteLine($"Created Fabric item class at {itemClassPath}");

            HandleTexture(projectPath, texturePath, modId, itemName);
            UpdateFabricItemRegistry(projectPath, packageName, itemClassName, modId, modClassName);
            UpdateLanguageFile(projectPath, modId, itemName);
        }

        private string GenerateForgeItemClass(string packageName, string itemClassName)
        {
            return $@"package {packageName}.item;

import net.minecraft.world.item.Item;

public class {itemClassName}Item extends Item {{
    public {itemClassName}Item(Properties properties) {{
        super(properties);
    }}
}}";
        }


        private string GenerateFabricItemClass(string packageName, string modId, string itemClassName, string itemName, string version, bool isModern, string modClassName)
        {
            Debug.WriteLine($"Generating Fabric item class with {(isModern ? "modern" : "legacy")} registry imports");
            Debug.WriteLine($"Using mod class name: {modClassName}");
            if (isModern)
            {
                // Modern Fabric: store registered item in a static field to avoid NPE/translation key crash
                return $@"package {packageName}.item;

import net.minecraft.item.Item;
import net.minecraft.registry.Registries;
import net.minecraft.registry.Registry;
import net.minecraft.util.Identifier;
import {packageName}.{modClassName};

public class {itemClassName}Item extends Item {{
    public static final String ID = ""{ToSnakeCase(itemName)}"";
    public static Item ITEM;

    public {itemClassName}Item() {{
        super(new Item.Settings());
    }}

    public static void register() {{
        ITEM = Registry.register(Registries.ITEM, Identifier.of({modClassName}.MOD_ID, ID), new {itemClassName}Item());
    }}
}}";
            }
            else
            {
                // Legacy Fabric (pre-1.17)
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
            var snakeCaseName = ToSnakeCase(itemClassName);

            if (!File.Exists(registryPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(registryPath));
                var registryClass = GenerateForgeItemRegistryClass(packageName, itemClassName, snakeCaseName);
                File.WriteAllText(registryPath, registryClass);
                Debug.WriteLine($"Created Forge ItemRegistry at {registryPath}");

                UpdateForgeModClass(projectPath, packageName);
            }
            else
            {
                var content = File.ReadAllText(registryPath);

                if (content.Contains($" {ToUpperSnakeCase(itemClassName)} ") ||
                    content.Contains($"{itemClassName}Item"))
                {
                    return;
                }

                string importStatement = $"import {packageName}.item.{itemClassName}Item;\n";
                content = InsertAfterLastImport(content, importStatement);

                string fieldDeclaration = $@"
                public static final RegistryObject<Item> {ToUpperSnakeCase(itemClassName)} = 
                    ITEMS.register(""{snakeCaseName}"", 
                        () -> new {itemClassName}Item(new Item.Properties()));";


                int insertPosition = FindLastRegistryObjectEnd(content);
                if (insertPosition == -1)
                {
                    insertPosition = content.IndexOf("public static void register(");
                    if (insertPosition == -1) insertPosition = content.LastIndexOf('}');
                }

                content = content.Insert(insertPosition, fieldDeclaration);
                File.WriteAllText(registryPath, content);
                Debug.WriteLine($"Updated Forge ItemRegistry at {registryPath}");
            }
        }

        private void UpdateFabricItemRegistry(string projectPath, string packageName, string itemClassName, string modId, string modClassName)
        {
            var javaDir = FindJavaDirectory(projectPath);
            if (string.IsNullOrEmpty(javaDir))
                return;
            var modClassPath = Path.Combine(javaDir, packageName.Replace('.', Path.DirectorySeparatorChar), $"{modClassName}.java");
            if (!File.Exists(modClassPath))
            {
                Debug.WriteLine($"Warning: Could not find main Fabric mod class at {modClassPath}. Skipping mod class update.");
                return;
            }
            var content = File.ReadAllText(modClassPath);
            if (content.Contains($"{itemClassName}Item"))
                return;
            string importStatement = $"import {packageName}.item.{itemClassName}Item;\n";
            int lastImportIndex = content.LastIndexOf("import ");
            if (lastImportIndex < 0)
            {
                int packageEnd = content.IndexOf(';');
                if (packageEnd > 0)
                    content = content.Insert(packageEnd + 2, importStatement);
            }
            else
            {
                int lastImportEnd = content.IndexOf(';', lastImportIndex) + 1;
                content = content.Insert(lastImportEnd + 1, importStatement);
            }
            int initIndex = content.IndexOf("public void onInitialize() {");
            if (initIndex == -1)
                initIndex = content.IndexOf("public void onInitialize(");
            if (initIndex != -1)
            {
                int openingBrace = content.IndexOf('{', initIndex);
                int closeBraceIndex = FindClosingBrace(content, openingBrace);
                string registrationLine = $"\n        {itemClassName}Item.register();";
                content = content.Insert(closeBraceIndex, registrationLine);
                File.WriteAllText(modClassPath, content);
                Debug.WriteLine($"Updated Fabric mod class at {modClassPath}");
            }
        }

        private string GenerateForgeItemRegistryClass(string packageName, string itemClassName, string snakeCaseName)
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
import {packageName}.item.{itemClassName}Item;

public class ItemRegistry {{
    public static final DeferredRegister<Item> ITEMS = 
        DeferredRegister.create(ForgeRegistries.ITEMS, {modClassName}.MOD_ID);
    
    // Items
    public static final RegistryObject<Item> {ToUpperSnakeCase(itemClassName)} = 
        ITEMS.register(""{snakeCaseName}"", 
            () -> new {itemClassName}Item(new Item.Properties()));
    
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

            if (!File.Exists(modClassPath)) return;

            var content = File.ReadAllText(modClassPath);

            // Skip if already registered
            if (content.Contains("ItemRegistry.register(")) return;

            // Add import
            string importStatement = $"import {packageName}.registry.ItemRegistry;\n";
            content = InsertAfterLastImport(content, importStatement);

            // Find constructor
            int constructorIndex = content.IndexOf($"public {modClassName}(");
            if (constructorIndex == -1) return;

            int openBrace = content.IndexOf('{', constructorIndex);
            int closeBrace = FindClosingBrace(content, openBrace);

            // Check if modEventBus is already declared
            bool hasExistingEventBus = content.Contains("IEventBus modEventBus");

            // Add registration call
            string registration;
            if (hasExistingEventBus)
            {
                // Use existing event bus variable
                registration = "\n        ItemRegistry.register(modEventBus);";
            }
            else
            {
                // Create new event bus declaration
                registration = @"
        IEventBus modEventBus = FMLJavaModLoadingContext.get().getModEventBus();
        ItemRegistry.register(modEventBus);";

                // Add FML import if needed
                if (!content.Contains("FMLJavaModLoadingContext"))
                {
                    string fmlImport = "import net.minecraftforge.fml.javafmlmod.FMLJavaModLoadingContext;\n";
                    content = InsertAfterLastImport(content, fmlImport);
                }
            }

            content = content.Insert(closeBrace, registration);
            File.WriteAllText(modClassPath, content);
        }

        private void HandleTexture(string projectPath, string texturePath, string modId, string itemName)
        {
            var resourcesDir = FindResourcesDirectory(projectPath) ??
                Path.Combine(projectPath, "src", "main", "resources");

            Directory.CreateDirectory(resourcesDir);

            // Copy texture
            var textureDestDir = Path.Combine(resourcesDir, "assets", modId, "textures", "item");
            Directory.CreateDirectory(textureDestDir);

            var textureFileName = ToSnakeCase(itemName) + ".png";
            var textureDestPath = Path.Combine(textureDestDir, textureFileName);

            if (File.Exists(texturePath))
            {
                File.Copy(texturePath, textureDestPath, true);
            }

            // Create item model
            var modelDir = Path.Combine(resourcesDir, "assets", modId, "models", "item");
            Directory.CreateDirectory(modelDir);

            var modelPath = Path.Combine(modelDir, $"{ToSnakeCase(itemName)}.json");
            File.WriteAllText(modelPath, $@"{{
  ""parent"": ""item/generated"",
  ""textures"": {{
    ""layer0"": ""{modId}:item/{ToSnakeCase(itemName)}""
  }}
}}");
        }

        private void UpdateLanguageFile(string projectPath, string modId, string itemName)
        {
            var resourcesDir = FindResourcesDirectory(projectPath) ??
                Path.Combine(projectPath, "src", "main", "resources");

            var langDir = Path.Combine(resourcesDir, "assets", modId, "lang");
            Directory.CreateDirectory(langDir);

            var langPath = Path.Combine(langDir, "en_us.json");
            var translationKey = $"item.{modId}.{ToSnakeCase(itemName)}";
            var translation = itemName;

            Dictionary<string, string> langEntries = new();

            if (File.Exists(langPath))
            {
                try
                {
                    langEntries = JsonSerializer.Deserialize<Dictionary<string, string>>(
                        File.ReadAllText(langPath)) ?? new Dictionary<string, string>();
                }
                catch { /* Ignore parse errors */ }
            }

            langEntries[translationKey] = translation;

            File.WriteAllText(langPath, JsonSerializer.Serialize(langEntries,
                new JsonSerializerOptions { WriteIndented = true }));
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
                    // Handle lowercase-only words (like "lols")
                    if (part.All(char.IsLower))
                    {
                        sb.Append(char.ToUpper(part[0]));
                        if (part.Length > 1)
                        {
                            sb.Append(part.Substring(1));
                        }
                    }
                    else
                    {
                        // Original behavior for mixed-case words
                        sb.Append(char.ToUpper(part[0]));
                        if (part.Length > 1)
                        {
                            sb.Append(part.Substring(1));
                        }
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

        private string NormalizeMinecraftVersion(string version)
        {
            // Remove any non-version content like "fabric-" prefix or suffixes
            version = version.Trim();
            
            // Extract just the basic version number (e.g., from "1.20.4-fabric" to "1.20")
            if (version.Contains('.'))
            {
                // Find first dot
                int firstDot = version.IndexOf('.');
                if (firstDot >= 0)
                {
                    // Find second dot
                    int secondDot = version.IndexOf('.', firstDot + 1);
                    if (secondDot > 0)
                    {
                        // Return major.minor (e.g., "1.20")
                        return version.Substring(0, secondDot);
                    }
                    else
                    {
                        // If no second dot, return what we have
                        return version;
                    }
                }
            }
            
            // If no dots, return as is
            return version;
        }

        private string InsertAfterLastImport(string content, string importStatement)
        {
            int lastImport = content.LastIndexOf("import ");
            if (lastImport != -1)
            {
                int endOfImport = content.IndexOf(';', lastImport) + 1;
                return content.Insert(endOfImport, "\n" + importStatement);
            }
            else
            {
                int packageEnd = content.IndexOf(';') + 1;
                return content.Insert(packageEnd, "\n\n" + importStatement);
            }
        }

        private int FindLastRegistryObjectEnd(string content)
        {
            int lastRegistry = content.LastIndexOf("RegistryObject<Item>");
            if (lastRegistry == -1) return -1;

            int endOfLine = content.IndexOf(';', lastRegistry);
            return endOfLine + 1;
        }

        private bool IsModernFabricVersion(string normalizedVersion)
        {
            // Parse major and minor versions
            if (normalizedVersion.Contains('.'))
            {
                string[] parts = normalizedVersion.Split('.');
                if (parts.Length >= 2 && int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
                {
                    // Modern Fabric starts with 1.17
                    if (major > 1 || (major == 1 && minor >= 17))
                    {
                        return true;
                    }
                }
            }
            
            // Default to modern for newer Minecraft versions and undefined versions
            // This ensures we use the current Registry class by default
            return true;
        }

        #endregion
    }
}
