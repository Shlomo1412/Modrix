using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Modrix.Models;
using Modrix.Views.Windows;

namespace Modrix.Views.Controls
{
    public partial class ItemPicker : UserControl
    {
        public static readonly DependencyProperty SelectedItemPathProperty =
            DependencyProperty.Register(
                nameof(SelectedItemPath),
                typeof(string),
                typeof(ItemPicker),
                new PropertyMetadata(null, OnSelectedItemPathChanged));

        public static readonly DependencyProperty ProjectDataProperty =
            DependencyProperty.Register(
                nameof(ProjectData),
                typeof(ModProjectData),
                typeof(ItemPicker),
                new PropertyMetadata(null));

        public static readonly DependencyProperty MinecraftVersionProperty =
            DependencyProperty.Register(
                nameof(MinecraftVersion),
                typeof(string),
                typeof(ItemPicker),
                new PropertyMetadata("1.20.1"));

        public static readonly DependencyProperty ItemTypeProperty =
            DependencyProperty.Register(
                nameof(ItemType),
                typeof(ItemPickerType),
                typeof(ItemPicker),
                new PropertyMetadata(ItemPickerType.Both));

        public string? SelectedItemPath
        {
            get => (string?)GetValue(SelectedItemPathProperty);
            set => SetValue(SelectedItemPathProperty, value);
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

        public ItemPickerType ItemType
        {
            get => (ItemPickerType)GetValue(ItemTypeProperty);
            set => SetValue(ItemTypeProperty, value);
        }

        public event EventHandler<string?>? ItemSelected;

        public ItemPicker()
        {
            InitializeComponent();
        }

        private static void OnSelectedItemPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ItemPicker picker)
            {
                picker.UpdateItemDisplay();
            }
        }

        private void UpdateItemDisplay()
        {
            if (string.IsNullOrEmpty(SelectedItemPath) || !File.Exists(SelectedItemPath))
            {
                // Show default state
                DefaultContent.Visibility = Visibility.Visible;
                ItemImage.Visibility = Visibility.Collapsed;
                ClearButton.Visibility = Visibility.Collapsed;
                ItemImage.Source = null;
            }
            else
            {
                try
                {
                    // Load and display item texture
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(SelectedItemPath);
                    bitmap.DecodePixelWidth = 72; // Optimize for display size
                    bitmap.EndInit();
                    bitmap.Freeze();

                    ItemImage.Source = bitmap;
                    
                    // Show item state
                    DefaultContent.Visibility = Visibility.Collapsed;
                    ItemImage.Visibility = Visibility.Visible;
                    ClearButton.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading item texture: {ex.Message}");
                    
                    // Fall back to default state on error
                    DefaultContent.Visibility = Visibility.Visible;
                    ItemImage.Visibility = Visibility.Collapsed;
                    ClearButton.Visibility = Visibility.Collapsed;
                    ItemImage.Source = null;
                }
            }
        }

        private async void ItemPickerCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                var dialog = new ItemSelectionDialog(ProjectData, MinecraftVersion, ItemType)
                {
                    Owner = Window.GetWindow(this)
                };

                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedItemPath))
                {
                    SelectedItemPath = dialog.SelectedItemPath;
                    ItemSelected?.Invoke(this, SelectedItemPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening item selection dialog: {ex.Message}");
                await ShowErrorMessage($"Error opening item selection: {ex.Message}");
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true; // Prevent the card click event from firing
            
            SelectedItemPath = null;
            UpdateItemDisplay();
            ItemSelected?.Invoke(this, null);
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

    public enum ItemPickerType
    {
        Items,
        Blocks,
        Both
    }
}