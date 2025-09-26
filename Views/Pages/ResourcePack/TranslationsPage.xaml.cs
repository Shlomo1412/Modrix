using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Modrix.Services;
using Modrix.Views.Windows;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace Modrix.Views.Pages.ResourcePack
{
    public partial class TranslationsPage : System.Windows.Controls.Page, INavigableView<object>
    {
        public object ViewModel => this;
        
        private ResourcePackData? _currentPack;
        private List<LanguageItem> _languages = new();
        private List<TranslationKeyItem> _allTranslations = new();
        private string? _selectedLanguageCode;
        private MinecraftAssetExtractor? _assetExtractor;

        public TranslationsPage()
        {
            try
            {
                InitializeComponent();
                DataContext = this;
                
                // Get asset extractor from DI container
                _assetExtractor = App.Services.GetService(typeof(MinecraftAssetExtractor)) as MinecraftAssetExtractor;
                
                LoadCurrentPack();
                LoadAvailableLanguages();
                UpdateEmptyState();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TranslationsPage constructor error: {ex.Message}");
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

        private void LoadAvailableLanguages()
        {
            _languages.Clear();

            if (_currentPack == null || _assetExtractor == null) return;

            var minecraftVersion = _currentPack.MinecraftVersion ?? "1.21.4";
            
            if (!_assetExtractor.AreLanguageAssetsAvailable(minecraftVersion))
            {
                // Show only basic language options if assets aren't extracted
                _languages.Add(new LanguageItem("en_us", "English (US)", 0));
                _languages.Add(new LanguageItem("en_gb", "English (UK)", 0));
                _languages.Add(new LanguageItem("de_de", "German", 0));
                _languages.Add(new LanguageItem("fr_fr", "French", 0));
                _languages.Add(new LanguageItem("es_es", "Spanish", 0));
            }
            else
            {
                var langDir = _assetExtractor.GetLanguageAssetsPath(minecraftVersion);
                var langFiles = Directory.GetFiles(langDir, "*.json");
                
                foreach (var file in langFiles)
                {
                    var code = Path.GetFileNameWithoutExtension(file);
                    var displayName = GetLanguageDisplayName(code);
                    var keyCount = CountKeysInFile(file);
                    
                    _languages.Add(new LanguageItem(code, displayName, keyCount));
                }
            }

            LanguagesList.ItemsSource = _languages;
        }

        private int CountKeysInFile(string filePath)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                var doc = JsonSerializer.Deserialize<Dictionary<string, object>>(content);
                return doc?.Count ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        private string GetLanguageDisplayName(string code)
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
                "zh_tw" => "Chinese (Traditional)",
                "ru_ru" => "Russian",
                "pt_br" => "Portuguese (Brazil)",
                "nl_nl" => "Dutch",
                "sv_se" => "Swedish",
                "pl_pl" => "Polish",
                _ => code.Replace("_", "-").ToUpperInvariant()
            };
        }

        private void UpdateEmptyState()
        {
            var hasTranslations = _allTranslations.Count > 0;
            EmptyState.Visibility = hasTranslations ? Visibility.Collapsed : Visibility.Visible;
            CategoryTabs.Visibility = hasTranslations ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LanguagesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguagesList.SelectedItem is LanguageItem selectedLang)
            {
                _selectedLanguageCode = selectedLang.Code;
                LoadTranslationsForLanguage(selectedLang.Code);
            }
        }

        private void LoadTranslationsForLanguage(string languageCode)
        {
            _allTranslations.Clear();

            if (_currentPack == null || _assetExtractor == null) return;

            var minecraftVersion = _currentPack.MinecraftVersion ?? "1.21.4";
            
            if (!_assetExtractor.AreLanguageAssetsAvailable(minecraftVersion))
            {
                System.Diagnostics.Debug.WriteLine($"Language assets not available for {minecraftVersion}");
                UpdateEmptyState();
                return;
            }

            var langDir = _assetExtractor.GetLanguageAssetsPath(minecraftVersion);
            var langFile = Path.Combine(langDir, $"{languageCode}.json");
            
            if (!File.Exists(langFile))
            {
                System.Diagnostics.Debug.WriteLine($"Language file not found: {langFile}");
                UpdateEmptyState();
                return;
            }

            try
            {
                var content = File.ReadAllText(langFile);
                var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(content);
                
                if (translations != null)
                {
                    foreach (var kvp in translations)
                    {
                        var category = CategorizeTranslationKey(kvp.Key);
                        _allTranslations.Add(new TranslationKeyItem
                        {
                            Key = kvp.Key,
                            Value = kvp.Value,
                            Category = category,
                            LanguageCode = languageCode
                        });
                    }
                }

                FilterAndDisplayTranslations();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading translations: {ex.Message}");
            }
            
            UpdateEmptyState();
        }

        private string CategorizeTranslationKey(string key)
        {
            if (key.StartsWith("gui.") || key.StartsWith("menu.") || key.StartsWith("options.") || 
                key.StartsWith("controls.") || key.StartsWith("key."))
                return "GUI";
            
            if (key.StartsWith("item.") || key.StartsWith("enchantment.") || key.StartsWith("potion."))
                return "Items";
            
            if (key.StartsWith("block.") || key.StartsWith("tile."))
                return "Blocks";
            
            return "Other";
        }

        private void FilterAndDisplayTranslations()
        {
            var guiTranslations = _allTranslations.Where(t => t.Category == "GUI").ToList();
            var itemTranslations = _allTranslations.Where(t => t.Category == "Items").ToList();
            var blockTranslations = _allTranslations.Where(t => t.Category == "Blocks").ToList();
            var otherTranslations = _allTranslations.Where(t => t.Category == "Other").ToList();

            GuiTranslationsList.ItemsSource = guiTranslations;
            ItemsTranslationsList.ItemsSource = itemTranslations;
            BlocksTranslationsList.ItemsSource = blockTranslations;
            OtherTranslationsList.ItemsSource = otherTranslations;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterTranslationsWithSearch();
        }

        private void FilterTranslationsWithSearch()
        {
            var query = SearchBox?.Text?.ToLowerInvariant() ?? "";
            var filteredTranslations = string.IsNullOrEmpty(query) ? 
                _allTranslations : 
                _allTranslations.Where(t => 
                    t.Key.ToLowerInvariant().Contains(query) || 
                    t.Value.ToLowerInvariant().Contains(query)).ToList();

            var guiTranslations = filteredTranslations.Where(t => t.Category == "GUI").ToList();
            var itemTranslations = filteredTranslations.Where(t => t.Category == "Items").ToList();
            var blockTranslations = filteredTranslations.Where(t => t.Category == "Blocks").ToList();
            var otherTranslations = filteredTranslations.Where(t => t.Category == "Other").ToList();

            GuiTranslationsList.ItemsSource = guiTranslations;
            ItemsTranslationsList.ItemsSource = itemTranslations;
            BlocksTranslationsList.ItemsSource = blockTranslations;
            OtherTranslationsList.ItemsSource = otherTranslations;
        }

        private async void CreateKeyOverride_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPack == null || _selectedLanguageCode == null) return;
            if (sender is not Wpf.Ui.Controls.Button button || button.Tag is not TranslationKeyItem item) return;

            try
            {
                var overridesDir = Path.Combine(_currentPack.Location, "overrides", "translations");
                Directory.CreateDirectory(overridesDir);
                var overrideFile = Path.Combine(overridesDir, $"{_selectedLanguageCode}.json");

                Dictionary<string, string> overrides;
                if (File.Exists(overrideFile))
                {
                    var content = File.ReadAllText(overrideFile);
                    overrides = JsonSerializer.Deserialize<Dictionary<string, string>>(content) ?? new();
                }
                else
                {
                    overrides = new Dictionary<string, string>();
                }

                // Add or update the key
                overrides[item.Key] = item.Value; // Start with original value for editing

                // Save the override file
                var json = JsonSerializer.Serialize(overrides, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(overrideFile, json);

                // Refresh pack data
                var manager = new ResourcePackTemplateManager();
                _currentPack = manager.ReadResourcePack(_currentPack.Location);

                ShowMessage($"Created override for key '{item.Key}'", "Override Created");
            }
            catch (Exception ex)
            {
                ShowMessage($"Failed to create override: {ex.Message}", "Error");
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
                    LoadAvailableLanguages();
                    ShowMessage($"Successfully extracted language assets for Minecraft {minecraftVersion}!", "Extraction Complete");
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

        private void RefreshTranslations_Click(object sender, RoutedEventArgs e)
        {
            LoadAvailableLanguages();
            if (_selectedLanguageCode != null)
            {
                LoadTranslationsForLanguage(_selectedLanguageCode);
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
        public class LanguageItem
        {
            public string Code { get; set; }
            public string DisplayName { get; set; }
            public int KeyCount { get; set; }

            public LanguageItem(string code, string displayName, int keyCount)
            {
                Code = code;
                DisplayName = displayName;
                KeyCount = keyCount;
            }
        }

        public class TranslationKeyItem
        {
            public string Key { get; set; } = "";
            public string Value { get; set; } = "";
            public string Category { get; set; } = "";
            public string LanguageCode { get; set; } = "";
        }
    }
}