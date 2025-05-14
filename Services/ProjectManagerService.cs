// Services/ProjectManagerService.cs
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Modrix.Models;

namespace Modrix.Services
{
    public class ProjectManagerService
    {
        private readonly string _projectsFilePath;
        private ObservableCollection<ModProjectData> _projects;

        public ProjectManagerService(AppConfig config)
        {
            _projectsFilePath = Path.Combine(config.ConfigurationsFolder, config.ProjectsFileName);
            _projects = new ObservableCollection<ModProjectData>();
            
            // Create directory if it doesn't exist
            Directory.CreateDirectory(config.ConfigurationsFolder);
            
            // Load projects immediately
            LoadProjectsAsync().Wait();
        }

        public async Task<ObservableCollection<ModProjectData>> LoadProjectsAsync()
        {
            if (File.Exists(_projectsFilePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_projectsFilePath);
                    var projects = JsonSerializer.Deserialize<List<ModProjectData>>(json);
                    if (projects != null)
                    {
                        _projects = new ObservableCollection<ModProjectData>(projects);
                        return _projects;
                    }
                }
                catch (Exception ex)
                {
                    // Log error or handle it appropriately
                    System.Diagnostics.Debug.WriteLine($"Error loading projects: {ex.Message}");
                }
            }
            
            _projects = new ObservableCollection<ModProjectData>();
            return _projects;
        }

        private async Task SaveProjectsAsync()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_projects.ToList(), options);
            await File.WriteAllTextAsync(_projectsFilePath, json);
        }

        public async Task AddProjectAsync(ModProjectData project)
        {
            _projects.Add(project);
            await SaveProjectsAsync();
        }

        public async Task RemoveProjectAsync(ModProjectData project)
        {
            _projects.Remove(project);
            await SaveProjectsAsync();
        }

        public async Task UpdateProjectAsync(ModProjectData project)
        {
            var existing = _projects.FirstOrDefault(p => p.ModId == project.ModId);
            if (existing != null)
            {
                var index = _projects.IndexOf(existing);
                _projects[index] = project;
                await SaveProjectsAsync();
            }
        }

        public ObservableCollection<ModProjectData> GetProjects()
        {
            return _projects;
        }
    }
}
