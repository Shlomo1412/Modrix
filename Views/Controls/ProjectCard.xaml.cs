﻿using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Modrix.Models;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace Modrix.Views.Controls
{
    public partial class ProjectCard : UserControl
    {
        public static readonly DependencyProperty ProjectDataProperty =
            DependencyProperty.Register(
                "ProjectData",
                typeof(ModProjectData),
                typeof(ProjectCard),
                new PropertyMetadata(null, OnProjectDataChanged));

        public ModProjectData ProjectData
        {
            get => (ModProjectData)GetValue(ProjectDataProperty);
            set => SetValue(ProjectDataProperty, value);
        }

        public event RoutedEventHandler EditClicked;
        public event RoutedEventHandler DeleteClicked;
        public event RoutedEventHandler OpenFolderClicked;

        public ProjectCard()
        {
            InitializeComponent();

            EditButton.Click += (s, e) => EditClicked?.Invoke(this, e);
        }

        private void OptionsButton_Click(object sender, RoutedEventArgs e)
        {
            OptionsFlyout.IsOpen = true;
            e.Handled = true;
        }

        private void FlyoutDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            OptionsFlyout.IsOpen = false;
            DeleteClicked?.Invoke(this, e);
        }

        private void FlyoutOpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            OptionsFlyout.IsOpen = false;
            OpenFolderClicked?.Invoke(this, e);
        }

        private async void FlyoutOpenInIDEButton_Click(object sender, RoutedEventArgs e)
        {
            OptionsFlyout.IsOpen = false;
            if (ProjectData == null) return;
            var gradlePath = System.IO.Path.Combine(ProjectData.Location, "build.gradle");
            if (File.Exists(gradlePath))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = gradlePath,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    var msgBox = new MessageBox
                    {
                        Title = "Error",
                        Content = "Could not open build.gradle in IDE.",
                        PrimaryButtonText = "OK"
                    };
                    await msgBox.ShowDialogAsync();
                }
            }
            else
            {
                var msgBox = new MessageBox
                {
                    Title = "File Not Found",
                    Content = "build.gradle not found in project directory.",
                    PrimaryButtonText = "OK"
                };
                await msgBox.ShowDialogAsync();
            }
        }

        private static void OnProjectDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ProjectCard card && e.NewValue is ModProjectData data)
            {
                card.UpdateCard(data);
            }
        }

        private void UpdateCard(ModProjectData data)
        {
            ProjectNameText.Text = data.Name;
            ModTypeText.Text = data.ModType switch
            {
                "Fabric Mod" => "Fabric Mod",
                "Forge Mod" => "Forge Mod",
                _ => "Unknown Mod Type"
            };
            VersionText.Text = data.MinecraftVersion;

            string fullIconPath = Path.Combine(data.Location, data.IconPath);

            if (File.Exists(fullIconPath))
            {
                var bmp = new BitmapImage();
                using (var stream = File.OpenRead(fullIconPath))
                {
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = stream;
                    bmp.EndInit();
                    bmp.Freeze();
                }
                ProjectIconImage.Source = bmp;
                ProjectIconImage.Visibility = Visibility.Visible;
                DefaultIconText.Visibility = Visibility.Collapsed;
            }
            else
            {
                ProjectIconImage.Source = null;
                ProjectIconImage.Visibility = Visibility.Collapsed;
                DefaultIconText.Visibility = Visibility.Visible;

                DefaultIconText.Text = !string.IsNullOrEmpty(data.Name)
                    ? data.Name.Substring(0, 1).ToUpper()
                    : "?";
            }
        }
    }
}

