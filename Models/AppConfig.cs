// Models/AppConfig.cs
using System.Collections.ObjectModel;
using System.IO;

namespace Modrix.Models
{
    public class AppConfig
    {
        public string ConfigurationsFolder { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Modrix"
        );

        public string AppPropertiesFileName { get; set; } = "appsettings.json";

        public string ProjectsFileName { get; set; } = "projects.json";

        public ObservableCollection<ModProjectData> Projects { get; set; } = new();
    }
    public class ProjectManagerService
    {
        private readonly AppConfig _config;

        public ProjectManagerService(AppConfig config)
        {
            _config = config;
        }


        public void AddProject(ModProjectData project)
        {
            _config.Projects.Add(project);
        }


        public void RemoveProject(ModProjectData project)
        {
            _config.Projects.Remove(project);
        }


        public void UpdateProject(ModProjectData project)
        {
            var existing = _config.Projects.FirstOrDefault(p => p.ModId == project.ModId);
            if (existing != null)
            {
                var index = _config.Projects.IndexOf(existing);
                _config.Projects[index] = project;
            }
        }


        public ObservableCollection<ModProjectData> GetProjects()
        {
            return _config.Projects;
        }
    }
}
