using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Modrix.Services;
using Modrix.Views.Windows;
using Wpf.Ui.Controls;
using Wpf.Ui.Abstractions.Controls;

namespace Modrix.Views.Pages.ResourcePack
{
    public partial class TexturesPage : System.Windows.Controls.Page, INavigableView<object>
    {
        public object ViewModel => this;
        
        private ResourcePackData? _currentPack;
        private List<TextureCategory> _categories = new();
        private List<TextureItem> _allTextures = new();
        private List<TextureItem> _filteredTextures = new();
        private bool _isGridView = true;
        private MinecraftAssetExtractor? _assetExtractor;

        public TexturesPage()
        {
            try
            {
                InitializeComponent();
                System.Diagnostics.Debug.WriteLine("TexturesPage: InitializeComponent completed");
                
                // Get asset extractor from DI container
                _assetExtractor = App.Services.GetService(typeof(MinecraftAssetExtractor)) as MinecraftAssetExtractor;
                System.Diagnostics.Debug.WriteLine($"TexturesPage: AssetExtractor = {_assetExtractor != null}");
                
                DataContext = this;
                LoadCurrentPack();
                System.Diagnostics.Debug.WriteLine($"TexturesPage: CurrentPack = {_currentPack?.Name ?? "null"}");
                
                LoadCategories();
                LoadTextures();
                System.Diagnostics.Debug.WriteLine("TexturesPage: LoadTextures completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TexturesPage constructor error: {ex.Message}");
                throw;
            }
        }

        private void LoadCurrentPack()
        {
            var workspace = Application.Current.Windows
                .OfType<ResourcePackWorkspace>()
                .FirstOrDefault();

            if (workspace?.ViewModel?.CurrentPack != null)
            {
                _currentPack = workspace.ViewModel.CurrentPack;
            }
        }

        private void LoadCategories()
        {
            _categories = new List<TextureCategory>
            {
                new TextureCategory("All", Wpf.Ui.Controls.SymbolRegular.Grid20, ""),
                new TextureCategory("Blocks", Wpf.Ui.Controls.SymbolRegular.Grid20, "block"),
                new TextureCategory("Items", Wpf.Ui.Controls.SymbolRegular.Box20, "item"),
                new TextureCategory("GUIs", Wpf.Ui.Controls.SymbolRegular.Desktop20, "gui"),
                new TextureCategory("Entities", Wpf.Ui.Controls.SymbolRegular.People20, "entity"),
                new TextureCategory("Particles", Wpf.Ui.Controls.SymbolRegular.Sparkle20, "particle"),
                new TextureCategory("Environment", Wpf.Ui.Controls.SymbolRegular.WeatherCloudy20, "environment"),
                new TextureCategory("Effects", Wpf.Ui.Controls.SymbolRegular.Flash20, "effect")
            };

            // Update counts with actual texture data
            foreach (var category in _categories)
            {
                if (string.IsNullOrEmpty(category.Category))
                {
                    category.Count = _allTextures.Count;
                }
                else
                {
                    category.Count = _allTextures.Count(t => t.Category == category.Category);
                }
            }

            // Update UI if controls are available
            var categoriesList = this.FindName("CategoriesList") as ListBox;
            if (categoriesList != null)
            {
                categoriesList.ItemsSource = _categories;
                categoriesList.SelectedIndex = 0; // Select "All" by default
            }
        }

        private void LoadTextures()
        {
            _allTextures.Clear();

            if (_currentPack == null || _assetExtractor == null) 
            {
                UpdateEmptyState();
                return;
            }

            try
            {
                // Check if assets are available for the current pack's Minecraft version
                var minecraftVersion = _currentPack.MinecraftVersion ?? "1.20.1";
                
                if (_assetExtractor.AreAssetsAvailable(minecraftVersion))
                {
                    var assetsPath = _assetExtractor.GetAssetsPath(minecraftVersion);
                    LoadTexturesFromDirectory(assetsPath, minecraftVersion);
                }
                else
                {
                    // Assets not available - show empty state with extraction option
                    System.Diagnostics.Debug.WriteLine($"Assets not available for Minecraft {minecraftVersion}");
                }

                FilterTextures();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading textures: {ex.Message}");
            }
            finally
            {
                UpdateEmptyState();
            }
        }

        private void LoadTexturesFromDirectory(string texturesDir, string minecraftVersion)
        {
            if (!Directory.Exists(texturesDir)) return;

            foreach (var file in Directory.GetFiles(texturesDir, "*.png", SearchOption.AllDirectories))
            {
                try
                {
                    var relativePath = Path.GetRelativePath(texturesDir, file);
                    var pathParts = relativePath.Split(Path.DirectorySeparatorChar);
                    var category = pathParts.Length > 0 ? pathParts[0] : "other";

                    var texture = new TextureItem
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        Category = category,
                        FilePath = file,
                        Size = GetFileSize(file),
                        RelativePath = relativePath.Replace('\\', '/') // Use forward slashes for consistency
                    };

                    // Load preview image
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(file);
                    bitmap.DecodePixelWidth = 64;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    texture.PreviewImage = bitmap;

                    _allTextures.Add(texture);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading texture {file}: {ex.Message}");
                }
            }

            // Update category counts
            LoadCategories();
        }

        private string GetFileSize(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var bytes = fileInfo.Length;
                
                if (bytes < 1024)
                    return $"{bytes} B";
                else if (bytes < 1024 * 1024)
                    return $"{bytes / 1024:F1} KB";
                else
                    return $"{bytes / (1024 * 1024):F1} MB";
            }
            catch
            {
                return "Unknown";
            }
        }

        private void UpdateDisplayMode()
        {
            var gridView = this.FindName("GridView") as FrameworkElement;
            var listView = this.FindName("ListView") as FrameworkElement;
            var viewModeButton = this.FindName("ViewModeButton") as Wpf.Ui.Controls.Button;

            if (gridView != null && listView != null && viewModeButton != null)
            {
                if (_isGridView)
                {
                    gridView.Visibility = Visibility.Visible;
                    listView.Visibility = Visibility.Collapsed;
                    viewModeButton.Icon = new SymbolIcon(Wpf.Ui.Controls.SymbolRegular.GridDots24);
                    viewModeButton.ToolTip = "Switch to list view";
                }
                else
                {
                    gridView.Visibility = Visibility.Collapsed;
                    listView.Visibility = Visibility.Visible;
                    viewModeButton.Icon = new SymbolIcon(Wpf.Ui.Controls.SymbolRegular.List24);
                    viewModeButton.ToolTip = "Switch to grid view";
                }
            }

            UpdateTextures();
        }

        private void UpdateEmptyState()
        {
            var emptyState = this.FindName("EmptyState") as FrameworkElement;
            if (emptyState != null)
            {
                var hasTextures = _filteredTextures.Count > 0;
                emptyState.Visibility = hasTextures ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void FilterTextures()
        {
            var selectedCategory = (this.FindName("CategoriesList") as ListBox)?.SelectedItem as TextureCategory;
            var searchQuery = (this.FindName("SearchBox") as Wpf.Ui.Controls.TextBox)?.Text?.ToLowerInvariant() ?? "";

            _filteredTextures = _allTextures.Where(texture =>
            {
                var matchesCategory = selectedCategory == null || 
                                     string.IsNullOrEmpty(selectedCategory.Category) ||
                                     texture.Category == selectedCategory.Category;

                var matchesSearch = string.IsNullOrEmpty(searchQuery) ||
                                   texture.Name.ToLowerInvariant().Contains(searchQuery) ||
                                   texture.Category.ToLowerInvariant().Contains(searchQuery);

                return matchesCategory && matchesSearch;
            }).ToList();

            UpdateTextures();
            UpdateEmptyState();
        }

        private void UpdateTextures()
        {
            var texturesGrid = this.FindName("TexturesGrid") as ItemsControl;
            var texturesList = this.FindName("TexturesList") as System.Windows.Controls.ListView;

            if (_isGridView && texturesGrid != null)
            {
                texturesGrid.ItemsSource = _filteredTextures;
            }
            else if (!_isGridView && texturesList != null)
            {
                texturesList.ItemsSource = _filteredTextures;
            }
        }

        // Event handlers
        public void OnLoaded(object sender, RoutedEventArgs e)
        {
            LoadTextures();
            FilterTextures();
        }

        public void CategoriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FilterTextures();
        }

        public void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterTextures();
        }

        public void ViewModeButton_Click(object sender, RoutedEventArgs e)
        {
            _isGridView = !_isGridView;
            UpdateDisplayMode();
        }

        public void TextureItem_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Context menu for texture operations - TODO: Implement texture override creation
        }

        public void RefreshTextures_Click(object sender, RoutedEventArgs e)
        {
            LoadTextures();
            FilterTextures();
        }

        public async void ExtractAssets_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPack == null || _assetExtractor == null) return;

            var minecraftVersion = _currentPack.MinecraftVersion ?? "1.20.1";
            
            // Show progress dialog
            var progressDialog = new AssetExtractionProgressDialog();
            progressDialog.Owner = Window.GetWindow(this);
            
            var progress = new Progress<string>(status => 
            {
                progressDialog.UpdateStatus(status);
            });

            progressDialog.Show();

            try
            {
                var success = await _assetExtractor.ExtractAssetsForVersion(minecraftVersion, progress);
                
                if (success)
                {
                    progressDialog.Close();
                    LoadTextures();
                    FilterTextures();
                    ShowMessage($"Successfully extracted assets for Minecraft {minecraftVersion}!", "Extraction Complete");
                }
                else
                {
                    progressDialog.Close();
                    ShowMessage($"Failed to extract assets for Minecraft {minecraftVersion}. Please check your internet connection and try again.", "Extraction Failed");
                }
            }
            catch (Exception ex)
            {
                progressDialog.Close();
                ShowMessage($"Error during asset extraction: {ex.Message}", "Error");
            }
        }

        private async void ShowMessage(string message, string title)
        {
            var msgBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "OK"
            };
            await msgBox.ShowDialogAsync();
        }

        // Helper classes
        public class TextureCategory
        {
            public string Name { get; set; }
            public Wpf.Ui.Controls.SymbolRegular Icon { get; set; }
            public string Category { get; set; }
            public int Count { get; set; }

            public TextureCategory(string name, Wpf.Ui.Controls.SymbolRegular icon, string category)
            {
                Name = name;
                Icon = icon;
                Category = category;
                Count = 0;
            }
        }

        public class TextureItem
        {
            public string Name { get; set; } = "";
            public string Category { get; set; } = "";
            public string FilePath { get; set; } = "";
            public string RelativePath { get; set; } = "";
            public string Size { get; set; } = "";
            public BitmapImage? PreviewImage { get; set; }
        }

        public async void CreateOverride_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPack == null) return;
            if (sender is not System.Windows.Controls.MenuItem mi || mi.Tag is not TextureItem item) return;

            try
            {
                var overridesRoot = Path.Combine(_currentPack.Location, "overrides", "textures");
                var relative = item.RelativePath.Replace('\\','/');
                var parts = relative.Split('/');
                string category = parts.Length > 1 ? parts[0] : "misc";
                var targetDir = Path.Combine(overridesRoot, category);
                Directory.CreateDirectory(targetDir);
                var targetPath = Path.Combine(targetDir, Path.GetFileName(item.FilePath));
                File.Copy(item.FilePath, targetPath, true);

                var manager = new ResourcePackTemplateManager();
                _currentPack = manager.ReadResourcePack(_currentPack.Location);

                ShowMessage($"Created override for {item.Name}", "Override Created");
            }
            catch (Exception ex) 
            {
                ShowMessage($"Failed creating override: {ex.Message}", "Error");
            }
        }
    }
}