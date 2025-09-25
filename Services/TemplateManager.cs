using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Modrix.Models;
using Modrix.Services;

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

            if (!Directory.Exists(projectsDir)) return projects;

            foreach (var projectDir in Directory.GetDirectories(projectsDir))
            {
                var configPath = Path.Combine(projectDir, "modrix.config");
                if (File.Exists(configPath))
                {
                    try
                    {
                        var lines = File.ReadAllLines(configPath);
                        var modType = GetConfigValue(lines, "ModType");
                        
                        if (modType == "Resource Pack")
                        {
                            // Convert ResourcePack to ModProjectData for display compatibility
                            var resourcePack = LoadResourcePackFromConfig(projectDir, lines);
                            if (resourcePack != null)
                            {
                                projects.Add(resourcePack);
                            }
                        }
                        else
                        {
                            // Regular mod project
                            var project = new ModProjectData
                            {
                                Location = projectDir,
                                Package = GetConfigValue(lines, "Package"),
                                ModId = GetConfigValue(lines, "ModId"),
                                Name = GetConfigValue(lines, "Name"),
                                ModType = modType,
                                IconPath = GetFullIconPath(projectDir, GetConfigValue(lines, "IconPath")),
                                MinecraftVersion = GetConfigValue(lines, "MinecraftVersion"),
                                Description = GetConfigValue(lines, "Description"),
                                Authors = GetConfigValue(lines, "Authors"),
                                License = GetConfigValue(lines, "License"),
                                Version = GetConfigValue(lines, "Version") ?? "1.0.0"
                            };
                            projects.Add(project);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading project from {projectDir}: {ex.Message}");
                    }
                }
            }
            return projects;
        }

        private static ModProjectData? LoadResourcePackFromConfig(string projectDir, string[] lines)
        {
            try
            {
                return new ModProjectData
                {
                    Location = projectDir,
                    Package = "", // Resource packs don't use packages
                    ModId = GetConfigValue(lines, "ModId"),
                    Name = GetConfigValue(lines, "Name"),
                    ModType = "Resource Pack",
                    IconPath = GetFullIconPath(projectDir, GetConfigValue(lines, "IconPath")),
                    MinecraftVersion = GetConfigValue(lines, "MinecraftVersion"),
                    Description = GetConfigValue(lines, "Description"),
                    Authors = GetConfigValue(lines, "Authors"),
                    License = GetConfigValue(lines, "License"),
                    Version = GetConfigValue(lines, "Version") ?? "1.0.0"
                };
            }
            catch
            {
                return null;
            }
        }

        public static List<ResourcePackData> LoadAllResourcePacks()
        {
            var resourcePacks = new List<ResourcePackData>();
            var projectsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Modrix",
                "Projects"
            );

            if (!Directory.Exists(projectsDir)) return resourcePacks;

            var manager = new ResourcePackTemplateManager();
            
            foreach (var projectDir in Directory.GetDirectories(projectsDir))
            {
                var configPath = Path.Combine(projectDir, "modrix.config");
                if (File.Exists(configPath))
                {
                    try
                    {
                        var lines = File.ReadAllLines(configPath);
                        var modType = GetConfigValue(lines, "ModType");
                        
                        if (modType == "Resource Pack")
                        {
                            var pack = manager.ReadResourcePack(projectDir);
                            resourcePacks.Add(pack);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading resource pack from {projectDir}: {ex.Message}");
                    }
                }
            }
            
            return resourcePacks;
        }

        private static string GetFullIconPath(string projectDir, string iconPath)
        {
            if (string.IsNullOrEmpty(iconPath)) return "";
            
            // If it's already a full path, return it
            if (Path.IsPathRooted(iconPath)) return iconPath;
            
            // Otherwise, combine with project directory
            var fullPath = Path.Combine(projectDir, iconPath);
            return File.Exists(fullPath) ? fullPath : "";
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
