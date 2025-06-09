using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Modrix.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;
using Modrix.Views.Windows;

namespace Modrix.Views.Pages
{
    public class ZoomMultiplierConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int dimension)
            {
                var zoomLevel = Application.Current.Windows[0]?.DataContext is MainWindow mainWindow 
                    ? mainWindow.GetType().GetProperty("ViewModel")?.GetValue(mainWindow) is TextureEditorViewModel vm 
                        ? vm.ZoomLevel 
                        : 5
                    : 5;
                return dimension * zoomLevel;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class TextureEditorPage : INavigableView<TextureEditorViewModel>
    {
        public TextureEditorViewModel ViewModel { get; }

        public TextureEditorPage(TextureEditorViewModel viewModel)
        {
            ViewModel = viewModel;
            InitializeComponent();
            DataContext = this; // Keep this as 'this' since we're binding to ViewModel property
        }

        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var image = sender as FrameworkElement;
                if (image != null)
                {
                    var pos = e.GetPosition(image);
                    ProcessPixelAction(pos);
                    ViewModel.IsDrawing = true;
                }
            }
        }

        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            var image = sender as FrameworkElement;
            if (image != null)
            {
                var pos = e.GetPosition(image);
                
                // Convert position to actual pixel coordinates for status display
                int pixelX = (int)(pos.X / ViewModel.ZoomLevel);
                int pixelY = (int)(pos.Y / ViewModel.ZoomLevel);
                ViewModel.UpdateCursorPosition(pixelX, pixelY);

                if (e.LeftButton == MouseButtonState.Pressed && ViewModel.IsDrawing)
                {
                    ProcessPixelAction(pos);
                }
            }
        }

        private void Image_MouseUp(object sender, MouseButtonEventArgs e)
        {
            ViewModel.IsDrawing = false;
        }

        private void ProcessPixelAction(Point position)
        {
            if (!(PixelCanvas.Source is WriteableBitmap bitmap))
                return;

            // Convert position to actual pixel coordinates
            int pixelX = (int)(position.X / ViewModel.ZoomLevel);
            int pixelY = (int)(position.Y / ViewModel.ZoomLevel);

            // Ensure we're within the image bounds
            if (pixelX >= 0 && pixelX < ViewModel.ImageWidth && 
                pixelY >= 0 && pixelY < ViewModel.ImageHeight)
            {
                ViewModel.HandlePixelAction(pixelX, pixelY);
            }
        }
    }
}