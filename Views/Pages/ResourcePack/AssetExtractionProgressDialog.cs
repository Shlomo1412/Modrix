using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace Modrix.Views.Pages.ResourcePack
{
    public partial class AssetExtractionProgressDialog : FluentWindow
    {
        private System.Windows.Controls.TextBlock _statusText;

        public AssetExtractionProgressDialog()
        {
            InitializeDialog();
        }

        private void InitializeDialog()
        {
            Title = "Extracting Minecraft Assets";
            Width = 450;
            Height = 200;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var mainPanel = new StackPanel { Margin = new Thickness(24) };

            // Title
            var titleBlock = new System.Windows.Controls.TextBlock
            {
                Text = "Extracting Minecraft Assets",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 16),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            mainPanel.Children.Add(titleBlock);

            // Progress bar
            var progressBar = new ProgressBar
            {
                Height = 20,
                Margin = new Thickness(0, 0, 0, 16),
                IsIndeterminate = true
            };
            mainPanel.Children.Add(progressBar);

            // Status text - store direct reference instead of using RegisterName
            _statusText = new System.Windows.Controls.TextBlock
            {
                Text = "Preparing...",
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
            };
            mainPanel.Children.Add(_statusText);

            // Cancel button
            var cancelButton = new Wpf.Ui.Controls.Button
            {
                Content = "Cancel",
                Appearance = ControlAppearance.Secondary,
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 100
            };
            cancelButton.Click += (s, e) => Close();
            mainPanel.Children.Add(cancelButton);

            Content = mainPanel;
        }

        public void UpdateStatus(string status)
        {
            Dispatcher.Invoke(() =>
            {
                if (_statusText != null)
                {
                    _statusText.Text = status;
                }
            });
        }
    }
}