using System.Collections.Generic;
using System.Windows.Controls;

namespace Modrix.ModElements
{
    public interface IModElementGenerator
    {
        string Name { get; } // e.g., "Block"
        string Description { get; } // Short description
        string Icon { get; } // Path to icon resource or identifier
        IReadOnlyList<string> SupportedModLoaders { get; } // e.g., ["Forge", "Fabric"]
        IReadOnlyList<string> SupportedMinecraftVersions { get; } // e.g., ["1.20.1", "1.19.4"]
        // Any additional technical details can be added as needed

        /// <summary>
        /// Generates the code/files for this mod element.
        /// </summary>
        /// <param name="context">Context with project info, user input, etc.</param>
        void Generate(ModElementGenerationContext context);

        /// <summary>
        /// Returns the XAML Page (UserControl or Page) for this generator, for use in the workspace.
        /// </summary>
        /// <returns>A WPF Page or UserControl instance for editing/creating this mod element.</returns>
        Page CreatePage();
    }

    public class ModElementGenerationContext
    {
        public string ProjectPath { get; set; }
        public string ModLoader { get; set; }
        public string MinecraftVersion { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }
}
