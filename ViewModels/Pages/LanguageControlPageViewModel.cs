using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace Modrix.ViewModels.Pages
{
    public partial class LanguageControlPageViewModel : ObservableObject
    {
        private readonly string _langDirectoryPath;
        private readonly string _modId;

        [ObservableProperty]
        private ObservableCollection<LanguageFile> _languageFiles = new();

        [ObservableProperty]
        private LanguageFile _selectedLanguageFile;

        [ObservableProperty]
        private ObservableCollection<TranslationEntry> _translations = new();

        [ObservableProperty]
        private ObservableCollection<TranslationEntry> _filteredTranslations = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _newLanguageCode = string.Empty;

        [ObservableProperty]
        private TranslationEntry _selectedTranslation;

        [ObservableProperty]
        private string _editingKey;

        [ObservableProperty]
        private string _editingValue;

        [ObservableProperty]
        private bool _showMissingOnly = false;

        [ObservableProperty]
        private int _missingTranslationsCount = 0;

        [ObservableProperty]
        private bool _isEditing = false;

        [ObservableProperty]
        private bool _isAddingNew = false;

        public LanguageControlPageViewModel(string projectPath, string modId)
        {
            _modId = modId;
            _langDirectoryPath = Path.Combine(projectPath, "src", "main", "resources", "assets", modId, "lang");
            
            // Ensure lang directory exists
            if (!Directory.Exists(_langDirectoryPath))
            {
                Directory.CreateDirectory(_langDirectoryPath);
            }

            LoadLanguageFiles();
        }

        partial void OnShowMissingOnlyChanged(bool value)
        {
            ApplyFilter();
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
        }

        partial void OnSelectedLanguageFileChanged(LanguageFile value)
        {
            if (value != null)
            {
                LoadTranslations(value);
            }
        }

        private void LoadLanguageFiles()
        {
            LanguageFiles.Clear();

            if (!Directory.Exists(_langDirectoryPath))
            {
                return;
            }

            foreach (var filePath in Directory.GetFiles(_langDirectoryPath, "*.json"))
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var languageFile = new LanguageFile
                {
                    Code = fileName,
                    FilePath = filePath,
                    DisplayName = GetLanguageDisplayName(fileName),
                    Entries = LoadLanguageEntries(filePath)
                };

                LanguageFiles.Add(languageFile);
            }

            // If en_us.json doesn't exist, create it as the default
            if (!LanguageFiles.Any(lf => lf.Code == "en_us"))
            {
                var defaultPath = Path.Combine(_langDirectoryPath, "en_us.json");
                var defaultFile = new LanguageFile
                {
                    Code = "en_us",
                    FilePath = defaultPath,
                    DisplayName = "English (US)",
                    Entries = new Dictionary<string, string>()
                };

                // Save empty language file
                SaveLanguageFile(defaultFile);
                LanguageFiles.Add(defaultFile);
            }

            // Select en_us or the first language file
            SelectedLanguageFile = LanguageFiles.FirstOrDefault(lf => lf.Code == "en_us") ?? LanguageFiles.FirstOrDefault();

            if (SelectedLanguageFile != null)
            {
                LoadTranslations(SelectedLanguageFile);
            }
        }

        private Dictionary<string, string> LoadLanguageEntries(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(json, 
                        new JsonSerializerOptions { AllowTrailingCommas = true });
                    
                    return entries ?? new Dictionary<string, string>();
                }
            }
            catch (Exception ex)
            {
                // Handle error
                Console.WriteLine($"Error loading language file: {ex.Message}");
            }
            
            return new Dictionary<string, string>();
        }

        private string GetLanguageDisplayName(string languageCode)
        {
            return languageCode switch
            {
                "en_us" => "English (US)",
                "en_gb" => "English (UK)",
                "de_de" => "German",
                "es_es" => "Spanish (Spain)",
                "es_mx" => "Spanish (Mexico)",
                "fr_fr" => "French",
                "it_it" => "Italian",
                "ja_jp" => "Japanese",
                "ko_kr" => "Korean",
                "pt_br" => "Portuguese (Brazil)",
                "ru_ru" => "Russian",
                "zh_cn" => "Chinese (Simplified)",
                "zh_tw" => "Chinese (Traditional)",
                _ => languageCode
            };
        }

        public void LoadTranslations(LanguageFile languageFile)
        {
            if (languageFile == null)
            {
                return;
            }

            SelectedLanguageFile = languageFile;
            Translations.Clear();

            var baseLanguage = LanguageFiles.FirstOrDefault(lf => lf.Code == "en_us");
            var allKeys = new HashSet<string>();

            // Collect all keys from the base (en_us) language
            if (baseLanguage != null)
            {
                foreach (var key in baseLanguage.Entries.Keys)
                {
                    allKeys.Add(key);
                }
            }

            // Also add keys from the current language file
            foreach (var key in languageFile.Entries.Keys)
            {
                allKeys.Add(key);
            }

            // Create translation entries for all keys
            foreach (var key in allKeys)
            {
                var baseValue = baseLanguage?.Entries.ContainsKey(key) == true 
                    ? baseLanguage.Entries[key] 
                    : string.Empty;

                var localValue = languageFile.Entries.ContainsKey(key) 
                    ? languageFile.Entries[key] 
                    : string.Empty;

                var entry = new TranslationEntry
                {
                    Key = key,
                    BaseValue = baseValue,
                    LocalValue = localValue,
                    IsMissing = string.IsNullOrEmpty(localValue) && !string.IsNullOrEmpty(baseValue)
                };

                Translations.Add(entry);
            }

            MissingTranslationsCount = Translations.Count(t => t.IsMissing);
            ApplyFilter();
        }

        [RelayCommand]
        private void ApplyFilter()
        {
            IEnumerable<TranslationEntry> filtered = Translations;

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchLower = SearchText.ToLowerInvariant();
                filtered = filtered.Where(t => 
                    t.Key.ToLowerInvariant().Contains(searchLower) || 
                    t.BaseValue.ToLowerInvariant().Contains(searchLower) || 
                    t.LocalValue.ToLowerInvariant().Contains(searchLower));
            }

            // Apply missing only filter
            if (ShowMissingOnly)
            {
                filtered = filtered.Where(t => t.IsMissing);
            }

            // Update filtered collection
            FilteredTranslations.Clear();
            foreach (var item in filtered)
            {
                FilteredTranslations.Add(item);
            }
        }

        [RelayCommand]
        private void SaveChanges()
        {
            if (SelectedLanguageFile == null)
            {
                return;
            }

            // Update entries with current translations
            foreach (var translation in Translations)
            {
                if (!string.IsNullOrEmpty(translation.LocalValue))
                {
                    SelectedLanguageFile.Entries[translation.Key] = translation.LocalValue;
                }
                else if (SelectedLanguageFile.Entries.ContainsKey(translation.Key))
                {
                    SelectedLanguageFile.Entries.Remove(translation.Key);
                }
            }

            // Save the language file
            SaveLanguageFile(SelectedLanguageFile);

            // Refresh
            LoadTranslations(SelectedLanguageFile);
        }

        private async void SaveLanguageFile(LanguageFile languageFile)
        {
            try
            {
                var json = JsonSerializer.Serialize(languageFile.Entries, 
                    new JsonSerializerOptions { WriteIndented = true });
                
                Directory.CreateDirectory(Path.GetDirectoryName(languageFile.FilePath));
                File.WriteAllText(languageFile.FilePath, json);
            }
            catch (Exception ex)
            {
                // Handle error
                Console.WriteLine($"Error saving language file: {ex.Message}");
                
                var msgBox = new MessageBox
                {
                    Title = "Error",
                    Content = $"Error saving language file: {ex.Message}",
                    PrimaryButtonText = "OK"
                };
                await msgBox.ShowDialogAsync();
            }
        }

        [RelayCommand]
        private void StartEditing(TranslationEntry translation)
        {
            if (translation == null)
            {
                return;
            }

            SelectedTranslation = translation;
            EditingKey = translation.Key;
            EditingValue = translation.LocalValue;
            IsEditing = true;
        }

        [RelayCommand]
        private void CancelEditing()
        {
            IsEditing = false;
            IsAddingNew = false;
            SelectedTranslation = null;
            EditingKey = string.Empty;
            EditingValue = string.Empty;
        }

        [RelayCommand]
        private void SaveEditing()
        {
            if (SelectedTranslation != null && IsEditing)
            {
                SelectedTranslation.LocalValue = EditingValue;
                SelectedTranslation.IsMissing = string.IsNullOrEmpty(EditingValue) && !string.IsNullOrEmpty(SelectedTranslation.BaseValue);
                
                // Update the language file entries
                if (!string.IsNullOrEmpty(EditingValue))
                {
                    SelectedLanguageFile.Entries[EditingKey] = EditingValue;
                }
                else if (SelectedLanguageFile.Entries.ContainsKey(EditingKey))
                {
                    SelectedLanguageFile.Entries.Remove(EditingKey);
                }

                SaveLanguageFile(SelectedLanguageFile);
                ApplyFilter();
            }
            else if (IsAddingNew && !string.IsNullOrEmpty(EditingKey))
            {
                // Add new translation
                if (SelectedLanguageFile.Entries.ContainsKey(EditingKey))
                {
                    // Update existing
                    SelectedLanguageFile.Entries[EditingKey] = EditingValue;
                }
                else
                {
                    // Add new
                    SelectedLanguageFile.Entries.Add(EditingKey, EditingValue);
                }

                SaveLanguageFile(SelectedLanguageFile);
                LoadTranslations(SelectedLanguageFile);
            }

            IsEditing = false;
            IsAddingNew = false;
            SelectedTranslation = null;
            EditingKey = string.Empty;
            EditingValue = string.Empty;
        }

        [RelayCommand]
        private void StartAddNew()
        {
            IsAddingNew = true;
            IsEditing = false;
            SelectedTranslation = null;
            EditingKey = string.Empty;
            EditingValue = string.Empty;
        }

        [RelayCommand]
        private async void CreateNewLanguage()
        {
            if (string.IsNullOrWhiteSpace(NewLanguageCode) || 
                LanguageFiles.Any(lf => lf.Code.Equals(NewLanguageCode, StringComparison.OrdinalIgnoreCase)))
            {
                var msgBox = new MessageBox
                {
                    Title = "Invalid Language Code",
                    Content = "Please enter a valid language code that doesn't already exist",
                    PrimaryButtonText = "OK"
                };
                await msgBox.ShowDialogAsync();
                return;
            }

            var newFilePath = Path.Combine(_langDirectoryPath, $"{NewLanguageCode}.json");
            var newFile = new LanguageFile
            {
                Code = NewLanguageCode,
                FilePath = newFilePath,
                DisplayName = GetLanguageDisplayName(NewLanguageCode),
                Entries = new Dictionary<string, string>()
            };

            // Copy entries from base language if it exists
            var baseLanguage = LanguageFiles.FirstOrDefault(lf => lf.Code == "en_us");
            if (baseLanguage != null)
            {
                // We might want to copy or leave blank for translation
                // For now, we'll leave them blank
            }

            SaveLanguageFile(newFile);
            LanguageFiles.Add(newFile);
            SelectedLanguageFile = newFile;
            LoadTranslations(newFile);
            NewLanguageCode = string.Empty;
        }

        [RelayCommand]
        private async void DeleteLanguage()
        {
            if (SelectedLanguageFile == null || SelectedLanguageFile.Code == "en_us")
            {
                var msgBox = new MessageBox
                {
                    Title = "Cannot Delete",
                    Content = "Cannot delete the base language (en_us)",
                    PrimaryButtonText = "OK"
                };
                await msgBox.ShowDialogAsync();
                return;
            }

            var confirmBox = new MessageBox
            {
                Title = "Confirm Deletion",
                Content = $"Are you sure you want to delete the {SelectedLanguageFile.DisplayName} language file?",
                PrimaryButtonText = "Yes",
                CloseButtonText = "No"
            };
            
            var result = await confirmBox.ShowDialogAsync();
            if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
            {
                try
                {
                    if (File.Exists(SelectedLanguageFile.FilePath))
                    {
                        File.Delete(SelectedLanguageFile.FilePath);
                    }

                    LanguageFiles.Remove(SelectedLanguageFile);
                    SelectedLanguageFile = LanguageFiles.FirstOrDefault(lf => lf.Code == "en_us") ?? LanguageFiles.FirstOrDefault();
                    
                    if (SelectedLanguageFile != null)
                    {
                        LoadTranslations(SelectedLanguageFile);
                    }
                    else
                    {
                        Translations.Clear();
                        FilteredTranslations.Clear();
                    }
                }
                catch (Exception ex)
                {
                    var errorBox = new MessageBox
                    {
                        Title = "Error",
                        Content = $"Error deleting language file: {ex.Message}",
                        PrimaryButtonText = "OK"
                    };
                    await errorBox.ShowDialogAsync();
                }
            }
        }

        [RelayCommand]
        private void OpenLanguageFolder()
        {
            if (Directory.Exists(_langDirectoryPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", _langDirectoryPath);
            }
            else
            {
                Directory.CreateDirectory(_langDirectoryPath);
                System.Diagnostics.Process.Start("explorer.exe", _langDirectoryPath);
            }
        }

        [RelayCommand]
        private void ImportFromBase()
        {
            if (SelectedLanguageFile == null || SelectedLanguageFile.Code == "en_us")
            {
                return;
            }

            var baseLanguage = LanguageFiles.FirstOrDefault(lf => lf.Code == "en_us");
            if (baseLanguage == null)
            {
                return;
            }

            // Add missing keys from base language
            bool changes = false;
            foreach (var key in baseLanguage.Entries.Keys)
            {
                if (!SelectedLanguageFile.Entries.ContainsKey(key))
                {
                    // Add empty translation (or optionally copy the English value)
                    SelectedLanguageFile.Entries.Add(key, string.Empty);
                    changes = true;
                }
            }

            if (changes)
            {
                SaveLanguageFile(SelectedLanguageFile);
                LoadTranslations(SelectedLanguageFile);
            }
        }
    }

    public class LanguageFile
    {
        public string Code { get; set; }
        public string DisplayName { get; set; }
        public string FilePath { get; set; }
        public Dictionary<string, string> Entries { get; set; } = new();
    }

    public class TranslationEntry : ObservableObject
    {
        private string _localValue;

        public string Key { get; set; }
        public string BaseValue { get; set; }
        
        public string LocalValue
        {
            get => _localValue;
            set
            {
                SetProperty(ref _localValue, value);
                OnPropertyChanged(nameof(IsMissing));
            }
        }
        
        public bool IsMissing { get; set; }
    }
}