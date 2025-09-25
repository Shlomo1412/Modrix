using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;
using Modrix.Services;
using Modrix.Views.Windows;

namespace Modrix.Views.Pages.ResourcePack
{
    public partial class TextureViewerWindow : FluentWindow
    {
        private readonly TexturesPage.TextureItem _textureItem;

        public TextureViewerWindow(TexturesPage.TextureItem textureItem)
        {
            _textureItem = textureItem;
            InitializeViewer();
        }

        private void InitializeViewer()
        {
            Title = $"Texture Viewer - {_textureItem.Name}";
            Width = 600;
            Height = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var mainGrid = new System.Windows.Controls.Grid();
            mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

            // Header
            var headerPanel = new System.Windows.Controls.StackPanel 
            { 
                Margin = new Thickness(20, 20, 20, 10),
                Orientation = System.Windows.Controls.Orientation.Vertical
            };

            var titleBlock = new System.Windows.Controls.TextBlock
            {
                Text = _textureItem.Name,
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var infoPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            };

            var categoryText = new System.Windows.Controls.TextBlock
            {
                Text = $"Category: {_textureItem.Category}",
                Margin = new Thickness(0, 0, 20, 0)
            };

            var sizeText = new System.Windows.Controls.TextBlock
            {
                Text = $"Size: {_textureItem.Size}",
                Margin = new Thickness(0, 0, 20, 0)
            };

            var pathText = new System.Windows.Controls.TextBlock
            {
                Text = $"Path: {_textureItem.RelativePath}",
                FontSize = 10,
                Foreground = System.Windows.Media.Brushes.Gray
            };

            infoPanel.Children.Add(categoryText);
            infoPanel.Children.Add(sizeText);
            headerPanel.Children.Add(titleBlock);
            headerPanel.Children.Add(infoPanel);
            headerPanel.Children.Add(pathText);

            System.Windows.Controls.Grid.SetRow(headerPanel, 0);
            mainGrid.Children.Add(headerPanel);

            // Image viewer
            var scrollViewer = new System.Windows.Controls.ScrollViewer
            {
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                Margin = new Thickness(20, 10, 20, 10)
            };

            var imageViewer = new System.Windows.Controls.Image
            {
                Source = _textureItem.PreviewImage,
                Stretch = System.Windows.Media.Stretch.None,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Set bitmap scaling mode using attached property
            System.Windows.Media.RenderOptions.SetBitmapScalingMode(imageViewer, System.Windows.Media.BitmapScalingMode.NearestNeighbor);

            scrollViewer.Content = imageViewer;
            System.Windows.Controls.Grid.SetRow(scrollViewer, 1);
            mainGrid.Children.Add(scrollViewer);

            // Footer with buttons
            var footerPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(20, 10, 20, 20)
            };

            var createOverrideButton = new Wpf.Ui.Controls.Button
            {
                Content = "Create Override",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Copy24 },
                Appearance = ControlAppearance.Primary,
                Margin = new Thickness(0, 0, 10, 0)
            };
            createOverrideButton.Click += CreateOverrideButton_Click;

            var closeButton = new Wpf.Ui.Controls.Button
            {
                Content = "Close",
                Appearance = ControlAppearance.Secondary
            };
            closeButton.Click += (s, e) => Close();

            footerPanel.Children.Add(createOverrideButton);
            footerPanel.Children.Add(closeButton);

            System.Windows.Controls.Grid.SetRow(footerPanel, 2);
            mainGrid.Children.Add(footerPanel);

            Content = mainGrid;
        }

        private async void CreateOverrideButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Find the current resource pack
                var workspace = Application.Current.Windows
                    .OfType<ResourcePackWorkspace>()
                    .FirstOrDefault();

                if (workspace?.ViewModel?.CurrentPack == null)
                {
                    await ShowMessageAsync("Error", "No resource pack is currently loaded.");
                    return;
                }

                var currentPack = workspace.ViewModel.CurrentPack;

                // Create override directory structure
                var overrideDir = Path.Combine(currentPack.Location, "overrides", "textures", _textureItem.Category);
                Directory.CreateDirectory(overrideDir);

                // Create the override file path
                var overrideFileName = $"{_textureItem.Name}.png";
                var overridePath = Path.Combine(overrideDir, overrideFileName);

                // Copy the original texture to create the override
                File.Copy(_textureItem.FilePath, overridePath, true);

                // Update the resource pack data
                var manager = new ResourcePackTemplateManager();
                workspace.ViewModel.LoadPack(manager.ReadResourcePack(currentPack.Location));

                await ShowMessageAsync("Override Created", 
                    $"Override created for '{_textureItem.Name}'.\nYou can now edit it in the Overrides tab.");
                
                Close();
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Error", $"Failed to create override: {ex.Message}");
            }
        }

        private async Task ShowMessageAsync(string title, string message)
        {
            var msgBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "OK"
            };
            await msgBox.ShowDialogAsync();
        }
    }
}