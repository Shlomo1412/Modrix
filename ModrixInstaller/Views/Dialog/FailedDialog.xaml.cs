using System.Diagnostics;
using System.Windows;
using Wpf.Ui.Controls;

namespace ModrixInstaller.Views.Dialogs
{
    public partial class FailedDialog : FluentWindow
    {
        public string ErrorMessage { get; }

        public bool ShouldRetry { get; private set; } = false;

        public FailedDialog(string errorMessage)
        {
            ErrorMessage = errorMessage;
            InitializeComponent();
            DataContext = this;
        }

        private void TryAgain_Click(object sender, RoutedEventArgs e)
        {
            ShouldRetry = true;
            this.Close();
        }

        private void GetHelp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://github.com/Shlomo1412/Modrix/issues") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                var msgBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "Error",
                    Content = $"Could not open help page: {ex.Message}",
                    PrimaryButtonText = "OK"
                };
                _ = msgBox.ShowDialogAsync();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}