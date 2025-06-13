using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace Modrix.Views.Windows
{
    public partial class NewTextureDialog : FluentWindow
    {
        public string TextureName { get; private set; } = string.Empty;
        public int TextureWidth { get; private set; } = 16;
        public int TextureHeight { get; private set; } = 16;
        public bool UseTransparentBackground { get; private set; } = true;
        public Color BackgroundColor { get; private set; } = Colors.Transparent;
        public string SavedFilePath { get; private set; } = string.Empty;

        public NewTextureDialog()
        {
            InitializeComponent();
            TextureNameTextBox.Focus();

            // Set default color
            if (BackgroundColorPicker != null)
            {
                BackgroundColorPicker.SelectedColor = Colors.White;
            }
        }

        private void BackgroundTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorPickerPanel == null || BackgroundTypeComboBox == null)
                return;

            if (BackgroundTypeComboBox.SelectedIndex == 1) // Solid Color
            {
                ColorPickerPanel.Visibility = Visibility.Visible;
            }
            else // Transparent
            {
                ColorPickerPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TextureNameTextBox.Text))
            {
                var msgBox = new MessageBox
                {
                    Title = "Missing Name",
                    Content = "Please enter a texture name.",
                    PrimaryButtonText = "OK"
                };
                msgBox.ShowDialogAsync();
                return;
            }

            TextureName = TextureNameTextBox.Text;

            // Ensure texture name has .png extension
            if (!TextureName.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            {
                TextureName += ".png";
            }

            // Get dimensions
            TextureWidth = (int)WidthNumberBox.Value;
            TextureHeight = (int)HeightNumberBox.Value;

            // Check if transparent or colored background
            UseTransparentBackground = BackgroundTypeComboBox.SelectedIndex == 0;

            // Get background color if not transparent
            if (!UseTransparentBackground && BackgroundColorPicker != null)
            {
                // The BackgroundColorPicker.SelectedColor is already a Color, not a nullable type
                var selectedColor = BackgroundColorPicker.SelectedColor;

                // Default to white if no valid color (shouldn't happen but just in case)
                if (selectedColor.A == 0 && selectedColor.R == 0 &&
                    selectedColor.G == 0 && selectedColor.B == 0)
                {
                    BackgroundColor = Colors.White;
                }
                else
                {
                    BackgroundColor = selectedColor;
                }
            }

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        public WriteableBitmap CreateTextureImage()
        {
            // Create a new writable bitmap with the specified dimensions
            var bitmap = new WriteableBitmap(
                TextureWidth,
                TextureHeight,
                96, 96,
                PixelFormats.Pbgra32,
                null);

            // If we're using a solid background, fill the bitmap with that color
            if (!UseTransparentBackground)
            {
                bitmap.Lock();
                try
                {
                    // Create a byte array for the pixels (4 bytes per pixel: B, G, R, A)
                    var pixelData = new byte[TextureWidth * TextureHeight * 4];

                    // Fill with the selected color
                    for (int i = 0; i < pixelData.Length; i += 4)
                    {
                        pixelData[i] = BackgroundColor.B;     // Blue
                        pixelData[i + 1] = BackgroundColor.G; // Green
                        pixelData[i + 2] = BackgroundColor.R; // Red
                        pixelData[i + 3] = BackgroundColor.A; // Alpha
                    }

                    // Write the pixels to the bitmap
                    bitmap.WritePixels(
                        new Int32Rect(0, 0, TextureWidth, TextureHeight),
                        pixelData,
                        TextureWidth * 4,  // stride (bytes per row)
                        0);
                }
                finally
                {
                    bitmap.Unlock();
                }
            }

            return bitmap;
        }

        public bool SaveTexture(string directoryPath)
        {
            try
            {
                // Create the directory if it doesn't exist
                if (!Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath);

                // Create the full file path
                var filePath = Path.Combine(directoryPath, TextureName);

                // Create the texture image
                var bitmap = CreateTextureImage();

                // Save the image as a PNG file
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(fileStream);
                }

                SavedFilePath = filePath;
                return true;
            }
            catch (Exception ex)
            {
                var msgBox = new MessageBox
                {
                    Title = "Error",
                    Content = $"Failed to save texture: {ex.Message}",
                    PrimaryButtonText = "OK"
                };
                msgBox.ShowDialogAsync();
                return false;
            }
        }
    }
}