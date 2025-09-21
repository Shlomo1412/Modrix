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