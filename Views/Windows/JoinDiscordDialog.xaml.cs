using System.Diagnostics;
using System.Windows;
using Wpf.Ui.Controls;

namespace Modrix.Views.Windows
{
    public partial class JoinDiscordDialog : FluentWindow
    {
        public JoinDiscordDialog()
        {
            InitializeComponent();
        }

        private void JoinDiscord_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://discord.gg/SHSAuM3H3D",
                UseShellExecute = true
            });
            this.Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
