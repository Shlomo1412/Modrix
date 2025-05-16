using System;
using System.IO;
using System.Threading.Tasks;
using LibGit2Sharp;
using Modrix.Models;

namespace Modrix.Services
{
    public class FabricTemplateManager
    {
        public async Task FullSetupWithGradle(ModProjectData data, IProgress<(string Message, int Progress)> progress)
        {
            await Task.Run(() =>
            {
                try
                {
                    progress.Report(("Cloning Fabric example mod...", 10));
                    CloneTemplateRepository(data.Location);

                    progress.Report(("Updating mod metadata...", 30));
                    UpdateModMetadata(data);

                    progress.Report(("Configuring build files...", 50));
                    UpdateBuildFiles(data);

                    progress.Report(("Finalizing setup...", 80));
                    CleanupTemplateFiles(data.Location);

                    progress.Report(("Project ready!", 100));
                }
                catch (Exception ex)
                {
                    throw new Exception($"Fabric setup failed: {ex.Message}");
                }
            });
        }

        private void CloneTemplateRepository(string targetPath)
        {
            if (Directory.Exists(targetPath))
            {
                Directory.Delete(targetPath, true);
            }

            Repository.Clone("https://github.com/FabricMC/fabric-example-mod.git", targetPath, new CloneOptions
            {
                Checkout = true,
                RecurseSubmodules = true
            });
        }

        private void UpdateModMetadata(ModProjectData data)
        {
            // Update gradle.properties
            var gradlePropsPath = Path.Combine(data.Location, "gradle.properties");
            var gradleProps = File.ReadAllText(gradlePropsPath)
                .Replace("mod_version=0.0.1", $"mod_version={data.Version}")
                .Replace("maven_group=com.example", $"maven_group={data.Package}")
                .Replace("archives_base_name=modid", $"archives_base_name={data.ModId}");

            File.WriteAllText(gradlePropsPath, gradleProps);

            // Update main mod class
            var packagePath = data.Package.Replace('.', Path.DirectorySeparatorChar);
            var srcPath = Path.Combine(data.Location, "src", "main", "java", packagePath);
            Directory.CreateDirectory(srcPath);

            var exampleModPath = Path.Combine(data.Location, "src", "main", "java", "net", "fabricmc", "example");
            if (Directory.Exists(exampleModPath))
            {
                Directory.Move(exampleModPath, srcPath);
            }

            var modFile = Path.Combine(srcPath, $"{data.ModId}Mod.java");
            if (File.Exists(modFile))
            {
                var content = File.ReadAllText(modFile)
                    .Replace("net.fabricmc.example", data.Package)
                    .Replace("ExampleMod", $"{SanitizeClassName(data.Name)}Mod");
                File.WriteAllText(modFile, content);
            }
        }

        private void UpdateBuildFiles(ModProjectData data)
        {
            // Update fabric.mod.json
            var modJsonPath = Path.Combine(data.Location, "src", "main", "resources", "fabric.mod.json");
            var modJson = File.ReadAllText(modJsonPath)
                .Replace("\"id\": \"example-mod\"", $"\"id\": \"{data.ModId}\"")
                .Replace("\"name\": \"Example Mod\"", $"\"name\": \"{data.Name}\"")
                .Replace("\"version\": \"${version}\"", $"\"version\": \"{data.Version}\"")
                .Replace("net.fabricmc.example", data.Package);

            if (!string.IsNullOrEmpty(data.Description))
            {
                modJson = modJson.Replace("\"description\": \"Example description!\"",
                    $"\"description\": \"{data.Description}\"");
            }

            if (!string.IsNullOrEmpty(data.Authors))
            {
                var authors = string.Join(", ", data.Authors.Split(',').Select(a => $"\"{a.Trim()}\""));
                modJson = modJson.Replace(
                    "\"authors\": [\n    \"FabricMC\"\n  ]",
                    $"\"authors\": [\n    {authors}\n  ]");
            }

            File.WriteAllText(modJsonPath, modJson);
        }

        private void CleanupTemplateFiles(string projectPath)
        {
            try
            {
                var gitPath = Path.Combine(projectPath, ".git");
                if (Directory.Exists(gitPath))
                {
                    Directory.Delete(gitPath, true);
                }
            }
            catch { /* Ignore cleanup errors */ }
        }

        private string SanitizeClassName(string name)
        {
            // Remove invalid characters for class names
            var invalidChars = new[] { ' ', '-', '.', ':', '&', '/', '\\' };
            return string.Concat(name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}