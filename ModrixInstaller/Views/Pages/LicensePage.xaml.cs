using ModrixInstaller.ViewModels.Pages;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ModrixInstaller.Views.Pages
{
    public partial class LicensePage : Page
    {
        public LicensePageViewModel ViewModel { get; }

        public LicensePage(LicensePageViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;

            InitializeComponent();
        }

        private void PrintLicense_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var printDialog = new System.Windows.Controls.PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    var document = new System.Windows.Documents.FlowDocument();
                    var paragraph = new System.Windows.Documents.Paragraph();
                    paragraph.Inlines.Add(new System.Windows.Documents.Run(ViewModel.LicenseText));
                    document.Blocks.Add(paragraph);
                    
                    printDialog.PrintDocument(((System.Windows.Documents.IDocumentPaginatorSource)document).DocumentPaginator, "Modrix License Agreement");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to print license: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveLicense_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Title = "Save License Agreement",
                    Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    FileName = "Modrix_License_Agreement.txt"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveFileDialog.FileName, ViewModel.LicenseText);
                    MessageBox.Show("License agreement saved successfully.", "Save Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save license: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}