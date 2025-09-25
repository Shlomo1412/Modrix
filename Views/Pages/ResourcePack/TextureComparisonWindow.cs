using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Modrix.Services;
using Modrix.Views.Windows;
using Wpf.Ui.Controls;

namespace Modrix.Views.Pages.ResourcePack
{
    public partial class TextureComparisonWindow : FluentWindow
    {
        private readonly OverridesPage.TextureOverrideItem _overrideItem;
        private readonly ResourcePackData? _resourcePack;
        private System.Windows.Controls.Image _overrideImage;

        public TextureComparisonWindow(OverridesPage.TextureOverrideItem overrideItem, ResourcePackData? resourcePack)
        {
            _overrideItem = overrideItem;
            _resourcePack = resourcePack;
            InitializeWindow();
        }

        private void InitializeWindow()
        {
            Title = $"Texture Comparison - {_overrideItem.Name}";
            Width = 900;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var headerPanel = CreateHeaderPanel();
            Grid.SetRow(headerPanel, 0);
            mainGrid.Children.Add(headerPanel);

            // Main comparison area
            var comparisonPanel = CreateComparisonPanel();
            Grid.SetRow(comparisonPanel, 1);
            mainGrid.Children.Add(comparisonPanel);

            // Footer with buttons
            var footerPanel = CreateFooterPanel();
            Grid.SetRow(footerPanel, 2);
            mainGrid.Children.Add(footerPanel);

            Content = mainGrid;
        }

        private StackPanel CreateHeaderPanel()
        {
            var headerPanel = new StackPanel
            {
                Margin = new Thickness(20, 20, 20, 10),
                Orientation = Orientation.Vertical
            };

            var titleBlock = new System.Windows.Controls.TextBlock
            {
                Text = _overrideItem.Name,
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var infoBlock = new System.Windows.Controls.TextBlock
            {
                Text = $"Category: {_overrideItem.Category} | Original: {_overrideItem.OriginalPath}",
                FontSize = 12,
                Foreground = System.Windows.Media.Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            };

            headerPanel.Children.Add(titleBlock);
            headerPanel.Children.Add(infoBlock);

            return headerPanel;
        }

        private Grid CreateComparisonPanel()
        {
            var comparisonGrid = new Grid { Margin = new Thickness(20, 10, 20, 10) };
            comparisonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            comparisonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20, GridUnitType.Pixel) });
            comparisonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Original texture panel
            var originalPanel = CreateTexturePanel("Original Texture", FindOriginalTexture(), true);
            Grid.SetColumn(originalPanel, 0);
            comparisonGrid.Children.Add(originalPanel);

            // Override texture panel
            var overridePanel = CreateTexturePanel("Your Override", _overrideItem.PreviewImage, false);
            Grid.SetColumn(overridePanel, 2);
            comparisonGrid.Children.Add(overridePanel);

            return comparisonGrid;
        }

        private Border CreateTexturePanel(string title, BitmapImage? image, bool isOriginal)
        {
            var panel = new Border
            {
                Background = System.Windows.Media.Brushes.White,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16)
            };

            var stackPanel = new StackPanel();

            // Title
            var titleBlock = new System.Windows.Controls.TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };
            stackPanel.Children.Add(titleBlock);

            // Image viewer
            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 300,
                Background = System.Windows.Media.Brushes.LightGray
            };

            var imageControl = new System.Windows.Controls.Image
            {
                Source = image,
                Stretch = System.Windows.Media.Stretch.None,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Set the bitmap scaling mode using attached property
            if (image != null)
            {
                System.Windows.Media.RenderOptions.SetBitmapScalingMode(imageControl, System.Windows.Media.BitmapScalingMode.NearestNeighbor);
            }

            if (!isOriginal)
            {
                _overrideImage = imageControl;
            }

            scrollViewer.Content = imageControl;
            stackPanel.Children.Add(scrollViewer);

            // Action buttons
            var buttonPanel = CreateButtonPanel(isOriginal);
            stackPanel.Children.Add(buttonPanel);

            panel.Child = stackPanel;
            return panel;
        }

        private StackPanel CreateButtonPanel(bool isOriginal)
        {
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };

            if (!isOriginal)
            {
                var editButton = new Wpf.Ui.Controls.Button
                {
                    Content = "Edit in External Editor",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Edit24 },
                    Margin = new Thickness(0, 0, 8, 0)
                };
                editButton.Click += EditOverride_Click;

                var replaceButton = new Wpf.Ui.Controls.Button
                {
                    Content = "Replace Image",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.ArrowUpload24 }
                };
                replaceButton.Click += ReplaceOverride_Click;

                buttonPanel.Children.Add(editButton);
                buttonPanel.Children.Add(replaceButton);
            }
            else
            {
                var copyButton = new Wpf.Ui.Controls.Button
                {
                    Content = "Copy to Override",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Copy24 }
                };
                copyButton.Click += CopyToOverride_Click;
                buttonPanel.Children.Add(copyButton);
            }

            return buttonPanel;
        }

        private BitmapImage? FindOriginalTexture()
        {
            try
            {
                // Try to find the original texture in extracted assets
                if (_resourcePack != null)
                {
                    var assetExtractor = new MinecraftAssetExtractor();
                    var assetsPath = assetExtractor.GetAssetsPath(_resourcePack.MinecraftVersion ?? "1.21.4");
                    var originalPath = Path.Combine(assetsPath, _overrideItem.Category, $"{_overrideItem.Name}.png");

                    if (File.Exists(originalPath))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(originalPath);
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return bitmap;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private StackPanel CreateFooterPanel()
        {
            var footerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(20, 10, 20, 20)
            };

            var saveButton = new Wpf.Ui.Controls.Button
            {
                Content = "Save Changes",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Save24 },
                Appearance = ControlAppearance.Primary,
                Margin = new Thickness(0, 0, 10, 0)
            };
            saveButton.Click += SaveChanges_Click;

            var closeButton = new Wpf.Ui.Controls.Button
            {
                Content = "Close",
                Appearance = ControlAppearance.Secondary
            };
            closeButton.Click += (s, e) => Close();

            footerPanel.Children.Add(saveButton);
            footerPanel.Children.Add(closeButton);

            return footerPanel;
        }

        private void EditOverride_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open the override file in the default image editor
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _overrideItem.OverridePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowMessage($"Failed to open image editor: {ex.Message}", "Error");
            }
        }

        private void ReplaceOverride_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select Replacement Image",
                    Filter = "PNG Images|*.png|All Images|*.png;*.jpg;*.jpeg;*.bmp",
                    Multiselect = false
                };

                if (dialog.ShowDialog() == true)
                {
                    // Copy the selected image to replace the override
                    File.Copy(dialog.FileName, _overrideItem.OverridePath, true);

                    // Update the preview
                    RefreshOverrideImage();

                    ShowMessage("Override image replaced successfully!", "Success");
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Failed to replace image: {ex.Message}", "Error");
            }
        }

        private void CopyToOverride_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var originalTexture = FindOriginalTexture();
                if (originalTexture != null && _resourcePack != null)
                {
                    var assetExtractor = new MinecraftAssetExtractor();
                    var assetsPath = assetExtractor.GetAssetsPath(_resourcePack.MinecraftVersion ?? "1.21.4");
                    var originalPath = Path.Combine(assetsPath, _overrideItem.Category, $"{_overrideItem.Name}.png");

                    if (File.Exists(originalPath))
                    {
                        File.Copy(originalPath, _overrideItem.OverridePath, true);
                        RefreshOverrideImage();
                        ShowMessage("Original texture copied to override!", "Success");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Failed to copy texture: {ex.Message}", "Error");
            }
        }

        private void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            ShowMessage("Changes have been saved automatically!", "Saved");
        }

        private void RefreshOverrideImage()
        {
            try
            {
                // Reload the override image
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(_overrideItem.OverridePath);
                bitmap.EndInit();
                bitmap.Freeze();

                if (_overrideImage != null)
                {
                    _overrideImage.Source = bitmap;
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Failed to refresh image: {ex.Message}", "Error");
            }
        }

        private async void ShowMessage(string message, string title)
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