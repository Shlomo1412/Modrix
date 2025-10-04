using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Modrix.Views.Windows;
using Modrix.ViewModels.Pages;
using Modrix.Services;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MenuItem = Wpf.Ui.Controls.MenuItem;
using Button = Wpf.Ui.Controls.Button;
using SystemMenuItem = System.Windows.Controls.MenuItem;
using SystemTextBlock = System.Windows.Controls.TextBlock;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace Modrix.Views.Pages
{
    public partial class ResourcesPage : Page
    {
        private string _projectPath;
        private string _modId;
        private string _readmePath;
        private MediaPlayer _mediaPlayer = new();
        private ModelValidationService _validationService = new();
        private ModelValidationService.ValidationResult _lastValidationResult;
        private ObservableCollection<ModelFileViewModel> _allModels = new();
        private ObservableCollection<ModelFileViewModel> _filteredModels = new();

        public ResourcesPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public void Refresh()
        {
            if (string.IsNullOrEmpty(_projectPath) || string.IsNullOrEmpty(_modId))
            {
                var workspace = Application.Current.Windows.OfType<ProjectWorkspace>().FirstOrDefault();
                if (workspace?.ViewModel?.CurrentProject != null)
                {
                    _projectPath = workspace.ViewModel.CurrentProject.Location;
                    _modId = workspace.ViewModel.CurrentProject.ModId;
                    _readmePath = Path.Combine(_projectPath, "README.md");
                }
            }

            LoadResources();
        }

        private void UpdateEmptyStates()
        {
            // Textures
            if (TexturesEmptyState != null && TexturesList != null)
            {
                TexturesEmptyState.Visibility = TexturesList.Items.Count == 0 ?
                    Visibility.Visible : Visibility.Collapsed;
            }

            // Models
            if (ModelsEmptyState != null && ModelsList != null)
            {
                ModelsEmptyState.Visibility = ModelsList.Items.Count == 0 ?
                    Visibility.Visible : Visibility.Collapsed;
            }

            // Sounds
            if (SoundsEmptyState != null && SoundsList != null)
            {
                SoundsEmptyState.Visibility = SoundsList.Items.Count == 0 ?
                    Visibility.Visible : Visibility.Collapsed;
            }

            // README
            if (ReadmeEmptyState != null && ReadmeEditor != null)
            {
                ReadmeEmptyState.Visibility = string.IsNullOrWhiteSpace(ReadmeEditor.Text) ?
                    Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var workspace = Application.Current.Windows
                .OfType<ProjectWorkspace>()
                .FirstOrDefault();

            if (workspace?.ViewModel?.CurrentProject != null)
            {
                _projectPath = workspace.ViewModel.CurrentProject.Location;
                _modId = workspace.ViewModel.CurrentProject.ModId;
                _readmePath = Path.Combine(_projectPath, "README.md");

                LoadResources();
            }
        }

        private void LoadResources()
        {
            if (string.IsNullOrEmpty(_projectPath) || string.IsNullOrEmpty(_modId))
                return;

            LoadTextures(Path.Combine(_projectPath,
                                     "src", "main", "resources", "assets", _modId, "textures"));
            LoadModels(Path.Combine(_projectPath,
                                     "src", "main", "resources", "assets", _modId, "models"));
            LoadSounds(Path.Combine(_projectPath,
                                     "src", "main", "resources", "assets", _modId, "sounds"));
            LoadIcon(Path.Combine(_projectPath,
                                     "src", "main", "resources", "assets", _modId, "icon.png"));

            LoadReadme();

            UpdateEmptyStates();
        }

        // Texture tab buttons
        private void AddTexture_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new NewTextureDialog
                {
                    Owner = Window.GetWindow(this)
                };

                if (dialog.ShowDialog() == true)
                {
                    // Determine the texture directory path
                    var texturesDir = Path.Combine(_projectPath, "src", "main", "resources", "assets", _modId, "textures");
                    
                    // Save the new texture
                    if (dialog.SaveTexture(texturesDir))
                    {
                        // Reload textures to show the new one
                        LoadTextures(texturesDir);
                        
                        // Optionally, open the new texture in the editor
                        if (!string.IsNullOrEmpty(dialog.SavedFilePath) && File.Exists(dialog.SavedFilePath))
                        {
                            OpenTextureInEditor(dialog.SavedFilePath, dialog.TextureName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Error creating texture: {ex.Message}", "Error");
            }
        }

        private void OpenTextureInEditor(string filePath, string fileName)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return;
                }

                // Check if a tab for this texture is already open
                TabItem existingTab = null;
                StackPanel foundHeaderPanel = null;
                if (ResourcesTabs != null)
                {
                    foreach (var item in ResourcesTabs.Items)
                    {
                        if (item is TabItem tabItem && tabItem.Header is StackPanel headerPanel1)
                        {
                            foreach (var child in headerPanel1.Children)
                            {
                                if (child is SystemTextBlock tb && tb.Text == $"Edit: {fileName}")
                                {
                                    existingTab = tabItem;
                                    foundHeaderPanel = headerPanel1;
                                    break;
                                }
                            }
                        }
                        if (existingTab != null) break;
                    }
                    
                    if (existingTab != null)
                    {
                        // Tab already open, just select it
                        ResourcesTabs.SelectedItem = existingTab;
                        return;
                    }
                }
                
                // Create the editor page
                var editorVm = new TextureEditorViewModel();
                var editorPage = new TextureEditorPage(editorVm);
                editorVm.SetPngPath(filePath);

                // Create a Frame to host the page
                var frame = new Frame();
                frame.Navigate(editorPage);

                // Create a close button
                var closeButton = new Button
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
                            Text = $"Edit: {fileName}"
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
                openInWindowMenuItem.Click += (s, e) => OpenTabAsWindow(tab);
                contextMenu.Items.Add(openInWindowMenuItem);
                tab.ContextMenu = contextMenu;

                // Close button event
                closeButton.Click += (s, e) =>
                {
                    ResourcesTabs.Items.Remove(tab);
                };

                // Add and select the new tab
                if (ResourcesTabs != null)
                {
                    ResourcesTabs.Items.Add(tab);
                    ResourcesTabs.SelectedItem = tab;
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Error opening texture editor: {ex.Message}", "Error");
            }
        }

        private void OpenTabAsWindow(TabItem tabItem)
        {
            if (tabItem == null) return;
            ResourcesTabs.Items.Remove(tabItem);
            var newWindow = new Window
            {
                Title = (tabItem.Header as StackPanel)?.Children.OfType<SystemTextBlock>().FirstOrDefault()?.Text ?? "Detached Tab",
                Content = tabItem.Content,
                Width = 800,
                Height = 600
            };
            newWindow.Show();
        }

        // Icon tab buttons
        private void RefreshIcon_Click(object sender, RoutedEventArgs e)
        {
            var iconPath = Path.Combine(_projectPath, "src", "main", "resources", "assets", _modId, "icon.png");
            LoadIcon(iconPath);

            UpdateEmptyStates();
        }

        private void OpenIconFolder_Click(object sender, RoutedEventArgs e)
        {
            var iconDir = Path.Combine(_projectPath, "src", "main", "resources", "assets", _modId);
            if (Directory.Exists(iconDir))
            {
                Process.Start("explorer.exe", iconDir);
            }

            UpdateEmptyStates();
        }

        // README tab buttons
        private void OpenReadmeFolder_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(_readmePath))
            {
                Process.Start("explorer.exe", $"/select,\"{_readmePath}\"");
            }
            else
            {
                Process.Start("explorer.exe", _projectPath);
            }

            UpdateEmptyStates();
        }

        private void OpenReadmeInEditor_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(_readmePath))
            {
                try
                {
                    // Try to open with default editor
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _readmePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    ShowMessage($"Could not open README: {ex.Message}", "Error");
                }
            }
            else
            {
                ShowMessage("README.md file not found", "File Missing");
            }

            UpdateEmptyStates();
        }

        private void ShowMessage(string message, string title)
        {
            var msgBox = new MessageBox
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "OK"
            };
            msgBox.ShowDialogAsync();

            UpdateEmptyStates();
        }

        private void LoadReadme()
        {
            if (ReadmeEditor != null)
            {
                if (File.Exists(_readmePath))
                {
                    ReadmeEditor.Text = File.ReadAllText(_readmePath);
                }
                else
                {
                    ReadmeEditor.Text = string.Empty;
                }
            }

            UpdateEmptyStates();
        }

        private void SaveReadme_Click(object sender, RoutedEventArgs e)
        {
            if (ReadmeEditor == null) return;
            
            try
            {
                File.WriteAllText(_readmePath, ReadmeEditor.Text);

                UpdateEmptyStates();
            }
            catch (IOException ex)
            {
                _ = new MessageBox
                {
                    Title = "Error",
                    Content = $"Could not save README.md:\n{ex.Message}",
                    PrimaryButtonText = "OK"
                }
                .ShowDialogAsync();
                UpdateEmptyStates();
            }
        }

        // Markdown formatting button handlers for README editor
        private void BoldButton_Click(object sender, RoutedEventArgs e)
        {
            InsertMarkdownSyntax("**", "**");
        }

        private void ItalicButton_Click(object sender, RoutedEventArgs e)
        {
            InsertMarkdownSyntax("*", "*");
        }

        private void UnderlineButton_Click(object sender, RoutedEventArgs e)
        {
            // Markdown does not support underline natively, use HTML <u> tag
            InsertMarkdownSyntax("<u>", "</u>");
        }

        private void SpoilerButton_Click(object sender, RoutedEventArgs e)
        {
            // Common spoiler syntax: ">!spoiler!<" (used by Discord/GitHub)
            InsertMarkdownSyntax(">!", "!<");
        }

        private void BulletButton_Click(object sender, RoutedEventArgs e)
        {
            InsertListMarkdown("- ");
        }

        private void NumberedButton_Click(object sender, RoutedEventArgs e)
        {
            InsertListMarkdown("1. ");
        }

        private void CodeButton_Click(object sender, RoutedEventArgs e)
        {
            // Inline code or code block depending on selection
            if (ReadmeEditor != null && !string.IsNullOrEmpty(ReadmeEditor.SelectedText) && ReadmeEditor.SelectedText.Contains("\n"))
                InsertMarkdownSyntax("\n```\n", "\n```\n");
            else
                InsertMarkdownSyntax("`", "`");
        }

        // Helper for wrapping selected text
        private void InsertMarkdownSyntax(string prefix, string suffix)
        {
            if (ReadmeEditor == null) return;
            var selStart = ReadmeEditor.SelectionStart;
            var selLength = ReadmeEditor.SelectionLength;
            var text = ReadmeEditor.Text;
            var selected = selLength > 0 ? text.Substring(selStart, selLength) : "";
            var newText = text.Remove(selStart, selLength).Insert(selStart, prefix + selected + suffix);
            ReadmeEditor.Text = newText;
            ReadmeEditor.Focus();
            ReadmeEditor.SelectionStart = selStart + prefix.Length;
            ReadmeEditor.SelectionLength = selected.Length;
        }

        // Helper for inserting list syntax at line starts
        private void InsertListMarkdown(string listPrefix)
        {
            if (ReadmeEditor == null) return;
            var selStart = ReadmeEditor.SelectionStart;
            var selLength = ReadmeEditor.SelectionLength;
            var text = ReadmeEditor.Text;
            var selected = selLength > 0 ? text.Substring(selStart, selLength) : "";
            var lines = selected.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]) && !lines[i].TrimStart().StartsWith(listPrefix))
                    lines[i] = listPrefix + lines[i].TrimStart();
            }
            var newSelected = string.Join("\n", lines);
            var newText = text.Remove(selStart, selLength).Insert(selStart, newSelected);
            ReadmeEditor.Text = newText;
            ReadmeEditor.Focus();
            ReadmeEditor.SelectionStart = selStart;
            ReadmeEditor.SelectionLength = newSelected.Length;
        }

        private void LoadTextures(string dir)
        {
            if (!Directory.Exists(dir) || TexturesList == null) return;

            var list = new List<ImageContainer>();
            foreach (var file in Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories))
            {
                try
                {
                    list.Add(new ImageContainer
                    {
                        FullPath = file, // Store full path for PngDisplay
                        FileName = Path.GetFileName(file)
                    });
                }
                catch { /* skip invalid images */ }
            }

            TexturesList.ItemsSource = list;
            UpdateEmptyStates();
        }

        private void OpenTexturesFolder_Click(object sender, RoutedEventArgs e)
        {
            string texturePath = Path.Combine(_projectPath, "src\\main\\resources\\assets", _modId, "textures");
            // Create the directory if it doesn't exist
            if (!Directory.Exists(texturePath))
            {
                Directory.CreateDirectory(texturePath);
            }
            Process.Start("explorer.exe", texturePath);
        }

        private void OpenModelsFolder_Click(object sender, RoutedEventArgs e)
        {
            string modelPath = Path.Combine(_projectPath, "src\\main\\resources\\assets", _modId, "models");
            // Create the directory if it doesn't exist
            if (!Directory.Exists(modelPath))
            {
                Directory.CreateDirectory(modelPath);
            }
            Process.Start("explorer.exe", modelPath);
        }

        private void OpenSoundsFolder_Click(object sender, RoutedEventArgs e)
        {
            string soundPath = Path.Combine(_projectPath, "src\\main\\resources\\assets", _modId, "sounds");
            // Create the directory if it doesn't exist
            if (!Directory.Exists(soundPath))
            {
                Directory.CreateDirectory(soundPath);
            }
            Process.Start("explorer.exe", soundPath);
        }

        private void LoadModels(string dir)
        {
            if (!Directory.Exists(dir) || ModelsList == null) return;

            _allModels.Clear();
            
            var modelFiles = Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories);
            
            foreach (var file in modelFiles)
            {
                var viewModel = new ModelFileViewModel
                {
                    FullPath = file,
                    FileName = Path.GetFileName(file),
                    StatusIcon = Wpf.Ui.Controls.SymbolRegular.Document24,
                    StatusColor = Brushes.Gray,
                    StatusTooltip = "Not validated",
                    ValidationMessage = "",
                    HasValidationMessage = false,
                    HasMissingTextures = false
                };
                
                _allModels.Add(viewModel);
            }

            _filteredModels = new ObservableCollection<ModelFileViewModel>(_allModels);
            ModelsList.ItemsSource = _filteredModels;
            UpdateEmptyStates();
        }

        private void LoadSounds(string dir)
        {
            if (!Directory.Exists(dir) || SoundsList == null) return;

            var list = Directory.GetFiles(dir, "*.ogg", SearchOption.AllDirectories)
                                .Select(f => new SoundFile
                                {
                                    FullPath = f,
                                    FileName = Path.GetFileName(f)
                                })
                                .ToList();

            SoundsList.ItemsSource = list;
            UpdateEmptyStates();
        }

        private void LoadIcon(string path)
        {
            // Find the PngDisplay control instead of Image
            var iconDisplay = this.FindName("IconDisplay") as Modrix.Views.Controls.PngDisplay;
            var emptyIconText = this.FindName("EmptyIconText") as System.Windows.Controls.TextBlock;
            
            if (iconDisplay == null || emptyIconText == null) return;
            
            if (File.Exists(path))
            {
                iconDisplay.Visibility = Visibility.Visible;
                iconDisplay.SourcePath = path;
                emptyIconText.Visibility = Visibility.Collapsed;
            }
            else
            {
                iconDisplay.SourcePath = null;
                iconDisplay.Visibility = Visibility.Collapsed;
                emptyIconText.Visibility = Visibility.Visible;
            }

            UpdateEmptyStates();
        }

        #region Import Handlers
        private void ImportTextures_Click(object s, RoutedEventArgs e)
            => ImportFiles("Image Files|*.png;*.jpg;*.jpeg",
                           "textures", LoadTextures);

        private void ImportModels_Click(object s, RoutedEventArgs e)
            => ImportFiles("JSON Models|*.json",
                           "models", LoadModels);

        private void ImportSounds_Click(object s, RoutedEventArgs e)
            => ImportFiles("Sound Files|*.ogg",
                           "sounds", LoadSounds);

        private void ImportFiles(string filter, string subfolder, Action<string> reloadAction)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Import files",
                Filter = filter,
                Multiselect = true
            };

            if (dlg.ShowDialog() == true)
            {
                var targetDir = Path.Combine(_projectPath,
                                             "src", "main", "resources", "assets", _modId, subfolder);
                Directory.CreateDirectory(targetDir);

                foreach (var src in dlg.FileNames)
                {
                    var dest = Path.Combine(targetDir, Path.GetFileName(src));
                    File.Copy(src, dest, overwrite: true);
                }

                // refresh
                reloadAction(targetDir);
            }
            UpdateEmptyStates();
        }
        #endregion

        private void PlaySound_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path && File.Exists(path))
            {
                try
                {
                    _mediaPlayer.Stop();
                    _mediaPlayer.Close();
                    _mediaPlayer.Open(new Uri(path));
                    _mediaPlayer.Play();
                }
                catch (Exception ex)
                {
                    _ = new MessageBox
                    {
                        Title = "Error",
                        Content = $"Could not play sound:\n{ex.Message}",
                        PrimaryButtonText = "OK"
                    }
                    .ShowDialogAsync();
                }
            }

            UpdateEmptyStates();
        }

        private void TexturesList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement element && element.DataContext is ImageContainer img)
            {
                var contextMenu = new ContextMenu();

                var editItem = new MenuItem
                {
                    Header = "Edit...",
                    Icon = new SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Edit24)
                };

                var openItem = new MenuItem
                {
                    Header = "Open in External Editor",
                    Icon = new SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Open28)
                };

                var deleteItem = new MenuItem
                {
                    Header = "Delete",
                    Icon = new SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Delete24)
                };

                editItem.Click += (s, args) =>
                {
                    var filePath = Path.Combine(_projectPath, "src", "main", "resources", "assets", _modId, "textures", img.FileName);
                    OpenTextureInEditor(filePath, img.FileName);
                };

                openItem.Click += (s, args) =>
                {
                    var filePath = Path.Combine(_projectPath, "src", "main", "resources", "assets", _modId, "textures", img.FileName);
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = filePath,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            ShowMessage($"Could not open texture: {ex.Message}", "Error");
                        }
                    }
                };

                deleteItem.Click += (s, args) =>
                {
                    var filePath = Path.Combine(_projectPath, "src", "main", "resources", "assets", _modId, "textures", img.FileName);
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            File.Delete(filePath);
                            LoadTextures(Path.Combine(_projectPath, "src", "main", "resources", "assets", _modId, "textures"));
                        }
                        catch (Exception ex)
                        {
                            ShowMessage($"Could not delete texture: {ex.Message}", "Error");
                        }
                    }
                };

                contextMenu.Items.Add(editItem);
                contextMenu.Items.Add(openItem);
                contextMenu.Items.Add(new Separator());
                contextMenu.Items.Add(deleteItem);

                contextMenu.IsOpen = true;
                e.Handled = true;
            }

            UpdateEmptyStates();
        }

        private void ChangeIcon_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Mod Icon",
                Filter = "Image Files|*.png;*.jpg;*.jpeg",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            UpdateEmptyStates();

            if (dlg.ShowDialog() == true)
            {
                var dest = Path.Combine(_projectPath,
                                        "src", "main", "resources", "assets", _modId, "icon.png");
                try
                {
                    // Create the directory if it doesn't exist
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    File.Copy(dlg.FileName, dest, true);
                    LoadIcon(dest);
                }
                catch (Exception ex)
                {
                    _ = new MessageBox
                    {
                        Title = "Error",
                        Content = $"Could not update icon:\n{ex.Message}",
                        PrimaryButtonText = "OK"
                    }
                    .ShowDialogAsync();
                }
            }
        }

        private void RemoveIcon_Click(object sender, RoutedEventArgs e)
        {
            var path = Path.Combine(_projectPath,
                                    "src", "main", "resources", "assets", _modId, "icon.png");
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                    LoadIcon(path);
                }
                catch (Exception ex)
                {
                    _ = new MessageBox
                    {
                        Title = "Error",
                        Content = $"Could not remove icon:\n{ex.Message}",
                        PrimaryButtonText = "OK"
                    }
                    .ShowDialogAsync();
                }
            }

            UpdateEmptyStates();
        }

        // Model validation methods
        private async void ValidateModels_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowNotification("Validating models...", "Please wait while we check your models.", NotificationType.Info);
                
                _lastValidationResult = await _validationService.ValidateModelsAsync(_projectPath, _modId);
                
                UpdateModelValidationUI();
                
                var errorCount = _lastValidationResult.Issues.Count(i => i.Type == "Error");
                var warningCount = _lastValidationResult.Issues.Count(i => i.Type == "Warning");
                var missingMappings = _lastValidationResult.MissingMappings.Count;
                
                ShowValidationResults(errorCount, warningCount, missingMappings);
                
                // Find the button by name if it exists
                var fixButton = this.FindName("FixMappingsButton") as Wpf.Ui.Controls.Button;
                if (fixButton != null)
                    fixButton.IsEnabled = missingMappings > 0;
            }
            catch (Exception ex)
            {
                ShowNotification("Validation Failed", $"Error during validation: {ex.Message}", NotificationType.Error);
            }
        }

        private async void FixMissingMappings_Click(object sender, RoutedEventArgs e)
        {
            if (_lastValidationResult?.MissingMappings?.Any() != true)
            {
                ShowNotification("No Issues", "No missing mappings found. Run validation first.", NotificationType.Info);
                return;
            }

            var dialog = new MissingMappingsDialog(_projectPath, _modId, _lastValidationResult.MissingMappings);
            dialog.Owner = Window.GetWindow(this);
            
            if (dialog.ShowDialog() == true && dialog.HasChanges)
            {
                // Re-validate after changes
                ValidateModels_Click(sender, e);
                ShowNotification("Mappings Fixed", "Model mappings have been updated successfully.", NotificationType.Success);
            }
        }

        private void ModelsSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterModels();
        }

        private void FilterModels()
        {
            var searchBox = this.FindName("ModelsSearchBox") as Wpf.Ui.Controls.TextBox;
            if (searchBox == null || _allModels == null) return;
            
            var searchText = searchBox.Text?.ToLower() ?? "";
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                _filteredModels = new ObservableCollection<ModelFileViewModel>(_allModels);
            }
            else
            {
                _filteredModels = new ObservableCollection<ModelFileViewModel>(
                    _allModels.Where(m => m.FileName.ToLower().Contains(searchText)));
            }
            
            ModelsList.ItemsSource = _filteredModels;
            UpdateEmptyStates();
        }

        private void EditModel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ModelFileViewModel model)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = model.FullPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    ShowMessage($"Could not open model: {ex.Message}", "Error");
                }
            }
        }

        private void ViewModel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ModelFileViewModel model)
            {
                OpenModelInViewer(model.FullPath, model.FileName);
            }
        }

        private void OpenModelInViewer(string modelPath, string modelName)
        {
            try
            {
                if (!File.Exists(modelPath))
                {
                    ShowMessage("Model file not found.", "Error");
                    return;
                }

                // Check if a tab for this model is already open
                TabItem existingTab = null;
                if (ResourcesTabs != null)
                {
                    foreach (var item in ResourcesTabs.Items)
                    {
                        if (item is TabItem tabItem && tabItem.Header is StackPanel existingHeaderPanel)
                        {
                            foreach (var child in existingHeaderPanel.Children)
                            {
                                if (child is SystemTextBlock tb && tb.Text == $"View: {modelName}")
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
                        ResourcesTabs.SelectedItem = existingTab;
                        return;
                    }
                }
                
                // Create the model viewer page
                var viewerVm = new ModelViewerViewModel();
                
                // Set project information for texture loading
                viewerVm.SetProjectInfo(_projectPath, _modId);
                
                var viewerPage = new ModelViewerPage(viewerVm);
                viewerPage.SetModelPath(modelPath);

                // Create a Frame to host the page
                var frame = new Frame();
                frame.Navigate(viewerPage);

                // Create a close button
                var closeButton = new Button
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
                            Symbol = Wpf.Ui.Controls.SymbolRegular.Cube24,
                            Margin = new Thickness(0, 0, 4, 0)
                        },
                        new SystemTextBlock
                        {
                            Text = $"View: {modelName}",
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
                openInWindowMenuItem.Click += (s, e) => OpenTabAsWindow(tab);
                contextMenu.Items.Add(openInWindowMenuItem);
                tab.ContextMenu = contextMenu;

                // Close button event
                closeButton.Click += (s, e) =>
                {
                    ResourcesTabs.Items.Remove(tab);
                };

                // Add and select the new tab
                if (ResourcesTabs != null)
                {
                    ResourcesTabs.Items.Add(tab);
                    ResourcesTabs.SelectedItem = tab;
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Error opening model viewer: {ex.Message}", "Error");
            }
        }

        private async void RemapModelTextures_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ModelFileViewModel model)
            {
                try
                {
                    var missingMappings = _lastValidationResult?.MissingMappings
                        ?.Where(m => m.ModelPath == model.FullPath)
                        ?.ToList() ?? new List<ModelValidationService.MissingMapping>();

                    if (!missingMappings.Any())
                    {
                        ShowNotification("No Issues", "This model has no missing texture mappings.", NotificationType.Info);
                        return;
                    }

                    var dialog = new MissingMappingsDialog(_projectPath, _modId, missingMappings);
                    dialog.Owner = Window.GetWindow(this);
                    
                    if (dialog.ShowDialog() == true && dialog.HasChanges)
                    {
                        // Re-validate this specific model
                        ValidateModels_Click(sender, e);
                    }
                }
                catch (Exception ex)
                {
                    ShowMessage($"Error opening remap dialog: {ex.Message}", "Error");
                }
            }
        }

        private void ModelsList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement element && element.DataContext is ModelFileViewModel model)
            {
                var contextMenu = new ContextMenu();

                var viewItem = new MenuItem
                {
                    Header = "View Model",
                    Icon = new SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Eye24)
                };

                var editItem = new MenuItem
                {
                    Header = "Edit in External Editor",
                    Icon = new SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Edit24)
                };

                var validateItem = new MenuItem
                {
                    Header = "Validate This Model",
                    Icon = new SymbolIcon(Wpf.Ui.Controls.SymbolRegular.CheckmarkStarburst24)
                };

                var remapItem = new MenuItem
                {
                    Header = "Remap Textures",
                    Icon = new SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Wand24),
                    IsEnabled = model.HasMissingTextures
                };

                var deleteItem = new MenuItem
                {
                    Header = "Delete",
                    Icon = new SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Delete24)
                };

                viewItem.Click += (s, args) => OpenModelInViewer(model.FullPath, model.FileName);
                editItem.Click += (s, args) => EditModel_Click(s, null);
                validateItem.Click += async (s, args) => await ValidateSingleModel(model);
                remapItem.Click += (s, args) => RemapModelTextures_Click(s, null);
                deleteItem.Click += (s, args) => DeleteModel(model);

                contextMenu.Items.Add(viewItem);
                contextMenu.Items.Add(editItem);
                contextMenu.Items.Add(validateItem);
                contextMenu.Items.Add(remapItem);
                contextMenu.Items.Add(new Separator());
                contextMenu.Items.Add(deleteItem);

                contextMenu.IsOpen = true;
                e.Handled = true;
            }
        }

        private async Task ValidateSingleModel(ModelFileViewModel model)
        {
            try
            {
                var singleResult = await _validationService.ValidateModelsAsync(_projectPath, _modId);
                var modelIssues = singleResult.Issues.Where(i => i.FilePath == model.FullPath).ToList();
                var modelMappings = singleResult.MissingMappings.Where(m => m.ModelPath == model.FullPath).ToList();

                UpdateSingleModelUI(model, modelIssues, modelMappings);

                var message = modelIssues.Any() || modelMappings.Any() 
                    ? $"Found {modelIssues.Count} issues and {modelMappings.Count} missing mappings."
                    : "Model is valid with no issues.";
                
                ShowNotification("Single Model Validation", message, 
                    modelIssues.Any(i => i.Type == "Error") ? NotificationType.Error : NotificationType.Success);
            }
            catch (Exception ex)
            {
                ShowMessage($"Error validating model: {ex.Message}", "Validation Error");
            }
        }

        private void DeleteModel(ModelFileViewModel model)
        {
            try
            {
                if (File.Exists(model.FullPath))
                {
                    File.Delete(model.FullPath);
                    _allModels.Remove(model);
                    FilterModels();
                    ShowNotification("Model Deleted", $"Successfully deleted {model.FileName}", NotificationType.Success);
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Could not delete model: {ex.Message}", "Error");
            }
        }

        private void UpdateModelValidationUI()
        {
            if (_lastValidationResult == null) return;

            foreach (var model in _allModels)
            {
                var issues = _lastValidationResult.Issues.Where(i => i.FilePath == model.FullPath).ToList();
                var mappings = _lastValidationResult.MissingMappings.Where(m => m.ModelPath == model.FullPath).ToList();
                
                UpdateSingleModelUI(model, issues, mappings);
            }
        }

        private void UpdateSingleModelUI(ModelFileViewModel model, List<ModelValidationService.ValidationIssue> issues, List<ModelValidationService.MissingMapping> mappings)
        {
            var hasErrors = issues.Any(i => i.Type == "Error");
            var hasWarnings = issues.Any(i => i.Type == "Warning");
            var hasMissingMappings = mappings.Any();

            if (hasErrors)
            {
                model.StatusIcon = Wpf.Ui.Controls.SymbolRegular.ErrorCircle24;
                model.StatusColor = Brushes.Red;
                model.StatusTooltip = "Has validation errors";
            }
            else if (hasWarnings || hasMissingMappings)
            {
                model.StatusIcon = Wpf.Ui.Controls.SymbolRegular.Warning24;
                model.StatusColor = Brushes.Orange;
                model.StatusTooltip = "Has warnings or missing mappings";
            }
            else
            {
                model.StatusIcon = Wpf.Ui.Controls.SymbolRegular.CheckmarkCircle24;
                model.StatusColor = Brushes.Green;
                model.StatusTooltip = "Valid";
            }

            model.HasMissingTextures = hasMissingMappings;
            
            if (issues.Any() || mappings.Any())
            {
                var messages = new List<string>();
                if (issues.Any()) messages.Add($"{issues.Count} validation issues");
                if (mappings.Any()) messages.Add($"{mappings.Count} missing texture mappings");
                
                model.ValidationMessage = string.Join(", ", messages);
                model.HasValidationMessage = true;
            }
            else
            {
                model.ValidationMessage = "";
                model.HasValidationMessage = false;
            }
        }

        private void ShowValidationResults(int errorCount, int warningCount, int missingMappings)
        {
            if (errorCount == 0 && warningCount == 0 && missingMappings == 0)
            {
                ShowNotification("Validation Complete", "All models are valid with no issues found.", NotificationType.Success);
                return;
            }

            var title = "Model Validation Results";
            var message = $"Found {errorCount} errors, {warningCount} warnings";
            if (missingMappings > 0)
            {
                message += $", and {missingMappings} missing texture mappings";
            }

            var type = errorCount > 0 ? NotificationType.Error : NotificationType.Warning;
            
            ShowNotification(title, message, type, showAction: missingMappings > 0);
        }

        // Notification system
        private void ShowNotification(string title, string message, NotificationType type, int autoHideSeconds = 5, bool showAction = false)
        {
            var notificationPanel = this.FindName("NotificationPanel") as Border;
            if (notificationPanel == null) 
            {
                // Fallback to MessageBox if notification panel doesn't exist
                var messageBox = new MessageBox
                {
                    Title = title,
                    Content = message,
                    PrimaryButtonText = "OK"
                };
                _ = messageBox.ShowDialogAsync();
                return;
            }

            var notificationTitle = this.FindName("NotificationTitle") as System.Windows.Controls.TextBlock;
            var notificationMessage = this.FindName("NotificationMessage") as System.Windows.Controls.TextBlock;
            var notificationIcon = this.FindName("NotificationIcon") as SymbolIcon;
            var notificationActionButton = this.FindName("NotificationActionButton") as Wpf.Ui.Controls.Button;

            if (notificationTitle != null) notificationTitle.Text = title;
            if (notificationMessage != null) notificationMessage.Text = message;
            
            if (notificationIcon != null)
            {
                switch (type)
                {
                    case NotificationType.Success:
                        notificationIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.CheckmarkCircle24;
                        notificationIcon.Foreground = Brushes.Green;
                        break;
                    case NotificationType.Warning:
                        notificationIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Warning24;
                        notificationIcon.Foreground = Brushes.Orange;
                        break;
                    case NotificationType.Error:
                        notificationIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.ErrorCircle24;
                        notificationIcon.Foreground = Brushes.Red;
                        break;
                    default:
                        notificationIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Info24;
                        notificationIcon.Foreground = Brushes.Blue;
                        break;
                }
            }

            if (notificationActionButton != null)
                notificationActionButton.Visibility = showAction ? Visibility.Visible : Visibility.Collapsed;
            
            notificationPanel.Visibility = Visibility.Visible;

            if (autoHideSeconds > 0)
            {
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(autoHideSeconds)
                };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    DismissNotification();
                };
                timer.Start();
            }
        }

        private void NotificationAction_Click(object sender, RoutedEventArgs e)
        {
            FixMissingMappings_Click(sender, e);
        }

        private void DismissNotification_Click(object sender, RoutedEventArgs e)
        {
            DismissNotification();
        }

        private void DismissNotification()
        {
            var notificationPanel = this.FindName("NotificationPanel") as Border;
            if (notificationPanel != null)
            {
                notificationPanel.Visibility = Visibility.Collapsed;
            }
        }

        public enum NotificationType
        {
            Info,
            Success,
            Warning,
            Error
        }

        // Helper classes
        private class ImageContainer
        {
            public string FullPath { get; set; } // Changed from BitmapImage to string path
            public string FileName { get; set; }
        }

        private class ModelFile
        {
            public string FullPath { get; set; }
            public string FileName { get; set; }
        }

        private class SoundFile
        {
            public string FullPath { get; set; }
            public string FileName { get; set; }
        }
    }

    // ViewModel for model files
    public class ModelFileViewModel
    {
        public string FullPath { get; set; }
        public string FileName { get; set; }
        public Wpf.Ui.Controls.SymbolRegular StatusIcon { get; set; }
        public Brush StatusColor { get; set; }
        public string StatusTooltip { get; set; }
        public string ValidationMessage { get; set; }
        public bool HasValidationMessage { get; set; }
        public bool HasMissingTextures { get; set; }
    }
}
