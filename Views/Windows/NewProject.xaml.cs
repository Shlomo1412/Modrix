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

            SetupEventHandlers();
            ValidateFields();
        }


        private void SetupEventHandlers()
        {
            ProjectNameBox.TextChanged += (s, e) =>
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

            

            ModTypeComboBox.SelectionChanged += (s, e) => ValidateFields();
            MinecraftVersionComboBox.SelectionChanged += (s, e) => ValidateFields();
        }

        private void ValidateFields()
        {
            AreFieldsValid = !string.IsNullOrWhiteSpace(ProjectNameBox.Text) &&
                           !string.IsNullOrWhiteSpace(ModIdBox.Text) &&
                           !string.IsNullOrWhiteSpace(PackageBox.Text) &&
                           
                           ModTypeComboBox.SelectedItem != null &&
                           MinecraftVersionComboBox.SelectedItem != null;
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

                
                if (Directory.Exists(ProjectData.Location))
                {
                    var checkFile = Path.Combine(ProjectData.Location, "build.gradle");
                    if (File.Exists(checkFile))
                    {
                        //MessageBox.Show(
                        //    $"Project created successfully at:\n{ProjectData.Location}",
                        //    "Success",
                        //    MessageBoxButton.OK,
                        //    MessageBoxImage.Information
                        //);
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

                new MessageBox
                {
                    Title = "Error",
                    Content = $"Failed to create project:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}"
                }.ShowDialog();

                
                try
                {
                    if (ProjectData?.Location != null && Directory.Exists(ProjectData.Location))
                    {
                        Directory.Delete(ProjectData.Location, true);
                    }
                }
                catch { }
            }
            finally
            {
                loadingWindow.Close();
            }
        }

        private async Task CreateModProjectAsync(LoadingProjectWindow loadingWindow)
        {
            ProjectData = new ModProjectData
            {
                Name = ProjectNameBox.Text,
                ModId = ModIdBox.Text,
                Package = PackageBox.Text,
                Location = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Modrix",
                    "Projects",
                    ModIdBox.Text
                ),
                IconPath = _selectedIconPath,
                ModType = ((ComboBoxItem)ModTypeComboBox.SelectedItem).Content.ToString(),
                MinecraftVersion = ((ComboBoxItem)MinecraftVersionComboBox.SelectedItem).Content.ToString(),
                Description = DescriptionBox.Text,
                Authors = AuthorsBox.Text,
                License = ((ComboBoxItem)LicenseComboBox.SelectedItem).Content.ToString(),
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
            else
            {
                var manager = new TemplateManager();
                await manager.FullSetupWithGradle(ProjectData, progress);
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
