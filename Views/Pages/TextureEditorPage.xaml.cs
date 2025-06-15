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
        private Point _lastProcessedPoint;

        public TextureEditorPage(TextureEditorViewModel viewModel)
        {
            ViewModel = viewModel;
            InitializeComponent();
            DataContext = this; // Keep this as 'this' since we're binding to ViewModel property
            
            // Set up keyboard event handling
            this.Focusable = true;
            this.PreviewKeyDown += TextureEditorPage_PreviewKeyDown;
        }

        private void TextureEditorPage_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Handle keyboard shortcuts
            switch (e.Key)
            {
                case Key.P: // Pencil
                    if (ViewModel.SelectPencilCommand.CanExecute(null))
                        ViewModel.SelectPencilCommand.Execute(null);
                    e.Handled = true;
                    break;
                    
                case Key.E: // Eraser
                    if (ViewModel.SelectEraserCommand.CanExecute(null))
                        ViewModel.SelectEraserCommand.Execute(null);
                    e.Handled = true;
                    break;
                    
                case Key.B: // Bucket Fill
                    if (ViewModel.SelectBucketCommand.CanExecute(null))
                        ViewModel.SelectBucketCommand.Execute(null);
                    e.Handled = true;
                    break;
                    
                case Key.I: // Color Picker
                    if (ViewModel.SelectPickerCommand.CanExecute(null))
                        ViewModel.SelectPickerCommand.Execute(null);
                    e.Handled = true;
                    break;
                    
                case Key.G: // Toggle Grid
                    if (ViewModel.ToggleGridCommand.CanExecute(null))
                        ViewModel.ToggleGridCommand.Execute(null);
                    e.Handled = true;
                    break;
                    
                case Key.OemPlus: // Zoom In (also works with '+' key)
                case Key.Add:
                    if (ViewModel.ZoomInCommand.CanExecute(null))
                        ViewModel.ZoomInCommand.Execute(null);
                    e.Handled = true;
                    break;
                    
                case Key.OemMinus: // Zoom Out (also works with '-' key)
                case Key.Subtract:
                    if (ViewModel.ZoomOutCommand.CanExecute(null))
                        ViewModel.ZoomOutCommand.Execute(null);
                    e.Handled = true;
                    break;

                case Key.S: // Save (Ctrl+S)
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        if (ViewModel.SaveCommand.CanExecute(null))
                            ViewModel.SaveCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;

                case Key.Z: // Undo (Ctrl+Z)
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        if (ViewModel.UndoCommand.CanExecute(null))
                            ViewModel.UndoCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;

                case Key.Y: // Redo (Ctrl+Y)
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        if (ViewModel.RedoCommand.CanExecute(null))
                            ViewModel.RedoCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var pixelCoords = GetPixelCoordinates(e.GetPosition(PixelCanvas));
                ProcessPixelAction(pixelCoords);
                ViewModel.IsDrawing = true;
            }
        }

        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            var pixelCoords = GetPixelCoordinates(e.GetPosition(PixelCanvas));
            ViewModel.UpdateCursorPosition(pixelCoords.Item1, pixelCoords.Item2);

            if (e.LeftButton == MouseButtonState.Pressed && ViewModel.IsDrawing)
            {
                ProcessPixelAction(pixelCoords);
            }
        }

        private void Image_MouseUp(object sender, MouseButtonEventArgs e)
        {
            ViewModel.IsDrawing = false;
        }

        private (int x, int y) GetPixelCoordinates(Point mousePosition)
        {
            // Get the scroll viewer's offset
            var scrollViewer = FindParent<ScrollViewer>(PixelCanvas);
            double scrollX = scrollViewer?.HorizontalOffset ?? 0;
            double scrollY = scrollViewer?.VerticalOffset ?? 0;

            // Do NOT divide by zoom level, since LayoutTransform already applies it
            int x = (int)(mousePosition.X + scrollX);
            int y = (int)(mousePosition.Y + scrollY);

            return (x, y);
        }

        private void ProcessPixelAction((int x, int y) coordinates)
        {
            // Ensure we're within the image bounds
            if (coordinates.x >= 0 && coordinates.x < ViewModel.ImageWidth && 
                coordinates.y >= 0 && coordinates.y < ViewModel.ImageHeight)
            {
                ViewModel.HandlePixelAction(coordinates.x, coordinates.y);
            }
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null)
                return null;

            if (parentObject is T parent)
                return parent;

            return FindParent<T>(parentObject);
        }

        // Handle Ctrl+Scroll for zoom in/out
        private void Page_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (e.Delta > 0)
                {
                    if (ViewModel.ZoomInCommand.CanExecute(null))
                        ViewModel.ZoomInCommand.Execute(null);
                }
                else if (e.Delta < 0)
                {
                    if (ViewModel.ZoomOutCommand.CanExecute(null))
                        ViewModel.ZoomOutCommand.Execute(null);
                }
                e.Handled = true;
            }
        }
    }
}
