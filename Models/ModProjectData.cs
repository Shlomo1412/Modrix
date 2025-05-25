
namespace Modrix.Models
{
    public class ModProjectData
    {
        public required string Name { get; set; }
        public required string ModId { get; set; }
        public required string Package { get; set; }
        public required string Location { get; set; }
        public string? IconPath { get; set; }
        public required string ModType { get; set; }
        public required string MinecraftVersion { get; set; }
        public string Description { get; set; }
        public string Authors { get; set; }
        public string License { get; set; }
        public string ModVersion { get; set; } = "1.0.0";

        public string ProjectDir { get; set; }
        public string SrcDir { get; set; }
        public string JavaDir { get; set; }
        public string ResourcesDir { get; set; }
        public string PackageDir { get; set; }

        public string Version { get; set; } = "1.0.0";

        public bool IncludeReadme { get; set; }

        public ModProjectData()
        {

            ProjectDir = string.Empty;
            SrcDir = string.Empty;
            JavaDir = string.Empty;
            ResourcesDir = string.Empty;
            PackageDir = string.Empty;
            Description = string.Empty;
            Authors = string.Empty;
            License = string.Empty;
        }
    }
}
