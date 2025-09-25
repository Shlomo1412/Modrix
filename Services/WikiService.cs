using System.Collections.ObjectModel;
using Modrix.Models;

namespace Modrix.Services
{
    public class WikiService
    {
        private static WikiService? _instance;
        public static WikiService Instance => _instance ??= new WikiService();

        private readonly Dictionary<string, WikiEntry> _entries = new();
        private readonly Dictionary<string, WikiCategory> _categories = new();

        public ObservableCollection<WikiCategory> Categories { get; } = new();
        public ObservableCollection<WikiEntry> AllEntries { get; } = new();

        private WikiService() 
        {
            InitializeCommonEntries();
        }

        /// <summary>
        /// Initialize the wiki with common entries that are useful across the application
        /// </summary>
        private void InitializeCommonEntries()
        {
            // Add some fundamental entries that don't require UI tooltips
            RegisterWikiEntry(new WikiEntry
            {
                Id = "getting-started",
                Title = "Getting Started with Modding",
                Category = "General",
                Description = "Minecraft modding involves creating modifications that add new features, blocks, items, or gameplay mechanics to Minecraft. Start by choosing a mod loader (Fabric or Forge) and setting up your development environment with the right Java version.",
                Keywords = new[] { "modding", "getting started", "beginner", "tutorial", "setup" }
            });

            RegisterWikiEntry(new WikiEntry
            {
                Id = "project-structure",
                Title = "Project Structure",
                Category = "Projects",
                Description = "A typical mod project contains several key directories: src/main/java for your Java code, src/main/resources for assets and data, and configuration files like build.gradle and fabric.mod.json (or mods.toml for Forge).",
                Keywords = new[] { "project", "structure", "directories", "organization", "files" }
            });

            RegisterWikiEntry(new WikiEntry
            {
                Id = "asset-naming",
                Title = "Asset Naming Conventions",
                Category = "General",
                Description = "Assets in Minecraft follow strict naming conventions. Use only lowercase letters, numbers, and underscores. Paths should be namespaced with your mod ID (e.g., mymod:block/stone). This prevents conflicts with other mods.",
                Keywords = new[] { "naming", "conventions", "assets", "namespace", "modid" }
            });

            // Add entries to ensure all categories exist
            RegisterWikiEntry(new WikiEntry
            {
                Id = "model-basics",
                Title = "Model Basics",
                Category = "Models",
                Description = "3D models in Minecraft are defined using JSON files that specify geometry, textures, and display settings. Most models are created using tools like Blockbench and must follow Minecraft's model format.",
                Keywords = new[] { "models", "json", "blockbench", "3d", "geometry" }
            });

            RegisterWikiEntry(new WikiEntry
            {
                Id = "texture-basics",
                Title = "Texture Basics",
                Category = "Textures",
                Description = "Textures are PNG images that define the appearance of blocks, items, and entities. Standard Minecraft textures are 16x16 pixels, though higher resolutions are supported for resource packs.",
                Keywords = new[] { "textures", "png", "16x16", "images", "pixels" }
            });

            RegisterWikiEntry(new WikiEntry
            {
                Id = "development-tools",
                Title = "Development Tools",
                Category = "Tools",
                Description = "Essential tools for Minecraft modding include your IDE (IntelliJ IDEA or Eclipse), Blockbench for models, image editors for textures, and version control systems like Git.",
                Keywords = new[] { "tools", "ide", "blockbench", "git", "development" }
            });

            // Advanced modding topics
            RegisterWikiEntry(new WikiEntry
            {
                Id = "data-generation",
                Title = "Data Generation",
                Category = "Tools",
                Description = "Data generation automatically creates JSON files for recipes, loot tables, advancements, and tags. This prevents errors and ensures consistency across your mod's data files.",
                Keywords = new[] { "data", "generation", "recipes", "loot tables", "json", "automation" }
            });

            RegisterWikiEntry(new WikiEntry
            {
                Id = "mixins-overview",
                Title = "Mixins",
                Category = "Tools",
                Description = "Mixins allow you to modify existing Minecraft code without directly editing it. They're powerful but should be used carefully to maintain compatibility with other mods.",
                Keywords = new[] { "mixins", "injection", "modification", "compatibility", "advanced" }
            });

            RegisterWikiEntry(new WikiEntry
            {
                Id = "registries",
                Title = "Registries",
                Category = "General",
                Description = "Registries are how Minecraft keeps track of all blocks, items, entities, and other content. Everything you add to the game must be registered with the appropriate registry using your mod's namespace.",
                Keywords = new[] { "registry", "registration", "blocks", "items", "namespace", "content" }
            });

            RegisterWikiEntry(new WikiEntry
            {
                Id = "client-server",
                Title = "Client vs Server",
                Category = "General",
                Description = "Minecraft has two sides: client (what players see) and server (game logic). Some code only runs on one side. Understanding this distinction is crucial for multiplayer compatibility.",
                Keywords = new[] { "client", "server", "sides", "multiplayer", "compatibility", "logic" }
            });

            RegisterWikiEntry(new WikiEntry
            {
                Id = "resource-packs",
                Title = "Resource Packs",
                Category = "Textures",
                Description = "Resource packs change the game's appearance without code modifications. They can replace textures, models, sounds, and add custom font characters. Resource packs are easier to create but more limited than mods.",
                Keywords = new[] { "resource pack", "textures", "models", "sounds", "appearance", "visual" }
            });

            // ResourcePack-specific entries
            RegisterWikiEntry(new WikiEntry
            {
                Id = "resource-pack-overrides",
                Title = "Resource Pack Overrides",
                Category = "Textures",
                Description = "Overrides allow you to replace default Minecraft assets with your custom versions. Place textures in overrides/textures/ and translations in overrides/translations/ to override the vanilla Minecraft files.",
                Keywords = new[] { "resource pack", "overrides", "textures", "translations", "minecraft", "custom", "replace" }
            });

            RegisterWikiEntry(new WikiEntry
            {
                Id = "minecraft-textures",
                Title = "Minecraft Textures",
                Category = "Textures",
                Description = "Browse all original Minecraft textures organized by category. You can view, copy, and create overrides from these base textures. Use these as reference when creating your custom resource pack.",
                Keywords = new[] { "minecraft", "textures", "vanilla", "assets", "browse", "original", "reference" }
            });

            RegisterWikiEntry(new WikiEntry
            {
                Id = "minecraft-translations",
                Title = "Minecraft Translations",
                Category = "Textures",
                Description = "Browse and edit Minecraft's language files to create custom translations. Each language file contains key-value pairs that define text shown in the game interface, items, blocks, and more.",
                Keywords = new[] { "minecraft", "translations", "language", "localization", "lang", "json", "interface" }
            });

            RegisterWikiEntry(new WikiEntry
            {
                Id = "resource-pack-properties",
                Title = "Resource Pack Properties",
                Category = "Textures",
                Description = "Configure your resource pack's metadata including name, description, pack format, and icon. These properties are displayed in Minecraft's resource pack selection menu.",
                Keywords = new[] { "resource pack", "properties", "metadata", "pack.mcmeta", "pack format", "description", "configuration" }
            });

            RegisterWikiEntry(new WikiEntry
            {
                Id = "pack-format",
                Title = "Pack Format",
                Category = "Textures",
                Description = "The pack format determines which Minecraft versions your resource pack is compatible with. Each major Minecraft version typically has its own pack format number.",
                Keywords = new[] { "format", "version", "compatibility", "minecraft", "pack format", "versions" }
            });

            RegisterWikiEntry(new WikiEntry
            {
                Id = "pack-icon",
                Title = "Pack Icon",
                Category = "Textures",
                Description = "The pack icon appears in Minecraft's resource pack menu. It should be a square PNG image, typically 128x128 or 256x256 pixels for best quality.",
                Keywords = new[] { "icon", "pack.png", "image", "thumbnail", "preview", "128x128", "256x256" }
            });

            RegisterWikiEntry(new WikiEntry
            {
                Id = "debugging",
                Title = "Debugging Your Mod",
                Category = "Tools",
                Description = "Debugging involves finding and fixing issues in your mod. Use the console output to identify errors, add logging statements to track execution, and test thoroughly in both single and multiplayer environments.",
                Keywords = new[] { "debugging", "errors", "logging", "testing", "console", "troubleshooting" }
            });

            RegisterWikiEntry(new WikiEntry
            {
                Id = "performance",
                Title = "Performance Considerations",
                Category = "General",
                Description = "Mod performance affects gameplay experience. Avoid heavy computations in tick events, cache expensive calculations, and be mindful of memory usage. Profile your mod to identify bottlenecks.",
                Keywords = new[] { "performance", "optimization", "ticking", "memory", "lag", "fps" }
            });

            RegisterWikiEntry(new WikiEntry
            {
                Id = "publishing",
                Title = "Publishing Your Mod",
                Category = "General",
                Description = "Publishing involves uploading your mod to platforms like CurseForge or Modrinth. Prepare clear descriptions, screenshots, and ensure your mod works properly before release. Consider versioning and update policies.",
                Keywords = new[] { "publishing", "curseforge", "modrinth", "release", "distribution", "versioning" }
            });

            RegisterWikiEntry(new WikiEntry
            {
                Id = "dependencies",
                Title = "Mod Dependencies",
                Category = "Projects",
                Description = "Dependencies are other mods your mod requires to function. Declare them in your mod metadata file. Consider soft dependencies for optional integrations. Too many dependencies can limit your mod's adoption.",
                Keywords = new[] { "dependencies", "requirements", "compatibility", "integration", "metadata" }
            });

            RegisterWikiEntry(new WikiEntry
            {
                Id = "version-control",
                Title = "Version Control with Git",
                Category = "Tools",
                Description = "Git tracks changes to your code over time and enables collaboration. Initialize a Git repository for your mod project, commit changes regularly, and consider hosting on GitHub for backup and sharing.",
                Keywords = new[] { "git", "version control", "github", "backup", "collaboration", "commits" }
            });
        }

        public void RegisterWikiEntry(WikiEntry entry)
        {
            if (string.IsNullOrEmpty(entry.Id))
                return;

            // Add or update entry
            _entries[entry.Id] = entry;
            
            // Update AllEntries collection
            var existingEntry = AllEntries.FirstOrDefault(e => e.Id == entry.Id);
            if (existingEntry != null)
            {
                var index = AllEntries.IndexOf(existingEntry);
                AllEntries[index] = entry;
            }
            else
            {
                AllEntries.Add(entry);
            }

            // Update category
            if (!string.IsNullOrEmpty(entry.Category))
            {
                if (!_categories.ContainsKey(entry.Category))
                {
                    var category = new WikiCategory 
                    { 
                        Name = entry.Category,
                        Description = GetCategoryDescription(entry.Category)
                    };
                    _categories[entry.Category] = category;
                    Categories.Add(category);
                }

                var cat = _categories[entry.Category];
                var existingInCategory = cat.Entries.FirstOrDefault(e => e.Id == entry.Id);
                if (existingInCategory != null)
                {
                    var index = cat.Entries.IndexOf(existingInCategory);
                    cat.Entries[index] = entry;
                }
                else
                {
                    cat.Entries.Add(entry);
                }
            }
        }

        public WikiEntry? GetEntry(string id)
        {
            return _entries.TryGetValue(id, out var entry) ? entry : null;
        }

        public List<WikiEntry> SearchEntries(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return AllEntries.ToList();

            var lowerSearchTerm = searchTerm.ToLowerInvariant();
            
            return AllEntries.Where(entry =>
                entry.Title.ToLowerInvariant().Contains(lowerSearchTerm) ||
                entry.Description.ToLowerInvariant().Contains(lowerSearchTerm) ||
                entry.Category.ToLowerInvariant().Contains(lowerSearchTerm) ||
                entry.Keywords.Any(k => k.ToLowerInvariant().Contains(lowerSearchTerm))
            ).ToList();
        }

        public List<WikiEntry> GetEntriesByCategory(string category)
        {
            return _categories.TryGetValue(category, out var cat) ? cat.Entries : new List<WikiEntry>();
        }

        private static string GetCategoryDescription(string category)
        {
            return category switch
            {
                "Models" => "Information about 3D models, textures, and related concepts",
                "Textures" => "Details about texture files, formats, and mapping",
                "Projects" => "Project management and workspace concepts",
                "Tools" => "Development tools and utilities",
                "General" => "General application concepts and features",
                _ => $"Information related to {category}"
            };
        }

        public void ClearEntries()
        {
            _entries.Clear();
            _categories.Clear();
            Categories.Clear();
            AllEntries.Clear();
        }
    }
}