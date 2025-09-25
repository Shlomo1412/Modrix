using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Modrix.Services;
using Modrix.Views.Windows;
using Wpf.Ui.Controls;
using Wpf.Ui.Abstractions.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using WpfButton = System.Windows.Controls.Button;
using UiButton = Wpf.Ui.Controls.Button;

namespace Modrix.Views.Pages.ResourcePack
{
    public partial class OverridesPage : System.Windows.Controls.Page, INavigableView<object>
    {
        public object ViewModel => this;
        
        private ResourcePackData? _currentPack;
        private List<TextureOverrideItem> _allTextureOverrides = new();
        private List<TranslationOverrideItem> _allTranslationOverrides = new();
        
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OverridesPage constructor error: {ex.Message}");
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

        private void RefreshOverrides()
        {
            if (_currentPack == null) return;

            LoadTextureOverrides();
            LoadTranslationOverrides();
            UpdateEmptyStates();
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

                    // Load preview image
                    if (File.Exists(override_.OverridePath))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(override_.OverridePath);
                        bitmap.DecodePixelWidth = 40;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        item.PreviewImage = bitmap;
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
            
            if (textureEmptyState != null)
                textureEmptyState.Visibility = _allTextureOverrides.Count == 0 ? 
                    Visibility.Visible : Visibility.Collapsed;
                    
            if (translationEmptyState != null)
                translationEmptyState.Visibility = _allTranslationOverrides.Count == 0 ? 
                    Visibility.Visible : Visibility.Collapsed;
        }

        private void TextureSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterTextureOverrides();
        }

        private void TranslationSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterTranslationOverrides();
        }

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
                        
                        // Refresh the pack data
                        var manager = new ResourcePackTemplateManager();
                        _currentPack = manager.ReadResourcePack(_currentPack.Location);
                        
                        RefreshOverrides();
                        
                        ShowMessage("Texture imported successfully", "Success");
                    }
                }
                catch (Exception ex)
                {
                    ShowMessage($"Failed to import texture: {ex.Message}", "Error");
                }
            }
        }

        private void CreateTranslation_Click(object sender, RoutedEventArgs e)
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
                    
                    // Refresh the pack data
                    var manager = new ResourcePackTemplateManager();
                    _currentPack = manager.ReadResourcePack(_currentPack.Location);
                    
                    RefreshOverrides();
                    
                    ShowMessage("Translation file created successfully", "Success");
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

        private void EditTextureOverride_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not WpfButton button || button.Tag is not TextureOverrideItem item)
                return;

            try
            {
                // Open texture comparison editor
                var comparisonWindow = new TextureComparisonWindow(item, _currentPack);
                comparisonWindow.Owner = Window.GetWindow(this);
                comparisonWindow.Show();
            }
            catch (Exception ex)
            {
                ShowMessage($"Failed to open texture comparison: {ex.Message}", "Error");
            }
        }

        private void EditTranslationOverride_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not WpfButton button || button.Tag is not TranslationOverrideItem item)
                return;

            try
            {
                // Open translation file in external editor
                Process.Start(new ProcessStartInfo
                {
                    FileName = item.OverridePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowMessage($"Failed to open translation file: {ex.Message}", "Error");
            }
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
                    
                    // Refresh the pack data
                    var manager = new ResourcePackTemplateManager();
                    _currentPack = manager.ReadResourcePack(_currentPack.Location);
                    
                    RefreshOverrides();
                    
                    ShowMessage("Texture override removed", "Success");
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
                    
                    // Refresh the pack data
                    var manager = new ResourcePackTemplateManager();
                    _currentPack = manager.ReadResourcePack(_currentPack.Location);
                    
                    RefreshOverrides();
                    
                    ShowMessage("Translation override removed", "Success");
                }
                catch (Exception ex)
                {
                    ShowMessage($"Failed to remove translation override: {ex.Message}", "Error");
                }
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

        // Helper classes
        public class TextureOverrideItem
        {
            public string Name { get; set; } = "";
            public string Category { get; set; } = "";
            public string OriginalPath { get; set; } = "";
            public string OverridePath { get; set; } = "";
            public BitmapImage? PreviewImage { get; set; }
        }

        public class TranslationOverrideItem
        {
            public string Name { get; set; } = "";
            public string Language { get; set; } = "";
            public string OverridePath { get; set; } = "";
            public int KeyCount { get; set; }
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
}