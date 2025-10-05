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
using Modrix.Views.Controls;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Modrix.Views.Windows
{
    public partial class ItemSelectionDialog : FluentWindow
    {
        private readonly ModProjectData? _projectData;
        private readonly string _minecraftVersion;
        private readonly ItemPickerType _itemType;
        private readonly MinecraftAssetExtractor _assetExtractor;
        
        private List<ItemCategory> _categories = new();
        private List<ItemAsset> _allItems = new();
        private List<ItemAsset> _filteredItems = new();
        private bool _isMinecraftTab = false;
        private ItemAsset? _selectedItem;

        public string? SelectedItemPath { get; private set; }

        public ItemSelectionDialog(ModProjectData? projectData, string minecraftVersion, ItemPickerType itemType)
        {
            InitializeComponent();
            
            _projectData = projectData;
            _minecraftVersion = minecraftVersion ?? "1.20.1";
            _itemType = itemType;
            _assetExtractor = App.Services.GetService(typeof(MinecraftAssetExtractor)) as MinecraftAssetExtractor 
                             ?? new MinecraftAssetExtractor();

            // Update title based on item type
            Title = _itemType switch
            {
                ItemPickerType.Items => "Select Item",
                ItemPickerType.Blocks => "Select Block",
                _ => "Select Item or Block"
            };

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
            ShowLoadingState("Loading project items...");
            
            Task.Run(async () =>
            {
                try
                {
                    var projectItems = LoadProjectItems();
                    
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _allItems = projectItems;
                        LoadProjectCategories();
                        FilterItems();
                        UpdateItemsDisplay();
                    });
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading project items: {ex.Message}");
                        ShowEmptyState("No project items found", "Add texture files to your project's texture directory");
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
                    var minecraftItems = await LoadMinecraftItems();
                    
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _allItems = minecraftItems;
                        LoadMinecraftCategories();
                        FilterItems();
                        UpdateItemsDisplay();
                    });
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading Minecraft items: {ex.Message}");
                        ShowEmptyState("Minecraft assets not available", 
                                     $"Click 'Extract Assets' to download Minecraft {_minecraftVersion} textures");
                    });
                }
            });
        }

        private List<ItemAsset> LoadProjectItems()
        {
            var items = new List<ItemAsset>();
            
            if (_projectData == null || string.IsNullOrEmpty(_projectData.Location))
                return items;

            // Look for item/block textures in common project directories
            var textureDirectories = new[]
            {
                Path.Combine(_projectData.Location, "src", "main", "resources", "assets", _projectData.ModId, "textures"),
                Path.Combine(_projectData.Location, "assets", _projectData.ModId, "textures"),
                Path.Combine(_projectData.Location, "textures"),
                Path.Combine(_projectData.Location, "overrides", "textures")
            };

            foreach (var textureDir in textureDirectories.Where(Directory.Exists))
            {
                // Load items
                if (_itemType == ItemPickerType.Items || _itemType == ItemPickerType.Both)
                {
                    var itemDir = Path.Combine(textureDir, "item");
                    if (Directory.Exists(itemDir))
                    {
                        LoadItemsFromDirectory(itemDir, "item", items, true);
                    }
                }

                // Load blocks
                if (_itemType == ItemPickerType.Blocks || _itemType == ItemPickerType.Both)
                {
                    var blockDir = Path.Combine(textureDir, "block");
                    if (Directory.Exists(blockDir))
                    {
                        LoadItemsFromDirectory(blockDir, "block", items, true);
                    }
                }
            }

            return items;
        }

        private void LoadItemsFromDirectory(string directory, string category, List<ItemAsset> items, bool isProjectItem)
        {
            foreach (var file in Directory.GetFiles(directory, "*.png", SearchOption.AllDirectories))
            {
                try
                {
                    var relativePath = Path.GetRelativePath(directory, file);
                    var itemName = Path.GetFileNameWithoutExtension(file);

                    var item = new ItemAsset
                    {
                        Name = itemName,
                        Category = category,
                        FilePath = file,
                        RelativePath = relativePath.Replace('\\', '/'),
                        IsProjectItem = isProjectItem
                    };

                    // Load preview image
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(file);
                        bitmap.DecodePixelWidth = 64;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        item.PreviewImage = bitmap;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading preview for {file}: {ex.Message}");
                    }

                    items.Add(item);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading project item {file}: {ex.Message}");
                }
            }
        }

        private async Task<List<ItemAsset>> LoadMinecraftItems()
        {
            var items = new List<ItemAsset>();

            if (!_assetExtractor.AreAssetsAvailable(_minecraftVersion))
                return items;

            var assetsPath = _assetExtractor.GetAssetsPath(_minecraftVersion);
            if (!Directory.Exists(assetsPath))
                return items;

            // Load items
            if (_itemType == ItemPickerType.Items || _itemType == ItemPickerType.Both)
            {
                var itemPath = Path.Combine(assetsPath, "item");
                if (Directory.Exists(itemPath))
                {
                    LoadItemsFromDirectory(itemPath, "item", items, false);
                }
            }

            // Load blocks
            if (_itemType == ItemPickerType.Blocks || _itemType == ItemPickerType.Both)
            {
                var blockPath = Path.Combine(assetsPath, "block");
                if (Directory.Exists(blockPath))
                {
                    LoadItemsFromDirectory(blockPath, "block", items, false);
                }
            }

            return items;
        }

        private void LoadProjectCategories()
        {
            _categories = new List<ItemCategory>();

            if (_itemType == ItemPickerType.Both)
            {
                _categories.Add(new ItemCategory("All", Wpf.Ui.Controls.SymbolRegular.Grid20, ""));
            }

            if (_itemType == ItemPickerType.Items || _itemType == ItemPickerType.Both)
            {
                _categories.Add(new ItemCategory("Items", Wpf.Ui.Controls.SymbolRegular.Box20, "item"));
            }

            if (_itemType == ItemPickerType.Blocks || _itemType == ItemPickerType.Both)
            {
                _categories.Add(new ItemCategory("Blocks", Wpf.Ui.Controls.SymbolRegular.Grid20, "block"));
            }

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
            _categories = new List<ItemCategory>();

            if (_itemType == ItemPickerType.Both)
            {
                _categories.Add(new ItemCategory("All", Wpf.Ui.Controls.SymbolRegular.Grid20, ""));
            }

            if (_itemType == ItemPickerType.Blocks || _itemType == ItemPickerType.Both)
            {
                _categories.Add(new ItemCategory("Blocks", Wpf.Ui.Controls.SymbolRegular.Grid20, "block"));
            }

            if (_itemType == ItemPickerType.Items || _itemType == ItemPickerType.Both)
            {
                _categories.Add(new ItemCategory("Items", Wpf.Ui.Controls.SymbolRegular.Box20, "item"));
            }

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
                    category.Count = _allItems.Count;
                }
                else
                {
                    category.Count = _allItems.Count(t => t.Category == category.Category);
                }
            }
        }

        private void FilterItems()
        {
            var categoriesList = FindName("CategoriesList") as ListBox;
            var searchBox = FindName("SearchBox") as Wpf.Ui.Controls.TextBox;
            
            var selectedCategory = categoriesList?.SelectedItem as ItemCategory;
            var searchQuery = searchBox?.Text?.ToLowerInvariant() ?? "";

            _filteredItems = _allItems.Where(item =>
            {
                var matchesCategory = selectedCategory == null || 
                                     string.IsNullOrEmpty(selectedCategory.Category) ||
                                     item.Category == selectedCategory.Category;

                var matchesSearch = string.IsNullOrEmpty(searchQuery) ||
                                   item.Name.ToLowerInvariant().Contains(searchQuery) ||
                                   item.Category.ToLowerInvariant().Contains(searchQuery);

                return matchesCategory && matchesSearch;
            }).ToList();

            UpdateItemsDisplay();
        }

        private void UpdateItemsDisplay()
        {
            var itemsGrid = FindName("ItemsGrid") as ItemsControl;
            if (itemsGrid != null)
            {
                itemsGrid.ItemsSource = _filteredItems;
            }
            
            if (_filteredItems.Count == 0)
            {
                ShowEmptyState("No items found", "Try adjusting your search or category filter");
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
            var itemsGrid = FindName("ItemsGrid") as ItemsControl;
            var loadingText = FindName("LoadingText") as TextBlock;
            
            if (loadingState != null) loadingState.Visibility = Visibility.Visible;
            if (emptyState != null) emptyState.Visibility = Visibility.Collapsed;
            if (itemsGrid != null) itemsGrid.Visibility = Visibility.Collapsed;
            if (loadingText != null) loadingText.Text = message;
        }

        private void ShowEmptyState(string title, string subtitle)
        {
            var loadingState = FindName("LoadingState") as StackPanel;
            var emptyState = FindName("EmptyState") as StackPanel;
            var itemsGrid = FindName("ItemsGrid") as ItemsControl;
            var emptyStateText = FindName("EmptyStateText") as TextBlock;
            var emptyStateSubtext = FindName("EmptyStateSubtext") as TextBlock;
            
            if (loadingState != null) loadingState.Visibility = Visibility.Collapsed;
            if (emptyState != null) emptyState.Visibility = Visibility.Visible;
            if (itemsGrid != null) itemsGrid.Visibility = Visibility.Collapsed;
            if (emptyStateText != null) emptyStateText.Text = title;
            if (emptyStateSubtext != null) emptyStateSubtext.Text = subtitle;
        }

        private void HideLoadingAndEmptyStates()
        {
            var loadingState = FindName("LoadingState") as StackPanel;
            var emptyState = FindName("EmptyState") as StackPanel;
            var itemsGrid = FindName("ItemsGrid") as ItemsControl;
            
            if (loadingState != null) loadingState.Visibility = Visibility.Collapsed;
            if (emptyState != null) emptyState.Visibility = Visibility.Collapsed;
            if (itemsGrid != null) itemsGrid.Visibility = Visibility.Visible;
        }

        private void ItemCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is FrameworkElement element && element.Tag is ItemAsset item)
            {
                // Double-click to select
                _selectedItem = item;
                SelectItem();
            }
            else if (sender is FrameworkElement el && el.Tag is ItemAsset itm)
            {
                // Single click to highlight
                _selectedItem = itm;
                var selectButton = FindName("SelectButton") as Wpf.Ui.Controls.Button;
                if (selectButton != null) selectButton.IsEnabled = true;
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterItems();
        }

        private void CategoriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FilterItems();
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
            SelectItem();
        }

        private void SelectItem()
        {
            if (_selectedItem != null)
            {
                SelectedItemPath = _selectedItem.FilePath;
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

    // Helper classes
    public class ItemCategory
    {
        public string Name { get; set; }
        public Wpf.Ui.Controls.SymbolRegular Icon { get; set; }
        public string Category { get; set; }
        public int Count { get; set; }

        public ItemCategory(string name, Wpf.Ui.Controls.SymbolRegular icon, string category)
        {
            Name = name;
            Icon = icon;
            Category = category;
            Count = 0;
        }
    }

    public class ItemAsset
    {
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string RelativePath { get; set; } = "";
        public BitmapImage? PreviewImage { get; set; }
        public bool IsProjectItem { get; set; }
    }
}