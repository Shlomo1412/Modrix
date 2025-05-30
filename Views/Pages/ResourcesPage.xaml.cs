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
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace Modrix.Views.Pages
{
    public partial class ResourcesPage : Page
    {
        private string _projectPath;
        private string _modId;
        private string _readmePath;  // ← הוסף שדה זה
        private MediaPlayer _mediaPlayer = new();

        public ResourcesPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void UpdateEmptyStates()
        {
            // Textures
            TexturesEmptyState.Visibility = TexturesList.Items.Count == 0 ?
                Visibility.Visible : Visibility.Collapsed;

            // Models
            ModelsEmptyState.Visibility = ModelsList.Items.Count == 0 ?
                Visibility.Visible : Visibility.Collapsed;

            // Sounds
            SoundsEmptyState.Visibility = SoundsList.Items.Count == 0 ?
                Visibility.Visible : Visibility.Collapsed;

            
            

            // README
            ReadmeEmptyState.Visibility = string.IsNullOrWhiteSpace(ReadmeEditor.Text) ?
                Visibility.Visible : Visibility.Collapsed;
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
                _readmePath = Path.Combine(_projectPath, "README.md");  // ← קבע כאן

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
            if (File.Exists(_readmePath))
                ReadmeEditor.Text = File.ReadAllText(_readmePath);
            else
                ReadmeEditor.Text = string.Empty;

            UpdateEmptyStates();
        }

        private void SaveReadme_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                File.WriteAllText(_readmePath, ReadmeEditor.Text);

                UpdateEmptyStates();
                // var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                // mainWindow?.ShowSnackbar("README.md saved successfully!");
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
            if (!Directory.Exists(dir)) return;

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
            => Process.Start("explorer.exe", Path.Combine(_projectPath, "src\\main\\resources\\assets", _modId, "textures"));

        private void OpenModelsFolder_Click(object sender, RoutedEventArgs e)
            => Process.Start("explorer.exe", Path.Combine(_projectPath, "src\\main\\resources\\assets", _modId, "models"));

        private void OpenSoundsFolder_Click(object sender, RoutedEventArgs e)
            => Process.Start("explorer.exe", Path.Combine(_projectPath, "src\\main\\resources\\assets", _modId, "sounds"));

        private void LoadModels(string dir)
        {
            if (!Directory.Exists(dir)) return;

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
            if (!Directory.Exists(dir)) return;

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
            if (sender is Wpf.Ui.Controls.Button btn && btn.Tag is string path && File.Exists(path))
            {
                try
                {
                    _mediaPlayer.Stop(); // ← עצור קודם
                    _mediaPlayer.Close(); // ← נקה את המדיה הקודמת
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

                var openItem = new Wpf.Ui.Controls.MenuItem
                {
                    Header = "Open in Editor",
                    Icon = new SymbolIcon(SymbolRegular.Edit24)
                };

                var deleteItem = new Wpf.Ui.Controls.MenuItem
                {
                    Header = "Delete",
                    Icon = new SymbolIcon(SymbolRegular.Delete24)
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
