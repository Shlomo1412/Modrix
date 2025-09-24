using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Modrix.Models;
using Modrix.Services;
using Modrix.Views.Windows;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace Modrix.Views.Pages
{
    public partial class OverridesPage : Page
    {
        private ResourcePackWorkspaceViewModel _workspaceViewModel;
        private ObservableCollection<TextureOverrideItem> _textureOverrides;
        private ObservableCollection<TranslationOverrideItem> _translationOverrides;
        private ObservableCollection<string> _availableLanguages;
        
        public OverridesPage()
        {
            InitializeComponent();
            InitializeCollections();
            LoadData();
        }

        private void InitializeCollections()
        {
            _textureOverrides = new ObservableCollection<TextureOverrideItem>();
            _translationOverrides = new ObservableCollection<TranslationOverrideItem>();
            _availableLanguages = new ObservableCollection<string>
            {
                "en_us", "en_gb", "de_de", "fr_fr", "es_es", "it_it", 
                "ja_jp", "ko_kr", "pt_br", "ru_ru", "zh_cn"
            };

            TextureOverridesList.ItemsSource = _textureOverrides;
            TranslationOverridesList.ItemsSource = _translationOverrides;
            LanguageSelector.ItemsSource = _availableLanguages;
            LanguageSelector.SelectedItem = "en_us";
        }

        private async void LoadData()
        {
            try
            {
                // Get workspace view model from parent window
                if (Application.Current.Windows.OfType<ResourcePackWorkspace>().FirstOrDefault()?.ViewModel is ResourcePackWorkspaceViewModel workspaceVm)
                {
                    _workspaceViewModel = workspaceVm;
                    await LoadOverrides();
                }
                
                UpdateEmptyStates();
            }
            catch (Exception ex)
            {
                ShowMessage("Error loading overrides", ex.Message);
            }
        }

        private async System.Threading.Tasks.Task LoadOverrides()
        {
            if (_workspaceViewModel?.CurrentProject?.Location == null)
                return;

            _textureOverrides.Clear();
            _translationOverrides.Clear();

            var projectLocation = _workspaceViewModel.CurrentProject.Location;
            
            // Load texture overrides
            var texturesPath = Path.Combine(projectLocation, "assets", "minecraft", "textures");
            if (Directory.Exists(texturesPath))
            {
                LoadTextureOverrides(texturesPath);
            }

            // Load translation overrides
            var langPath = Path.Combine(projectLocation, "assets", "minecraft", "lang");
            if (Directory.Exists(langPath))
            {
                LoadTranslationOverrides(langPath);
            }

            // Update counts
            TextureOverrideCount.Text = _textureOverrides.Count.ToString();
            TranslationOverrideCount.Text = _translationOverrides.Count.ToString();
        }

        private void LoadTextureOverrides(string texturesPath)
        {
            var textureFiles = Directory.GetFiles(texturesPath, "*.png", SearchOption.AllDirectories);
            
            foreach (var file in textureFiles)
            {
                var relativePath = Path.GetRelativePath(texturesPath, file);
                var overridePath = relativePath.Replace('\\', '/').Replace(".png", "");
                
                _textureOverrides.Add(new TextureOverrideItem
                {
                    OverridePath = $"textures/{overridePath}",
                    FilePath = file,
                    PreviewImage = file,
                    Status = File.Exists(file) ? "Active" : "Missing"
                });
            }
        }

        private void LoadTranslationOverrides(string langPath)
        {
            var langFiles = Directory.GetFiles(langPath, "*.json");
            
            foreach (var file in langFiles)
            {
                var language = Path.GetFileNameWithoutExtension(file);
                try
                {
                    var content = File.ReadAllText(file);
                    var translations = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(content);
                    
                    if (translations != null)
                    {
                        foreach (var kvp in translations)
                        {
                            if (!kvp.Key.StartsWith("_comment"))
                            {
                                _translationOverrides.Add(new TranslationOverrideItem
                                {
                                    Language = language,
                                    Key = kvp.Key,
                                    CustomValue = kvp.Value,
                                    OriginalValue = GetOriginalTranslation(kvp.Key) // TODO: Implement
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Skip malformed JSON files
                    continue;
                }
            }
        }

        private string GetOriginalTranslation(string key)
        {
            // TODO: Implement fetching original Minecraft translations
            return "Original translation"; // Placeholder
        }

        private void UpdateEmptyStates()
        {
            TextureEmptyState.Visibility = _textureOverrides.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TranslationEmptyState.Visibility = _translationOverrides.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // Event Handlers
        private void AddTextureOverride_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Texture to Override",
                Filter = "PNG Images|*.png|All Files|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var file in openFileDialog.FileNames)
                {
                    ImportTextureOverride(file);
                }
                _ = LoadOverrides();
            }
        }

        private void ImportTextures_Click(object sender, RoutedEventArgs e)
        {
            AddTextureOverride_Click(sender, e);
        }

        private void EditTextureOverride_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is TextureOverrideItem item)
            {
                // Open texture editor
                try
                {
                    var textureEditor = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.GetType().Name == "TextureEditor");
                    if (textureEditor != null)
                    {
                        textureEditor.Show();
                        textureEditor.Activate();
                    }
                    else
                    {
                        // TODO: Create and show texture editor window with the file
                        ShowMessage("Texture Editor", "Texture editor functionality will be available soon");
                    }
                }
                catch (Exception ex)
                {
                    ShowMessage("Error", $"Failed to open texture editor: {ex.Message}");
                }
            }
        }

        private void RemoveTextureOverride_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is TextureOverrideItem item)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to remove the texture override for '{item.OverridePath}'?",
                    "Remove Override",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (File.Exists(item.FilePath))
                        {
                            File.Delete(item.FilePath);
                        }
                        _ = LoadOverrides();
                    }
                    catch (Exception ex)
                    {
                        ShowMessage("Error", $"Failed to remove override: {ex.Message}");
                    }
                }
            }
        }

        private void AddTranslationOverride_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Show dialog to add translation override
            ShowMessage("Add Translation", "Translation override editor coming soon");
        }

        private void ImportLanguages_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Import Language File",
                Filter = "JSON Files|*.json|All Files|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var file in openFileDialog.FileNames)
                {
                    ImportLanguageFile(file);
                }
                _ = LoadOverrides();
            }
        }

        private void EditTranslationOverride_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is TranslationOverrideItem item)
            {
                // TODO: Show translation edit dialog
                ShowMessage("Edit Translation", $"Editing translation for key: {item.Key}");
            }
        }

        private void RemoveTranslationOverride_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is TranslationOverrideItem item)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to remove the translation override for '{item.Key}'?",
                    "Remove Override",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        RemoveTranslationFromFile(item);
                        _ = LoadOverrides();
                    }
                    catch (Exception ex)
                    {
                        ShowMessage("Error", $"Failed to remove translation override: {ex.Message}");
                    }
                }
            }
        }

        private void RefreshTextureOverrides_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadOverrides();
        }

        private void TextureSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterTextureOverrides();
        }

        private void TranslationSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterTranslationOverrides();
        }

        private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FilterTranslationOverrides();
        }

        // Helper Methods
        private void ImportTextureOverride(string sourceFile)
        {
            if (_workspaceViewModel?.CurrentProject?.Location == null)
                return;

            try
            {
                var fileName = Path.GetFileName(sourceFile);
                var targetDir = Path.Combine(_workspaceViewModel.CurrentProject.Location, "assets", "minecraft", "textures");
                
                // Try to determine the appropriate subdirectory
                var subdirectory = DetermineTextureDirectory(fileName);
                targetDir = Path.Combine(targetDir, subdirectory);
                
                Directory.CreateDirectory(targetDir);
                var targetPath = Path.Combine(targetDir, fileName);
                
                File.Copy(sourceFile, targetPath, true);
            }
            catch (Exception ex)
            {
                ShowMessage("Import Error", $"Failed to import texture: {ex.Message}");
            }
        }

        private void ImportLanguageFile(string sourceFile)
        {
            if (_workspaceViewModel?.CurrentProject?.Location == null)
                return;

            try
            {
                var fileName = Path.GetFileName(sourceFile);
                var targetDir = Path.Combine(_workspaceViewModel.CurrentProject.Location, "assets", "minecraft", "lang");
                
                Directory.CreateDirectory(targetDir);
                var targetPath = Path.Combine(targetDir, fileName);
                
                File.Copy(sourceFile, targetPath, true);
            }
            catch (Exception ex)
            {
                ShowMessage("Import Error", $"Failed to import language file: {ex.Message}");
            }
        }

        private void RemoveTranslationFromFile(TranslationOverrideItem item)
        {
            if (_workspaceViewModel?.CurrentProject?.Location == null)
                return;

            var langFile = Path.Combine(_workspaceViewModel.CurrentProject.Location, "assets", "minecraft", "lang", $"{item.Language}.json");
            
            if (File.Exists(langFile))
            {
                var content = File.ReadAllText(langFile);
                var translations = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(content);
                
                if (translations != null && translations.ContainsKey(item.Key))
                {
                    translations.Remove(item.Key);
                    
                    var updatedContent = System.Text.Json.JsonSerializer.Serialize(translations, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(langFile, updatedContent);
                }
            }
        }

        private string DetermineTextureDirectory(string fileName)
        {
            var name = fileName.ToLower();
            
            if (name.Contains("block"))
                return "block";
            else if (name.Contains("item"))
                return "item";
            else if (name.Contains("entity"))
                return "entity";
            else if (name.Contains("gui"))
                return "gui";
            else
                return "misc";
        }

        private void FilterTextureOverrides()
        {
            // TODO: Implement filtering logic
        }

        private void FilterTranslationOverrides()
        {
            // TODO: Implement filtering logic  
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

    // Data models for overrides
    public class TextureOverrideItem
    {
        public string OverridePath { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string PreviewImage { get; set; } = "";
        public string Status { get; set; } = "";
    }

    public class TranslationOverrideItem
    {
        public string Language { get; set; } = "";
        public string Key { get; set; } = "";
        public string OriginalValue { get; set; } = "";
        public string CustomValue { get; set; } = "";
    }
}