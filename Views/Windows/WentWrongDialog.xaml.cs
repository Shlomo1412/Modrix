using System.Windows;
using Wpf.Ui.Controls;

namespace Modrix.Views.Windows
{
    public partial class WentWrongDialog : FluentWindow
    {
        public WentWrongDialog(string errorMessage)
        {
            InitializeComponent();
            ErrorMessageText.Text = errorMessage;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
