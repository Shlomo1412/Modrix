using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;
using System.Diagnostics;

namespace Modrix.Views.Windows
{
    public partial class ChooseTextureDialog : FluentWindow
    {
        private readonly string _projectPath;
        private string _selectedTexturePath;
        private List<TextureItem> _projectTextures = new();
        private List<TextureItem> _minecraftTextures = new();

        public string SelectedTexturePath => _selectedTexturePath;

        public ChooseTextureDialog(string projectPath)
        {
            InitializeComponent();

            // Validate project path
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
            {
                System.Windows.MessageBox.Show($"Invalid project path: {projectPath ?? "null"}. Please select a valid project.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                _projectPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            else
            {
                _projectPath = projectPath;
            }

            // Load textures with error handling
            try
            {
                LoadTextures();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading textures: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }

            SearchBox.TextChanged += SearchBox_TextChanged;
        }

        private void LoadTextures()
        {
            // Ensure project textures list is created
            _projectTextures = new List<TextureItem>();

            try
            {
                // Load project textures
                var projectTexturesDir = FindProjectTexturesDir(_projectPath);
                if (!string.IsNullOrEmpty(projectTexturesDir) && Directory.Exists(projectTexturesDir))
                {
                    _projectTextures = LoadTextureItems(projectTexturesDir);
                }
                else
                {
                    // Create a local temp directory for textures if none exists
                    var localDir = Path.Combine(_projectPath, "src", "main", "resources", "assets");
                    Directory.CreateDirectory(localDir);
                    Debug.WriteLine($"Created directory for textures: {localDir}");

                    // Add a message to the UI
                    _projectTextures.Add(new TextureItem
                    {
                        FileName = "No project textures found. Add textures to your project first.",
                        FilePath = string.Empty
                    });
                }
                ProjectTexturesList.ItemsSource = _projectTextures;

                // Ensure minecraft textures list is created
                _minecraftTextures = new List<TextureItem>();

                // Load Minecraft textures (assume a known path or fallback)
                var minecraftTexturesDir = FindMinecraftTexturesDir();
                if (!string.IsNullOrEmpty(minecraftTexturesDir) && Directory.Exists(minecraftTexturesDir))
                {
                    _minecraftTextures = LoadTextureItems(minecraftTexturesDir);
                }
                else
                {
                    // Add a message to the UI
                    _minecraftTextures.Add(new TextureItem
                    {
                        FileName = "No Minecraft textures found. Configure Minecraft path in settings.",
                        FilePath = string.Empty
                    });
                }
                MinecraftTexturesList.ItemsSource = _minecraftTextures;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in LoadTextures: {ex.Message}");
                throw;
            }
        }

        private string FindProjectTexturesDir(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
            {
                return null;
            }

            try
            {
                // Try to find the modid subfolder if possible
                var assetsDir = Path.Combine(projectPath, "src", "main", "resources", "assets");
                if (Directory.Exists(assetsDir))
                {
                    var modDirs = Directory.GetDirectories(assetsDir);
                    if (modDirs.Length > 0)
                    {
                        var texturesDir = Path.Combine(modDirs[0], "textures");
                        if (Directory.Exists(texturesDir))
                            return texturesDir;
                    }

                    // If we found assets dir but no mod directories, create one based on project name
                    var projectName = new DirectoryInfo(projectPath).Name.ToLowerInvariant();
                    var newModDir = Path.Combine(assetsDir, projectName);
                    var newTextureDir = Path.Combine(newModDir, "textures", "item");

                    Directory.CreateDirectory(newTextureDir);
                    return Path.Combine(newModDir, "textures");
                }

                // If we get here, create the required structure
                assetsDir = Path.Combine(projectPath, "src", "main", "resources", "assets");
                var modDir = Path.Combine(assetsDir, "resources");
                var textureDir = Path.Combine(modDir, "textures", "item");
                Directory.CreateDirectory(textureDir);

                return Path.Combine(modDir, "textures");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FindProjectTexturesDir: {ex.Message}");
                // Return resources directory as fallback
                return Path.Combine(projectPath, "src", "main", "resources");
            }
        }

        private string FindMinecraftTexturesDir()
        {
            try
            {
                // Try to find Minecraft's default assets (user may need to configure this)
                // Example: %APPDATA%\.minecraft\versions\1.20.1\1.20.1.jar extracted to assets/minecraft/textures
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var mcAssets = Path.Combine(appData, ".minecraft", "assets", "minecraft", "textures");
                if (Directory.Exists(mcAssets))
                    return mcAssets;

                // Try alternative location
                mcAssets = Path.Combine(appData, ".minecraft", "versions");
                if (Directory.Exists(mcAssets))
                {
                    // Look for the most recent version
                    var versions = Directory.GetDirectories(mcAssets);

                    // Return any textures directory we can find
                    foreach (var version in versions)
                    {
                        var extracted = Path.Combine(version, "assets", "minecraft", "textures");
                        if (Directory.Exists(extracted))
                            return extracted;
                    }
                }

                // Fallback: empty
                return string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FindMinecraftTexturesDir: {ex.Message}");
                return string.Empty;
            }
        }

        private List<TextureItem> LoadTextureItems(string dir)
        {
            var list = new List<TextureItem>();

            try
            {
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    // Also allow browsing for any file if no PNGs are found
                    var files = Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories);

                    if (files.Length == 0)
                    {
                        // If no PNGs, look for any images
                        var additionalExtensions = new[] { "*.jpg", "*.jpeg", "*.gif" };
                        foreach (var ext in additionalExtensions)
                        {
                            files = files.Concat(Directory.GetFiles(dir, ext, SearchOption.AllDirectories)).ToArray();
                        }
                    }

                    foreach (var file in files)
                    {
                        try
                        {
                            // Create a simple item for files that can't be loaded as images
                            var item = new TextureItem
                            {
                                FilePath = file,
                                FileName = Path.GetFileName(file)
                            };

                            try
                            {
                                var bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.UriSource = new Uri(file);
                                bmp.DecodePixelWidth = 48;
                                bmp.EndInit();
                                bmp.Freeze();
                                item.Image = bmp;
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error loading image {file}: {ex.Message}");
                                // Continue with item that has no image
                            }

                            list.Add(item);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error processing texture {file}: {ex.Message}");
                            // Skip this file
                        }
                    }

                    // If still no files found, add placeholder
                    if (list.Count == 0)
                    {
                        list.Add(new TextureItem
                        {
                            FileName = "No textures found in this directory.",
                            FilePath = string.Empty
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in LoadTextureItems: {ex.Message}");

                // Add an error message item
                list.Add(new TextureItem
                {
                    FileName = $"Error loading textures: {ex.Message}",
                    FilePath = string.Empty
                });
            }

            return list;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var query = SearchBox.Text.Trim().ToLowerInvariant();
                if (SourceTabControl.SelectedIndex == 0)
                {
                    ProjectTexturesList.ItemsSource = string.IsNullOrEmpty(query)
                        ? _projectTextures
                        : _projectTextures.Where(t => t.FileName.ToLowerInvariant().Contains(query)).ToList();
                }
                else
                {
                    MinecraftTexturesList.ItemsSource = string.IsNullOrEmpty(query)
                        ? _minecraftTextures
                        : _minecraftTextures.Where(t => t.FileName.ToLowerInvariant().Contains(query)).ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in SearchBox_TextChanged: {ex.Message}");
            }
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TextureItem? selected = null;
                if (SourceTabControl.SelectedIndex == 0)
                {
                    if (ProjectTexturesList.SelectedItem is TextureItem item)
                        selected = item;
                }
                else
                {
                    if (MinecraftTexturesList.SelectedItem is TextureItem item)
                        selected = item;
                }

                if (selected != null && !string.IsNullOrEmpty(selected.FilePath) && File.Exists(selected.FilePath))
                {
                    _selectedTexturePath = selected.FilePath;
                    DialogResult = true;
                }
                else
                {
                    System.Windows.MessageBox.Show("Please select a valid texture file.", "Selection Required", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error selecting texture: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ImportNewTexture_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open file dialog to select an image file
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Image files (*.png;*.jpg;*.jpeg;*.gif)|*.png;*.jpg;*.jpeg;*.gif|All files (*.*)|*.*",
                    Title = "Select Texture Image File"
                };

                if (dialog.ShowDialog() == true)
                {
                    var sourceFilePath = dialog.FileName;
                    if (File.Exists(sourceFilePath))
                    {
                        // Ensure we have a valid project texture directory
                        var projectTexturesDir = FindProjectTexturesDir(_projectPath);
                        var itemTexturesDir = Path.Combine(projectTexturesDir, "item");

                        // Create the directory if it doesn't exist
                        Directory.CreateDirectory(itemTexturesDir);

                        // Copy file to the project textures directory
                        var fileName = Path.GetFileName(sourceFilePath);
                        var destPath = Path.Combine(itemTexturesDir, fileName);

                        // If file with same name exists, append number
                        var baseName = Path.GetFileNameWithoutExtension(fileName);
                        var extension = Path.GetExtension(fileName);
                        int counter = 1;

                        while (File.Exists(destPath))
                        {
                            destPath = Path.Combine(itemTexturesDir, $"{baseName}_{counter}{extension}");
                            counter++;
                        }

                        // Copy the file
                        File.Copy(sourceFilePath, destPath);

                        // Update the selected path
                        _selectedTexturePath = destPath;

                        // Reload textures
                        LoadTextures();

                        // Select the tab with project textures
                        SourceTabControl.SelectedIndex = 0;

                        System.Windows.MessageBox.Show($"Texture imported successfully to project textures.", "Import Successful", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                        // Optionally auto-select the imported texture and return
                        if (System.Windows.MessageBox.Show("Do you want to use the imported texture?", "Select Texture", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes)
                        {
                            DialogResult = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error importing texture: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        public class TextureItem
        {
            public BitmapImage Image { get; set; }
            public string FilePath { get; set; }
            public string FileName { get; set; }
        }
    }
}
