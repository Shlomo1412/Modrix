using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Modrix.Models;
using Modrix.Services;
using Wpf.Ui.Controls;

namespace Modrix.Views.Windows
{
    public partial class ShareDialog : FluentWindow
  {
        private readonly ModProjectData _project;
        private readonly ShareableProjectService _shareService;
        private bool _isExporting;
        private bool _hasHashManifest;
        private bool _isResourcePack;

        public ShareDialog(ModProjectData project)
        {
            InitializeComponent();
          _project = project;
  _shareService = new ShareableProjectService();
         _isResourcePack = project.ModType?.Contains("Resource Pack", StringComparison.OrdinalIgnoreCase) == true;

            Loaded += ShareDialog_Loaded;
        }

        private async void ShareDialog_Loaded(object sender, RoutedEventArgs e)
 {
       await LoadProjectSummaryAsync();
        }

        private async Task LoadProjectSummaryAsync()
        {
  try
  {
              if (_isResourcePack)
       {
     await LoadResourcePackSummaryAsync();
    }
       else
    {
      await LoadModProjectSummaryAsync();
     }
 }
    catch (Exception ex)
      {
            Debug.WriteLine($"Error loading project summary: {ex.Message}");
            }
  }

        private async Task LoadModProjectSummaryAsync()
    {
     // Count mod elements
            var elementsDir = Path.Combine(_project.Location, "modrix", "elements");
            int elementsCount = 0;
       if (Directory.Exists(elementsDir))
    {
    elementsCount = Directory.GetFiles(elementsDir, "*.json").Length;
         }
            TxtModElementsCount.Text = elementsCount.ToString();

      // Count resources
            var assetsDir = Path.Combine(_project.Location, "src", "main", "resources", "assets", _project.ModId);
 int resourcesCount = 0;
 if (Directory.Exists(assetsDir))
   {
      var texturesDir = Path.Combine(assetsDir, "textures");
         var soundsDir = Path.Combine(assetsDir, "sounds");
                var modelsDir = Path.Combine(assetsDir, "models");

   if (Directory.Exists(texturesDir))
      resourcesCount += Directory.GetFiles(texturesDir, "*.*", SearchOption.AllDirectories).Length;
        if (Directory.Exists(soundsDir))
        resourcesCount += Directory.GetFiles(soundsDir, "*.*", SearchOption.AllDirectories).Length;
        if (Directory.Exists(modelsDir))
          resourcesCount += Directory.GetFiles(modelsDir, "*.json", SearchOption.AllDirectories).Length;
       }
            TxtResourcesCount.Text = resourcesCount.ToString();

       // Check for hash manifest
            var manifest = await FileHashTracker.LoadProjectHashesAsync(_project.Location);
      _hasHashManifest = manifest != null;
TxtHashStatus.Text = _hasHashManifest ? "Available ?" : "Using heuristics";
   TxtHashStatus.Foreground = _hasHashManifest
   ? (System.Windows.Media.Brush)FindResource("SystemFillColorSuccessBrush")
     : (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush");

    // Count new and modified code files
 var javaDir = Path.Combine(_project.Location, "src", "main", "java");
          int newFilesCount = 0;
  int modifiedFilesCount = 0;

            if (Directory.Exists(javaDir))
  {
        foreach (var file in Directory.GetFiles(javaDir, "*.java", SearchOption.AllDirectories))
     {
        if (manifest != null)
          {
      if (FileHashTracker.IsNewFile(file, _project.Location, manifest))
          newFilesCount++;
          else if (FileHashTracker.IsFileModified(file, _project.Location, manifest))
             modifiedFilesCount++;
         }
        else
       {
           newFilesCount++;
}
   }
  }

            var clientJavaDir = Path.Combine(_project.Location, "src", "client", "java");
     if (Directory.Exists(clientJavaDir))
     {
       foreach (var file in Directory.GetFiles(clientJavaDir, "*.java", SearchOption.AllDirectories))
      {
      if (manifest != null)
          {
             if (FileHashTracker.IsNewFile(file, _project.Location, manifest))
              newFilesCount++;
        else if (FileHashTracker.IsFileModified(file, _project.Location, manifest))
           modifiedFilesCount++;
        }
else
        {
              newFilesCount++;
             }
          }
    }

            TxtNewCodeFilesCount.Text = newFilesCount.ToString();
    TxtModifiedFilesCount.Text = modifiedFilesCount.ToString();

       // Count language files
       var langDir = Path.Combine(_project.Location, "src", "main", "resources", "assets", _project.ModId, "lang");
            int langCount = 0;
    if (Directory.Exists(langDir))
            {
  langCount = Directory.GetFiles(langDir, "*.json").Length;
            }
          TxtLanguagesCount.Text = langCount.ToString();
        }

        private async Task LoadResourcePackSummaryAsync()
     {
            // Count texture overrides
  var texturesDir = Path.Combine(_project.Location, "overrides", "textures");
    int textureCount = 0;
if (Directory.Exists(texturesDir))
            {
        textureCount = Directory.GetFiles(texturesDir, "*.png", SearchOption.AllDirectories).Length;
            }
  TxtModElementsCount.Text = $"{textureCount} (Textures)";

         // Count model overrides
  var modelsDir = Path.Combine(_project.Location, "overrides", "models");
     int modelCount = 0;
        if (Directory.Exists(modelsDir))
      {
       modelCount = Directory.GetFiles(modelsDir, "*.json", SearchOption.AllDirectories).Length;
            }
 TxtResourcesCount.Text = $"{modelCount} (Models)";

            // Count sound overrides
            var soundsDir = Path.Combine(_project.Location, "overrides", "sounds");
         int soundCount = 0;
     if (Directory.Exists(soundsDir))
     {
           soundCount = Directory.GetFiles(soundsDir, "*.*", SearchOption.AllDirectories).Length;
         }
          TxtNewCodeFilesCount.Text = $"{soundCount} (Sounds)";

       // Count translation overrides
   var translationsDir = Path.Combine(_project.Location, "overrides", "translations");
     int translationCount = 0;
      if (Directory.Exists(translationsDir))
   {
       translationCount = Directory.GetFiles(translationsDir, "*.json", SearchOption.AllDirectories).Length;
      }
      
     // Also count lang files in assets
   var langDir = Path.Combine(_project.Location, "assets", "minecraft", "lang");
     int langCount = 0;
 if (Directory.Exists(langDir))
          {
      langCount = Directory.GetFiles(langDir, "*.json").Length;
 }
  TxtModifiedFilesCount.Text = $"{translationCount} (Translations)";
       TxtLanguagesCount.Text = langCount.ToString();

  // Resource packs don't use hash tracking
    TxtHashStatus.Text = "N/A (Resource Pack)";
            TxtHashStatus.Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush");
        }

 private async void BtnShare_Click(object sender, RoutedEventArgs e)
  {
            if (_isExporting) return;

         var dlg = new SaveFileDialog
   {
           Title = "Save Shareable Project As...",
     Filter = "Modrix Project (*.modrix)|*.modrix",
        FileName = $"{_project.Name}.modrix",
      InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
    };

       if (dlg.ShowDialog(this) != true)
         return;

            string outputPath = Path.GetDirectoryName(dlg.FileName) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
         string fileName = Path.GetFileName(dlg.FileName);

   _isExporting = true;
            BtnShare.IsEnabled = false;
         BtnCancel.Content = "Close";
       ProgressPanel.Visibility = Visibility.Visible;

            try
       {
                var progress = new Progress<(string Message, int Progress)>(report =>
          {
       TxtProgress.Text = report.Message;
         ProgressBar.Value = report.Progress;
      });

    var resultPath = await _shareService.ExportProjectAsync(_project, outputPath, progress);

      var expectedPath = dlg.FileName;
                if (!string.Equals(resultPath, expectedPath, StringComparison.OrdinalIgnoreCase) && File.Exists(resultPath))
                {
 if (File.Exists(expectedPath))
 File.Delete(expectedPath);
           File.Move(resultPath, expectedPath);
        resultPath = expectedPath;
                }

      var fileInfo = new FileInfo(resultPath);
 var fileSizeKb = fileInfo.Length / 1024.0;
         var fileSizeText = fileSizeKb >= 1024
       ? $"{fileSizeKb / 1024:F2} MB"
: $"{fileSizeKb:F1} KB";

         var projectType = _isResourcePack ? "resource pack" : "project";
            var messageBox = new Wpf.Ui.Controls.MessageBox
        {
          Title = "Share Complete",
           Content = $"Your {projectType} has been exported successfully!\n\n" +
       $"File: {Path.GetFileName(resultPath)}\n" +
    $"Size: {fileSizeText}\n\n" +
         $"Others can import this file in Modrix to recreate your {projectType}.",
 CloseButtonText = "OK",
  PrimaryButtonText = "Open Folder"
         };

    var result = await messageBox.ShowDialogAsync();

             if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
           {
            Process.Start("explorer.exe", $"/select,\"{resultPath}\"");
           }

     Close();
            }
            catch (Exception ex)
            {
    var messageBox = new Wpf.Ui.Controls.MessageBox
         {
    Title = "Export Error",
       Content = $"Failed to create shareable file:\n{ex.Message}",
   CloseButtonText = "Close"
            };

         await messageBox.ShowDialogAsync();
  }
            finally
      {
     _isExporting = false;
       BtnShare.IsEnabled = true;
        ProgressPanel.Visibility = Visibility.Collapsed;
    }
        }

      private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
       Close();
     }
    }
}
