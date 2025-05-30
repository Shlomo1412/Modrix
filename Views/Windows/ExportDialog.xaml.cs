using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Modrix.Views.Pages;
using Wpf.Ui.Controls;

namespace Modrix.Views.Windows
{
    public partial class ExportDialog : FluentWindow
    {
        private readonly string _projectDir;
        private readonly string _jdkHome;

        public ExportDialog(string projectDir, string jdkHome)
        {
            InitializeComponent();
            _projectDir = projectDir;
            _jdkHome = jdkHome;
        }

        private void ChkAccept_Changed(object sender, RoutedEventArgs e)
        {
            BtnExport.IsEnabled = ChkAccept.IsChecked == true;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Save Mod JAR As...",
                Filter = "Java Archive (*.jar)|*.jar",
                FileName = Path.GetFileName(_projectDir) + ".jar",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dlg.ShowDialog(this) != true)
                return;

            string dest = dlg.FileName;

            try
            {
                await RunGradleBuild("build", _projectDir, _jdkHome);

                var libsDir = Path.Combine(_projectDir, "build", "libs");
                var jarFile = Directory.GetFiles(libsDir, "*.jar", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .FirstOrDefault();

                if (jarFile == null)
                    throw new FileNotFoundException("Could not find JAR file in build/libs");

                File.Copy(jarFile, dest, true);

                var messageBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "Export Complete",
                    Content = $"Export succeeded!\n\nJAR saved to:\n{dest}",
                    CloseButtonText = "OK"
                };

                await messageBox.ShowDialogAsync();
                Close();
            }
            catch (Exception ex)
            {
                var messageBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "Export Error",
                    Content = $"Export failed:\n{ex.Message}",
                    CloseButtonText = "Close"
                };

                await messageBox.ShowDialogAsync();
            }
        }

        private Task RunGradleBuild(string task, string projectDir, string jdkHome)
        {
            var page = new ConsolePage();
            return page.StartGradleBuild(projectDir, task, jdkHome);
        }
    }
}
