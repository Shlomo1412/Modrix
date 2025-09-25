using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Modrix.Models;
using Modrix.Services;
using Modrix.ViewModels.Windows;
using Modrix.Views.Windows;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace Modrix.Views.Pages
{
    public partial class PropertiesPage : Page
    {
        private ResourcePackWorkspaceViewModel _workspaceViewModel;
        private bool _isLoading;
        private bool _hasUnsavedChanges;
        private string _currentIconPath;

        private static readonly Dictionary<string, int> PackFormats = new()
        {
            {"1.21.5", 34}, {"1.21.4", 34}, {"1.21.3", 34}, {"1.21.2", 34}, {"1.21.1", 34}, {"1.21", 34},
            {"1.20.6", 32}, {"1.20.5", 32}, {"1.20.4", 32}, {"1.20.3", 22}, {"1.20.2", 18}, {"1.20.1", 18},
            {"1.19.4", 13}, {"1.19.3", 13}, {"1.19.2", 13}, {"1.18.2", 9}, {"1.18.1", 8}, {"1.17.1", 8},
            {"1.16.5", 7}, {"1.15.2", 6}, {"1.14.4", 5}, {"1.13.2", 4}
        };

        public bool HasIcon => !string.IsNullOrEmpty(_currentIconPath) && File.Exists(_currentIconPath);

        public PropertiesPage()
        {
            InitializeComponent();
            InitializeUI();
            LoadData();
        }

        private void InitializeUI()
        {
            // Populate Minecraft version ComboBox
            MinecraftVersionComboBox.Items.Clear();
            foreach (var version in PackFormats.Keys.OrderByDescending(v => v))
            {
                MinecraftVersionComboBox.Items.Add(new ComboBoxItem { Content = version });
            }
        }

        private void LoadData()
        {
            try
            {
                // Get workspace view model from parent window
                if (Application.Current.Windows.OfType<ResourcePackWorkspace>().FirstOrDefault()?.ViewModel is ResourcePackWorkspaceViewModel workspaceVm)
                {
                    _workspaceViewModel = workspaceVm;
                    LoadProjectProperties();
                }
            }
            catch (Exception ex)
            {
                ShowMessage("Error", $"Failed to load project properties: {ex.Message}");
            }
        }

        private void LoadProjectProperties()
        {
            if (_workspaceViewModel?.CurrentProject?.Location == null)
                return;

            _isLoading = true;
            
            try
            {
                var project = _workspaceViewModel.CurrentProject;
                
                // Load basic information
                PackNameTextBox.Text = project.Name ?? "";
                PackIdTextBox.Text = project.ModId ?? "";
                DescriptionTextBox.Text = project.Description ?? "";
                AuthorsTextBox.Text = project.Authors ?? "";
                VersionTextBox.Text = project.Version ?? "1.0.0";
                
                // Set license
                SetLicenseSelection(project.License ?? "All Rights Reserved");
                
                // Set Minecraft version
                SetMinecraftVersionSelection(project.MinecraftVersion ?? "1.20.1");
                
                // Load icon
                LoadIcon();
                
                // Load pack.mcmeta for additional properties
                LoadPackMeta();
                
                _hasUnsavedChanges = false;
                UpdateSaveButton();
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void SetLicenseSelection(string license)
        {
            foreach (ComboBoxItem item in LicenseComboBox.Items)
            {
                if (item.Content.ToString() == license)
                {
                    LicenseComboBox.SelectedItem = item;
                    return;
                }
            }
            
            // If not found, set as custom
            LicenseComboBox.Text = license;
        }

        private void SetMinecraftVersionSelection(string version)
        {
            foreach (ComboBoxItem item in MinecraftVersionComboBox.Items)
            {
                if (item.Content.ToString() == version)
                {
                    MinecraftVersionComboBox.SelectedItem = item;
                    UpdatePackFormat(version);
                    UpdateCompatibilityInfo(version);
                    return;
                }
            }
        }

        private void LoadIcon()
        {
            if (_workspaceViewModel?.CurrentProject?.Location == null)
                return;

            var iconPath = Path.Combine(_workspaceViewModel.CurrentProject.Location, "pack.png");
            
            if (File.Exists(iconPath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(iconPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    
                    IconPreview.Source = bitmap;
                    NoIconPlaceholder.Visibility = Visibility.Collapsed;
                    _currentIconPath = iconPath;
                }
                catch (Exception ex)
                {
                    // Icon file exists but is corrupted
                    ShowNoIconState();
                    _currentIconPath = null;
                }
            }
            else
            {
                ShowNoIconState();
                _currentIconPath = null;
            }
            
            OnPropertyChanged(nameof(HasIcon));
        }

        private void ShowNoIconState()
        {
            IconPreview.Source = null;
            NoIconPlaceholder.Visibility = Visibility.Visible;
        }

        private void LoadPackMeta()
        {
            if (_workspaceViewModel?.CurrentProject?.Location == null)
                return;

            var packMetaPath = Path.Combine(_workspaceViewModel.CurrentProject.Location, "pack.mcmeta");
            
            if (!File.Exists(packMetaPath))
                return;

            try
            {
                var content = File.ReadAllText(packMetaPath);
                using var doc = JsonDocument.Parse(content);
                
                if (doc.RootElement.TryGetProperty("pack", out var pack))
                {
                    if (pack.TryGetProperty("description", out var description))
                    {
                        if (string.IsNullOrEmpty(DescriptionTextBox.Text))
                        {
                            DescriptionTextBox.Text = description.GetString() ?? "";
                        }
                    }
                    
                    if (pack.TryGetProperty("pack_format", out var format))
                    {
                        PackFormatTextBox.Text = format.GetInt32().ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                // Ignore malformed pack.mcmeta files
            }
        }

        private void UpdatePackFormat(string minecraftVersion)
        {
            if (PackFormats.TryGetValue(minecraftVersion, out var format))
            {
                PackFormatTextBox.Text = format.ToString();
            }
            else
            {
                PackFormatTextBox.Text = "Unknown";
            }
        }

        private void UpdateCompatibilityInfo(string minecraftVersion)
        {
            if (PackFormats.TryGetValue(minecraftVersion, out var format))
            {
                var compatibleVersions = PackFormats
                    .Where(kvp => kvp.Value == format)
                    .Select(kvp => kvp.Key)
                    .OrderByDescending(v => v)
                    .ToList();

                if (compatibleVersions.Count > 1)
                {
                    var versionRange = $"{compatibleVersions.Last()} - {compatibleVersions.First()}";
                    CompatibilityText.Text = $"This resource pack is compatible with Minecraft {versionRange}.";
                }
                else
                {
                    CompatibilityText.Text = $"This resource pack is compatible with Minecraft {minecraftVersion}.";
                }
            }
            else
            {
                CompatibilityText.Text = "Compatibility information unavailable for this version.";
            }
        }

        private void UpdateSaveButton()
        {
            SaveButton.IsEnabled = _hasUnsavedChanges;
        }

        // Event Handlers
        private void PropertyChanged(object sender, EventArgs e)
        {
            if (_isLoading) return;
            
            _hasUnsavedChanges = true;
            UpdateSaveButton();
        }

        private void MinecraftVersionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
            {
                var version = item.Content.ToString();
                UpdatePackFormat(version);
                UpdateCompatibilityInfo(version);
                PropertyChanged(sender, e);
            }
        }

        private void ChangeIcon_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Resource Pack Icon",
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp|PNG Files|*.png|All Files|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    SetIcon(openFileDialog.FileName);
                    PropertyChanged(sender, e);
                }
                catch (Exception ex)
                {
                    ShowMessage("Error", $"Failed to set icon: {ex.Message}");
                }
            }
        }

        private void RemoveIcon_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_currentIconPath) && File.Exists(_currentIconPath))
                {
                    File.Delete(_currentIconPath);
                }
                
                ShowNoIconState();
                _currentIconPath = null;
                OnPropertyChanged(nameof(HasIcon));
                PropertyChanged(sender, e);
            }
            catch (Exception ex)
            {
                ShowMessage("Error", $"Failed to remove icon: {ex.Message}");
            }
        }

        private void SetIcon(string sourcePath)
        {
            if (_workspaceViewModel?.CurrentProject?.Location == null)
                return;

            var destPath = Path.Combine(_workspaceViewModel.CurrentProject.Location, "pack.png");
            
            // Convert and copy the icon
            if (sourcePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourcePath, destPath, true);
            }
            else
            {
                // Convert to PNG
                BitmapFrame bitmapFrame;
                using (var stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
                {
                    var decoder = BitmapDecoder.Create(
                        stream,
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.Default);
                    
                    bitmapFrame = decoder.Frames[0];
                }

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(bitmapFrame);

                using (var fileStream = new FileStream(destPath, FileMode.Create))
                {
                    encoder.Save(fileStream);
                }
            }
            
            // Update the display
            LoadIcon();
        }

        private async void ExportResourcePack_Click(object sender, RoutedEventArgs e)
        {
            if (_workspaceViewModel?.CurrentProject?.Location == null)
                return;

            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Do you want to save them before exporting?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    SaveChanges_Click(sender, e);
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return;
                }
            }

            var saveFileDialog = new SaveFileDialog
            {
                Title = "Export Resource Pack",
                Filter = "ZIP Archives|*.zip",
                FileName = $"{PackNameTextBox.Text.Replace(" ", "_")}.zip",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    await ExportResourcePackToZip(saveFileDialog.FileName);
                    ShowMessage("Export Complete", $"Resource pack exported to:\n{saveFileDialog.FileName}");
                }
                catch (Exception ex)
                {
                    ShowMessage("Export Error", $"Failed to export resource pack: {ex.Message}");
                }
            }
        }

        private async System.Threading.Tasks.Task ExportResourcePackToZip(string zipPath)
        {
            if (_workspaceViewModel?.CurrentProject?.Location == null)
                return;

            var projectPath = _workspaceViewModel.CurrentProject.Location;
            
            // Validate pack before export if enabled
            if (ValidateBeforeExportCheckBox.IsChecked == true)
            {
                // TODO: Implement validation
            }

            // Create ZIP archive
            System.IO.Compression.ZipFile.CreateFromDirectory(projectPath, zipPath, 
                System.IO.Compression.CompressionLevel.Optimal, false);
        }

        private void ResetToDefaults_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset all properties to their default values? This cannot be undone.",
                "Reset to Defaults",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _isLoading = true;
                
                try
                {
                    DescriptionTextBox.Text = "A resource pack created with Modrix";
                    AuthorsTextBox.Text = "";
                    VersionTextBox.Text = "1.0.0";
                    LicenseComboBox.SelectedIndex = 4; // All Rights Reserved
                    IncludeReadmeCheckBox.IsChecked = true;
                    OptimizeTexturesCheckBox.IsChecked = false;
                    ValidateBeforeExportCheckBox.IsChecked = true;
                    
                    _hasUnsavedChanges = true;
                    UpdateSaveButton();
                }
                finally
                {
                    _isLoading = false;
                }
            }
        }

        private void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveProperties();
                _hasUnsavedChanges = false;
                UpdateSaveButton();
                ShowMessage("Saved", "Properties saved successfully");
            }
            catch (Exception ex)
            {
                ShowMessage("Save Error", $"Failed to save properties: {ex.Message}");
            }
        }

        private void SaveProperties()
        {
            if (_workspaceViewModel?.CurrentProject?.Location == null)
                return;

            var projectPath = _workspaceViewModel.CurrentProject.Location;
            
            // Update project data
            var project = _workspaceViewModel.CurrentProject;
            project.Name = PackNameTextBox.Text;
            project.Description = DescriptionTextBox.Text;
            project.Authors = AuthorsTextBox.Text;
            project.Version = VersionTextBox.Text;
            project.License = GetSelectedLicense();
            
            if (MinecraftVersionComboBox.SelectedItem is ComboBoxItem selectedVersion)
            {
                project.MinecraftVersion = selectedVersion.Content.ToString();
            }

            // Save modrix.config
            SaveModrixConfig(projectPath, project);
            
            // Save pack.mcmeta
            SavePackMeta(projectPath, project);
        }

        private string GetSelectedLicense()
        {
            if (LicenseComboBox.SelectedItem is ComboBoxItem item)
            {
                return item.Content.ToString() ?? "All Rights Reserved";
            }
            return LicenseComboBox.Text ?? "All Rights Reserved";
        }

        private void SaveModrixConfig(string projectPath, ModProjectData project)
        {
            var configContent = $@"ModId={project.ModId}
Name={project.Name}
ModType=Resource Pack
MinecraftVersion={project.MinecraftVersion}
Description={project.Description ?? ""}
Authors={project.Authors ?? ""}
License={project.License ?? "All Rights Reserved"}
Version={project.Version}
IconPath=pack.png
IncludeReadme={IncludeReadmeCheckBox.IsChecked}";

            File.WriteAllText(Path.Combine(projectPath, "modrix.config"), configContent);
        }

        private void SavePackMeta(string projectPath, ModProjectData project)
        {
            if (!PackFormats.TryGetValue(project.MinecraftVersion, out var format))
            {
                format = 18; // Default to 1.20.1 format
            }

            var metaContent = new
            {
                pack = new
                {
                    pack_format = format,
                    description = project.Description ?? "A resource pack created with Modrix"
                }
            };

            var json = JsonSerializer.Serialize(metaContent, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(projectPath, "pack.mcmeta"), json);
        }

        private void ShowMessage(string title, string message)
        {
            var messageBox = new MessageBox
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "OK"
            };
            messageBox.ShowDialog();
        }

        private void OnPropertyChanged(string propertyName)
        {
            // Simple property change notification for bindings
            // PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}