using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Modrix.Models;

namespace Modrix.Services
{
    public class TemplateManager
    {
        public static readonly string ProjectsBasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Modrix",
            "Projects"
        );

        public TemplateManager()
        {
            Directory.CreateDirectory(ProjectsBasePath);
        }

        public static List<ModProjectData> LoadAllProjects()
        {
            var projects = new List<ModProjectData>();
            var projectsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Modrix",
                "Projects"
            );

            foreach (var projectDir in Directory.GetDirectories(projectsDir))
            {
                var configPath = Path.Combine(projectDir, "modrix.config");
                if (File.Exists(configPath))
                {
                    var lines = File.ReadAllLines(configPath);
                    var project = new ModProjectData
                    {
                        Location = projectDir,
                        Package = GetConfigValue(lines, "Package"),
                        ModId = GetConfigValue(lines, "ModId"),
                        Name = GetConfigValue(lines, "Name"),
                        ModType = GetConfigValue(lines, "ModType"),
                        IconPath = GetConfigValue(lines, "IconPath"),
                        MinecraftVersion = GetConfigValue(lines, "MinecraftVersion")
                    };
                    projects.Add(project);
                }
            }
            return projects;
        }

        private static string GetConfigValue(string[] lines, string key)
        {
            return lines.FirstOrDefault(l => l.StartsWith(key + "="))?.Split('=')[1] ?? string.Empty;
        }

        private static ModProjectData? ParseProjectData(string projectDir, string modToml, string gradleProperties)
        {
            try
            {
                var modId = Path.GetFileName(projectDir);
                var iconPath = Path.Combine(projectDir, "src", "main", "resources", "icon.png");
                
                var gradleProps = File.ReadAllLines(gradleProperties)
                    .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                    .ToDictionary(
                        line => line.Split('=')[0].Trim(),
                        line => line.Split('=')[1].Trim()
                    );

                return new ModProjectData
                {
                    Name = gradleProps.GetValueOrDefault("mod_name", modId),
                    ModId = modId,
                    Package = gradleProps.GetValueOrDefault("package", ""),
                    Location = projectDir,
                    IconPath = File.Exists(iconPath) ? iconPath : null,
                    ModType = "Forge Mod",
                    MinecraftVersion = gradleProps.GetValueOrDefault("minecraft_version", ""),
                    Description = gradleProps.GetValueOrDefault("mod_description", ""),
                    Authors = gradleProps.GetValueOrDefault("mod_authors", ""),
                    License = gradleProps.GetValueOrDefault("mod_license", ""),
                    ModVersion = gradleProps.GetValueOrDefault("mod_version", "1.0.0")
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
