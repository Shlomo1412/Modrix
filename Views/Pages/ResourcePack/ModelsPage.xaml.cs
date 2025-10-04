using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Modrix.Services;
using Modrix.Views.Windows;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;
using SystemTextBlock = System.Windows.Controls.TextBlock;

namespace Modrix.Views.Pages.ResourcePack
{
    public partial class ModelsPage : System.Windows.Controls.Page, INavigableView<object>
    {
        public object ViewModel => this;
        
        private ResourcePackData? _currentPack;
        private List<ModelCategory> _categories = new();
        private List<ModelItem> _allModels = new();
        private List<ModelItem> _filteredModels = new();
        private bool _isGridView = true;
        private MinecraftAssetExtractor? _assetExtractor;
        private bool _isLoading = false;

        public ModelsPage()
        {
            try
            {
                InitializeComponent();
                DataContext = this;
                
                // Get asset extractor from DI container
                _assetExtractor = App.Services.GetService(typeof(MinecraftAssetExtractor)) as MinecraftAssetExtractor;
                
                LoadCurrentPack();
                LoadCategories();
                _ = LoadModelsAsync(); // Start loading asynchronously
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ModelsPage constructor error: {ex.Message}");
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
            _categories = new List<ModelCategory>
            {
                new ModelCategory("All", Wpf.Ui.Controls.SymbolRegular.Cube20, ""),
                new ModelCategory("Blocks", Wpf.Ui.Controls.SymbolRegular.Grid20, "block"),
                new ModelCategory("Items", Wpf.Ui.Controls.SymbolRegular.Box20, "item")
            };

            // Update counts with actual model data
            foreach (var category in _categories)
            {
                if (string.IsNullOrEmpty(category.Category))
                {
                    category.Count = _allModels.Count;
                }
                else
                {
                    category.Count = _allModels.Count(m => m.Category == category.Category);
                }
            }

            CategoriesList.ItemsSource = _categories;
            CategoriesList.SelectedIndex = 0; // Select "All" by default
        }

        private async Task LoadModelsAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                ShowLoadingState("Loading models...", "Please wait while we load the model data...");
                
                _allModels.Clear();

                if (_currentPack == null || _assetExtractor == null) 
                {
                    HideLoadingState();
                    ShowEmptyState();
                    return;
                }

                var minecraftVersion = _currentPack.MinecraftVersion ?? "1.21.4";
                
                if (_assetExtractor.AreModelsAssetsAvailable(minecraftVersion))
                {
                    var modelsPath = _assetExtractor.GetModelsAssetsPath(minecraftVersion);
                    await LoadModelsFromDirectoryAsync(modelsPath);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Model assets not available for Minecraft {minecraftVersion}");
                }

                FilterModels();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading models: {ex.Message}");
            }
            finally
            {
                LoadCategories(); // Update category counts
                _isLoading = false;
                HideLoadingState();
            }
        }

        private async Task LoadModelsFromDirectoryAsync(string modelsDir)
        {
            if (!Directory.Exists(modelsDir)) return;

            await Task.Run(() =>
            {
                var files = Directory.GetFiles(modelsDir, "*.json", SearchOption.AllDirectories);
                var models = new List<ModelItem>();

                for (int i = 0; i < files.Length; i++)
                {
                    var file = files[i];
                    
                    // Update progress periodically
                    if (i % 25 == 0)
                    {
                        var progress = (i + 1) * 100 / files.Length;
                        Dispatcher.Invoke(() => UpdateLoadingProgress($"Loading models... ({i + 1}/{files.Length})", progress));
                    }

                    try
                    {
                        var relativePath = Path.GetRelativePath(modelsDir, file);
                        var pathParts = relativePath.Split(Path.DirectorySeparatorChar);
                        var category = pathParts.Length > 0 ? pathParts[0] : "other";

                        var model = new ModelItem
                        {
                            Name = Path.GetFileNameWithoutExtension(file),
                            Category = category,
                            FilePath = file,
                            Size = GetFileSize(file),
                            RelativePath = relativePath.Replace('\\', '/')
                        };

                        models.Add(model);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading model {file}: {ex.Message}");
                    }
                }

                // Update UI on main thread
                Dispatcher.Invoke(() =>
                {
                    _allModels.AddRange(models);
                });
            });
        }

        private void ShowLoadingState(string message = "Loading...", string subtext = "Please wait...")
        {
            var loadingState = this.FindName("LoadingState") as FrameworkElement;
            var loadingText = this.FindName("LoadingText") as SystemTextBlock;
            var loadingSubtext = this.FindName("LoadingSubtext") as SystemTextBlock;
            var gridView = this.FindName("GridView") as FrameworkElement;
            var listView = this.FindName("ListView") as FrameworkElement;
            var emptyState = this.FindName("EmptyState") as FrameworkElement;
            
            if (loadingState != null) loadingState.Visibility = Visibility.Visible;
            if (loadingText != null) loadingText.Text = message;
            if (loadingSubtext != null) loadingSubtext.Text = subtext;
            if (gridView != null) gridView.Visibility = Visibility.Collapsed;
            if (listView != null) listView.Visibility = Visibility.Collapsed;
            if (emptyState != null) emptyState.Visibility = Visibility.Collapsed;
        }

        private void UpdateLoadingProgress(string message, int progress)
        {
            var loadingText = this.FindName("LoadingText") as SystemTextBlock;
            if (loadingText != null)
            {
                loadingText.Text = $"{message} ({progress}%)";
            }
        }

        private void HideLoadingState()
        {
            var loadingState = this.FindName("LoadingState") as FrameworkElement;
            if (loadingState != null) loadingState.Visibility = Visibility.Collapsed;
            
            UpdateDisplayMode(); // Show the appropriate view
            UpdateEmptyState();
        }

        private void ShowEmptyState()
        {
            var emptyState = this.FindName("EmptyState") as FrameworkElement;
            if (emptyState != null) emptyState.Visibility = Visibility.Visible;
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
            if (_isGridView)
            {
                GridView.Visibility = Visibility.Visible;
                ListView.Visibility = Visibility.Collapsed;
                ViewModeButton.Icon = new SymbolIcon(Wpf.Ui.Controls.SymbolRegular.GridDots24);
                ViewModeButton.ToolTip = "Switch to list view";
            }
            else
            {
                GridView.Visibility = Visibility.Collapsed;
                ListView.Visibility = Visibility.Visible;
                ViewModeButton.Icon = new SymbolIcon(Wpf.Ui.Controls.SymbolRegular.List24);
                ViewModeButton.ToolTip = "Switch to grid view";
            }

            UpdateModels();
        }

        private void UpdateEmptyState()
        {
            var hasModels = _filteredModels.Count > 0;
            EmptyState.Visibility = hasModels ? Visibility.Collapsed : Visibility.Visible;
        }

        private void FilterModels()
        {
            var selectedCategory = CategoriesList?.SelectedItem as ModelCategory;
            var searchQuery = SearchBox?.Text?.ToLowerInvariant() ?? "";

            _filteredModels = _allModels.Where(model =>
            {
                var matchesCategory = selectedCategory == null || 
                                     string.IsNullOrEmpty(selectedCategory.Category) ||
                                     model.Category == selectedCategory.Category;

                var matchesSearch = string.IsNullOrEmpty(searchQuery) ||
                                   model.Name.ToLowerInvariant().Contains(searchQuery) ||
                                   model.Category.ToLowerInvariant().Contains(searchQuery);

                return matchesCategory && matchesSearch;
            }).ToList();

            UpdateModels();
            UpdateEmptyState();
        }

        private void UpdateModels()
        {
            if (_isGridView)
            {
                ModelsGrid.ItemsSource = _filteredModels;
            }
            else
            {
                ModelsList.ItemsSource = _filteredModels;
            }
        }

        // Event handlers
        public void CategoriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FilterModels();
        }

        public void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterModels();
        }

        public void ViewModeButton_Click(object sender, RoutedEventArgs e)
        {
            _isGridView = !_isGridView;
            UpdateDisplayMode();
        }

        public void ModelItem_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ModelItem? item = null;
                
                // Handle double-click from grid view
                if (sender is Border border && border.DataContext is ModelItem gridItem)
                {
                    item = gridItem;
                }
                // Handle double-click from data grid
                else if (sender is Wpf.Ui.Controls.DataGrid dataGrid && dataGrid.SelectedItem is ModelItem listItem)
                {
                    item = listItem;
                }

                if (item != null)
                {
                    CreateOverrideForItem(item);
                }
            }
        }

        private async void CreateOverrideForItem(ModelItem item)
        {
            if (_currentPack == null) return;

            try
            {
                var overridesRoot = Path.Combine(_currentPack.Location, "overrides", "models");
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

        public async void RefreshModels_Click(object sender, RoutedEventArgs e)
        {
            await LoadModelsAsync();
        }

        public void CreateOverride_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPack == null) return;
            ModelItem? item = null;
            
            if (sender is System.Windows.Controls.MenuItem mi)
                item = mi.Tag as ModelItem;
            else if (sender is Wpf.Ui.Controls.Button btn)
                item = btn.Tag as ModelItem;
            
            if (item != null)
            {
                CreateOverrideForItem(item);
            }
        }

        public void ViewJson_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.MenuItem? mi = sender as System.Windows.Controls.MenuItem;
            Wpf.Ui.Controls.Button? btn = sender as Wpf.Ui.Controls.Button;
            
            var item = (mi?.Tag ?? btn?.Tag) as ModelItem;
            if (item == null) return;

            try
            {
                // Open JSON file in external editor for now
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = item.FilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowMessage($"Failed to open model: {ex.Message}", "Error");
            }
        }

        private async void ExtractAssets_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPack == null || _assetExtractor == null) return;

            var minecraftVersion = _currentPack.MinecraftVersion ?? "1.21.4";
            
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
                    await LoadModelsAsync();
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
        public class ModelCategory
        {
            public string Name { get; set; }
            public Wpf.Ui.Controls.SymbolRegular Icon { get; set; }
            public string Category { get; set; }
            public int Count { get; set; }

            public ModelCategory(string name, Wpf.Ui.Controls.SymbolRegular icon, string category)
            {
                Name = name;
                Icon = icon;
                Category = category;
                Count = 0;
            }
        }

        public class ModelItem
        {
            public string Name { get; set; } = "";
            public string Category { get; set; } = "";
            public string FilePath { get; set; } = "";
            public string RelativePath { get; set; } = "";
            public string Size { get; set; } = "";
        }
    }
}