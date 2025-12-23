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

 public ShareDialog(ModProjectData project)
        {
     InitializeComponent();
          _project = project;
    _shareService = new ShareableProjectService();

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
          // Fallback: count based on heuristics (will be refined during export)
       newFilesCount++;
   }
 }
  }

// Also check client java dir (for Fabric)
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
   catch (Exception ex)
       {
Debug.WriteLine($"Error loading project summary: {ex.Message}");
            }
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

// Use the directory for export, the service will create the file with proper name
var resultPath = await _shareService.ExportProjectAsync(_project, outputPath, progress);

    // If the user specified a different filename, rename the result
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

       var messageBox = new Wpf.Ui.Controls.MessageBox
 {
    Title = "Share Complete",
        Content = $"Your project has been exported successfully!\n\n" +
        $"File: {Path.GetFileName(resultPath)}\n" +
   $"Size: {fileSizeText}\n\n" +
$"Others can import this file in Modrix to recreate your project.",
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
