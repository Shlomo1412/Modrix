using System.IO;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace Modrix.Views.Windows
{
    public partial class CreateFileDialog : FluentWindow
    {
        public string FileName { get; private set; } = string.Empty;

        private readonly string[] _commonFileTypes = new[]
        {
            ".txt",
            ".md",
            ".java",
            ".json",
            ".xml",
            ".yaml",
            ".yml",
            ".properties",
            ".toml",
            ".gradle",
            ".cs"
        };

        public CreateFileDialog()
        {
            InitializeComponent();
            SetupFileTypes();
            FileNameTextBox.Focus();
        }

        private void SetupFileTypes()
        {
            foreach (var fileType in _commonFileTypes)
            {
                FileTypeComboBox.Items.Add(new ComboBoxItem { Content = fileType });
            }
            FileTypeComboBox.SelectedIndex = 0;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(FileNameTextBox.Text))
            {
                var fileType = ((ComboBoxItem)FileTypeComboBox.SelectedItem).Content.ToString();
                var fileName = FileNameTextBox.Text;

                // If the user has already included an extension, use that instead
                if (!Path.HasExtension(fileName))
                {
                    fileName += fileType;
                }

                FileName = fileName;
                DialogResult = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}