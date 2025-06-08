using System.Windows;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit.Highlighting;
using Modrix.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;
using System.ComponentModel;

namespace Modrix.Views.Pages
{
    public partial class IDEPage : Page, INavigableView<IDEPageViewModel>
    {
        public IDEPageViewModel ViewModel { get; }
        private bool _updatingText;

        public IDEPage(IDEPageViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
            SetupFileTreeViewEvents();
            SetupEditor();

            if (ViewModel is INotifyPropertyChanged notifyViewModel)
            {
                notifyViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.SelectedFileContent) && !_updatingText)
            {
                CodeEditor.Text = ViewModel.SelectedFileContent;
            }
        }

        private void SetupFileTreeViewEvents()
        {
            FileTreeView.SelectedItemChanged += (s, e) =>
            {
                if (e.NewValue is FileTreeItem item && !item.IsDirectory)
                {
                    ViewModel.OpenFile(item.FullPath);
                }
            };
        }

        private void SetupEditor()
        {
            // Set dark theme if application is in dark mode
            if (Application.Current.Resources["ApplicationBackgroundBrush"]?.ToString() == "#FF202020")
            {
                CodeEditor.Background = Application.Current.Resources["ApplicationBackgroundBrush"] as System.Windows.Media.Brush;
                CodeEditor.Foreground = System.Windows.Media.Brushes.White;
            }

            // Handle text changes
            CodeEditor.TextChanged += (s, e) =>
            {
                try
                {
                    _updatingText = true;
                    ViewModel.SelectedFileContent = CodeEditor.Text;
                }
                finally
                {
                    _updatingText = false;
                }

                UpdateSyntaxHighlighting();
            };
        }

        private void UpdateSyntaxHighlighting()
        {
            if (string.IsNullOrEmpty(ViewModel.SelectedFilePath)) return;

            var extension = System.IO.Path.GetExtension(ViewModel.SelectedFilePath).ToLower();
            string syntaxName = extension switch
            {
                ".cs" => "C#",
                ".java" => "Java",
                ".json" => "JavaScript",
                ".xml" => "XML",
                ".xaml" => "XML",
                ".txt" => null,
                _ => null
            };

            if (!string.IsNullOrEmpty(syntaxName))
            {
                CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition(syntaxName);
            }
            else
            {
                CodeEditor.SyntaxHighlighting = null;
            }
        }
    }
}