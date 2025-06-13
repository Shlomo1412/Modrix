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
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MenuItem = Wpf.Ui.Controls.MenuItem;
using Button = Wpf.Ui.Controls.Button;

namespace Modrix.Views.Pages
{
    public partial class ResourcesPage : Page
    {
        private string _projectPath;
        private string _modId;
        private string _readmePath;
        private MediaPlayer _mediaPlayer = new();

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
                if (ResourcesTabs != null)
                {
                    foreach (var item in ResourcesTabs.Items)
                    {
                        if (item is TabItem tabItem && tabItem.Header is StackPanel headerPanel)
                        {
                            foreach (var child in headerPanel.Children)
                            {
                                if (child is System.Windows.Controls.TextBlock tb && tb.Text == $"Edit: {fileName}")
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

                // Create a new tab
                var tab = new TabItem
                {
                    Header = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children =
                        {
                            new SymbolIcon
                            {
                                Symbol = Wpf.Ui.Controls.SymbolRegular.ImageEdit24,
                                Margin = new Thickness(0, 0, 4, 0)
                            },
                            new System.Windows.Controls.TextBlock
                            {
                                Text = $"Edit: {fileName}"
                            }
                        }
                    },
                    Content = frame
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

        private void LoadTextures(string dir)
        {
            if (!Directory.Exists(dir) || TexturesList == null) return;

            var list = new List<ImageContainer>();
            foreach (var file in Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(file);
                    bmp.EndInit();
                    bmp.Freeze();

                    list.Add(new ImageContainer
                    {
                        Image = bmp,
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

            var list = Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories)
                                .Select(f => new ModelFile
                                {
                                    FullPath = f,
                                    FileName = Path.GetFileName(f)
                                })
                                .ToList();

            ModelsList.ItemsSource = list;
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
            if (IconImage == null || EmptyIconText == null) return;
            
            if (File.Exists(path))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path);
                bmp.EndInit();
                bmp.Freeze();

                IconImage.Visibility = Visibility.Visible;
                IconImage.Source = bmp;
                EmptyIconText.Visibility = Visibility.Collapsed;
            }
            else
            {
                IconImage.Source = null;
                IconImage.Visibility = Visibility.Collapsed;
                EmptyIconText.Visibility = Visibility.Visible;
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

        // Helper classes
        private class ImageContainer
        {
            public BitmapImage Image { get; set; }
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
}
