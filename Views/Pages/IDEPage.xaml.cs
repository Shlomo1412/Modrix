using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Highlighting;
using Modrix.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;
using System.ComponentModel;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using System.Windows.Media;
using System.Threading.Tasks;

namespace Modrix.Views.Pages
{
    public partial class IDEPage : Page, INavigableView<IDEPageViewModel>
    {
        public IDEPageViewModel ViewModel { get; }
        private bool _updatingText;

        // For drag-and-drop
        private Point _dragStartPoint;
        private FileTreeItem _draggedItem;

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
            FileTreeView.PreviewMouseLeftButtonDown += FileTreeView_PreviewMouseLeftButtonDown;
            FileTreeView.PreviewMouseMove += FileTreeView_PreviewMouseMove;
            FileTreeView.DragOver += FileTreeView_DragOver;
            FileTreeView.Drop += FileTreeView_Drop;
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

                var renameItem = new Wpf.Ui.Controls.MenuItem
                {
                    Header = "Rename",
                    Icon = new SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Rename24)
                };

                renameItem.Click += (s, args) =>
                {
                    ViewModel.RenameItem(item.FullPath);
                };

                var deleteItem = new Wpf.Ui.Controls.MenuItem
                {
                    Header = "Delete",
                    Icon = new SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Delete24)
                };

                deleteItem.Click += async (s, args) =>
                {
                    await ViewModel.DeleteItemAsync(item.FullPath);
                };

                contextMenu.Items.Add(renameItem);
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

        // Drag-and-drop handlers
        private void FileTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _draggedItem = GetTreeViewItemUnderMouse(e.OriginalSource);
        }

        private void FileTreeView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null)
            {
                Point currentPosition = e.GetPosition(null);
                if (Math.Abs(currentPosition.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(currentPosition.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    DragDrop.DoDragDrop(FileTreeView, _draggedItem, DragDropEffects.Move);
                }
            }
        }

        private void FileTreeView_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(FileTreeItem)))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var target = GetTreeViewItemUnderMouse(e.OriginalSource);
            if (target == null || !target.IsDirectory)
            {
                e.Effects = DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.Move;
            }
            e.Handled = true;
        }

        private async void FileTreeView_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(FileTreeItem)))
                return;

            var sourceItem = (FileTreeItem)e.Data.GetData(typeof(FileTreeItem));
            var targetItem = GetTreeViewItemUnderMouse(e.OriginalSource);
            if (sourceItem == null || targetItem == null || !targetItem.IsDirectory)
                return;

            // Prevent moving into itself or its own subdirectory
            if (IsDescendantOrSelf(sourceItem.FullPath, targetItem.FullPath))
                return;

            await ViewModel.MoveItemAsync(sourceItem.FullPath, targetItem.FullPath);
        }

        private FileTreeItem GetTreeViewItemUnderMouse(object originalSource)
        {
            DependencyObject current = originalSource as DependencyObject;
            while (current != null && !(current is System.Windows.Controls.TreeViewItem))
            {
                current = VisualTreeHelper.GetParent(current);
            }
            if (current is System.Windows.Controls.TreeViewItem tvi)
            {
                return tvi.DataContext as FileTreeItem;
            }
            return null;
        }

        private bool IsDescendantOrSelf(string sourcePath, string targetPath)
        {
            var normalizedSource = System.IO.Path.GetFullPath(sourcePath).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
            var normalizedTarget = System.IO.Path.GetFullPath(targetPath).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
            return normalizedTarget.StartsWith(normalizedSource, StringComparison.OrdinalIgnoreCase);
        }
    }
}