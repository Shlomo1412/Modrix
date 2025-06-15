using System.Collections.Generic;

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
    }

    public class ModElementGenerationContext
    {
        // Project directory, user input, etc. Add properties as needed.
        public string ProjectPath { get; set; }
        public string ModLoader { get; set; }
        public string MinecraftVersion { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }
}
