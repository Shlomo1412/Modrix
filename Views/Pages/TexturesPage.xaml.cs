using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Modrix.Services;
using Modrix.ViewModels.Windows;
using Modrix.Views.Windows;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace Modrix.Views.Pages
{
    public partial class TexturesPage : Page
    {
        private ResourcePackWorkspaceViewModel _workspaceViewModel;
        private ObservableCollection<MinecraftTextureItem> _allTextures;
        private ObservableCollection<MinecraftTextureItem> _filteredTextures;
        private ResourcePackTemplateManager _resourcePackManager;
        private string _currentCategory = "All";
        private string _currentSearchText = "";

        public TexturesPage()
        {
            InitializeComponent();
            InitializeCollections();
            LoadData();
        }

        private void InitializeCollections()
        {
            _allTextures = new ObservableCollection<MinecraftTextureItem>();
            _filteredTextures = new ObservableCollection<MinecraftTextureItem>();
            _resourcePackManager = new ResourcePackTemplateManager();
            
            TexturesGrid.ItemsSource = _filteredTextures;
        }

        private async void LoadData()
        {
            try
            {
                // Get workspace view model from parent window
                if (Application.Current.Windows.OfType<ResourcePackWorkspace>().FirstOrDefault()?.ViewModel is ResourcePackWorkspaceViewModel workspaceVm)
                {
                    _workspaceViewModel = workspaceVm;
                    await LoadMinecraftTextures();
                }
            }
            catch (Exception ex)
            {
                ShowMessage("Error", $"Failed to load textures: {ex.Message}");
            }
        }

        private async Task LoadMinecraftTextures()
        {
            if (_workspaceViewModel?.CurrentProject == null)
                return;

            ShowLoadingState(true);
            StatusText.Text = "Loading Minecraft textures...";

            try
            {
                _allTextures.Clear();

                // Get available textures from the template manager
                var availableTextures = await _resourcePackManager.GetAvailableTextures(_workspaceViewModel.CurrentProject.MinecraftVersion);
                
                // Load textures with their override status
                var existingOverrides = GetExistingOverrides();
                
                foreach (var texturePath in availableTextures)
                {
                    var textureItem = new MinecraftTextureItem
                    {
                        Path = texturePath,
                        Name = GetTextureName(texturePath),
                        Category = GetTextureCategory(texturePath),
                        HasOverride = existingOverrides.ContainsKey(texturePath),
                        PreviewImage = GetTexturePreviewPath(texturePath, existingOverrides.ContainsKey(texturePath) ? existingOverrides[texturePath] : null)
                    };
                    
                    _allTextures.Add(textureItem);
                }

                ApplyFilters();
                UpdateStatusText();
            }
            catch (Exception ex)
            {
                ShowMessage("Error", $"Failed to load textures: {ex.Message}");
                ShowEmptyState();
            }
            finally
            {
                ShowLoadingState(false);
            }
        }

        private Dictionary<string, string> GetExistingOverrides()
        {
            var overrides = new Dictionary<string, string>();
            
            if (_workspaceViewModel?.CurrentProject?.Location == null)
                return overrides;

            var texturesPath = Path.Combine(_workspaceViewModel.CurrentProject.Location, "assets", "minecraft", "textures");
            
            if (!Directory.Exists(texturesPath))
                return overrides;

            var textureFiles = Directory.GetFiles(texturesPath, "*.png", SearchOption.AllDirectories);
            
            foreach (var file in textureFiles)
            {
                var relativePath = Path.GetRelativePath(texturesPath, file);
                var texturePath = relativePath.Replace('\\', '/').Replace(".png", "");
                overrides[$"textures/{texturePath}"] = file;
            }

            return overrides;
        }

        private string GetTextureName(string texturePath)
        {
            return Path.GetFileName(texturePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private string GetTextureCategory(string texturePath)
        {
            var parts = texturePath.Split('/');
            if (parts.Length >= 2)
            {
                return parts[1].ToUpperInvariant() switch
                {
                    "BLOCK" => "Block",
                    "ITEM" => "Item", 
                    "ENTITY" => "Entity",
                    "GUI" => "GUI",
                    "PARTICLE" => "Particle",
                    "ENVIRONMENT" => "Environment",
                    _ => "Other"
                };
            }
            return "Other";
        }

        private string GetTexturePreviewPath(string texturePath, string? overridePath = null)
        {
            // If there's an override, use that
            if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath))
            {
                return overridePath;
            }

            // Otherwise, try to get the default Minecraft texture
            // This would ideally be from extracted Minecraft assets
            // For now, return a placeholder path
            return "pack://application:,,,/Resources/Icons/TexturePlaceholder.png";
        }

        private void ApplyFilters()
        {
            _filteredTextures.Clear();

            var filtered = _allTextures.AsEnumerable();

            // Apply category filter
            if (_currentCategory != "All")
            {
                filtered = filtered.Where(t => t.Category == _currentCategory);
            }

            // Apply search filter
            if (!string.IsNullOrEmpty(_currentSearchText))
            {
                filtered = filtered.Where(t => 
                    t.Name.Contains(_currentSearchText, StringComparison.OrdinalIgnoreCase) ||
                    t.Path.Contains(_currentSearchText, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var texture in filtered)
            {
                _filteredTextures.Add(texture);
            }

            UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            var totalCount = _allTextures.Count;
            var filteredCount = _filteredTextures.Count;
            var overrideCount = _allTextures.Count(t => t.HasOverride);

            StatusText.Text = filteredCount == totalCount 
                ? $"Showing all {totalCount} textures"
                : $"Showing {filteredCount} of {totalCount} textures";
                
            TextureCount.Text = $"{filteredCount} textures";
            OverrideCount.Text = $"{overrideCount} overrides";

            // Show/hide empty state
            if (filteredCount == 0)
            {
                ShowEmptyState();
            }
            else
            {
                EmptyState.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowLoadingState(bool isLoading)
        {
            LoadingState.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            LoadingProgress.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            TextureScrollViewer.Visibility = isLoading ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ShowEmptyState()
        {
            EmptyState.Visibility = Visibility.Visible;
            TextureScrollViewer.Visibility = Visibility.Collapsed;
        }

        // Event Handlers
        private async void CreateOverride_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Create override for selected texture
            ShowMessage("Create Override", "Override creation functionality coming soon");
        }

        private void ImportTexture_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Import Texture Override",
                Filter = "PNG Images|*.png|All Files|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var file in openFileDialog.FileNames)
                {
                    ImportTextureFile(file);
                }
                _ = LoadMinecraftTextures(); // Refresh
            }
        }

        private async void ExtractAll_Click(object sender, RoutedEventArgs e)
        {
            ShowMessage("Extract Textures", "Automatic texture extraction from Minecraft assets is not yet implemented. You can manually place PNG files in the assets/minecraft/textures folder structure.");
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadMinecraftTextures();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _currentSearchText = SearchBox.Text ?? "";
            ApplyFilters();
        }

        private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
            {
                _currentCategory = item.Content.ToString() ?? "All";
                ApplyFilters();
            }
        }

        private void TextureCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is MinecraftTextureItem texture)
            {
                // Select the texture (you could implement selection logic here)
                CreateTextureOverride(texture);
            }
        }

        private void TextureCard_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is MinecraftTextureItem texture)
            {
                border.ContextMenu = FindResource("TextureContextMenu") as ContextMenu;
                if (border.ContextMenu != null)
                {
                    border.ContextMenu.Tag = texture;
                    border.ContextMenu.IsOpen = true;
                }
            }
        }

        private void CreateTextureOverride_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is MinecraftTextureItem texture)
            {
                CreateTextureOverride(texture);
            }
        }

        private void PreviewTexture_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is MinecraftTextureItem texture)
            {
                // TODO: Show texture preview dialog
                ShowMessage("Preview", $"Previewing texture: {texture.Name}");
            }
        }

        // Context menu handlers
        private void CreateOverrideMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu && 
                contextMenu.Tag is MinecraftTextureItem texture)
            {
                CreateTextureOverride(texture);
            }
        }

        private void PreviewMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu && 
                contextMenu.Tag is MinecraftTextureItem texture)
            {
                // TODO: Show full size preview
                ShowMessage("Preview", $"Full size preview for: {texture.Name}");
            }
        }

        private void CopyPathMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu && 
                contextMenu.Tag is MinecraftTextureItem texture)
            {
                Clipboard.SetText(texture.Path);
                ShowMessage("Copied", $"Path copied to clipboard: {texture.Path}");
            }
        }

        private void OpenInExplorerMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu && 
                contextMenu.Tag is MinecraftTextureItem texture)
            {
                // Open the textures folder
                if (_workspaceViewModel?.CurrentProject?.Location != null)
                {
                    var texturesPath = Path.Combine(_workspaceViewModel.CurrentProject.Location, "assets", "minecraft", "textures");
                    if (Directory.Exists(texturesPath))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", texturesPath);
                    }
                }
            }
        }

        // Helper methods
        private void CreateTextureOverride(MinecraftTextureItem texture)
        {
            try
            {
                // If texture already has an override, open it in editor
                if (texture.HasOverride)
                {
                    // TODO: Open in texture editor
                    ShowMessage("Edit Override", $"Opening texture editor for: {texture.Name}");
                    return;
                }

                // Create new override
                var dialog = new OpenFileDialog
                {
                    Title = $"Select texture file for {texture.Name}",
                    Filter = "PNG Images|*.png|All Files|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    CreateOverrideFromFile(texture, dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                ShowMessage("Error", $"Failed to create override: {ex.Message}");
            }
        }

        private void CreateOverrideFromFile(MinecraftTextureItem texture, string sourceFile)
        {
            if (_workspaceViewModel?.CurrentProject?.Location == null)
                return;

            try
            {
                // Determine target path
                var texturePath = texture.Path.Replace("textures/", "");
                var targetDir = Path.Combine(_workspaceViewModel.CurrentProject.Location, "assets", "minecraft", "textures");
                var targetPath = Path.Combine(targetDir, texturePath + ".png");
                
                // Create directory structure
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                
                // Copy the file
                File.Copy(sourceFile, targetPath, true);
                
                // Update the texture item
                texture.HasOverride = true;
                texture.PreviewImage = targetPath;
                
                ShowMessage("Success", $"Created override for {texture.Name}");
                
                // Refresh display
                _ = LoadMinecraftTextures();
            }
            catch (Exception ex)
            {
                ShowMessage("Error", $"Failed to create override: {ex.Message}");
            }
        }

        private void ImportTextureFile(string filePath)
        {
            if (_workspaceViewModel?.CurrentProject?.Location == null)
                return;

            try
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var targetDir = Path.Combine(_workspaceViewModel.CurrentProject.Location, "assets", "minecraft", "textures");
                
                // Try to determine appropriate subdirectory
                var subdirectory = DetermineTextureSubdirectory(fileName);
                targetDir = Path.Combine(targetDir, subdirectory);
                
                Directory.CreateDirectory(targetDir);
                var targetPath = Path.Combine(targetDir, Path.GetFileName(filePath));
                
                File.Copy(filePath, targetPath, true);
                
                ShowMessage("Import Success", $"Imported texture: {fileName}");
            }
            catch (Exception ex)
            {
                ShowMessage("Import Error", $"Failed to import texture: {ex.Message}");
            }
        }

        private string DetermineTextureSubdirectory(string fileName)
        {
            var name = fileName.ToLower();
            
            if (name.Contains("block") || name.Contains("stone") || name.Contains("wood") || name.Contains("dirt"))
                return "block";
            else if (name.Contains("item") || name.Contains("tool") || name.Contains("sword"))
                return "item";
            else if (name.Contains("entity") || name.Contains("mob"))
                return "entity";
            else if (name.Contains("gui"))
                return "gui";
            else
                return "misc";
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
    }

    // Data model for Minecraft textures
    public class MinecraftTextureItem
    {
        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public bool HasOverride { get; set; }
        public string PreviewImage { get; set; } = "";
    }
}