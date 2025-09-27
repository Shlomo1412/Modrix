using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Modrix.Models;
using Modrix.Services;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Modrix.Views.Windows
{
    public partial class TextureSelectionDialog : FluentWindow
    {
        private readonly ModProjectData? _projectData;
        private readonly string _minecraftVersion;
        private readonly MinecraftAssetExtractor _assetExtractor;
        
        private List<TextureCategory> _categories = new();
        private List<TextureItem> _allTextures = new();
        private List<TextureItem> _filteredTextures = new();
        private bool _isMinecraftTab = false;
        private TextureItem? _selectedTexture;

        public string? SelectedTexturePath { get; private set; }

        public TextureSelectionDialog(ModProjectData? projectData, string minecraftVersion)
        {
            InitializeComponent();
            
            _projectData = projectData;
            _minecraftVersion = minecraftVersion ?? "1.20.1";
            _assetExtractor = App.Services.GetService(typeof(MinecraftAssetExtractor)) as MinecraftAssetExtractor 
                             ?? new MinecraftAssetExtractor();

            // Start with project tab
            LoadProjectTab();
        }

        private async void ProjectTab_Click(object sender, RoutedEventArgs e)
        {
            if (!_isMinecraftTab) return; // Already on project tab
            
            _isMinecraftTab = false;
            var projectTabButton = FindName("ProjectTabButton") as Wpf.Ui.Controls.Button;
            var minecraftTabButton = FindName("MinecraftTabButton") as Wpf.Ui.Controls.Button;
            var extractAssetsButton = FindName("ExtractAssetsButton") as Wpf.Ui.Controls.Button;
            
            if (projectTabButton != null) projectTabButton.Appearance = ControlAppearance.Primary;
            if (minecraftTabButton != null) minecraftTabButton.Appearance = ControlAppearance.Secondary;
            if (extractAssetsButton != null) extractAssetsButton.Visibility = Visibility.Collapsed;
            
            LoadProjectTab();
        }

        private async void MinecraftTab_Click(object sender, RoutedEventArgs e)
        {
            if (_isMinecraftTab) return; // Already on minecraft tab
            
            _isMinecraftTab = true;
            var projectTabButton = FindName("ProjectTabButton") as Wpf.Ui.Controls.Button;
            var minecraftTabButton = FindName("MinecraftTabButton") as Wpf.Ui.Controls.Button;
            var extractAssetsButton = FindName("ExtractAssetsButton") as Wpf.Ui.Controls.Button;
            
            if (projectTabButton != null) projectTabButton.Appearance = ControlAppearance.Secondary;
            if (minecraftTabButton != null) minecraftTabButton.Appearance = ControlAppearance.Primary;
            if (extractAssetsButton != null) extractAssetsButton.Visibility = Visibility.Visible;
            
            await LoadMinecraftTab();
        }

        private void LoadProjectTab()
        {
            ShowLoadingState("Loading project textures...");
            
            Task.Run(async () =>
            {
                try
                {
                    var projectTextures = LoadProjectTextures();
                    
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _allTextures = projectTextures;
                        LoadProjectCategories();
                        FilterTextures();
                        UpdateTexturesDisplay();
                    });
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading project textures: {ex.Message}");
                        ShowEmptyState("No project textures found", "Add texture files to your project's texture directory");
                    });
                }
            });
        }

        private async Task LoadMinecraftTab()
        {
            ShowLoadingState("Loading Minecraft assets...");
            
            await Task.Run(async () =>
            {
                try
                {
                    var minecraftTextures = await LoadMinecraftTextures();
                    
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _allTextures = minecraftTextures;
                        LoadMinecraftCategories();
                        FilterTextures();
                        UpdateTexturesDisplay();
                    });
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading Minecraft textures: {ex.Message}");
                        ShowEmptyState("Minecraft assets not available", 
                                     $"Click 'Extract Assets' to download Minecraft {_minecraftVersion} textures");
                    });
                }
            });
        }

        private List<TextureItem> LoadProjectTextures()
        {
            var textures = new List<TextureItem>();
            
            if (_projectData == null || string.IsNullOrEmpty(_projectData.Location))
                return textures;

            // Look for textures in common project directories
            var textureDirectories = new[]
            {
                Path.Combine(_projectData.Location, "src", "main", "resources", "assets", _projectData.ModId, "textures"),
                Path.Combine(_projectData.Location, "assets", _projectData.ModId, "textures"),
                Path.Combine(_projectData.Location, "textures"),
                Path.Combine(_projectData.Location, "overrides", "textures") // For resource packs
            };

            foreach (var textureDir in textureDirectories.Where(Directory.Exists))
            {
                foreach (var file in Directory.GetFiles(textureDir, "*.png", SearchOption.AllDirectories))
                {
                    try
                    {
                        var relativePath = Path.GetRelativePath(textureDir, file);
                        var pathParts = relativePath.Split(Path.DirectorySeparatorChar);
                        var category = pathParts.Length > 1 ? pathParts[0] : "other";

                        var texture = new TextureItem
                        {
                            Name = Path.GetFileNameWithoutExtension(file),
                            Category = category,
                            FilePath = file,
                            RelativePath = relativePath.Replace('\\', '/'),
                            IsProjectTexture = true
                        };

                        // Load preview image
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(file);
                        bitmap.DecodePixelWidth = 80;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        texture.PreviewImage = bitmap;

                        textures.Add(texture);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading project texture {file}: {ex.Message}");
                    }
                }
            }

            return textures;
        }

        private async Task<List<TextureItem>> LoadMinecraftTextures()
        {
            var textures = new List<TextureItem>();

            if (!_assetExtractor.AreAssetsAvailable(_minecraftVersion))
                return textures;

            var assetsPath = _assetExtractor.GetAssetsPath(_minecraftVersion);
            if (!Directory.Exists(assetsPath))
                return textures;

            // Load textures from Minecraft assets (similar to TexturesPage.xaml.cs)
            foreach (var file in Directory.GetFiles(assetsPath, "*.png", SearchOption.AllDirectories))
            {
                try
                {
                    var relativePath = Path.GetRelativePath(assetsPath, file);
                    var pathParts = relativePath.Split(Path.DirectorySeparatorChar);
                    var category = pathParts.Length > 0 ? pathParts[0] : "other";

                    var texture = new TextureItem
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        Category = category,
                        FilePath = file,
                        RelativePath = relativePath.Replace('\\', '/'),
                        IsProjectTexture = false
                    };

                    // Load preview image
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(file);
                    bitmap.DecodePixelWidth = 80;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    texture.PreviewImage = bitmap;

                    textures.Add(texture);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading Minecraft texture {file}: {ex.Message}");
                }
            }

            return textures;
        }

        private void LoadProjectCategories()
        {
            _categories = new List<TextureCategory>
            {
                new TextureCategory("All", Wpf.Ui.Controls.SymbolRegular.Grid20, ""),
                new TextureCategory("Items", Wpf.Ui.Controls.SymbolRegular.Box20, "item"),
                new TextureCategory("Blocks", Wpf.Ui.Controls.SymbolRegular.Grid20, "block"),
                new TextureCategory("Other", Wpf.Ui.Controls.SymbolRegular.Document20, "other")
            };

            UpdateCategoryCounts();
            var categoriesList = FindName("CategoriesList") as ListBox;
            if (categoriesList != null)
            {
                categoriesList.ItemsSource = _categories;
                categoriesList.SelectedIndex = 0;
            }
        }

        private void LoadMinecraftCategories()
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
                new TextureCategory("Effects", Wpf.Ui.Controls.SymbolRegular.Flash20, "effect"),
                new TextureCategory("Other", Wpf.Ui.Controls.SymbolRegular.Document20, "other")
            };

            UpdateCategoryCounts();
            var categoriesList = FindName("CategoriesList") as ListBox;
            if (categoriesList != null)
            {
                categoriesList.ItemsSource = _categories;
                categoriesList.SelectedIndex = 0;
            }
        }

        private void UpdateCategoryCounts()
        {
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
        }

        private void FilterTextures()
        {
            var categoriesList = FindName("CategoriesList") as ListBox;
            var searchBox = FindName("SearchBox") as Wpf.Ui.Controls.TextBox;
            
            var selectedCategory = categoriesList?.SelectedItem as TextureCategory;
            var searchQuery = searchBox?.Text?.ToLowerInvariant() ?? "";

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

            UpdateTexturesDisplay();
        }

        private void UpdateTexturesDisplay()
        {
            var texturesGrid = FindName("TexturesGrid") as ItemsControl;
            if (texturesGrid != null)
            {
                texturesGrid.ItemsSource = _filteredTextures;
            }
            
            if (_filteredTextures.Count == 0)
            {
                ShowEmptyState("No textures found", "Try adjusting your search or category filter");
            }
            else
            {
                HideLoadingAndEmptyStates();
            }
        }

        private void ShowLoadingState(string message)
        {
            var loadingState = FindName("LoadingState") as StackPanel;
            var emptyState = FindName("EmptyState") as StackPanel;
            var texturesGrid = FindName("TexturesGrid") as ItemsControl;
            var loadingText = FindName("LoadingText") as TextBlock;
            
            if (loadingState != null) loadingState.Visibility = Visibility.Visible;
            if (emptyState != null) emptyState.Visibility = Visibility.Collapsed;
            if (texturesGrid != null) texturesGrid.Visibility = Visibility.Collapsed;
            if (loadingText != null) loadingText.Text = message;
        }

        private void ShowEmptyState(string title, string subtitle)
        {
            var loadingState = FindName("LoadingState") as StackPanel;
            var emptyState = FindName("EmptyState") as StackPanel;
            var texturesGrid = FindName("TexturesGrid") as ItemsControl;
            var emptyStateText = FindName("EmptyStateText") as TextBlock;
            var emptyStateSubtext = FindName("EmptyStateSubtext") as TextBlock;
            
            if (loadingState != null) loadingState.Visibility = Visibility.Collapsed;
            if (emptyState != null) emptyState.Visibility = Visibility.Visible;
            if (texturesGrid != null) texturesGrid.Visibility = Visibility.Collapsed;
            if (emptyStateText != null) emptyStateText.Text = title;
            if (emptyStateSubtext != null) emptyStateSubtext.Text = subtitle;
        }

        private void HideLoadingAndEmptyStates()
        {
            var loadingState = FindName("LoadingState") as StackPanel;
            var emptyState = FindName("EmptyState") as StackPanel;
            var texturesGrid = FindName("TexturesGrid") as ItemsControl;
            
            if (loadingState != null) loadingState.Visibility = Visibility.Collapsed;
            if (emptyState != null) emptyState.Visibility = Visibility.Collapsed;
            if (texturesGrid != null) texturesGrid.Visibility = Visibility.Visible;
        }

        private void TextureItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is FrameworkElement element && element.Tag is TextureItem texture)
            {
                // Double-click to select
                _selectedTexture = texture;
                SelectTexture();
            }
            else if (sender is FrameworkElement el && el.Tag is TextureItem tex)
            {
                // Single click to highlight
                _selectedTexture = tex;
                var selectButton = FindName("SelectButton") as Wpf.Ui.Controls.Button;
                if (selectButton != null) selectButton.IsEnabled = true;
                
                // Visual feedback could be added here (highlighting selected item)
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterTextures();
        }

        private void CategoriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FilterTextures();
        }

        private async void ExtractAssets_Click(object sender, RoutedEventArgs e)
        {
            ShowLoadingState($"Extracting Minecraft {_minecraftVersion} assets...");
            
            try
            {
                var progress = new Progress<string>(status => 
                {
                    Dispatcher.Invoke(() => {
                        var loadingText = FindName("LoadingText") as TextBlock;
                        if (loadingText != null) loadingText.Text = status;
                    });
                });

                var success = await _assetExtractor.ExtractAssetsForVersion(_minecraftVersion, progress);
                
                if (success)
                {
                    await LoadMinecraftTab();
                    await ShowMessage("Extraction Complete", $"Successfully extracted Minecraft {_minecraftVersion} assets!");
                }
                else
                {
                    ShowEmptyState("Extraction Failed", $"Failed to extract Minecraft {_minecraftVersion} assets. Please check your internet connection.");
                }
            }
            catch (Exception ex)
            {
                ShowEmptyState("Extraction Error", $"Error during extraction: {ex.Message}");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            SelectTexture();
        }

        private void SelectTexture()
        {
            if (_selectedTexture != null)
            {
                SelectedTexturePath = _selectedTexture.FilePath;
                DialogResult = true;
                Close();
            }
        }

        private async System.Threading.Tasks.Task ShowMessage(string title, string message)
        {
            var msgBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "OK"
            };
            await msgBox.ShowDialogAsync();
        }
    }

    // Helper classes (reusing from TexturesPage)
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
        public BitmapImage? PreviewImage { get; set; }
        public bool IsProjectTexture { get; set; }
    }
}