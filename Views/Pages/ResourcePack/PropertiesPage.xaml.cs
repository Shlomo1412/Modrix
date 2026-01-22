using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
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
            Loaded += OnLoaded;
        }

        public void OnLoaded(object sender, RoutedEventArgs e)
        {
            LoadCurrentPack();
            if (_currentPack != null)
            {
                LoadPackProperties();
            }
        }

        public void OnNavigatedTo() => RefreshProperties();

        private void LoadCurrentPack()
        {
            var workspace = Application.Current.Windows
                .OfType<ResourcePackWorkspace>()
                .FirstOrDefault();

            if (workspace?.ViewModel?.CurrentPack != null)
            {
                _currentPack = workspace.ViewModel.CurrentPack;
                _isDataLoaded = false; // Reset to force reload
            }
        }

        private void LoadPackProperties()
        {
            if (_currentPack == null) return;

            try
            {
                // Re-read the pack data to get the latest information
                var manager = new ResourcePackTemplateManager();
                var freshPackData = manager.ReadResourcePack(_currentPack.Location);
                _currentPack = freshPackData;

                // Load basic properties from _currentPack
                PackNameBox.Text = _currentPack.Name ?? "";
                PackIdBox.Text = _currentPack.ModId ?? "";
                DescriptionBox.Text = _currentPack.Description ?? "";

                // Set pack format from current pack or default based on MC version
                var packFormat = _currentPack.PackFormat;
                if (packFormat == 0)
                {
                    // Determine pack format based on Minecraft version
                    packFormat = GetPackFormatFromVersion(_currentPack.MinecraftVersion ?? "1.21.4");
                }
                SetPackFormatComboBox(packFormat);

                // Set Minecraft version
                var mcVersion = _currentPack.MinecraftVersion ?? "1.21.4";
                SetMinecraftVersionComboBox(mcVersion);

                // Load additional properties from modrix.config
                LoadAdditionalProperties();

                // Load pack icon
                LoadPackIcon();

                _isDataLoaded = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading pack properties: {ex.Message}");
                ShowMessage($"Error loading pack properties: {ex.Message}", "Error");
            }
        }

        private void LoadAdditionalProperties()
        {
            if (_currentPack == null) return;

            var configPath = Path.Combine(_currentPack.Location, "modrix.config");
            if (!File.Exists(configPath)) return;

            try
            {
                var lines = File.ReadAllLines(configPath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("Authors="))
                    {
                        AuthorsBox.Text = line.Substring(8);
                    }
                    else if (line.StartsWith("License="))
                    {
                        SetLicenseComboBox(line.Substring(8));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading modrix.config: {ex.Message}");
            }
        }

        private void LoadPackIcon()
        {
            if (_currentPack == null) return;

            var iconPath = Path.Combine(_currentPack.Location, "pack.png");
            if (File.Exists(iconPath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
                    bitmap.EndInit();
                    bitmap.Freeze();

                    IconPreview.Source = bitmap;
                    IconPreview.Visibility = Visibility.Visible;
                    IconPlaceholder.Visibility = Visibility.Collapsed;
                    RemoveIconButton.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading pack icon: {ex.Message}");
                    ShowNoIcon();
                }
            }
            else
            {
                ShowNoIcon();
            }
        }

        private void ShowNoIcon()
        {
            IconPreview.Source = null;
            IconPreview.Visibility = Visibility.Collapsed;
            IconPlaceholder.Visibility = Visibility.Visible;
            RemoveIconButton.Visibility = Visibility.Collapsed;
        }

        private int GetPackFormatFromVersion(string version)
        {
            return version switch
            {
                "1.21.5" or "1.21.4" => 22,
                "1.20.6" => 21,
                "1.20.4" => 20,
                "1.21.3" or "1.21.2" or "1.21.1" or "1.21" or "1.20.3" or "1.20.2" or "1.20.1" => 18,
                "1.19.4" => 13,
                "1.18.2" => 9,
                "1.17.1" => 8,
                "1.16.5" => 7,
                "1.15.2" => 6,
                "1.13.2" => 4,
                _ => 18 // Default to latest stable
            };
        }

        private void SetPackFormatComboBox(int packFormat)
        {
            foreach (ComboBoxItem item in PackFormatBox.Items)
            {
                if (item.Tag?.ToString() == packFormat.ToString())
                {
                    PackFormatBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void SetMinecraftVersionComboBox(string version)
        {
            foreach (ComboBoxItem item in MinecraftVersionBox.Items)
            {
                if (item.Content?.ToString() == version)
                {
                    MinecraftVersionBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void SetLicenseComboBox(string license)
        {
            if (string.IsNullOrEmpty(license)) return;

            // Try to find exact match first
            foreach (ComboBoxItem item in LicenseBox.Items)
            {
                if (item.Content?.ToString() == license)
                {
                    LicenseBox.SelectedItem = item;
                    return;
                }
            }

            // If no exact match, set the text directly (editable combobox)
            LicenseBox.Text = license;
        }

        public void SaveProperties_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPack == null) return;

            try
            {
                // Get values from UI
                var name = PackNameBox.Text?.Trim() ?? "";
                var modId = PackIdBox.Text?.Trim() ?? "";
                var description = DescriptionBox.Text?.Trim() ?? "";
                var authors = AuthorsBox.Text?.Trim() ?? "";
                var license = LicenseBox.Text?.Trim() ?? "";

                // Sanitize Mod ID
                modId = SanitizeId(modId);

                // Get pack format
                var packFormat = 18; // Default
                if (PackFormatBox.SelectedItem is ComboBoxItem formatItem && 
                    int.TryParse(formatItem.Tag?.ToString(), out var format))
                {
                    packFormat = format;
                }

                // Get Minecraft version
                var minecraftVersion = "1.21.4"; // Default
                if (MinecraftVersionBox.SelectedItem is ComboBoxItem versionItem)
                {
                    minecraftVersion = versionItem.Content?.ToString() ?? minecraftVersion;
                }

                // Validate required fields
                if (string.IsNullOrEmpty(name))
                {
                    ShowMessage("Pack name is required", "Validation Error");
                    return;
                }

                if (string.IsNullOrEmpty(modId))
                {
                    ShowMessage("Pack ID is required", "Validation Error");
                    return;
                }

                // Update pack data
                _currentPack.Name = name;
                _currentPack.ModId = modId;
                _currentPack.Description = description;
                _currentPack.PackFormat = packFormat;
                _currentPack.MinecraftVersion = minecraftVersion;

                // Save pack.mcmeta
                SavePackMeta(name, description, packFormat);

                // Save modrix.config
                SaveModrixConfig(name, modId, description, minecraftVersion, packFormat, authors, license);

                // Update the workspace title
                RefreshWorkspaceTitle();

                ShowMessage("Properties saved successfully!", "Success");
            }
            catch (Exception ex)
            {
                ShowMessage($"Failed to save properties: {ex.Message}", "Error");
            }
        }

        private void RefreshWorkspaceTitle()
        {
            try
            {
                var workspace = Application.Current.Windows
                    .OfType<ResourcePackWorkspace>()
                    .FirstOrDefault();

                if (workspace?.ViewModel != null && _currentPack != null)
                {
                    // Re-read the pack to get updated data
                    var manager = new ResourcePackTemplateManager();
                    var updatedPack = manager.ReadResourcePack(_currentPack.Location);
                    
                    // Update the workspace with the fresh data
                    workspace.ViewModel.LoadPack(updatedPack);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing workspace title: {ex.Message}");
            }
        }

        private void SavePackMeta(string name, string description, int packFormat)
        {
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

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(packMeta, options);
            File.WriteAllText(packMetaPath, json);
        }

        private void SaveModrixConfig(string name, string modId, string description, 
                                     string minecraftVersion, int packFormat, string authors, string license)
        {
            var configPath = Path.Combine(_currentPack.Location, "modrix.config");
            var configLines = new List<string>
            {
                $"ModType=Resource Pack",
                $"Name={name}",
                $"ModId={modId}",
                $"Description={description}",
                $"MinecraftVersion={minecraftVersion}",
                $"PackFormat={packFormat}",
                $"Version=1.0.0",
                $"IconPath=pack.png"
            };

            if (!string.IsNullOrEmpty(authors))
                configLines.Add($"Authors={authors}");
            
            if (!string.IsNullOrEmpty(license))
                configLines.Add($"License={license}");

            File.WriteAllLines(configPath, configLines);
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
                    
                    // Copy and convert to PNG if necessary
                    if (Path.GetExtension(dialog.FileName).ToLower() == ".png")
                    {
                        File.Copy(dialog.FileName, targetPath, true);
                    }
                    else
                    {
                        // Convert to PNG using WPF imaging
                        var bitmap = new BitmapImage(new Uri(dialog.FileName));
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        
                        using var fileStream = new FileStream(targetPath, FileMode.Create);
                        encoder.Save(fileStream);
                    }

                    // Update icon preview
                    LoadPackIcon();

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

                ShowNoIcon();
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
                PackNameBox.Text = "My Resource Pack";
                PackIdBox.Text = "my_resource_pack";
                DescriptionBox.Text = "A custom Minecraft resource pack created with Modrix";
                AuthorsBox.Text = "";
                LicenseBox.Text = "All Rights Reserved";

                // Reset to latest pack format and MC version
                SetPackFormatComboBox(22);
                SetMinecraftVersionComboBox("1.21.4");
            }
        }

        // Add refresh functionality
        public void RefreshProperties()
        {
            _isDataLoaded = false;
            LoadCurrentPack();
            if (_currentPack != null)
            {
                LoadPackProperties();
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

        private string SanitizeId(string input)
        {
            var safe = new string(input.ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-').ToArray());
            if (string.IsNullOrWhiteSpace(safe))
                safe = "resource_pack";
            return safe;
        }
    }
}