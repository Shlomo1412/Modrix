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
        private bool _pencilDragInProgress = false;
        private Point? _shapeStartPoint; // For line/rectangle tools
        private bool _initAnimationFlagApplied = false;

        public TextureEditorPage(TextureEditorViewModel viewModel, bool allowAnimation = false)
        {
            ViewModel = viewModel;
            InitializeComponent();
            DataContext = this; // Keep this as 'this' since we're binding to ViewModel property
            ViewModel.EnableAnimation(allowAnimation);
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
                case Key.L: // Line
                    if (ViewModel.SelectLineCommand.CanExecute(null))
                        ViewModel.SelectLineCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.R: // Rectangle
                    if (ViewModel.SelectRectangleCommand.CanExecute(null))
                        ViewModel.SelectRectangleCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.G: // Toggle Grid
                    if (ViewModel.ToggleGridCommand.CanExecute(null))
                        ViewModel.ToggleGridCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.OemPlus: // Zoom In
                case Key.Add:
                    if (ViewModel.ZoomInCommand.CanExecute(null))
                        ViewModel.ZoomInCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.OemMinus: // Zoom Out
                case Key.Subtract:
                    if (ViewModel.ZoomOutCommand.CanExecute(null))
                        ViewModel.ZoomOutCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.S: // Save
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        if (ViewModel.SaveCommand.CanExecute(null))
                            ViewModel.SaveCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
                case Key.Z: // Undo
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        if (ViewModel.UndoCommand.CanExecute(null))
                            ViewModel.UndoCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
                case Key.Y: // Redo
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        if (ViewModel.RedoCommand.CanExecute(null))
                            ViewModel.RedoCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
                case Key.Delete: // Clear canvas
                    if (ViewModel.ClearCommand.CanExecute(null))
                        ViewModel.ClearCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Space: // Play/Pause animation
                    if (ViewModel.PlayPauseCommand.CanExecute(null))
                        ViewModel.PlayPauseCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.PageDown:
                case Key.Right:
                    if (ViewModel.NextFrameCommand.CanExecute(null))
                        ViewModel.NextFrameCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.PageUp:
                case Key.Left:
                    if (ViewModel.PreviousFrameCommand.CanExecute(null))
                        ViewModel.PreviousFrameCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }

        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var pixelCoords = GetPixelCoordinates(e.GetPosition(PixelCanvas));
                // If pencil tool, start drag and push undo state ONCE
                if (ViewModel.CurrentTool == EditorTool.Pencil)
                {
                    if (!_pencilDragInProgress)
                    {
                        ViewModel.PushUndoState();
                        _pencilDragInProgress = true;
                    }
                }
                else if (ViewModel.CurrentTool == EditorTool.Line || ViewModel.CurrentTool == EditorTool.Rectangle)
                {
                    // Start shape drawing
                    ViewModel.PushUndoState();
                    _shapeStartPoint = new Point(pixelCoords.Item1, pixelCoords.Item2);
                }
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
                if (ViewModel.CurrentTool == EditorTool.Pencil)
                {
                    ProcessPixelAction(pixelCoords);
                }
                // For line/rectangle we only draw on mouse up (final position)
            }
        }

        private void Image_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel.IsDrawing)
            {
                var pixelCoords = GetPixelCoordinates(e.GetPosition(PixelCanvas));
                if ((ViewModel.CurrentTool == EditorTool.Line || ViewModel.CurrentTool == EditorTool.Rectangle) && _shapeStartPoint.HasValue)
                {
                    int x0 = (int)_shapeStartPoint.Value.X;
                    int y0 = (int)_shapeStartPoint.Value.Y;
                    int x1 = pixelCoords.Item1;
                    int y1 = pixelCoords.Item2;

                    if (ViewModel.CurrentTool == EditorTool.Line)
                    {
                        ViewModel.DrawLine(x0, y0, x1, y1, ViewModel.SelectedColor);
                    }
                    else if (ViewModel.CurrentTool == EditorTool.Rectangle)
                    {
                        ViewModel.DrawFilledRectangle(x0, y0, x1, y1, ViewModel.SelectedColor);
                    }
                }
            }
            ViewModel.IsDrawing = false;
            if (_pencilDragInProgress)
            {
                _pencilDragInProgress = false;
            }
            _shapeStartPoint = null;
        }

        private (int x, int y) GetPixelCoordinates(Point mousePosition)
        {
            var scrollViewer = FindParent<ScrollViewer>(PixelCanvas);
            double scrollX = scrollViewer?.HorizontalOffset ?? 0;
            double scrollY = scrollViewer?.VerticalOffset ?? 0;
            int x = (int)(mousePosition.X + scrollX);
            int y = (int)(mousePosition.Y + scrollY);
            return (x, y);
        }

        private void ProcessPixelAction((int x, int y) coordinates)
        {
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
