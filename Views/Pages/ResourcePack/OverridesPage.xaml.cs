using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Modrix.Services;
using Modrix.Views.Windows;
using Modrix.ViewModels.Pages;
using Wpf.Ui.Controls;
using Wpf.Ui.Abstractions.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using WpfButton = System.Windows.Controls.Button;
using UiButton = Wpf.Ui.Controls.Button;
using SystemMenuItem = System.Windows.Controls.MenuItem;
using SystemTextBlock = System.Windows.Controls.TextBlock;
using System.Text.Json; // added for mcmeta parsing
using System.ComponentModel; // for INotifyPropertyChanged
using System.Windows.Threading; // for animation timer

namespace Modrix.Views.Pages.ResourcePack
{
    public partial class OverridesPage : System.Windows.Controls.Page, INavigableView<object>
    {
        public object ViewModel => this;
        
        private ResourcePackData? _currentPack;
        private List<TextureOverrideItem> _allTextureOverrides = new();
        private List<TranslationOverrideItem> _allTranslationOverrides = new();
        private List<ModelOverrideItem> _allModelOverrides = new();
        private DispatcherTimer? _previewAnimationTimer; // timer for animated previews
        private FileSystemWatcher? _overridesWatcher;
        private DispatcherTimer? _refreshDelayTimer; // Debounce rapid file changes
        
        public OverridesPage()
        {
            try
            {
                InitializeComponent();
                DataContext = this;
                System.Diagnostics.Debug.WriteLine("OverridesPage: InitializeComponent completed");
                
                LoadCurrentPack();
                System.Diagnostics.Debug.WriteLine($"OverridesPage: CurrentPack loaded = {_currentPack?.Name ?? "null"}");
                
                RefreshOverrides();
                System.Diagnostics.Debug.WriteLine("OverridesPage: RefreshOverrides completed");

                // Initialize animation preview timer (20 FPS ~ 50ms)
                _previewAnimationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                _previewAnimationTimer.Tick += PreviewAnimationTimer_Tick;
                _previewAnimationTimer.Start();

                // Initialize file system watcher
                InitializeFileSystemWatcher();

                // Initialize refresh delay timer for debouncing
                _refreshDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                _refreshDelayTimer.Tick += (s, e) =>
                {
                    _refreshDelayTimer.Stop();
                    RefreshOverrides();
                };

                // Handle page unload to cleanup watchers
                Unloaded += OverridesPage_Unloaded;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OverridesPage constructor error: {ex.Message}");
                throw;
            }
        }

        private void OverridesPage_Unloaded(object sender, RoutedEventArgs e)
        {
            // Cleanup watchers when page is unloaded
            _overridesWatcher?.Dispose();
            _previewAnimationTimer?.Stop();
            _refreshDelayTimer?.Stop();
        }

        private void InitializeFileSystemWatcher()
        {
            if (_currentPack == null) return;

            try
            {
                var overridesPath = Path.Combine(_currentPack.Location, "overrides");
                
                // Ensure the overrides directory exists
                Directory.CreateDirectory(overridesPath);

                _overridesWatcher?.Dispose(); // Clean up existing watcher

                _overridesWatcher = new FileSystemWatcher(overridesPath)
                {
                    NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                _overridesWatcher.Created += OnOverridesChanged;
                _overridesWatcher.Deleted += OnOverridesChanged;
                _overridesWatcher.Changed += OnOverridesChanged;
                _overridesWatcher.Renamed += OnOverridesRenamed;

                System.Diagnostics.Debug.WriteLine($"File system watcher initialized for: {overridesPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize file system watcher: {ex.Message}");
            }
        }

        private void OnOverridesChanged(object sender, FileSystemEventArgs e)
        {
            // Debounce rapid changes by restarting the timer
            Dispatcher.BeginInvoke(() =>
            {
                _refreshDelayTimer?.Stop();
                _refreshDelayTimer?.Start();
                System.Diagnostics.Debug.WriteLine($"File system change detected: {e.ChangeType} - {e.Name}");
            });
        }

        private void OnOverridesRenamed(object sender, RenamedEventArgs e)
        {
            // Handle renamed files
            Dispatcher.BeginInvoke(() =>
            {
                _refreshDelayTimer?.Stop();
                _refreshDelayTimer?.Start();
                System.Diagnostics.Debug.WriteLine($"File system rename detected: {e.OldName} -> {e.Name}");
            });
        }

        private void PreviewAnimationTimer_Tick(object? sender, EventArgs e)
        {
            var now = DateTime.UtcNow;
            foreach (var tex in _allTextureOverrides)
            {
                if (!tex.IsAnimated || tex.FramesSequence.Count == 0) continue;
                // Check elapsed
                if ((now - tex.LastFrameSwitch).TotalMilliseconds >= tex.FrameDurations[tex.CurrentSequenceIndex])
                {
                    tex.CurrentSequenceIndex = (tex.CurrentSequenceIndex + 1) % tex.FramesSequence.Count;
                    tex.LastFrameSwitch = now;
                    tex.PreviewImage = tex.FramesSequence[tex.CurrentSequenceIndex];
                }
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
                
                // Reinitialize file system watcher when pack changes
                InitializeFileSystemWatcher();
            }
        }

        private void RefreshOverrides()
        {
            if (_currentPack == null) return;

            try
            {
                // Re-read the pack data to get the latest overrides
                var manager = new ResourcePackTemplateManager();
                var freshPackData = manager.ReadResourcePack(_currentPack.Location);
                _currentPack = freshPackData;

                // Update the workspace with fresh data
                UpdateWorkspacePack(freshPackData);

                LoadTextureOverrides();
                LoadTranslationOverrides();
                LoadModelOverrides();
                UpdateEmptyStates();

                System.Diagnostics.Debug.WriteLine("OverridesPage: Refreshed overrides successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing overrides: {ex.Message}");
            }
        }

        private void UpdateWorkspacePack(ResourcePackData freshPackData)
        {
            try
            {
                var workspace = Application.Current.Windows
                    .OfType<ResourcePackWorkspace>()
                    .FirstOrDefault();

                if (workspace?.ViewModel != null)
                {
                    workspace.ViewModel.LoadPack(freshPackData);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating workspace pack: {ex.Message}");
            }
        }

        private void RefreshOverrides_Click(object sender, RoutedEventArgs e)
        {
            RefreshOverrides();
        }

        private void LoadTextureOverrides()
        {
            _allTextureOverrides.Clear();

            if (_currentPack?.Overrides == null) return;

            var textureOverrides = _currentPack.Overrides
                .Where(o => o.Type == OverrideType.Texture)
                .ToList();

            foreach (var override_ in textureOverrides)
            {
                try
                {
                    var item = new TextureOverrideItem
                    {
                        Name = Path.GetFileNameWithoutExtension(override_.OverridePath),
                        Category = override_.Category,
                        OriginalPath = override_.OriginalPath,
                        OverridePath = override_.OverridePath
                    };

                    // Load preview & animation frames if present
                    if (File.Exists(override_.OverridePath))
                    {
                        try
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.UriSource = new Uri(override_.OverridePath);
                            bitmap.EndInit();
                            bitmap.Freeze();

                            bool looksLikeSpriteSheet = bitmap.PixelHeight > bitmap.PixelWidth && bitmap.PixelHeight % bitmap.PixelWidth == 0;
                            string mcmetaPath = override_.OverridePath + ".mcmeta";
                            bool hasMcmeta = File.Exists(mcmetaPath);

                            if (looksLikeSpriteSheet)
                            {
                                BuildAnimatedPreview(item, bitmap, mcmetaPath, hasMcmeta);
                            }
                            else
                            {
                                // Static - scale to 40px bounding box
                                item.PreviewImage = CreateScaledBitmap(bitmap, 40, 40);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error preparing preview: {ex.Message}");
                        }
                    }

                    _allTextureOverrides.Add(item);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading texture override: {ex.Message}");
                }
            }

            // Update UI using FindName to avoid null reference
            var textureOverridesList = this.FindName("TextureOverridesList") as ItemsControl;
            if (textureOverridesList != null)
            {
                textureOverridesList.ItemsSource = _allTextureOverrides;
            }
        }

        private BitmapSource CreateScaledBitmap(BitmapSource source, int maxW, int maxH)
        {
            double scale = Math.Min((double)maxW / source.PixelWidth, (double)maxH / source.PixelHeight);
            var tb = new TransformedBitmap(source, new ScaleTransform(scale, scale));
            tb.Freeze();
            return tb;
        }

        private void BuildAnimatedPreview(TextureOverrideItem item, BitmapImage sheet, string mcmetaPath, bool hasMcmeta)
        {
            int frameSize = sheet.PixelWidth; // assume vertical strip of square frames
            int frameCount = sheet.PixelHeight / frameSize;
            var baseFrames = new List<BitmapSource>();
            for (int i = 0; i < frameCount; i++)
            {
                try
                {
                    var crop = new CroppedBitmap(sheet, new Int32Rect(0, i * frameSize, frameSize, frameSize));
                    crop.Freeze();
                    baseFrames.Add(CreateScaledBitmap(crop, 40, 40));
                }
                catch { }
            }

            // Build animation sequence
            if (baseFrames.Count == 0)
            {
                item.PreviewImage = CreateScaledBitmap(sheet, 40, 40);
                return;
            }

            List<int> sequenceIndices = new();
            List<int> sequenceDurations = new(); // ms per sequence frame

            if (hasMcmeta)
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(mcmetaPath));
                    if (doc.RootElement.TryGetProperty("animation", out var anim))
                    {
                        int globalTicks = anim.TryGetProperty("frametime", out var ft) ? ft.GetInt32() : 1; // ticks (1 tick = 50ms)
                        int globalMs = Math.Max(1, globalTicks) * 50;

                        if (anim.TryGetProperty("frames", out var framesElem) && framesElem.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var f in framesElem.EnumerateArray())
                            {
                                if (f.ValueKind == JsonValueKind.Number)
                                {
                                    int idx = f.GetInt32();
                                    if (idx >= 0 && idx < baseFrames.Count)
                                    {
                                        sequenceIndices.Add(idx);
                                        sequenceDurations.Add(globalMs);
                                    }
                                }
                                else if (f.ValueKind == JsonValueKind.Object)
                                {
                                    if (f.TryGetProperty("index", out var idxProp))
                                    {
                                        int idx = idxProp.GetInt32();
                                        int timeTicks = f.TryGetProperty("time", out var timeProp) ? timeProp.GetInt32() : globalTicks;
                                        int ms = Math.Max(1, timeTicks) * 50;
                                        if (idx >= 0 && idx < baseFrames.Count)
                                        {
                                            sequenceIndices.Add(idx);
                                            sequenceDurations.Add(ms);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            // No frames array -> sequential
                            for (int i = 0; i < baseFrames.Count; i++)
                            {
                                sequenceIndices.Add(i);
                                sequenceDurations.Add(globalMs);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed parsing mcmeta: {ex.Message}");
                }
            }

            if (sequenceIndices.Count == 0)
            {
                // fallback sequential 100ms each
                for (int i = 0; i < baseFrames.Count; i++)
                {
                    sequenceIndices.Add(i);
                    sequenceDurations.Add(100);
                }
            }

            item.IsAnimated = true;
            item.FramesSequence = sequenceIndices.Select(i => baseFrames[i]).ToList();
            item.FrameDurations = sequenceDurations;
            item.CurrentSequenceIndex = 0;
            item.LastFrameSwitch = DateTime.UtcNow;
            item.PreviewImage = item.FramesSequence[0];
        }

        private void LoadTranslationOverrides()
        {
            _allTranslationOverrides.Clear();

            if (_currentPack?.Overrides == null) return;

            var translationOverrides = _currentPack.Overrides
                .Where(o => o.Type == OverrideType.Translation)
                .ToList();

            foreach (var override_ in translationOverrides)
            {
                try
                {
                    var item = new TranslationOverrideItem
                    {
                        Name = Path.GetFileNameWithoutExtension(override_.OverridePath),
                        Language = GetLanguageName(Path.GetFileNameWithoutExtension(override_.OverridePath)),
                        OverridePath = override_.OverridePath
                    };

                    // Count translation keys
                    if (File.Exists(override_.OverridePath))
                    {
                        var content = File.ReadAllText(override_.OverridePath);
                        try
                        {
                            var jsonDoc = System.Text.Json.JsonDocument.Parse(content);
                            item.KeyCount = CountJsonKeys(jsonDoc.RootElement);
                        }
                        catch
                        {
                            item.KeyCount = 0;
                        }
                    }

                    _allTranslationOverrides.Add(item);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading translation override: {ex.Message}");
                }
            }

            // Update UI using FindName to avoid null reference
            var translationOverridesList = this.FindName("TranslationOverridesList") as ItemsControl;
            if (translationOverridesList != null)
            {
                translationOverridesList.ItemsSource = _allTranslationOverrides;
            }
        }

        private void LoadModelOverrides()
        {
            _allModelOverrides.Clear();

            if (_currentPack?.Overrides == null) return;

            var modelOverrides = _currentPack.Overrides
                .Where(o => o.Type == OverrideType.Model)
                .ToList();

            foreach (var override_ in modelOverrides)
            {
                try
                {
                    var item = new ModelOverrideItem
                    {
                        Name = Path.GetFileNameWithoutExtension(override_.OverridePath),
                        Category = override_.Category,
                        OriginalPath = override_.OriginalPath,
                        OverridePath = override_.OverridePath
                    };

                    _allModelOverrides.Add(item);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading model override: {ex.Message}");
                }
            }

            // Update UI using FindName to avoid null reference
            var modelOverridesList = this.FindName("ModelOverridesList") as ItemsControl;
            if (modelOverridesList != null)
            {
                modelOverridesList.ItemsSource = _allModelOverrides;
            }
        }

        private int CountJsonKeys(System.Text.Json.JsonElement element)
        {
            if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                return element.EnumerateObject().Count();
            }
            return 0;
        }

        private string GetLanguageName(string code)
        {
            return code switch
            {
                "en_us" => "English (US)",
                "en_gb" => "English (UK)",
                "de_de" => "German",
                "fr_fr" => "French",
                "es_es" => "Spanish",
                "it_it" => "Italian",
                "ja_jp" => "Japanese",
                "ko_kr" => "Korean",
                "zh_cn" => "Chinese (Simplified)",
                _ => code.Replace("_", "-").ToUpperInvariant()
            };
        }

        private void UpdateEmptyStates()
        {
            // Find empty state controls by name
            var textureEmptyState = this.FindName("TextureEmptyState") as FrameworkElement;
            var translationEmptyState = this.FindName("TranslationEmptyState") as FrameworkElement;
            var modelEmptyState = this.FindName("ModelEmptyState") as FrameworkElement;
            
            if (textureEmptyState != null)
                textureEmptyState.Visibility = _allTextureOverrides.Count == 0 ? 
                    Visibility.Visible : Visibility.Collapsed;
                    
            if (translationEmptyState != null)
                translationEmptyState.Visibility = _allTranslationOverrides.Count == 0 ? 
                    Visibility.Visible : Visibility.Collapsed;

            if (modelEmptyState != null)
                modelEmptyState.Visibility = _allModelOverrides.Count == 0 ? 
                    Visibility.Visible : Visibility.Collapsed;
        }

        private void TextureSearchBox_TextChanged(object sender, TextChangedEventArgs e) => FilterTextureOverrides();
        private void TranslationSearchBox_TextChanged(object sender, TextChangedEventArgs e) => FilterTranslationOverrides();
        private void ModelSearchBox_TextChanged(object sender, TextChangedEventArgs e) => FilterModelOverrides();

        private void FilterTextureOverrides()
        {
            var searchBox = this.FindName("TextureSearchBox") as Wpf.Ui.Controls.TextBox;
            var textureOverridesList = this.FindName("TextureOverridesList") as ItemsControl;
            var query = searchBox?.Text?.ToLowerInvariant() ?? "";
            
            if (textureOverridesList == null) return;
            
            if (string.IsNullOrEmpty(query))
            {
                textureOverridesList.ItemsSource = _allTextureOverrides;
            }
            else
            {
                var filtered = _allTextureOverrides.Where(item =>
                    item.Name.ToLowerInvariant().Contains(query) ||
                    item.Category.ToLowerInvariant().Contains(query)).ToList();
                    
                textureOverridesList.ItemsSource = filtered;
            }
        }

        private void FilterTranslationOverrides()
        {
            var searchBox = this.FindName("TranslationSearchBox") as Wpf.Ui.Controls.TextBox;
            var translationOverridesList = this.FindName("TranslationOverridesList") as ItemsControl;
            var query = searchBox?.Text?.ToLowerInvariant() ?? "";
            
            if (translationOverridesList == null) return;
            
            if (string.IsNullOrEmpty(query))
            {
                translationOverridesList.ItemsSource = _allTranslationOverrides;
            }
            else
            {
                var filtered = _allTranslationOverrides.Where(item =>
                    item.Name.ToLowerInvariant().Contains(query) ||
                    item.Language.ToLowerInvariant().Contains(query)).ToList();
                    
                translationOverridesList.ItemsSource = filtered;
            }
        }

        private void FilterModelOverrides()
        {
            var searchBox = this.FindName("ModelSearchBox") as Wpf.Ui.Controls.TextBox;
            var modelOverridesList = this.FindName("ModelOverridesList") as ItemsControl;
            var query = searchBox?.Text?.ToLowerInvariant() ?? "";
            
            if (modelOverridesList == null) return;
            
            if (string.IsNullOrEmpty(query))
            {
                modelOverridesList.ItemsSource = _allModelOverrides;
            }
            else
            {
                var filtered = _allModelOverrides.Where(item =>
                    item.Name.ToLowerInvariant().Contains(query) ||
                    item.Category.ToLowerInvariant().Contains(query)).ToList();
                    
                modelOverridesList.ItemsSource = filtered;
            }
        }

        private async void ImportTexture_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPack == null) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import Texture Override",
                Filter = "PNG Images|*.png|All Images|*.png;*.jpg;*.jpeg",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var sourceFile = dialog.FileName;
                    var fileName = Path.GetFileName(sourceFile);
                    
                    // Let user choose the category/destination
                    var categoryDialog = new TextureCategoryDialog();
                    if (categoryDialog.ShowDialog() == true)
                    {
                        var category = categoryDialog.SelectedCategory;
                        var targetDir = Path.Combine(_currentPack.Location, "overrides", "textures", category);
                        Directory.CreateDirectory(targetDir);
                        
                        var targetFile = Path.Combine(targetDir, fileName);
                        File.Copy(sourceFile, targetFile, true);
                        
                        ShowMessage("Texture imported successfully", "Success");
                        
                        // The file system watcher will automatically refresh the overrides
                        // But we can force an immediate refresh if needed
                        await System.Threading.Tasks.Task.Delay(100); // Small delay to ensure file is fully written
                    }
                }
                catch (Exception ex)
                {
                    ShowMessage($"Failed to import texture: {ex.Message}", "Error");
                }
            }
        }

        private async void ImportModel_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPack == null) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import Model Override",
                Filter = "JSON Files|*.json|All Files|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var sourceFile = dialog.FileName;
                    var fileName = Path.GetFileName(sourceFile);
                    
                    // Let user choose the category/destination
                    var categoryDialog = new ModelCategoryDialog();
                    if (categoryDialog.ShowDialog() == true)
                    {
                        var category = categoryDialog.SelectedCategory;
                        var targetDir = Path.Combine(_currentPack.Location, "overrides", "models", category);
                        Directory.CreateDirectory(targetDir);
                        
                        var targetFile = Path.Combine(targetDir, fileName);
                        File.Copy(sourceFile, targetFile, true);
                        
                        ShowMessage("Model imported successfully", "Success");
                        
                        // The file system watcher will automatically refresh the overrides
                        await System.Threading.Tasks.Task.Delay(100); // Small delay to ensure file is fully written
                    }
                }
                catch (Exception ex)
                {
                    ShowMessage($"Failed to import model: {ex.Message}", "Error");
                }
            }
        }

        private async void CreateTranslation_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPack == null) return;
            
            var dialog = new CreateTranslationDialog();
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var languageCode = dialog.LanguageCode;
                    var targetDir = Path.Combine(_currentPack.Location, "overrides", "translations");
                    Directory.CreateDirectory(targetDir);
                    
                    var targetFile = Path.Combine(targetDir, $"{languageCode}.json");
                    
                    // Create empty translation file
                    var emptyTranslation = "{\n  \"example.key\": \"Example translation\"\n}";
                    File.WriteAllText(targetFile, emptyTranslation);
                    
                    ShowMessage("Translation file created successfully", "Success");
                    
                    // The file system watcher will automatically refresh the overrides
                    await System.Threading.Tasks.Task.Delay(100); // Small delay to ensure file is fully written
                }
                catch (Exception ex)
                {
                    ShowMessage($"Failed to create translation: {ex.Message}", "Error");
                }
            }
        }

        private void OpenTexturesFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPack == null) return;
            
            var texturesDir = Path.Combine(_currentPack.Location, "overrides", "textures");
            Directory.CreateDirectory(texturesDir);
            Process.Start("explorer.exe", texturesDir);
        }

        private void OpenTranslationsFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPack == null) return;
            
            var translationsDir = Path.Combine(_currentPack.Location, "overrides", "translations");
            Directory.CreateDirectory(translationsDir);
            Process.Start("explorer.exe", translationsDir);
        }

        private void OpenModelsFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPack == null) return;
            
            var modelsDir = Path.Combine(_currentPack.Location, "overrides", "models");
            Directory.CreateDirectory(modelsDir);
            Process.Start("explorer.exe", modelsDir);
        }

        private void EditTextureOverride_Click(object sender, RoutedEventArgs e)
        {
            WpfButton? button = sender as WpfButton;
            SystemMenuItem? mi = sender as SystemMenuItem;
            var item = (button?.Tag ?? mi?.Tag) as TextureOverrideItem;
            if (item == null) return;

            try
            {
                OpenTextureInEditor(item.OverridePath, item.Name);
            }
            catch (Exception ex)
            {
                ShowMessage($"Failed to open texture editor: {ex.Message}", "Error");
            }
        }

        private void OpenTextureInEditor(string filePath, string fileName)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    ShowMessage("Texture file not found.", "Error");
                    return;
                }

                // Find the parent TabControl (should be OverrideTabs)
                var parentTabControl = this.FindName("OverrideTabs") as TabControl;
                if (parentTabControl == null)
                {
                    // Fallback to external editor if we can't find the tab control
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                    return;
                }

                // Check if a tab for this texture is already open
                TabItem existingTab = null;
                foreach (var item in parentTabControl.Items)
                {
                    if (item is TabItem tabItem && tabItem.Header is StackPanel existingHeaderPanel)
                    {
                        foreach (var child in existingHeaderPanel.Children)
                        {
                            if (child is SystemTextBlock tb && tb.Text == $"Edit: {fileName}")
                            {
                                existingTab = tabItem;
                                break;
                            }
                        }
                    }
                    if (existingTab != null) break;
                }
                
                if (existingTab != null)
                {
                    // Tab already open, just select it
                    parentTabControl.SelectedItem = existingTab;
                    return;
                }
                
                // Create the editor page (allow animation for resource pack textures)
                var editorVm = new TextureEditorViewModel();
                editorVm.EnableAnimation(true);
                var editorPage = new TextureEditorPage(editorVm, allowAnimation: true);
                editorVm.SetPngPath(filePath);

                // Create a Frame to host the page
                var frame = new Frame();
                frame.Navigate(editorPage);

                // Create a close button
                var closeButton = new UiButton
                {
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Dismiss24 },
                    Width = 22,
                    Height = 22,
                    Margin = new Thickness(4, 0, 0, 0),
                    Padding = new Thickness(0),
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    ToolTip = "Close"
                };

                // Create the header with icon, text, and close button
                var headerPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new SymbolIcon
                        {
                            Symbol = Wpf.Ui.Controls.SymbolRegular.ImageEdit24,
                            Margin = new Thickness(0, 0, 4, 0)
                        },
                        new SystemTextBlock
                        {
                            Text = $"Edit: {fileName}",
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        closeButton
                    }
                };

                // Create a new tab
                var tab = new TabItem
                {
                    Header = headerPanel,
                    Content = frame
                };

                // ContextMenu for tab
                var contextMenu = new ContextMenu();
                var openInWindowMenuItem = new SystemMenuItem
                {
                    Header = "Open as a New Window",
                    Icon = new SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Open24)
                };
                openInWindowMenuItem.Click += (s, e) => OpenTabAsWindow(tab, parentTabControl);
                contextMenu.Items.Add(openInWindowMenuItem);
                tab.ContextMenu = contextMenu;

                // Close button event
                closeButton.Click += (s, e) =>
                {
                    parentTabControl.Items.Remove(tab);
                };

                // Add and select the new tab
                parentTabControl.Items.Add(tab);
                parentTabControl.SelectedItem = tab;
            }
            catch (Exception ex)
            {
                ShowMessage($"Error opening texture editor: {ex.Message}", "Error");
            }
        }

        private void OpenTabAsWindow(TabItem tabItem, TabControl parentTabControl)
        {
            if (tabItem == null) return;
            
            parentTabControl.Items.Remove(tabItem);
            
            // Extract the title from the header
            var title = "Detached Tab";
            if (tabItem.Header is StackPanel windowHeaderPanel)
            {
                var textBlock = windowHeaderPanel.Children.OfType<SystemTextBlock>().FirstOrDefault();
                if (textBlock != null)
                    title = textBlock.Text;
            }
            
            var newWindow = new Window
            {
                Title = title,
                Content = tabItem.Content,
                Width = 800,
                Height = 600,
                Owner = Window.GetWindow(this)
            };
            newWindow.Show();
        }

        private async void RemoveTextureOverride_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not WpfButton button || button.Tag is not TextureOverrideItem item)
                return;

            var result = await new MessageBox
            {
                Title = "Remove Override",
                Content = $"Are you sure you want to remove the texture override '{item.Name}'?",
                PrimaryButtonText = "Remove",
                CloseButtonText = "Cancel"
            }.ShowDialogAsync();

            if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
            {
                try
                {
                    File.Delete(item.OverridePath);
                    
                    // Also delete .mcmeta file if it exists
                    var mcmetaPath = item.OverridePath + ".mcmeta";
                    if (File.Exists(mcmetaPath))
                    {
                        File.Delete(mcmetaPath);
                    }
                    
                    ShowMessage("Texture override removed", "Success");
                    
                    // The file system watcher will automatically refresh the overrides
                }
                catch (Exception ex)
                {
                    ShowMessage($"Failed to remove texture override: {ex.Message}", "Error");
                }
            }
        }

        private async void RemoveTranslationOverride_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not WpfButton button || button.Tag is not TranslationOverrideItem item)
                return;

            var result = await new MessageBox
            {
                Title = "Remove Override",
                Content = $"Are you sure you want to remove the translation override '{item.Name}'?",
                PrimaryButtonText = "Remove",
                CloseButtonText = "Cancel"
            }.ShowDialogAsync();

            if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
            {
                try
                {
                    File.Delete(item.OverridePath);
                    
                    ShowMessage("Translation override removed", "Success");
                    
                    // The file system watcher will automatically refresh the overrides
                }
                catch (Exception ex)
                {
                    ShowMessage($"Failed to remove translation override: {ex.Message}", "Error");
                }
            }
        }

        private async void RemoveModelOverride_Click(object sender, RoutedEventArgs e)
        {
            WpfButton? button = sender as WpfButton;
            System.Windows.Controls.MenuItem? mi = sender as System.Windows.Controls.MenuItem;
            var item = (button?.Tag ?? mi?.Tag) as ModelOverrideItem;
            if (item == null) return;

            var result = await new MessageBox
            {
                Title = "Remove Override",
                Content = $"Are you sure you want to remove the model override '{item.Name}'?",
                PrimaryButtonText = "Remove",
                CloseButtonText = "Cancel"
            }.ShowDialogAsync();

            if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
            {
                try
                {
                    File.Delete(item.OverridePath);
                    
                    ShowMessage("Model override removed", "Success");
                    
                    // The file system watcher will automatically refresh the overrides
                }
                catch (Exception ex)
                {
                    ShowMessage($"Failed to remove model override: {ex.Message}", "Error");
                }
            }
        }

        private void EditModelOverride_Click(object sender, RoutedEventArgs e)
        {
            WpfButton? button = sender as WpfButton;
            SystemMenuItem? mi = sender as SystemMenuItem;
            var item = (button?.Tag ?? mi?.Tag) as ModelOverrideItem;
            if (item == null) return;

            try
            {
                // Open JSON file in external editor
                Process.Start(new ProcessStartInfo
                {
                    FileName = item.OverridePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowMessage($"Failed to open model editor: {ex.Message}", "Error");
            }
        }

        private async void ShowMessage(string message, string title)
        {
            var msgBox = new MessageBox
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "OK"
            };
            await msgBox.ShowDialogAsync();
        }

        // --- New: Open override viewer handlers ---
        private void TextureOverride_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is Border b && b.DataContext is TextureOverrideItem item)
            {
                OpenTextureOverrideViewer(item);
            }
        }

        private void ModelOverride_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is Border b && b.DataContext is ModelOverrideItem item)
            {
                OpenModelOverrideEditor(item);
            }
        }

        private void OpenTextureOverride_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem mi && mi.Tag is TextureOverrideItem item)
                OpenTextureOverrideViewer(item);
            else if (sender is WpfButton btn && btn.Tag is TextureOverrideItem item2)
                OpenTextureOverrideViewer(item2);
        }

        private void OpenTranslationOverride_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem mi && mi.Tag is TranslationOverrideItem tr)
            {
                try { Process.Start(new ProcessStartInfo { FileName = tr.OverridePath, UseShellExecute = true }); } catch { }
            }
            else if (sender is WpfButton btn && btn.Tag is TranslationOverrideItem tr2)
            {
                try { Process.Start(new ProcessStartInfo { FileName = tr2.OverridePath, UseShellExecute = true }); } catch { }
            }
        }

        private void OpenModelOverride_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem mi && mi.Tag is ModelOverrideItem item)
                OpenModelOverrideEditor(item);
            else if (sender is WpfButton btn && btn.Tag is ModelOverrideItem item2)
                OpenModelOverrideEditor(item2);
        }

        private void OpenTextureOverrideViewer(TextureOverrideItem item)
        {
            try
            {
                var originalAbs = string.Empty;
                if (_currentPack != null)
                {
                    originalAbs = Path.Combine(_currentPack.Location, item.OriginalPath.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(originalAbs))
                    {
                        var extracted = Path.Combine(_currentPack.Location, ".minecraft_assets");
                        if (Directory.Exists(extracted))
                        {
                            var alt = Path.Combine(extracted, item.OriginalPath.Replace('/', Path.DirectorySeparatorChar));
                            if (File.Exists(alt)) originalAbs = alt;
                        }
                    }
                }
                var viewer = new OverrideViewerWindow(item.OverridePath, originalAbs)
                {
                    Owner = Window.GetWindow(this)
                };
                viewer.Show();
            }
            catch (Exception ex)
            {
                ShowMessage($"Failed to open viewer: {ex.Message}", "Error");
            }
        }

        private void OpenModelOverrideEditor(ModelOverrideItem item)
        {
            try
            {
                // For now, just open in external editor
                Process.Start(new ProcessStartInfo
                {
                    FileName = item.OverridePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowMessage($"Failed to open model editor: {ex.Message}", "Error");
            }
        }

        // --- Inline viewer window class ---
        private class OverrideViewerWindow : FluentWindow
        {
            public OverrideViewerWindow(string overridePath, string originalPath)
            {
                // Initialize viewer window
                Title = "Override Viewer";
                Width = 800;
                Height = 600;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;

                var grid = new Grid();
                Content = grid;

                // Add image control
                var image = new System.Windows.Controls.Image();
                image.HorizontalAlignment = HorizontalAlignment.Center;
                image.VerticalAlignment = VerticalAlignment.Center;
                grid.Children.Add(image);

                // --- File open handling ---
                Loaded += (s, e) =>
                {
                    try
                    {
                        // Try to load the override file directly
                        image.Source = new BitmapImage(new Uri(overridePath));
                    }
                    catch
                    {
                        // If it fails, try to load the original file
                        try
                        {
                            image.Source = new BitmapImage(new Uri(originalPath));
                        }
                        catch
                        {
                            // If both fail, show a placeholder or error image
                            // image.Source = new BitmapImage(new Uri("pack://application:,,,/Images/error.png"));
                        }
                    }
                };

                // --- Close handling ---
                Closing += (s, e) =>
                {
                    // Handle any cleanup if necessary
                };
            }
        }
        
        // Helper classes
        public class TextureOverrideItem : INotifyPropertyChanged
        {
            public string Name { get; set; } = "";
            public string Category { get; set; } = "";
            public string OriginalPath { get; set; } = "";
            public string OverridePath { get; set; } = "";

            private BitmapSource? _previewImage;
            public BitmapSource? PreviewImage
            {
                get => _previewImage;
                set { _previewImage = value; OnPropertyChanged(nameof(PreviewImage)); }
            }

            // Animation metadata
            public bool IsAnimated { get; set; } = false;
            public List<BitmapSource> FramesSequence { get; set; } = new();
            public List<int> FrameDurations { get; set; } = new(); // ms
            public int CurrentSequenceIndex { get; set; } = 0;
            public DateTime LastFrameSwitch { get; set; } = DateTime.UtcNow;

            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        public class TranslationOverrideItem
        {
            public string Name { get; set; } = "";
            public string Language { get; set; } = "";
            public string OverridePath { get; set; } = "";
            public int KeyCount { get; set; }
        }

        public class ModelOverrideItem
        {
            public string Name { get; set; } = "";
            public string Category { get; set; } = "";
            public string OriginalPath { get; set; } = "";
            public string OverridePath { get; set; } = "";
        }
    }

    // Simple dialog for texture category selection
    public partial class TextureCategoryDialog : FluentWindow
    {
        public string SelectedCategory { get; private set; } = "item";

        public TextureCategoryDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Title = "Select Texture Category";
            Width = 400;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var stackPanel = new StackPanel { Margin = new Thickness(20) };
            
            stackPanel.Children.Add(new System.Windows.Controls.TextBlock 
            { 
                Text = "Select the category for this texture override:",
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 16)
            });

            var categories = new[] { "item", "block", "entity", "gui", "environment" };
            var comboBox = new ComboBox 
            { 
                ItemsSource = categories,
                SelectedIndex = 0,
                Margin = new Thickness(0, 0, 0, 20)
            };

            var buttonPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okButton = new Wpf.Ui.Controls.Button 
            { 
                Content = "OK", 
                Appearance = Wpf.Ui.Controls.ControlAppearance.Primary,
                Margin = new Thickness(0, 0, 8, 0)
            };
            okButton.Click += (s, e) =>
            {
                SelectedCategory = comboBox.SelectedItem?.ToString() ?? "item";
                DialogResult = true;
            };

            var cancelButton = new Wpf.Ui.Controls.Button 
            { 
                Content = "Cancel",
                Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary
            };
            cancelButton.Click += (s, e) => DialogResult = false;

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            stackPanel.Children.Add(comboBox);
            stackPanel.Children.Add(buttonPanel);

            Content = stackPanel;
        }
    }

    // New model category dialog
    public partial class ModelCategoryDialog : FluentWindow
    {
        public string SelectedCategory { get; private set; } = "generic";

        public ModelCategoryDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Title = "Select Model Category";
            Width = 400;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var stackPanel = new StackPanel { Margin = new Thickness(20) };
            
            stackPanel.Children.Add(new System.Windows.Controls.TextBlock 
            { 
                Text = "Select the category for this model override:",
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 16)
            });

            var categories = new[] { "generic", "armor", "dungeon", "ship", "vehicle" };
            var comboBox = new ComboBox 
            { 
                ItemsSource = categories,
                SelectedIndex = 0,
                Margin = new Thickness(0, 0, 0, 20)
            };

            var buttonPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okButton = new Wpf.Ui.Controls.Button 
            { 
                Content = "OK", 
                Appearance = Wpf.Ui.Controls.ControlAppearance.Primary,
                Margin = new Thickness(0, 0, 8, 0)
            };
            okButton.Click += (s, e) =>
            {
                SelectedCategory = comboBox.SelectedItem?.ToString() ?? "generic";
                DialogResult = true;
            };

            var cancelButton = new Wpf.Ui.Controls.Button 
            { 
                Content = "Cancel",
                Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary
            };
            cancelButton.Click += (s, e) => DialogResult = false;

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            stackPanel.Children.Add(comboBox);
            stackPanel.Children.Add(buttonPanel);

            Content = stackPanel;
        }
    }

    // Simple dialog for translation creation
    public partial class CreateTranslationDialog : FluentWindow
    {
        public string LanguageCode { get; private set; } = "en_us";

        public CreateTranslationDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Title = "Create Translation Override";
            Width = 400;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var stackPanel = new StackPanel { Margin = new Thickness(20) };
            
            stackPanel.Children.Add(new System.Windows.Controls.TextBlock 
            { 
                Text = "Select the language for this translation override:",
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 16)
            });

            var languages = new[] { 
                "en_us", "en_gb", "de_de", "fr_fr", "es_es", "it_it", 
                "ja_jp", "ko_kr", "zh_cn", "zh_tw", "ru_ru", "pt_br",
                "pl_pl", "nl_nl", "sv_se", "fi_fi", "da_dk", "cs_cz",
                "tr_tr", "hu_hu", "ro_ro", "uk_ua", "hi_in", "ar_sa"
             };
            var comboBox = new ComboBox 
            { 
                ItemsSource = languages,
                SelectedIndex = 0,
                Margin = new Thickness(0, 0, 0, 20)
            };

            var buttonPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okButton = new Wpf.Ui.Controls.Button 
            { 
                Content = "Create", 
                Appearance = Wpf.Ui.Controls.ControlAppearance.Primary,
                Margin = new Thickness(0, 0, 8, 0)
            };
            okButton.Click += (s, e) =>
            {
                LanguageCode = comboBox.SelectedItem?.ToString() ?? "en_us";
                DialogResult = true;
            };

            var cancelButton = new Wpf.Ui.Controls.Button 
            { 
                Content = "Cancel",
                Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary
            };
            cancelButton.Click += (s, e) => DialogResult = false;

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            stackPanel.Children.Add(comboBox);
            stackPanel.Children.Add(buttonPanel);

            Content = stackPanel;
        }
    }
}