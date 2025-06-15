using System.Diagnostics;
using System.Windows;
using Wpf.Ui.Controls;

namespace Modrix.Views.Windows
{
    public partial class DonateDialog : FluentWindow
    {
        public DonateDialog()
        {
            InitializeComponent();
        }

        private void Donate_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://ko-fi.com/xtreamc") { UseShellExecute = true });
            this.Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}