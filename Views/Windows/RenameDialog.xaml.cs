using System.IO;
using System.Windows;
using Wpf.Ui.Controls;

namespace Modrix.Views.Windows
{
    public partial class RenameDialog : FluentWindow
    {
        private readonly bool _isFile;
        private readonly string _oldName;
        private readonly string _extension;

        public string NewName { get; private set; } = string.Empty;

        public RenameDialog(string oldName, bool isFile)
        {
            InitializeComponent();
            _isFile = isFile;
            _oldName = oldName;

            if (isFile)
            {
                _extension = Path.GetExtension(oldName);
                NameTextBox.Text = Path.GetFileNameWithoutExtension(oldName);
                ExtensionText.Text = _extension;
            }
            else
            {
                NameTextBox.Text = oldName;
                ExtensionText.Visibility = Visibility.Collapsed;
            }

            NameTextBox.Focus();
            NameTextBox.SelectAll();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                NewName = _isFile ? NameTextBox.Text + _extension : NameTextBox.Text;
                DialogResult = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}