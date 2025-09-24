using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Modrix.Services;
using Modrix.ViewModels.Windows;
using Modrix.Views.Windows;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace Modrix.Views.Pages
{
    public partial class TranslationsPage : Page
    {
        private ResourcePackWorkspaceViewModel _workspaceViewModel;
        private ObservableCollection<TranslationItem> _allTranslations;
        private ObservableCollection<TranslationItem> _filteredTranslations;
        private ObservableCollection<string> _availableLanguages;
        private ResourcePackTemplateManager _resourcePackManager;
        private string _currentLanguage = "en_us";
        private string _currentSearchText = "";
        private string _currentCategory = "All";
        private TranslationItem _selectedTranslation;
        private bool _hasUnsavedChanges;

        public bool HasChanges
        {
            get => _hasUnsavedChanges;
            set
            {
                _hasUnsavedChanges = value;
                // Update binding notification if needed
            }
        }

        public TranslationsPage()
        {
            InitializeComponent();
            InitializeCollections();
            LoadData();
        }

        private void InitializeCollections()
        {
            _allTranslations = new ObservableCollection<TranslationItem>();
            _filteredTranslations = new ObservableCollection<TranslationItem>();
            _availableLanguages = new ObservableCollection<string>
            {
                "en_us", "en_gb", "de_de", "fr_fr", "es_es", "it_it", 
                "ja_jp", "ko_kr", "pt_br", "ru_ru", "zh_cn", "zh_tw",
                "nl_nl", "sv_se", "da_dk", "no_no", "fi_fi", "pl_pl"
            };
            
            _resourcePackManager = new ResourcePackTemplateManager();
            
            TranslationsList.ItemsSource = _filteredTranslations;
            LanguageSelector.ItemsSource = _availableLanguages;
            LanguageSelector.SelectedItem = _currentLanguage;
        }

        private async void LoadData()
        {
            try
            {
                // Get workspace view model from parent window
                if (Application.Current.Windows.OfType<ResourcePackWorkspace>().FirstOrDefault()?.ViewModel is ResourcePackWorkspaceViewModel workspaceVm)
                {
                    _workspaceViewModel = workspaceVm;
                    await LoadTranslations();
                }
            }
            catch (Exception ex)
            {
                ShowMessage("Error", $"Failed to load translations: {ex.Message}");
            }
        }

        private async Task LoadTranslations()
        {
            if (_workspaceViewModel?.CurrentProject == null)
                return;

            ShowLoadingState(true);
            StatusText.Text = "Loading translations...";

            try
            {
                _allTranslations.Clear();

                // Load default Minecraft translations
                var defaultTranslations = await _resourcePackManager.GetAvailableTranslations(
                    _workspaceViewModel.CurrentProject.MinecraftVersion, _currentLanguage);

                // Load existing custom translations
                var customTranslations = LoadCustomTranslations(_currentLanguage);

                // Combine default and custom translations
                foreach (var kvp in defaultTranslations)
                {
                    var translationItem = new TranslationItem
                    {
                        Key = kvp.Key,
                        OriginalValue = kvp.Value,
                        CustomValue = customTranslations.ContainsKey(kvp.Key) ? customTranslations[kvp.Key] : "",
                        HasOverride = customTranslations.ContainsKey(kvp.Key),
                        Category = DetermineTranslationCategory(kvp.Key),
                        Language = _currentLanguage
                    };
                    
                    translationItem.PreviewValue = translationItem.HasOverride ? translationItem.CustomValue : translationItem.OriginalValue;
                    _allTranslations.Add(translationItem);
                }

                ApplyFilters();
                UpdateStatusText();
                
                if (_allTranslations.Count == 0)
                {
                    ShowEmptyState();
                }
            }
            catch (Exception ex)
            {
                ShowMessage("Error", $"Failed to load translations: {ex.Message}");
                ShowEmptyState();
            }
            finally
            {
                ShowLoadingState(false);
            }
        }

        private Dictionary<string, string> LoadCustomTranslations(string language)
        {
            var translations = new Dictionary<string, string>();
            
            if (_workspaceViewModel?.CurrentProject?.Location == null)
                return translations;

            var langFile = Path.Combine(_workspaceViewModel.CurrentProject.Location, 
                "assets", "minecraft", "lang", $"{language}.json");
            
            if (!File.Exists(langFile))
                return translations;

            try
            {
                var content = File.ReadAllText(langFile);
                var jsonData = JsonSerializer.Deserialize<Dictionary<string, object>>(content);
                
                if (jsonData != null)
                {
                    foreach (var kvp in jsonData)
                    {
                        if (!kvp.Key.StartsWith("_comment"))
                        {
                            translations[kvp.Key] = kvp.Value?.ToString() ?? "";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Ignore malformed files
            }

            return translations;
        }

        private string DetermineTranslationCategory(string key)
        {
            if (key.StartsWith("block."))
                return "Block";
            else if (key.StartsWith("item."))
                return "Item";
            else if (key.StartsWith("entity."))
                return "Entity";
            else if (key.StartsWith("gui.") || key.StartsWith("menu."))
                return "GUI";
            else if (key.StartsWith("options.") || key.StartsWith("controls."))
                return "Menu";
            else
                return "Other";
        }

        private void ApplyFilters()
        {
            _filteredTranslations.Clear();

            var filtered = _allTranslations.AsEnumerable();

            // Apply category filter
            if (_currentCategory != "All")
            {
                filtered = filtered.Where(t => t.Category == _currentCategory);
            }

            // Apply search filter
            if (!string.IsNullOrEmpty(_currentSearchText))
            {
                filtered = filtered.Where(t => 
                    t.Key.Contains(_currentSearchText, StringComparison.OrdinalIgnoreCase) ||
                    t.OriginalValue.Contains(_currentSearchText, StringComparison.OrdinalIgnoreCase) ||
                    t.CustomValue.Contains(_currentSearchText, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var translation in filtered.OrderBy(t => t.Key))
            {
                _filteredTranslations.Add(translation);
            }

            UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            var totalCount = _allTranslations.Count;
            var filteredCount = _filteredTranslations.Count;
            var overrideCount = _allTranslations.Count(t => t.HasOverride);

            StatusText.Text = filteredCount == totalCount 
                ? $"Showing all {totalCount} translations"
                : $"Showing {filteredCount} of {totalCount} translations";
                
            TotalCount.Text = $"{filteredCount} entries";
            OverrideCount.Text = $"{overrideCount} overrides";
        }

        private void ShowLoadingState(bool isLoading)
        {
            LoadingState.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Collapsed;
        }

        private void ShowEmptyState()
        {
            EmptyState.Visibility = Visibility.Visible;
            LoadingState.Visibility = Visibility.Collapsed;
        }

        // Event Handlers
        private void AddTranslation_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Show dialog to add custom translation key
            ShowMessage("Add Translation", "Custom translation key addition coming soon");
        }

        private void ImportLanguageFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Import Language File",
                Filter = "JSON Files|*.json|All Files|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                ImportLanguageFileFromPath(openFileDialog.FileName);
            }
        }

        private void ExportLanguage_Click(object sender, RoutedEventArgs e)
        {
            if (_workspaceViewModel?.CurrentProject?.Location == null)
                return;

            var saveFileDialog = new SaveFileDialog
            {
                Title = "Export Language File",
                Filter = "JSON Files|*.json",
                FileName = $"{_currentLanguage}.json"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                ExportLanguageToFile(saveFileDialog.FileName);
            }
        }

        private async void LoadDefault_Click(object sender, RoutedEventArgs e)
        {
            await LoadTranslations();
        }

        private async void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is string selectedLanguage)
            {
                if (_hasUnsavedChanges)
                {
                    var result = MessageBox.Show(
                        "You have unsaved changes. Do you want to save them before switching languages?",
                        "Unsaved Changes",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        SaveCurrentTranslation();
                    }
                    else if (result == MessageBoxResult.Cancel)
                    {
                        comboBox.SelectedItem = _currentLanguage; // Revert selection
                        return;
                    }
                }

                _currentLanguage = selectedLanguage;
                LanguageLabel.Text = $"({_currentLanguage})";
                await LoadTranslations();
            }
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

        private void TranslationsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is TranslationItem selectedItem)
            {
                if (_hasUnsavedChanges && _selectedTranslation != null)
                {
                    // Save previous changes
                    SaveCurrentTranslation();
                }

                _selectedTranslation = selectedItem;
                LoadTranslationIntoEditor(selectedItem);
                HasChanges = false;
            }
        }

        private void CustomTranslationTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedTranslation != null)
            {
                var newValue = CustomTranslationTextBox.Text ?? "";
                HasChanges = newValue != _selectedTranslation.CustomValue;
                
                // Update character count
                CharacterCount.Text = $"{newValue.Length} characters";
                
                // Basic validation
                ValidateTranslation(newValue);
            }
        }

        private void ResetTranslation_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTranslation != null)
            {
                CustomTranslationTextBox.Text = "";
                HasChanges = !string.IsNullOrEmpty(_selectedTranslation.CustomValue);
            }
        }

        private void SaveTranslation_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentTranslation();
        }

        // Helper Methods
        private void LoadTranslationIntoEditor(TranslationItem item)
        {
            KeyTextBox.Text = item.Key;
            CategoryTextBlock.Text = item.Category;
            OriginalTextBox.Text = item.OriginalValue;
            CustomTranslationTextBox.Text = item.CustomValue;
            
            CharacterCount.Text = $"{item.CustomValue.Length} characters";
            ValidateTranslation(item.CustomValue);
        }

        private void ValidateTranslation(string value)
        {
            ValidationMessage.Visibility = Visibility.Collapsed;
            
            // Basic validation rules
            if (value.Length > 1000)
            {
                ValidationMessage.Text = "Translation is very long";
                ValidationMessage.Visibility = Visibility.Visible;
            }
            else if (value.Contains("§") && !value.Contains("§r"))
            {
                ValidationMessage.Text = "Color codes should be properly closed";
                ValidationMessage.Visibility = Visibility.Visible;
            }
        }

        private void SaveCurrentTranslation()
        {
            if (_selectedTranslation == null || _workspaceViewModel?.CurrentProject?.Location == null)
                return;

            try
            {
                var newValue = CustomTranslationTextBox.Text ?? "";
                _selectedTranslation.CustomValue = newValue;
                _selectedTranslation.HasOverride = !string.IsNullOrEmpty(newValue);
                _selectedTranslation.PreviewValue = _selectedTranslation.HasOverride ? newValue : _selectedTranslation.OriginalValue;

                // Save to file
                SaveTranslationToFile(_selectedTranslation.Key, newValue, _selectedTranslation.HasOverride);
                
                HasChanges = false;
                
                // Refresh the display
                ApplyFilters();
                UpdateStatusText();
            }
            catch (Exception ex)
            {
                ShowMessage("Save Error", $"Failed to save translation: {ex.Message}");
            }
        }

        private void SaveTranslationToFile(string key, string value, bool hasOverride)
        {
            if (_workspaceViewModel?.CurrentProject?.Location == null)
                return;

            var langDir = Path.Combine(_workspaceViewModel.CurrentProject.Location, "assets", "minecraft", "lang");
            Directory.CreateDirectory(langDir);
            
            var langFile = Path.Combine(langDir, $"{_currentLanguage}.json");
            
            // Load existing translations
            var translations = new Dictionary<string, object>();
            
            if (File.Exists(langFile))
            {
                try
                {
                    var content = File.ReadAllText(langFile);
                    translations = JsonSerializer.Deserialize<Dictionary<string, object>>(content) ?? new Dictionary<string, object>();
                }
                catch
                {
                    // Start fresh if file is corrupted
                    translations = new Dictionary<string, object>();
                }
            }

            // Update or remove the translation
            if (hasOverride && !string.IsNullOrEmpty(value))
            {
                translations[key] = value;
            }
            else if (translations.ContainsKey(key))
            {
                translations.Remove(key);
            }

            // Save the file
            var options = new JsonSerializerOptions { WriteIndented = true };
            var updatedContent = JsonSerializer.Serialize(translations, options);
            File.WriteAllText(langFile, updatedContent);
        }

        private void ImportLanguageFileFromPath(string filePath)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                var importedTranslations = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

                if (importedTranslations != null)
                {
                    var targetDir = Path.Combine(_workspaceViewModel.CurrentProject.Location, "assets", "minecraft", "lang");
                    Directory.CreateDirectory(targetDir);
                    
                    var targetFile = Path.Combine(targetDir, $"{_currentLanguage}.json");
                    File.Copy(filePath, targetFile, true);
                    
                    ShowMessage("Import Success", $"Imported {importedTranslations.Count} translations");
                    _ = LoadTranslations(); // Refresh
                }
            }
            catch (Exception ex)
            {
                ShowMessage("Import Error", $"Failed to import language file: {ex.Message}");
            }
        }

        private void ExportLanguageToFile(string filePath)
        {
            try
            {
                var customTranslations = _allTranslations
                    .Where(t => t.HasOverride)
                    .ToDictionary(t => t.Key, t => (object)t.CustomValue);

                var options = new JsonSerializerOptions { WriteIndented = true };
                var content = JsonSerializer.Serialize(customTranslations, options);
                File.WriteAllText(filePath, content);
                
                ShowMessage("Export Success", $"Exported {customTranslations.Count} custom translations");
            }
            catch (Exception ex)
            {
                ShowMessage("Export Error", $"Failed to export language file: {ex.Message}");
            }
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

    // Data model for translation entries
    public class TranslationItem
    {
        public string Key { get; set; } = "";
        public string OriginalValue { get; set; } = "";
        public string CustomValue { get; set; } = "";
        public string PreviewValue { get; set; } = "";
        public bool HasOverride { get; set; }
        public string Category { get; set; } = "";
        public string Language { get; set; } = "";
    }
}