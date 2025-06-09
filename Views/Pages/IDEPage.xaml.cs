using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Highlighting;
using Modrix.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;
using System.ComponentModel;
using Microsoft.Win32;
using Wpf.Ui.Controls;

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
                _updatingText = true;
                try
                {
                    CodeEditor.Text = ViewModel.SelectedFileContent;
                }
                finally
                {
                    _updatingText = false;
                }
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

            FileTreeView.PreviewMouseRightButtonDown += FileTreeView_PreviewMouseRightButtonDown;
        }

        private void FileTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement element && element.DataContext is FileTreeItem item)
            {
                var contextMenu = new ContextMenu();

                if (item.IsDirectory)
                {
                    var addMenu = new Wpf.Ui.Controls.MenuItem
                    {
                        Header = "Add",
                        Icon = new SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Add24)
                    };

                    var addFileItem = new Wpf.Ui.Controls.MenuItem
                    {
                        Header = "File",
                        Icon = new SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Document24)
                    };

                    var addFolderItem = new Wpf.Ui.Controls.MenuItem
                    {
                        Header = "Folder",
                        Icon = new SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Folder24)
                    };

                    addFileItem.Click += (s, args) =>
                    {
                        ViewModel.CreateNewFile(item.FullPath);
                    };

                    addFolderItem.Click += (s, args) =>
                    {
                        ViewModel.CreateNewFolder(item.FullPath);
                    };

                    addMenu.Items.Add(addFileItem);
                    addMenu.Items.Add(addFolderItem);
                    contextMenu.Items.Add(addMenu);
                    contextMenu.Items.Add(new Separator());
                }

                var deleteItem = new Wpf.Ui.Controls.MenuItem
                {
                    Header = "Delete",
                    Icon = new SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Delete24)
                };

                deleteItem.Click += (s, args) =>
                {
                    ViewModel.DeleteItem(item.FullPath);
                };

                contextMenu.Items.Add(deleteItem);
                contextMenu.IsOpen = true;
                e.Handled = true;
            }
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
                if (!_updatingText)
                {
                    try
                    {
                        _updatingText = true;
                        ViewModel.UpdateContent(CodeEditor.Text);
                    }
                    finally
                    {
                        _updatingText = false;
                    }
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

        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ViewModel.SaveCommand.Execute(null);
        }

        private void SaveCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ViewModel.HasUnsavedChanges;
        }
    }
}