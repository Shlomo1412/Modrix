using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Modrix.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Modrix.Views.Pages
{
    public partial class TextureEditorPage : INavigableView<TextureEditorViewModel>
    {
        public TextureEditorViewModel ViewModel { get; }

        public TextureEditorPage(TextureEditorViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }

        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                ProcessPixelAction(e.GetPosition(PixelCanvas));
                ViewModel.IsDrawing = true;
            }
        }

        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(PixelCanvas);
            ViewModel.UpdateCursorPosition((int)pos.X, (int)pos.Y);

            if (e.LeftButton == MouseButtonState.Pressed && ViewModel.IsDrawing)
            {
                ProcessPixelAction(pos);
            }
        }

        private void Image_MouseUp(object sender, MouseButtonEventArgs e)
        {
            ViewModel.IsDrawing = false;
        }

        private void ProcessPixelAction(Point position)
        {
            Point relativePoint = PixelCanvas.TranslatePoint(position, PixelCanvas);
            // Adjust for zoom level
            int x = (int)(relativePoint.X / ViewModel.ZoomLevel);
            int y = (int)(relativePoint.Y / ViewModel.ZoomLevel);

            ViewModel.HandlePixelAction(x, y);
        }
    }
}