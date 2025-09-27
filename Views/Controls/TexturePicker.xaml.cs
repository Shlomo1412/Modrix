using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Modrix.Models;
using Modrix.Views.Windows;

namespace Modrix.Views.Controls
{
    public partial class TexturePicker : UserControl
    {
        public static readonly DependencyProperty SelectedTexturePathProperty =
            DependencyProperty.Register(
                nameof(SelectedTexturePath),
                typeof(string),
                typeof(TexturePicker),
                new PropertyMetadata(null, OnSelectedTexturePathChanged));

        public static readonly DependencyProperty ProjectDataProperty =
            DependencyProperty.Register(
                nameof(ProjectData),
                typeof(ModProjectData),
                typeof(TexturePicker),
                new PropertyMetadata(null));

        public static readonly DependencyProperty MinecraftVersionProperty =
            DependencyProperty.Register(
                nameof(MinecraftVersion),
                typeof(string),
                typeof(TexturePicker),
                new PropertyMetadata("1.20.1"));

        public string? SelectedTexturePath
        {
            get => (string?)GetValue(SelectedTexturePathProperty);
            set => SetValue(SelectedTexturePathProperty, value);
        }

        public ModProjectData? ProjectData
        {
            get => (ModProjectData?)GetValue(ProjectDataProperty);
            set => SetValue(ProjectDataProperty, value);
        }

        public string MinecraftVersion
        {
            get => (string)GetValue(MinecraftVersionProperty);
            set => SetValue(MinecraftVersionProperty, value);
        }

        public event EventHandler<string?>? TextureSelected;

        public TexturePicker()
        {
            InitializeComponent();
        }

        private static void OnSelectedTexturePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TexturePicker picker)
            {
                picker.UpdateTextureDisplay();
            }
        }

        private void UpdateTextureDisplay()
        {
            if (string.IsNullOrEmpty(SelectedTexturePath) || !File.Exists(SelectedTexturePath))
            {
                // Show default state
                DefaultContent.Visibility = Visibility.Visible;
                TextureImage.Visibility = Visibility.Collapsed;
                ClearButton.Visibility = Visibility.Collapsed;
                TextureImage.Source = null;
            }
            else
            {
                try
                {
                    // Load and display texture
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(SelectedTexturePath);
                    bitmap.DecodePixelWidth = 72; // Optimize for display size
                    bitmap.EndInit();
                    bitmap.Freeze();

                    TextureImage.Source = bitmap;
                    
                    // Show texture state
                    DefaultContent.Visibility = Visibility.Collapsed;
                    TextureImage.Visibility = Visibility.Visible;
                    ClearButton.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading texture: {ex.Message}");
                    
                    // Fall back to default state on error
                    DefaultContent.Visibility = Visibility.Visible;
                    TextureImage.Visibility = Visibility.Collapsed;
                    ClearButton.Visibility = Visibility.Collapsed;
                    TextureImage.Source = null;
                }
            }
        }

        private async void TexturePickerCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                var dialog = new TextureSelectionDialog(ProjectData, MinecraftVersion)
                {
                    Owner = Window.GetWindow(this)
                };

                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedTexturePath))
                {
                    SelectedTexturePath = dialog.SelectedTexturePath;
                    TextureSelected?.Invoke(this, SelectedTexturePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening texture selection dialog: {ex.Message}");
                await ShowErrorMessage($"Error opening texture selection: {ex.Message}");
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true; // Prevent the card click event from firing
            
            SelectedTexturePath = null;
            UpdateTextureDisplay();
            TextureSelected?.Invoke(this, null);
        }

        private async System.Threading.Tasks.Task ShowErrorMessage(string message)
        {
            var msgBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = "Error",
                Content = message,
                PrimaryButtonText = "OK"
            };
            await msgBox.ShowDialogAsync();
        }
    }
}