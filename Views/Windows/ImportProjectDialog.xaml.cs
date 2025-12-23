using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Modrix.Models;
using Modrix.Services;
using Wpf.Ui.Controls;

namespace Modrix.Views.Windows
{
    public partial class ImportProjectDialog : FluentWindow
    {
      private readonly string _modrixFilePath;
        private readonly ShareableProjectService _shareService;
      private ModrixShareableProject? _shareableProject;
        private bool _isImporting;

        public bool ImportSuccessful { get; private set; }

        public ImportProjectDialog(string modrixFilePath)
      {
            InitializeComponent();
        _modrixFilePath = modrixFilePath;
          _shareService = new ShareableProjectService();

     Loaded += ImportProjectDialog_Loaded;
      }

    private async void ImportProjectDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadProjectInfoAsync();
        }

        private async Task LoadProjectInfoAsync()
   {
      try
       {
     BtnImport.IsEnabled = false;
            TxtProgress.Text = "Reading project file...";
                ProgressPanel.Visibility = Visibility.Visible;
      ProgressBar.IsIndeterminate = true;

      _shareableProject = await _shareService.GetProjectInfoAsync(_modrixFilePath);

           // Populate UI with project info
                var metadata = _shareableProject.Metadata;
        TxtProjectName.Text = metadata.Name;
                TxtModId.Text = metadata.ModId;
      TxtModType.Text = metadata.ModType;
             TxtMinecraftVersion.Text = metadata.MinecraftVersion;
     TxtAuthors.Text = string.IsNullOrWhiteSpace(metadata.Authors) ? "Unknown" : metadata.Authors;
                TxtDescription.Text = string.IsNullOrWhiteSpace(metadata.Description) ? "No description provided." : metadata.Description;
            TxtCreatedDate.Text = _shareableProject.CreatedDate.ToLocalTime().ToString("MMMM d, yyyy");
       TxtModrixVersion.Text = _shareableProject.ModrixVersion;

// Content counts
    TxtModElementsCount.Text = $"{_shareableProject.ModElements.Count} mod elements";
    TxtResourcesCount.Text = $"{_shareableProject.Resources.Count} resources";
 TxtCodeFilesCount.Text = $"{_shareableProject.CustomCode.Count} code files";
  TxtLanguagesCount.Text = $"{_shareableProject.Languages.Count} languages";

          BtnImport.IsEnabled = true;
     }
         catch (Exception ex)
            {
       var messageBox = new Wpf.Ui.Controls.MessageBox
      {
        Title = "Invalid File",
       Content = $"Could not read the .modrix file:\n{ex.Message}",
       CloseButtonText = "OK"
      };
 await messageBox.ShowDialogAsync();
       Close();
          }
          finally
   {
            ProgressPanel.Visibility = Visibility.Collapsed;
                ProgressBar.IsIndeterminate = false;
 }
      }

        private async void BtnImport_Click(object sender, RoutedEventArgs e)
     {
            if (_isImporting || _shareableProject == null) return;

       _isImporting = true;
       BtnImport.IsEnabled = false;
     BtnCancel.Content = "Close";
    ProgressPanel.Visibility = Visibility.Visible;
            ProgressBar.IsIndeterminate = false;
  ProgressBar.Value = 0;

try
       {
            var progress = new Progress<(string Message, int Progress)>(report =>
            {
      TxtProgress.Text = report.Message;
 ProgressBar.Value = report.Progress;
   });

 // Get the projects base path
         var projectsBasePath = TemplateManager.ProjectsBasePath;

     // First, create the project using the appropriate template manager
    UpdateProgress(progress, "Creating project structure...", 20);

    var metadata = _shareableProject.Metadata;
     var project = new ModProjectData
              {
            Name = metadata.Name,
       ModId = metadata.ModId,
          Package = metadata.Package,
     Location = Path.Combine(projectsBasePath, metadata.ModId),
      ModType = metadata.ModType,
       MinecraftVersion = metadata.MinecraftVersion,
         Description = metadata.Description,
  Authors = metadata.Authors,
        License = metadata.License,
   ModVersion = metadata.ModVersion,
             IncludeReadme = metadata.IncludeReadme
       };

 // Ensure unique directory
    var counter = 1;
      while (Directory.Exists(project.Location))
      {
         project.Location = Path.Combine(projectsBasePath, $"{metadata.ModId}_{counter}");
      counter++;
     }

      // Create project using template manager
        UpdateProgress(progress, "Generating project files...", 30);

        if (metadata.ModType.Contains("Fabric", StringComparison.OrdinalIgnoreCase))
       {
            await CreateFabricProjectAsync(project, progress);
     }
    else if (metadata.ModType.Contains("Forge", StringComparison.OrdinalIgnoreCase))
      {
   await CreateForgeProjectAsync(project, progress);
  }
         else
     {
          // Default to Forge if unknown
  await CreateForgeProjectAsync(project, progress);
 }

     // Import resources, elements, languages from the shareable project
         UpdateProgress(progress, "Restoring resources...", 60);
      await RestoreResourcesAsync(_shareableProject, project);

    UpdateProgress(progress, "Restoring mod elements...", 75);
       await RestoreModElementsAsync(_shareableProject, project);

       UpdateProgress(progress, "Restoring language files...", 85);
                await RestoreLanguageFilesAsync(_shareableProject, project);

                UpdateProgress(progress, "Restoring custom code...", 95);
       await _shareService.RestoreCustomCodeAsync(_shareableProject, project);

                UpdateProgress(progress, "Import complete!", 100);

           ImportSuccessful = true;
      DialogResult = true;

        var messageBox = new Wpf.Ui.Controls.MessageBox
     {
    Title = "Import Complete",
            Content = $"Project '{metadata.Name}' has been imported successfully!\n\n" +
    $"Location: {project.Location}",
        CloseButtonText = "OK"
         };
           await messageBox.ShowDialogAsync();

             Close();
 }
         catch (Exception ex)
      {
      var messageBox = new Wpf.Ui.Controls.MessageBox
   {
    Title = "Import Error",
         Content = $"Failed to import project:\n{ex.Message}",
     CloseButtonText = "Close"
         };
   await messageBox.ShowDialogAsync();
      }
  finally
   {
         _isImporting = false;
                BtnImport.IsEnabled = true;
        ProgressPanel.Visibility = Visibility.Collapsed;
     }
        }

        private void UpdateProgress(IProgress<(string Message, int Progress)> progress, string message, int value)
        {
            ((IProgress<(string, int)>)progress).Report((message, value));
        }

        private async Task CreateFabricProjectAsync(ModProjectData project, IProgress<(string Message, int Progress)> progress)
        {
            var fabricManager = new FabricTemplateManager();
            await fabricManager.FullSetupWithGradle(project, progress);
        }

      private async Task CreateForgeProjectAsync(ModProjectData project, IProgress<(string Message, int Progress)> progress)
        {
  var forgeManager = new ForgeTemplateManager();
            await forgeManager.FullSetupWithGradle(project, progress);
     }

        private async Task RestoreResourcesAsync(ModrixShareableProject shareable, ModProjectData project)
        {
       var assetsDir = Path.Combine(project.Location, "src", "main", "resources", "assets", project.ModId);

     foreach (var resource in shareable.Resources)
   {
    try
                {
 var targetPath = resource.ResourceType == "icon" && resource.RelativePath == "icon.png"
          ? Path.Combine(assetsDir, "icon.png")
       : Path.Combine(assetsDir, resource.RelativePath);

       Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            var bytes = Convert.FromBase64String(resource.Content);
   await File.WriteAllBytesAsync(targetPath, bytes);
                }
          catch (Exception ex)
       {
  Debug.WriteLine($"Error restoring resource {resource.RelativePath}: {ex.Message}");
        }
}
        }

        private async Task RestoreModElementsAsync(ModrixShareableProject shareable, ModProjectData project)
  {
            var elementsDir = Path.Combine(project.Location, "modrix", "elements");
       Directory.CreateDirectory(elementsDir);

            foreach (var element in shareable.ModElements)
            {
     try
                {
    var elementPath = Path.Combine(elementsDir, $"{element.Id}.json");
    await File.WriteAllTextAsync(elementPath, element.Data);
       }
           catch (Exception ex)
         {
        Debug.WriteLine($"Error restoring element {element.Name}: {ex.Message}");
       }
       }
        }

        private async Task RestoreLanguageFilesAsync(ModrixShareableProject shareable, ModProjectData project)
        {
    var langDir = Path.Combine(project.Location, "src", "main", "resources", "assets", project.ModId, "lang");
            Directory.CreateDirectory(langDir);

    foreach (var lang in shareable.Languages)
    {
    try
      {
 var langPath = Path.Combine(langDir, $"{lang.LanguageCode}.json");
             await File.WriteAllTextAsync(langPath, lang.Content);
         }
  catch (Exception ex)
        {
    Debug.WriteLine($"Error restoring language {lang.LanguageCode}: {ex.Message}");
             }
          }
        }

  private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
