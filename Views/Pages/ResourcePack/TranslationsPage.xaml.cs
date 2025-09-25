using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Modrix.Services;
using Modrix.Views.Windows;
using Wpf.Ui.Controls;
using Wpf.Ui.Abstractions.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using UiButton = Wpf.Ui.Controls.Button;
using WpfButton = System.Windows.Controls.Button;

namespace Modrix.Views.Pages.ResourcePack
{
    public partial class TranslationsPage : System.Windows.Controls.Page, INavigableView<object>
    {
        public object ViewModel => this;
        
        private ResourcePackData? _currentPack;
        private List<LanguageInfo> _availableLanguages = new();
        private List<TranslationKeyItem> _allTranslationKeys = new();
        private List<TranslationKeyItem> _filteredKeys = new();
        
        public TranslationsPage()
        {
            InitializeComponent();
            DataContext = this;
            LoadCurrentPack();
            LoadAvailableLanguages();
            UpdateEmptyState();
        }

        public void OnLoaded(object sender, RoutedEventArgs e)
        {
            LoadCurrentPack();
            LoadAvailableLanguages();
            UpdateEmptyState();
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
            _availableLanguages.Clear();

            if (_currentPack == null) return;

            var langPath = Path.Combine(_currentPack.Location, "assets", "minecraft", "lang");
            
            if (Directory.Exists(langPath))
            {
                foreach (var langFile in Directory.GetFiles(langPath, "*.json"))
                {
                    var code = Path.GetFileNameWithoutExtension(langFile);
                    var language = new LanguageInfo
                    {
                        Code = code,
                        DisplayName = GetLanguageDisplayName(code),
                        FilePath = langFile
                    };

                    // Count keys in language file
                    try
                    {
                        var content = File.ReadAllText(langFile);
                        var doc = JsonDocument.Parse(content);
                        language.KeyCount = CountJsonKeys(doc.RootElement);
                    }
                    catch
                    {
                        language.KeyCount = 0;
                    }

                    _availableLanguages.Add(language);
                }
            }

            // Add common languages even if not present
            var commonLanguages = new Dictionary<string, string>
            {
                {"en_us", "English (US)"},
                {"en_gb", "English (UK)"},
                {"de_de", "German"},
                {"fr_fr", "French"},
                {"es_es", "Spanish"},
                {"it_it", "Italian"},
                {"ja_jp", "Japanese"},
                {"ko_kr", "Korean"},
                {"zh_cn", "Chinese (Simplified)"},
                {"pt_br", "Portuguese (Brazil)"},
                {"ru_ru", "Russian"}
            };

            foreach (var kvp in commonLanguages)
            {
                if (!_availableLanguages.Any(l => l.Code == kvp.Key))
                {
                    _availableLanguages.Add(new LanguageInfo
                    {
                        Code = kvp.Key,
                        DisplayName = kvp.Value,
                        FilePath = "",
                        KeyCount = 0
                    });
                }
            }

            // Sort by display name
            _availableLanguages = _availableLanguages.OrderBy(l => l.DisplayName).ToList();
            
            var languagesList = this.FindName("LanguagesList") as ListBox;
            if (languagesList != null)
            {
                languagesList.ItemsSource = _availableLanguages;
                
                // Select English (US) by default if available
                var defaultLang = _availableLanguages.FirstOrDefault(l => l.Code == "en_us") ?? 
                                 _availableLanguages.FirstOrDefault();
                if (defaultLang != null)
                {
                    languagesList.SelectedItem = defaultLang;
                }
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
                "pt_br" => "Portuguese (Brazil)",
                "ru_ru" => "Russian",
                "nl_nl" => "Dutch",
                "sv_se" => "Swedish",
                "no_no" => "Norwegian",
                "da_dk" => "Danish",
                "fi_fi" => "Finnish",
                "pl_pl" => "Polish",
                "cs_cz" => "Czech",
                "sk_sk" => "Slovak",
                "hu_hu" => "Hungarian",
                "ro_ro" => "Romanian",
                "bg_bg" => "Bulgarian",
                "hr_hr" => "Croatian",
                "sl_si" => "Slovenian",
                "et_ee" => "Estonian",
                "lv_lv" => "Latvian",
                "lt_lt" => "Lithuanian",
                "el_gr" => "Greek",
                "tr_tr" => "Turkish",
                "ar_sa" => "Arabic",
                "he_il" => "Hebrew",
                "hi_in" => "Hindi",
                "th_th" => "Thai",
                "vi_vn" => "Vietnamese",
                "id_id" => "Indonesian",
                "ms_my" => "Malay",
                "tl_ph" => "Filipino",
                _ => code.Replace("_", "-").ToUpperInvariant()
            };
        }

        private int CountJsonKeys(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                return element.EnumerateObject().Count();
            }
            return 0;
        }

        private void LoadTranslationKeys(LanguageInfo language)
        {
            _allTranslationKeys.Clear();

            if (language == null || string.IsNullOrEmpty(language.FilePath) || !File.Exists(language.FilePath))
            {
                FilterTranslationKeys();
                return;
            }

            try
            {
                var content = File.ReadAllText(language.FilePath);
                var doc = JsonDocument.Parse(content);

                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    _allTranslationKeys.Add(new TranslationKeyItem
                    {
                        Key = property.Name,
                        OriginalValue = property.Value.GetString() ?? "",
                        LanguageCode = language.Code
                    });
                }

                // Sort by key
                _allTranslationKeys = _allTranslationKeys.OrderBy(k => k.Key).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading translations: {ex.Message}");
            }

            FilterTranslationKeys();
        }

        private void FilterTranslationKeys()
        {
            var searchBox = this.FindName("SearchBox") as Wpf.Ui.Controls.TextBox;
            var searchQuery = searchBox?.Text?.ToLowerInvariant() ?? "";

            _filteredKeys = _allTranslationKeys.Where(key =>
                string.IsNullOrEmpty(searchQuery) ||
                key.Key.ToLowerInvariant().Contains(searchQuery) ||
                key.OriginalValue.ToLowerInvariant().Contains(searchQuery)
            ).ToList();

            var translationsGrid = this.FindName("TranslationsGrid") as System.Windows.Controls.ListView;
            if (translationsGrid != null)
            {
                translationsGrid.ItemsSource = _filteredKeys;
            }
            
            UpdateEmptyState();
        }

        private void UpdateEmptyState()
        {
            var hasKeys = _filteredKeys.Count > 0;
            
            var emptyState = this.FindName("EmptyState") as FrameworkElement;
            var translationsGrid = this.FindName("TranslationsGrid") as FrameworkElement;
            
            if (emptyState != null)
                emptyState.Visibility = hasKeys ? Visibility.Collapsed : Visibility.Visible;
                
            if (translationsGrid != null)
                translationsGrid.Visibility = hasKeys ? Visibility.Visible : Visibility.Collapsed;
        }

        public void LanguagesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var languagesList = sender as ListBox;
            if (languagesList?.SelectedItem is LanguageInfo language)
            {
                LoadTranslationKeys(language);
            }
        }

        public void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterTranslationKeys();
        }

        public void RefreshTranslations_Click(object sender, RoutedEventArgs e)
        {
            LoadAvailableLanguages();
        }

        public async void CreateOverride_Click(object sender, RoutedEventArgs e)
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
                    
                    // Create empty translation file with some examples
                    var exampleTranslations = new Dictionary<string, string>
                    {
                        {"example.custom_item", "My Custom Item"},
                        {"example.custom_block", "My Custom Block"},
                        {"example.greeting", "Welcome to my resource pack!"}
                    };
                    
                    var json = JsonSerializer.Serialize(exampleTranslations, new JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                    
                    File.WriteAllText(targetFile, json);
                    
                    await new MessageBox
                    {
                        Title = "Translation Created",
                        Content = $"Created translation file for {GetLanguageDisplayName(languageCode)}. You can now edit it to add your custom translations.",
                        PrimaryButtonText = "OK"
                    }.ShowDialogAsync();
                }
                catch (Exception ex)
                {
                    ShowMessage($"Failed to create translation: {ex.Message}", "Error");
                }
            }
        }

        public async void CreateKeyOverride_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not WpfButton button || button.Tag is not TranslationKeyItem keyItem)
                return;

            var dialog = new TranslationOverrideDialog(keyItem);
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var languageCode = dialog.SelectedLanguageCode;
                    var customValue = dialog.CustomValue;
                    
                    var targetDir = Path.Combine(_currentPack.Location, "overrides", "translations");
                    Directory.CreateDirectory(targetDir);
                    
                    var targetFile = Path.Combine(targetDir, $"{languageCode}.json");
                    
                    // Load existing overrides or create new
                    Dictionary<string, string> overrides;
                    if (File.Exists(targetFile))
                    {
                        var existingContent = File.ReadAllText(targetFile);
                        overrides = JsonSerializer.Deserialize<Dictionary<string, string>>(existingContent) ?? 
                                   new Dictionary<string, string>();
                    }
                    else
                    {
                        overrides = new Dictionary<string, string>();
                    }
                    
                    // Add/update the override
                    overrides[keyItem.Key] = customValue;
                    
                    // Save the file
                    var json = JsonSerializer.Serialize(overrides, new JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                    
                    File.WriteAllText(targetFile, json);
                    
                    ShowMessage($"Created translation override for '{keyItem.Key}'", "Success");
                }
                catch (Exception ex)
                {
                    ShowMessage($"Failed to create override: {ex.Message}", "Error");
                }
            }
        }

        public void ExportLanguage_Click(object sender, RoutedEventArgs e)
        {
            var languagesList = this.FindName("LanguagesList") as ListBox;
            if (languagesList?.SelectedItem is not LanguageInfo language || 
                string.IsNullOrEmpty(language.FilePath) || 
                !File.Exists(language.FilePath))
            {
                ShowMessage("Please select a language with available translations", "No Language Selected");
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Language File",
                Filter = "JSON Files|*.json",
                FileName = $"{language.Code}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.Copy(language.FilePath, dialog.FileName, true);
                    ShowMessage($"Language file exported successfully to {Path.GetFileName(dialog.FileName)}", "Export Successful");
                }
                catch (Exception ex)
                {
                    ShowMessage($"Failed to export language file: {ex.Message}", "Error");
                }
            }
        }

        public void TranslationsGrid_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var translationsGrid = sender as System.Windows.Controls.ListView;
            if (translationsGrid?.SelectedItem is TranslationKeyItem keyItem)
            {
                var contextMenu = new ContextMenu();

                var createOverrideItem = new System.Windows.Controls.MenuItem
                {
                    Header = "Create Override",
                    Icon = new SymbolIcon(Wpf.Ui.Controls.SymbolRegular.DocumentCopy24)
                };
                createOverrideItem.Click += (s, args) => CreateKeyOverride_Click(s, null);

                var copyKeyItem = new System.Windows.Controls.MenuItem
                {
                    Header = "Copy Key",
                    Icon = new SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Copy24)
                };
                copyKeyItem.Click += (s, args) => Clipboard.SetText(keyItem.Key);

                var copyValueItem = new System.Windows.Controls.MenuItem
                {
                    Header = "Copy Value", 
                    Icon = new SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Copy24)
                };
                copyValueItem.Click += (s, args) => Clipboard.SetText(keyItem.OriginalValue);

                contextMenu.Items.Add(createOverrideItem);
                contextMenu.Items.Add(new Separator());
                contextMenu.Items.Add(copyKeyItem);
                contextMenu.Items.Add(copyValueItem);

                contextMenu.IsOpen = true;
                e.Handled = true;
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
        public class LanguageInfo
        {
            public string Code { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public string FilePath { get; set; } = "";
            public int KeyCount { get; set; }
        }

        public class TranslationKeyItem
        {
            public string Key { get; set; } = "";
            public string OriginalValue { get; set; } = "";
            public string LanguageCode { get; set; } = "";
        }
    }

    // Dialog for creating translation overrides - move this outside the class
    public partial class TranslationOverrideDialog : FluentWindow
    {
        public string SelectedLanguageCode { get; private set; } = "en_us";
        public string CustomValue { get; private set; } = "";

        private readonly TranslationsPage.TranslationKeyItem _keyItem;

        public TranslationOverrideDialog(TranslationsPage.TranslationKeyItem keyItem)
        {
            _keyItem = keyItem;
            InitializeDialog();
        }

        private void InitializeDialog()
        {
            Title = "Create Translation Override";
            Width = 500;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var mainPanel = new StackPanel { Margin = new Thickness(20) };

            // Title
            mainPanel.Children.Add(new System.Windows.Controls.TextBlock 
            { 
                Text = $"Create override for: {_keyItem.Key}",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 16)
            });

            // Original value
            mainPanel.Children.Add(new System.Windows.Controls.TextBlock 
            { 
                Text = "Original value:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            });

            var originalValueBox = new System.Windows.Controls.TextBox
            {
                Text = _keyItem.OriginalValue,
                IsReadOnly = true,
                Background = SystemColors.ControlBrush,
                TextWrapping = TextWrapping.Wrap,
                Height = 60,
                Margin = new Thickness(0, 0, 0, 16),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            mainPanel.Children.Add(originalValueBox);

            // Language selection
            mainPanel.Children.Add(new System.Windows.Controls.TextBlock 
            { 
                Text = "Target language:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            });

            var languages = new Dictionary<string, string>
            {
                { "en_us", "English (US)" },
                { "en_gb", "English (UK)" },
                { "de_de", "German" },
                { "fr_fr", "French" },
                { "es_es", "Spanish" },
                { "it_it", "Italian" },
                { "ja_jp", "Japanese" },
                { "ko_kr", "Korean" },
                { "zh_cn", "Chinese (Simplified)" }
            };

            var languageComboBox = new ComboBox 
            { 
                ItemsSource = languages,
                DisplayMemberPath = "Value",
                SelectedValuePath = "Key",
                SelectedValue = _keyItem.LanguageCode,
                Margin = new Thickness(0, 0, 0, 16)
            };

            if (languageComboBox.SelectedItem == null)
                languageComboBox.SelectedIndex = 0;

            mainPanel.Children.Add(languageComboBox);

            // Custom value
            mainPanel.Children.Add(new System.Windows.Controls.TextBlock 
            { 
                Text = "Your custom translation:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            });

            var customValueBox = new System.Windows.Controls.TextBox
            {
                Text = _keyItem.OriginalValue, // Start with original as template
                TextWrapping = TextWrapping.Wrap,
                Height = 80,
                Margin = new Thickness(0, 0, 0, 20),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            mainPanel.Children.Add(customValueBox);

            // Buttons
            var buttonPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var createButton = new UiButton 
            { 
                Content = "Create Override", 
                Appearance = Wpf.Ui.Controls.ControlAppearance.Primary,
                Margin = new Thickness(0, 0, 8, 0)
            };
            createButton.Click += (s, e) =>
            {
                SelectedLanguageCode = languageComboBox.SelectedValue?.ToString() ?? "en_us";
                CustomValue = customValueBox.Text;
                DialogResult = true;
            };

            var cancelButton = new UiButton 
            { 
                Content = "Cancel",
                Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary
            };
            cancelButton.Click += (s, e) => DialogResult = false;

            buttonPanel.Children.Add(createButton);
            buttonPanel.Children.Add(cancelButton);

            mainPanel.Children.Add(buttonPanel);

            Content = mainPanel;
        }
    }

    // Dialog for creating new translation files  
    public partial class CreateTranslationDialog : FluentWindow
    {
        public string LanguageCode { get; private set; } = "en_us";

        public CreateTranslationDialog()
        {
            InitializeDialog();
        }

        private void InitializeDialog()
        {
            Title = "Create Translation File";
            Width = 400;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var stackPanel = new StackPanel { Margin = new Thickness(20) };
            
            stackPanel.Children.Add(new System.Windows.Controls.TextBlock 
            { 
                Text = "Select the language for this translation:",
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 16)
            });

            var languages = new Dictionary<string, string>
            {
                { "en_us", "English (US)" },
                { "en_gb", "English (UK)" },
                { "de_de", "German" },
                { "fr_fr", "French" },
                { "es_es", "Spanish" },
                { "it_it", "Italian" },
                { "ja_jp", "Japanese" },
                { "ko_kr", "Korean" },
                { "zh_cn", "Chinese (Simplified)" }
            };

            var comboBox = new ComboBox 
            { 
                ItemsSource = languages,
                DisplayMemberPath = "Value",
                SelectedValuePath = "Key",
                SelectedIndex = 0,
                Margin = new Thickness(0, 0, 0, 20)
            };

            var buttonPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var createButton = new UiButton 
            { 
                Content = "Create", 
                Appearance = Wpf.Ui.Controls.ControlAppearance.Primary,
                Margin = new Thickness(0, 0, 8, 0)
            };
            createButton.Click += (s, e) =>
            {
                LanguageCode = comboBox.SelectedValue?.ToString() ?? "en_us";
                DialogResult = true;
            };

            var cancelButton = new UiButton 
            { 
                Content = "Cancel",
                Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary
            };
            cancelButton.Click += (s, e) => DialogResult = false;

            buttonPanel.Children.Add(createButton);
            buttonPanel.Children.Add(cancelButton);

            stackPanel.Children.Add(comboBox);
            stackPanel.Children.Add(buttonPanel);

            Content = stackPanel;
        }
    }}