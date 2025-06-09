using System.Windows;
using Wpf.Ui.Controls;

namespace Modrix.Views.Windows
{
    public partial class CreateFolderDialog : FluentWindow
    {
        public string FolderName { get; private set; } = string.Empty;

        public CreateFolderDialog()
        {
            InitializeComponent();
            FolderNameTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(FolderNameTextBox.Text))
            {
                FolderName = FolderNameTextBox.Text;
                DialogResult = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}