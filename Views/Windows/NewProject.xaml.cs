using System;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Modrix.Services;
using Modrix.Models;

using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace Modrix.Views.Windows
{
    public partial class NewProject : FluentWindow, INotifyPropertyChanged
    {
        private readonly Regex modIdRegex = new("[^a-z0-9_]"); // Only lowercase letters, numbers and underscore
        private readonly Regex packageRegex = new("[^a-z0-9._]"); // Only lowercase letters, numbers, dots and underscore
        private bool isAutoCompleting = false;
        private bool _areFieldsValid;

        private string? _selectedIconPath;
        private readonly string[] _supportedImageExtensions = { ".png" };

        public ModProjectData? ProjectData { get; private set; }

        private readonly TemplateManager _templateManager = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool AreFieldsValid
        {
            get => _areFieldsValid;
            private set
            {
                if (_areFieldsValid != value)
                {
                    _areFieldsValid = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AreFieldsValid)));
                }
            }
        }

        public NewProject(ModProjectData? existingProject = null)
        {
            InitializeComponent();
            DataContext = this;

            // Set default selection for ModTypeComboBox
            if (existingProject != null)
            {
                ProjectNameBox.Text = existingProject.Name;
                ModIdBox.Text = existingProject.ModId;
                PackageBox.Text = existingProject.Package;

                _selectedIconPath = existingProject.IconPath;

                if (existingProject.IconPath != null)
                {
                    IconPreview.Source = new BitmapImage(new Uri(existingProject.IconPath));
                    SelectIconButton.Visibility = Visibility.Collapsed;
                    IconPreview.Visibility = Visibility.Visible;
                    IconControls.Visibility = Visibility.Visible;
                }

                // Set ComboBox selections
                ModTypeComboBox.SelectedItem = ModTypeComboBox.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(i => i.Content.ToString() == existingProject.ModType);
                MinecraftVersionComboBox.SelectedItem = MinecraftVersionComboBox.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(i => i.Content.ToString() == existingProject.MinecraftVersion);
                LicenseComboBox.SelectedItem = LicenseComboBox.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(i => i.Content.ToString() == existingProject.License);

                DescriptionBox.Text = existingProject.Description;
                AuthorsBox.Text = existingProject.Authors;
            }
            else
            {
                // Set default selection to Fabric Mod
                ModTypeComboBox.SelectedIndex = 1; // Index of Fabric Mod
            }

            SetupEventHandlers();
            ValidateFields();
        }

        private void SetupEventHandlers()
        {
            // Only validate on text change
            ProjectNameBox.TextChanged += (s, e) =>
            {
                ValidateFields();
            };
            // Perform auto-complete when name field loses focus
            ProjectNameBox.LostFocus += (s, e) =>
            {
                ProjectNameBox_TextChanged(s, e);
                ValidateFields();
            };

            ModIdBox.TextChanged += (s, e) =>
            {
                ModIdBox_TextChanged(s, e);
                ValidateFields();
            };

            PackageBox.TextChanged += (s, e) =>
            {
                PackageBox_TextChanged(s, e);
                ValidateFields();
            };

            ModTypeComboBox.SelectionChanged += ModTypeComboBox_SelectionChanged;
            MinecraftVersionComboBox.SelectionChanged += (s, e) => ValidateFields();
            LicenseComboBox.SelectionChanged += (s, e) => ValidateFields();
            
            // Resource pack specific handlers
            var resourcePackVersionCombo = this.FindName("ResourcePackMinecraftVersionComboBox") as ComboBox;
            if (resourcePackVersionCombo != null)
            {
                resourcePackVersionCombo.SelectionChanged += ResourcePackMinecraftVersionComboBox_SelectionChanged;
                resourcePackVersionCombo.SelectionChanged += (s, e) => ValidateFields();
            }
        }

        private void ModTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModTypeComboBox?.SelectedItem is ComboBoxItem selectedItem)
            {
                bool isResourcePack = selectedItem.Content.ToString() == "Resource Pack";
                
                if (ModSpecificFields != null)
                    ModSpecificFields.Visibility = isResourcePack ? Visibility.Collapsed : Visibility.Visible;

                if (ResourcePackSpecificFields != null)
                    ResourcePackSpecificFields.Visibility = isResourcePack ? Visibility.Visible : Visibility.Collapsed;

                // Auto-select pack format based on Minecraft version for resource packs
                if (isResourcePack)
                {
                    var resourcePackVersionCombo = this.FindName("ResourcePackMinecraftVersionComboBox") as ComboBox;
                    var packFormatDisplay = this.FindName("PackFormatDisplayBox") as Wpf.Ui.Controls.TextBox;

                    if (resourcePackVersionCombo != null)
                    {
                        resourcePackVersionCombo.SelectedIndex = 0; // Default to latest version
                        ResourcePackMinecraftVersionComboBox_SelectionChanged(resourcePackVersionCombo, null);
                    }
                }

                ValidateFields();
            }
        }

        private void ResourcePackMinecraftVersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var packFormatDisplay = this.FindName("PackFormatDisplayBox") as Wpf.Ui.Controls.TextBox;

            if (comboBox?.SelectedItem is ComboBoxItem selectedItem && packFormatDisplay != null)
            {
                var packFormat = selectedItem.Tag?.ToString();
                var version = selectedItem.Content?.ToString();
                
                packFormatDisplay.Text = $"Pack Format {packFormat} (Minecraft {version})";
            }
        }

        private void ValidateFields()
        {
            if (ModTypeComboBox?.SelectedItem is ComboBoxItem selectedItem)
            {
                bool isResourcePack = selectedItem.Content.ToString() == "Resource Pack";
                
                if (isResourcePack)
                {
                    // Resource pack validation
                    var resourcePackVersionCombo = this.FindName("ResourcePackMinecraftVersionComboBox") as ComboBox;
                    
                    AreFieldsValid = !string.IsNullOrWhiteSpace(ProjectNameBox?.Text) &&
                                   !string.IsNullOrWhiteSpace(ModIdBox?.Text) &&
                                   resourcePackVersionCombo?.SelectedItem != null;
                }
                else
                {
                    // Regular mod validation
                    AreFieldsValid = !string.IsNullOrWhiteSpace(ProjectNameBox?.Text) &&
                                   !string.IsNullOrWhiteSpace(ModIdBox?.Text) &&
                                   !string.IsNullOrWhiteSpace(PackageBox?.Text) &&
                                   MinecraftVersionComboBox?.SelectedItem != null &&
                                   LicenseComboBox?.SelectedItem != null;
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            var loadingWindow = new LoadingProjectWindow { Owner = this };

            try
            {
                loadingWindow.Show();
                await CreateModProjectAsync(loadingWindow);

                var projectType = ((ComboBoxItem)ModTypeComboBox.SelectedItem).Content.ToString();
                if (projectType == "Resource Pack")
                {
                    // For resource packs, check for pack.mcmeta instead of build.gradle
                    var checkFile = Path.Combine(ProjectData.Location, "pack.mcmeta");
                    if (File.Exists(checkFile))
                    {
                        Close();
                    }
                    else
                    {
                        throw new Exception("Critical files missing - resource pack creation failed");
                    }
                }
                else if (Directory.Exists(ProjectData.Location))
                {
                    var checkFile = Path.Combine(ProjectData.Location, "build.gradle");
                    if (File.Exists(checkFile))
                    {
                        Close();
                    }
                    else
                    {
                        throw new Exception("Critical files missing - project creation failed");
                    }
                }
            }
            catch (Exception ex)
            {
                loadingWindow.Close();

                try
                {
                    if (ProjectData?.Location != null && Directory.Exists(ProjectData.Location))
                    {
                        Directory.Delete(ProjectData.Location, true);
                    }
                }
                catch { }
                
                var msgBox = new MessageBox
                {
                    Title = "Error",
                    Content = $"Failed to create project: {ex.Message}"
                };
                await msgBox.ShowDialogAsync();
            }
            finally
            {
                loadingWindow.Close();
            }
        }

        private async Task CreateModProjectAsync(LoadingProjectWindow loadingWindow)
        {
            var projectType = ((ComboBoxItem)ModTypeComboBox.SelectedItem).Content.ToString();
            
            string minecraftVersion;
            if (projectType == "Resource Pack")
            {
                var resourcePackVersionCombo = this.FindName("ResourcePackMinecraftVersionComboBox") as ComboBox;
                minecraftVersion = ((ComboBoxItem)resourcePackVersionCombo.SelectedItem).Content.ToString();
            }
            else
            {
                minecraftVersion = ((ComboBoxItem)MinecraftVersionComboBox.SelectedItem).Content.ToString();
            }
            
            ProjectData = new ModProjectData
            {
                Name = ProjectNameBox.Text,
                ModId = ModIdBox.Text,
                Package = projectType == "Resource Pack" ? "" : PackageBox.Text,
                Location = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Modrix",
                    "Projects",
                    ModIdBox.Text
                ),
                IncludeReadme = IncludeReadmeCheckbox.IsChecked ?? false,
                IconPath = _selectedIconPath,
                ModType = projectType,
                MinecraftVersion = minecraftVersion,
                Description = projectType == "Resource Pack" ? 
                    (this.FindName("ResourcePackDescriptionBox") as Wpf.Ui.Controls.TextBox)?.Text ?? DescriptionBox.Text : 
                    DescriptionBox.Text,
                Authors = projectType == "Resource Pack" ? 
                    (this.FindName("ResourcePackAuthorsBox") as Wpf.Ui.Controls.TextBox)?.Text ?? AuthorsBox.Text : 
                    AuthorsBox.Text,
                License = projectType == "Resource Pack" ? "All Rights Reserved" : ((ComboBoxItem)LicenseComboBox.SelectedItem).Content.ToString(),
                Version = "1.0.0"
            };

            var progress = new Progress<(string Message, int Progress)>(update =>
            {
                loadingWindow.UpdateStatus(update.Message, update.Progress);
            });

            if (ProjectData.ModType == "Fabric Mod")
            {
                var manager = new FabricTemplateManager();
                await manager.FullSetupWithGradle(ProjectData, progress);
            }
            else if (ProjectData.ModType == "Forge Mod")
            {
                var manager = new ForgeTemplateManager();
                await manager.FullSetupWithGradle(ProjectData, progress);
            }
            else if (ProjectData.ModType == "Resource Pack")
            {
                var manager = new ResourcePackTemplateManager();
                await manager.FullSetup(ProjectData, progress);

                // Auto-extract assets for the selected Minecraft version
                try
                {
                    ((IProgress<(string Message, int Progress)>)progress).Report(("Preparing to extract Minecraft assets...", 90));
                    
                    var assetExtractor = new MinecraftAssetExtractor();
                    var extractProgress = new Progress<string>(status => 
                    {
                        ((IProgress<(string Message, int Progress)>)progress).Report((status, 95));
                    });
                    
                    // Start asset extraction in background (don't wait for completion to avoid blocking UI)
                    _ = Task.Run(async () =>
                    {
                        await assetExtractor.ExtractAssetsForVersion(minecraftVersion, extractProgress);
                    });
                    
                    ((IProgress<(string Message, int Progress)>)progress).Report(("Resource pack created successfully! Assets will be extracted in the background.", 100));
                }
                catch (Exception ex)
                {
                    // Don't fail the entire project creation if asset extraction fails
                    System.Diagnostics.Debug.WriteLine($"Asset extraction failed: {ex.Message}");
                    ((IProgress<(string Message, int Progress)>)progress).Report(("Resource pack created successfully! You can extract assets later from the Textures page.", 100));
                }
            }
            else
            {
                throw new ArgumentException($"Unsupported project type: {ProjectData.ModType}");
            }
        }

        private void ProjectNameBox_TextChanged(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ProjectNameBox.Text) || isAutoCompleting)
                return;

            isAutoCompleting = true;

            // Auto-generate ModID
            string modId = ProjectNameBox.Text.ToLower()
                .Replace(" ", "_")
                .Replace("-", "_");
            modId = modIdRegex.Replace(modId, "");

            if (string.IsNullOrEmpty(ModIdBox.Text))
                ModIdBox.Text = modId;

            // Auto-generate Package only if it's empty or contains the default value
            if (string.IsNullOrEmpty(PackageBox.Text) || PackageBox.Text == "net.modrix.mymod")
                PackageBox.Text = $"net.modrix.{modId}";

            isAutoCompleting = false;
        }

        private void ModIdBox_TextChanged(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ModIdBox.Text) || isAutoCompleting)
                return;

            isAutoCompleting = true;
            int caretIndex = ModIdBox.CaretIndex;

            // Convert spaces to underscores and apply other filters
            string filtered = ModIdBox.Text.ToLower()
                .Replace(" ", "_")
                .Replace("-", "_");
            filtered = modIdRegex.Replace(filtered, "");

            if (filtered != ModIdBox.Text)
            {
                ModIdBox.Text = filtered;
                ModIdBox.CaretIndex = Math.Min(caretIndex, filtered.Length);
            }

            isAutoCompleting = false;
        }

        private void PackageBox_TextChanged(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PackageBox.Text) || isAutoCompleting)
                return;

            isAutoCompleting = true;
            int caretIndex = PackageBox.CaretIndex;

            // Convert spaces to dots and apply other filters
            string filtered = PackageBox.Text.ToLower()
                .Replace(" ", ".")
                .Replace("-", ".");
            filtered = packageRegex.Replace(filtered, "");

            if (filtered != PackageBox.Text)
            {
                PackageBox.Text = filtered;
                PackageBox.CaretIndex = Math.Min(caretIndex, filtered.Length);
            }

            isAutoCompleting = false;
        }

        private void SelectIconButton_Click(object sender, RoutedEventArgs e)
        {
            SelectIcon();
        }

        private void SwitchIconButton_Click(object sender, RoutedEventArgs e)
        {
            SelectIcon();
        }

        private void RemoveIconButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedIconPath = null;
            IconPreview.Source = null;

            // Update UI visibility
            SelectIconButton.Visibility = Visibility.Visible;
            IconPreview.Visibility = Visibility.Collapsed;
            IconControls.Visibility = Visibility.Collapsed;
        }

        private async void SelectIcon()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Project Icon",
                Filter = "PNG Images|*.png",
                FilterIndex = 1
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // Load and validate the image
                    var image = new BitmapImage(new Uri(dialog.FileName));

                    // Store the path and update the preview
                    _selectedIconPath = dialog.FileName;
                    IconPreview.Source = image;

                    // Update UI visibility
                    SelectIconButton.Visibility = Visibility.Collapsed;
                    IconPreview.Visibility = Visibility.Visible;
                    IconControls.Visibility = Visibility.Visible;
                }
                catch (Exception)
                {
                    var msgBox = new MessageBox
                    {
                        Title = "Error Loading Image",
                        Content = "Failed to load the selected image. Please ensure it's a valid PNG file."
                    };

                    await msgBox.ShowDialogAsync();
                }
            }
        }
    }
}
