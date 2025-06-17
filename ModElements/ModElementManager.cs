using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Modrix.Models;

namespace Modrix.ModElements
{
    /// <summary>
    /// Manages mod elements for a project
    /// </summary>
    public class ModElementManager
    {
        private readonly string _projectPath;
        private readonly string _elementsDirectory;
        private List<ModElementData> _elements = new();

        public IReadOnlyList<ModElementData> Elements => _elements.AsReadOnly();

        public ModElementManager(string projectPath)
        {
            _projectPath = projectPath;
            _elementsDirectory = Path.Combine(_projectPath, "modrix", "elements");
            
            // Ensure directories exist
            Directory.CreateDirectory(_elementsDirectory);
        }

        /// <summary>
        /// Loads all mod elements from the project
        /// </summary>
        public async Task LoadElementsAsync()
        {
            _elements.Clear();

            if (!Directory.Exists(_elementsDirectory))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(_elementsDirectory, "*.json"))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var element = JsonSerializer.Deserialize<ModElementData>(json, new JsonSerializerOptions());
                    if (element != null)
                    {
                        _elements.Add(element);
                    }
                }
                catch (Exception ex)
                {
                    // Log error or handle exception
                    MessageBox.Show($"Error loading mod element: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Saves a mod element to the project
        /// </summary>
        public async Task SaveElementAsync(ModElementData element)
        {
            element.UpdateLastModified();
            
            var fileName = $"{element.Id}.json";
            var filePath = Path.Combine(_elementsDirectory, fileName);
            
            var json = element.ToJson();
            await File.WriteAllTextAsync(filePath, json);
            
            // Update in-memory list
            var existingIndex = _elements.FindIndex(e => e.Id == element.Id);
            if (existingIndex >= 0)
            {
                _elements[existingIndex] = element;
            }
            else
            {
                _elements.Add(element);
            }
        }

        /// <summary>
        /// Generates code for a mod element
        /// </summary>
        public async Task GenerateCodeAsync(ModElementData element, IModElementGenerator generator)
        {
            var context = new ModElementGenerationContext
            {
                ProjectPath = _projectPath,
                ModLoader = GetModLoaderFromProject(),
                MinecraftVersion = GetMinecraftVersionFromProject()
            };

            // Add parameters from the element
            foreach (var property in element.GetType().GetProperties())
            {
                var value = property.GetValue(element);
                context.Parameters[property.Name] = value;
            }

            // Generate the code
            generator.Generate(context);

            // Update the element as generated
            await SaveElementAsync(element);
        }

        /// <summary>
        /// Deletes a mod element from the project
        /// </summary>
        public async Task DeleteElementAsync(ModElementData element)
        {
            var fileName = $"{element.Id}.json";
            var filePath = Path.Combine(_elementsDirectory, fileName);
            
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            
            _elements.RemoveAll(e => e.Id == element.Id);

            // Could also remove generated code here
        }

        private string GetModLoaderFromProject()
        {
            try
            {
                // Attempt to read from modrix.config
                var configPath = Path.Combine(_projectPath, "modrix.config");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<ModProjectData>(json);
                    return config?.ModType ?? "Forge"; // Default to Forge
                }
            }
            catch
            {
                // Fall back to default
            }
            return "Forge";
        }

        private string GetMinecraftVersionFromProject()
        {
            try
            {
                // Attempt to read from modrix.config
                var configPath = Path.Combine(_projectPath, "modrix.config");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<ModProjectData>(json);
                    return config?.MinecraftVersion ?? "1.21.x"; // Default version
                }
            }
            catch
            {
                // Fall back to default
            }
            return "1.21.x";
        }
    }
}