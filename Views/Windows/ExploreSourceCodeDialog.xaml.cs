using System.Diagnostics;
using System.Windows;
using Wpf.Ui.Controls;

namespace Modrix.Views.Windows
{
    public partial class ExploreSourceCodeDialog : FluentWindow
    {
        public ExploreSourceCodeDialog()
        {
            InitializeComponent();
        }

        private void OpenGitHub_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/Shlomo1412/Modrix") { UseShellExecute = true });
            this.Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
