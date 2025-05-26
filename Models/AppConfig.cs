
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
    
}
