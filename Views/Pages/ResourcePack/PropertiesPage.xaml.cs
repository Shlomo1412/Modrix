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

namespace Modrix.Views.Pages.ResourcePack
{
    public partial class PropertiesPage : System.Windows.Controls.Page, INavigableView<object>
    {
        public object ViewModel => this;
        
        private ResourcePackData? _currentPack;
        private bool _isDataLoaded = false;
        
        public PropertiesPage()
        {
            InitializeComponent();
            DataContext = this;
            LoadCurrentPack();
            if (_currentPack != null)
            {
                LoadPackProperties();
            }
        }

        public void OnLoaded(object sender, RoutedEventArgs e)
        {
            LoadCurrentPack();
            if (_currentPack != null && !_isDataLoaded)
            {
                LoadPackProperties();
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

        private void LoadPackProperties()
        {
            if (_currentPack == null) return;

            try
            {
                // Load pack.mcmeta file
                var packMetaPath = Path.Combine(_currentPack.Location, "pack.mcmeta");
                if (File.Exists(packMetaPath))
                {
                    var content = File.ReadAllText(packMetaPath);
                    var doc = JsonDocument.Parse(content);
                    
                    if (doc.RootElement.TryGetProperty("pack", out var packElement))
                    {
                        // Update UI fields if they exist
                        if (packElement.TryGetProperty("description", out var descElement))
                        {
                            var descriptionBox = this.FindName("DescriptionBox") as Wpf.Ui.Controls.TextBox;
                            if (descriptionBox != null)
                                descriptionBox.Text = descElement.GetString() ?? "";
                        }
                        
                        if (packElement.TryGetProperty("pack_format", out var formatElement))
                        {
                            var packFormatBox = this.FindName("PackFormatBox") as Wpf.Ui.Controls.NumberBox;
                            if (packFormatBox != null)
                                packFormatBox.Value = formatElement.GetInt32();
                        }
                    }
                }

                // Load other properties
                var nameBox = this.FindName("NameBox") as Wpf.Ui.Controls.TextBox;
                if (nameBox != null)
                    nameBox.Text = _currentPack.Name;

                var minecraftVersionBox = this.FindName("MinecraftVersionBox") as Wpf.Ui.Controls.TextBox;
                if (minecraftVersionBox != null)
                    minecraftVersionBox.Text = _currentPack.MinecraftVersion;

                _isDataLoaded = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading pack properties: {ex.Message}");
            }
        }

        public void SaveProperties_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPack == null) return;

            try
            {
                // Get values from UI
                var nameBox = this.FindName("NameBox") as Wpf.Ui.Controls.TextBox;
                var descriptionBox = this.FindName("DescriptionBox") as Wpf.Ui.Controls.TextBox;
                var packFormatBox = this.FindName("PackFormatBox") as Wpf.Ui.Controls.NumberBox;
                var minecraftVersionBox = this.FindName("MinecraftVersionBox") as Wpf.Ui.Controls.TextBox;

                var name = nameBox?.Text ?? _currentPack.Name;
                var description = descriptionBox?.Text ?? _currentPack.Description;
                var packFormat = (int)(packFormatBox?.Value ?? _currentPack.PackFormat);
                var minecraftVersion = minecraftVersionBox?.Text ?? _currentPack.MinecraftVersion;

                // Update pack data
                _currentPack.Name = name;
                _currentPack.Description = description;
                _currentPack.PackFormat = packFormat;
                _currentPack.MinecraftVersion = minecraftVersion;

                // Save pack.mcmeta
                var packMetaPath = Path.Combine(_currentPack.Location, "pack.mcmeta");
                var packMeta = new
                {
                    pack = new
                    {
                        pack_format = packFormat,
                        description = description,
                        supported_formats = new { min_inclusive = packFormat, max_inclusive = packFormat }
                    }
                };

                var json = JsonSerializer.Serialize(packMeta, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(packMetaPath, json);

                // Update modrix.config
                var configPath = Path.Combine(_currentPack.Location, "modrix.config");
                var configLines = new List<string>
                {
                    $"ModType=Resource Pack",
                    $"Name={name}",
                    $"Description={description}",
                    $"MinecraftVersion={minecraftVersion}",
                    $"PackFormat={packFormat}",
                    $"ModId={_currentPack.ModId}",
                    $"Version=1.0.0",
                    $"IconPath=pack.png"
                };
                File.WriteAllLines(configPath, configLines);

                ShowMessage("Properties saved successfully!", "Success");
            }
            catch (Exception ex)
            {
                ShowMessage($"Failed to save properties: {ex.Message}", "Error");
            }
        }

        public void SelectIcon_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPack == null) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Pack Icon",
                Filter = "PNG Images|*.png|All Images|*.png;*.jpg;*.jpeg;*.bmp",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var targetPath = Path.Combine(_currentPack.Location, "pack.png");
                    File.Copy(dialog.FileName, targetPath, true);
                    
                    _currentPack.IconPath = targetPath;
                    
                    // Update icon preview if available
                    var iconImage = this.FindName("IconImage") as System.Windows.Controls.Image;
                    if (iconImage != null)
                    {
                        iconImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(targetPath));
                    }

                    ShowMessage("Pack icon updated successfully!", "Success");
                }
                catch (Exception ex)
                {
                    ShowMessage($"Failed to update icon: {ex.Message}", "Error");
                }
            }
        }

        public void OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPack == null) return;

            try
            {
                System.Diagnostics.Process.Start("explorer.exe", _currentPack.Location);
            }
            catch (Exception ex)
            {
                ShowMessage($"Failed to open location: {ex.Message}", "Error");
            }
        }

        public void RefreshProperties_Click(object sender, RoutedEventArgs e)
        {
            _isDataLoaded = false;
            LoadCurrentPack();
            if (_currentPack != null)
            {
                LoadPackProperties();
            }
        }

        public void ChangeIcon_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPack == null) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Pack Icon",
                Filter = "PNG Images|*.png|All Images|*.png;*.jpg;*.jpeg;*.bmp",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var targetPath = Path.Combine(_currentPack.Location, "pack.png");
                    File.Copy(dialog.FileName, targetPath, true);
                    
                    _currentPack.IconPath = targetPath;
                    
                    // Update icon preview
                    var iconPreview = this.FindName("IconPreview") as System.Windows.Controls.Image;
                    var iconPlaceholder = this.FindName("IconPlaceholder") as FrameworkElement;
                    var removeIconButton = this.FindName("RemoveIconButton") as Wpf.Ui.Controls.Button;

                    if (iconPreview != null)
                    {
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(targetPath);
                        bitmap.EndInit();
                        iconPreview.Source = bitmap;
                        iconPreview.Visibility = Visibility.Visible;
                    }

                    if (iconPlaceholder != null)
                        iconPlaceholder.Visibility = Visibility.Collapsed;

                    if (removeIconButton != null)
                        removeIconButton.Visibility = Visibility.Visible;

                    ShowMessage("Pack icon updated successfully!", "Success");
                }
                catch (Exception ex)
                {
                    ShowMessage($"Failed to update icon: {ex.Message}", "Error");
                }
            }
        }

        public void RemoveIcon_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPack == null) return;

            try
            {
                var iconPath = Path.Combine(_currentPack.Location, "pack.png");
                if (File.Exists(iconPath))
                {
                    File.Delete(iconPath);
                }

                _currentPack.IconPath = "";

                // Update icon preview
                var iconPreview = this.FindName("IconPreview") as System.Windows.Controls.Image;
                var iconPlaceholder = this.FindName("IconPlaceholder") as FrameworkElement;
                var removeIconButton = this.FindName("RemoveIconButton") as Wpf.Ui.Controls.Button;

                if (iconPreview != null)
                {
                    iconPreview.Source = null;
                    iconPreview.Visibility = Visibility.Collapsed;
                }

                if (iconPlaceholder != null)
                    iconPlaceholder.Visibility = Visibility.Visible;

                if (removeIconButton != null)
                    removeIconButton.Visibility = Visibility.Collapsed;

                ShowMessage("Pack icon removed successfully!", "Success");
            }
            catch (Exception ex)
            {
                ShowMessage($"Failed to remove icon: {ex.Message}", "Error");
            }
        }

        private async void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            var result = await new MessageBox
            {
                Title = "Reset to Defaults",
                Content = "Are you sure you want to reset all properties to their default values? This will lose any unsaved changes.",
                PrimaryButtonText = "Reset",
                CloseButtonText = "Cancel"
            }.ShowDialogAsync();

            if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
            {
                // Reset UI fields to defaults
                var nameBox = this.FindName("NameBox") as Wpf.Ui.Controls.TextBox;
                var descriptionBox = this.FindName("DescriptionBox") as Wpf.Ui.Controls.TextBox;
                var packFormatBox = this.FindName("PackFormatBox") as Wpf.Ui.Controls.NumberBox;
                var minecraftVersionBox = this.FindName("MinecraftVersionBox") as Wpf.Ui.Controls.TextBox;

                if (nameBox != null) nameBox.Text = "My Resource Pack";
                if (descriptionBox != null) descriptionBox.Text = "A custom Minecraft resource pack";
                if (packFormatBox != null) packFormatBox.Value = 18; // Default for 1.20+
                if (minecraftVersionBox != null) minecraftVersionBox.Text = "1.20.1";
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
    }
}