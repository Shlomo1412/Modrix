using Wpf.Ui.Controls;

namespace Modrix.Views.Windows
{
    public partial class WentWrongDialog : FluentWindow
    {
        private string _errorMessage;

        public WentWrongDialog(string errorMessage)
        {
            InitializeComponent();
            _errorMessage = errorMessage;
            ErrorMessageText.Text = errorMessage;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void CopyError_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(_errorMessage);
        }
    }
}