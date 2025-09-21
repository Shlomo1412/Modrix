using System.Windows;
using System.Windows.Controls;
using Modrix.Views.Controls;

namespace Modrix.Helpers
{
    /// <summary>
    /// Helper class for easily adding WikiTooltips to UI elements
    /// </summary>
    public static class WikiHelper
    {
        /// <summary>
        /// Creates a WikiTooltip with the specified parameters
        /// </summary>
        /// <param name="wikiId">Unique identifier for the wiki entry</param>
        /// <param name="title">Title of the wiki entry</param>
        /// <param name="category">Category (e.g., "Models", "Textures", "Tools", etc.)</param>
        /// <param name="description">Detailed description</param>
        /// <param name="keywords">Comma-separated keywords for searching</param>
        /// <returns>A configured WikiTooltip control</returns>
        public static WikiTooltip CreateTooltip(string wikiId, string title, string category, string description, string keywords = "")
        {
            return new WikiTooltip
            {
                WikiId = wikiId,
                Title = title,
                Category = category,
                Description = description,
                Keywords = keywords,
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        /// <summary>
        /// Adds a WikiTooltip to a StackPanel (useful for headers)
        /// </summary>
        /// <param name="panel">The StackPanel to add the tooltip to</param>
        /// <param name="wikiId">Unique identifier for the wiki entry</param>
        /// <param name="title">Title of the wiki entry</param>
        /// <param name="category">Category</param>
        /// <param name="description">Detailed description</param>
        /// <param name="keywords">Comma-separated keywords</param>
        public static void AddTooltipToPanel(StackPanel panel, string wikiId, string title, string category, string description, string keywords = "")
        {
            var tooltip = CreateTooltip(wikiId, title, category, description, keywords);
            panel.Children.Add(tooltip);
        }

        /// <summary>
        /// Common wiki entries that can be reused across the application
        /// </summary>
        public static class CommonEntries
        {
            public static WikiTooltip ProjectManagement => CreateTooltip(
                "project-management",
                "Project Management",
                "Projects",
                "Projects in Modrix contain all your mod files including code, resources, and configuration. Each project represents a single Minecraft mod with its own mod ID, version, and dependencies.",
                "project,workspace,mod,management"
            );

            public static WikiTooltip ModLoaders => CreateTooltip(
                "mod-loaders",
                "Mod Loaders",
                "General",
                "Mod loaders like Fabric and Forge provide the framework for running mods in Minecraft. Fabric is lightweight and updates quickly, while Forge has more features but takes longer to update to new Minecraft versions.",
                "fabric,forge,modloader,minecraft"
            );

            public static WikiTooltip MinecraftVersions => CreateTooltip(
                "minecraft-versions",
                "Minecraft Versions",
                "General",
                "Different Minecraft versions have different capabilities and require different development tools. Always check which mod loader versions and Java versions are compatible with your target Minecraft version.",
                "minecraft,version,compatibility,java"
            );

            public static WikiTooltip BuildProcess => CreateTooltip(
                "build-process",
                "Build Process",
                "Tools",
                "Building your mod compiles your Java code and packages all resources into a JAR file that can be loaded by Minecraft. The build process uses Gradle to manage dependencies and compilation.",
                "build,gradle,compile,jar,development"
            );
        }
    }
}