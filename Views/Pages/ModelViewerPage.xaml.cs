using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Modrix.ViewModels.Pages;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace Modrix.Views.Pages
{
    public partial class ModelViewerPage : Page
    {
        public ModelViewerViewModel ViewModel { get; }

        public ModelViewerPage(ModelViewerViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;
            
            InitializeComponent();
        }

        public void SetModelPath(string modelPath)
        {
            ViewModel.ModelPath = modelPath;
            ViewModel.ModelName = Path.GetFileName(modelPath);
        }

        private void ReloadModel_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(ViewModel.ModelPath))
            {
                var currentPath = ViewModel.ModelPath;
                ViewModel.ModelPath = null; // Reset
                ViewModel.ModelPath = currentPath; // Reload
            }
        }

        private void OpenInEditor_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(ViewModel.ModelPath) && File.Exists(ViewModel.ModelPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = ViewModel.ModelPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    ShowMessage($"Could not open model in editor: {ex.Message}", "Error");
                }
            }
        }

        private void ResetView_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Reset camera to default position
                if (Viewport3D?.Camera is System.Windows.Media.Media3D.PerspectiveCamera camera)
                {
                    camera.Position = new System.Windows.Media.Media3D.Point3D(1.5, 1.5, 1.5);
                    camera.LookDirection = new System.Windows.Media.Media3D.Vector3D(-1, -1, -1);
                    camera.UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 1, 0);
                    camera.FieldOfView = 45;
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Could not reset view: {ex.Message}", "Error");
            }
        }

        private void ExportScreenshot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Title = "Save Screenshot",
                    Filter = "PNG Image|*.png|JPEG Image|*.jpg",
                    DefaultExt = "png",
                    FileName = $"{Path.GetFileNameWithoutExtension(ViewModel.ModelName)}_screenshot"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    // Capture the viewport
                    var renderBitmap = new RenderTargetBitmap(
                        (int)Viewport3D.ActualWidth,
                        (int)Viewport3D.ActualHeight,
                        96, 96,
                        System.Windows.Media.PixelFormats.Pbgra32);
                    
                    renderBitmap.Render(Viewport3D);

                    // Save the image
                    BitmapEncoder encoder = saveDialog.FilterIndex == 1 
                        ? new PngBitmapEncoder() 
                        : new JpegBitmapEncoder();
                    
                    encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
                    
                    using (var fileStream = new FileStream(saveDialog.FileName, FileMode.Create))
                    {
                        encoder.Save(fileStream);
                    }

                    ShowMessage("Screenshot saved successfully!", "Export Complete");
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Could not export screenshot: {ex.Message}", "Error");
            }
        }

        private void WireframeToggle_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                // Toggle wireframe mode
                // Note: This would require modifying the materials in the 3D model
                // For now, we'll just show a message
                if (WireframeCheckBox.IsChecked == true)
                {
                    ShowMessage("Wireframe mode is not yet implemented in this version.", "Feature Coming Soon");
                    WireframeCheckBox.IsChecked = false;
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Could not toggle wireframe: {ex.Message}", "Error");
            }
        }

        private void CoordinateSystemToggle_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Viewport3D != null)
                {
                    Viewport3D.ShowCoordinateSystem = CoordinateSystemCheckBox.IsChecked == true;
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Could not toggle coordinate system: {ex.Message}", "Error");
            }
        }

        private void ViewCubeToggle_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Viewport3D != null)
                {
                    Viewport3D.ShowViewCube = ViewCubeCheckBox.IsChecked == true;
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Could not toggle view cube: {ex.Message}", "Error");
            }
        }

        private async void ShowMessage(string message, string title)
        {
            var messageBox = new MessageBox
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "OK"
            };
            await messageBox.ShowDialogAsync();
        }
    }
}