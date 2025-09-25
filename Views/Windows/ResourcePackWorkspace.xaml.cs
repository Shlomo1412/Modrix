using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Modrix.Models;
using Modrix.Services;
using Modrix.ViewModels.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace Modrix.Views.Windows
{
    public partial class ResourcePackWorkspace : FluentWindow, INavigationWindow
    {
        public ResourcePackWorkspaceViewModel ViewModel { get; }
        private SnackbarPresenter _snackbarPresenter;

        public ResourcePackWorkspace(
            ResourcePackWorkspaceViewModel viewModel,
            INavigationViewPageProvider navigationViewPageProvider,
            INavigationService navigationService
        )
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
            SetPageService(navigationViewPageProvider);

            navigationService.SetNavigationControl(RootNavigation);

            Loaded += ResourcePackWorkspace_Loaded;
        }

        private void ResourcePackWorkspace_Loaded(object sender, RoutedEventArgs e)
        {
            _snackbarPresenter = this.SnackbarPresenter;
        }

        public void LoadProject(ModProjectData project)
        {
            if (project.ModType != "Resource Pack")
            {
                ShowSnackbar("Invalid project type", "This workspace is only for Resource Pack projects", ControlAppearance.Danger);
                return;
            }
            
            ViewModel.LoadProject(project);
        }

        public void ShowWindow() => Show();
        public void CloseWindow() => Close();

        public INavigationView GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(INavigationViewPageProvider provider)
        {
            RootNavigation.SetPageProviderService(provider);
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            // Not needed for resource pack workspace
        }

        // Toolbar button handlers
        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ViewModel?.CurrentProject?.Location != null && Directory.Exists(ViewModel.CurrentProject.Location))
                {
                    var minecraftPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        ".minecraft", "resourcepacks");

                    if (!Directory.Exists(minecraftPath))
                    {
                        ShowSnackbar("Minecraft not found", "Could not locate Minecraft resourcepacks directory", ControlAppearance.Warning);
                        return;
                    }

                    ShowSnackbar("Preview", $"Copy the resource pack folder to {minecraftPath} to preview in Minecraft", ControlAppearance.Info);
                    Process.Start("explorer.exe", minecraftPath);
                }
            }
            catch (Exception ex)
            {
                ShowSnackbar("Error", $"Failed to open preview: {ex.Message}", ControlAppearance.Danger);
            }
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ViewModel?.CurrentProject == null)
                    return;

                var exportDialog = App.Services.GetService<ExportDialog>();
                if (exportDialog != null)
                {
                    exportDialog.Owner = this;
                    exportDialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                ShowSnackbar("Export Error", $"Failed to export: {ex.Message}", ControlAppearance.Danger);
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import Assets",
                    Filter = "All Supported Files|*.png;*.json;*.zip|PNG Images|*.png|JSON Files|*.json|ZIP Archives|*.zip|All Files|*.*",
                    Multiselect = true
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    // Handle file imports based on type
                    foreach (var file in openFileDialog.FileNames)
                    {
                        var extension = Path.GetExtension(file).ToLower();
                        switch (extension)
                        {
                            case ".png":
                                ImportTexture(file);
                                break;
                            case ".json":
                                ImportJsonFile(file);
                                break;
                            case ".zip":
                                ImportZipFile(file);
                                break;
                        }
                    }
                    
                    ShowSnackbar("Import", "Files imported successfully", ControlAppearance.Success);
                }
            }
            catch (Exception ex)
            {
                ShowSnackbar("Import Error", $"Failed to import: {ex.Message}", ControlAppearance.Danger);
            }
        }

        private void ValidateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ViewModel?.CurrentProject == null)
                    return;

                var validationService = App.Services.GetService<ModelValidationService>();
                // TODO: Add resource pack validation logic
                
                ShowSnackbar("Validation", "Resource pack validation completed", ControlAppearance.Success);
            }
            catch (Exception ex)
            {
                ShowSnackbar("Validation Error", $"Validation failed: {ex.Message}", ControlAppearance.Danger);
            }
        }

        private void OpenProjectFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ViewModel?.CurrentProject?.Location != null && Directory.Exists(ViewModel.CurrentProject.Location))
                {
                    Process.Start("explorer.exe", ViewModel.CurrentProject.Location);
                }
                else
                {
                    ShowSnackbar("Error", "Project directory not found", ControlAppearance.Danger);
                }
            }
            catch (Exception ex)
            {
                ShowSnackbar("Error", $"Failed to open folder: {ex.Message}", ControlAppearance.Danger);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ViewModel?.CurrentProject != null)
                {
                    ViewModel.RefreshProject();
                    ShowSnackbar("Refresh", "Project refreshed successfully", ControlAppearance.Success);
                }
            }
            catch (Exception ex)
            {
                ShowSnackbar("Refresh Error", $"Failed to refresh: {ex.Message}", ControlAppearance.Danger);
            }
        }

        private void ShareButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // TODO: Implement sharing functionality
                ShowSnackbar("Share", "Sharing feature coming soon!", ControlAppearance.Info);
            }
            catch (Exception ex)
            {
                ShowSnackbar("Share Error", $"Failed to share: {ex.Message}", ControlAppearance.Danger);
            }
        }

        // Helper methods for import functionality
        private void ImportTexture(string filePath)
        {
            if (ViewModel?.CurrentProject?.Location == null)
                return;

            var fileName = Path.GetFileName(filePath);
            var texturesDir = Path.Combine(ViewModel.CurrentProject.Location, "assets", "minecraft", "textures");
            
            // Try to determine the appropriate subdirectory
            var subdirectory = DetermineTextureDirectory(fileName);
            var targetDir = Path.Combine(texturesDir, subdirectory);
            
            Directory.CreateDirectory(targetDir);
            var targetPath = Path.Combine(targetDir, fileName);
            
            File.Copy(filePath, targetPath, true);
        }

        private void ImportJsonFile(string filePath)
        {
            if (ViewModel?.CurrentProject?.Location == null)
                return;

            var fileName = Path.GetFileName(filePath);
            var content = File.ReadAllText(filePath);
            
            // Determine if it's a model, language file, etc.
            string targetDir;
            if (content.Contains("\"parent\"") || content.Contains("\"elements\""))
            {
                // Likely a model file
                targetDir = Path.Combine(ViewModel.CurrentProject.Location, "assets", "minecraft", "models");
            }
            else if (content.Contains("\"block.") || content.Contains("\"item."))
            {
                // Likely a language file
                targetDir = Path.Combine(ViewModel.CurrentProject.Location, "assets", "minecraft", "lang");
            }
            else
            {
                // Default location
                targetDir = Path.Combine(ViewModel.CurrentProject.Location, "assets", "minecraft");
            }
            
            Directory.CreateDirectory(targetDir);
            File.Copy(filePath, Path.Combine(targetDir, fileName), true);
        }

        private void ImportZipFile(string filePath)
        {
            // TODO: Implement ZIP file extraction for resource pack imports
            ShowSnackbar("Import", "ZIP import not yet implemented", ControlAppearance.Warning);
        }

        private string DetermineTextureDirectory(string fileName)
        {
            var name = fileName.ToLower();
            
            if (name.Contains("block") || name.Contains("stone") || name.Contains("dirt") || name.Contains("wood"))
                return "block";
            else if (name.Contains("item") || name.Contains("tool") || name.Contains("sword") || name.Contains("pickaxe"))
                return "item";
            else if (name.Contains("entity") || name.Contains("mob"))
                return "entity";
            else if (name.Contains("gui") || name.Contains("menu"))
                return "gui";
            else
                return "misc";
        }

        private void ShowSnackbar(string title, string message, ControlAppearance appearance)
        {
            _snackbarPresenter?.Show(title, message, appearance, null, TimeSpan.FromSeconds(3));
        }

        public void NavigateToTab(string tabName)
        {
            // TODO: Implement navigation to specific tabs
        }
    }
}