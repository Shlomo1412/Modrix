using System.Diagnostics;
using System.IO;
using System.Windows;
using Wpf.Ui.Controls;

namespace ModrixInstaller.Views.Dialogs
{
    public partial class SuccessDialog : FluentWindow
    {
        private readonly string _modrixPath;

        public SuccessDialog(string modrixPath)
        {
            _modrixPath = modrixPath;
            InitializeComponent();
        }

        private void RunModrix_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_modrixPath) && File.Exists(_modrixPath))
                {
                    Process.Start(new ProcessStartInfo 
                    { 
                        FileName = _modrixPath, 
                        UseShellExecute = true 
                    });
                }
            }
            catch (Exception ex)
            {
                var msgBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "Error",
                    Content = $"Failed to launch Modrix: {ex.Message}",
                    PrimaryButtonText = "OK"
                };
                _ = msgBox.ShowDialogAsync();
            }
            finally
            {
                this.Close();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}