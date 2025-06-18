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
using System.IO;
using Modrix.Views.Windows;

namespace Modrix.Views.Pages
{
    public partial class IDEPage : Page, INavigableView<IDEPageViewModel>
    {
        public IDEPageViewModel ViewModel { get; }
        private bool _updatingText;

        // For drag-and-drop
        private Point _dragStartPoint;
        private FileTreeItem _draggedItem;

        // Autosave and file watcher
        private System.Windows.Threading.DispatcherTimer? _autoSaveTimer;
        private FileSystemWatcher? _fileWatcher;
        private bool _editorLoaded = false;

        private double _editorFontSize = 14.0; // Default font size
        private const double MinFontSize = 8.0;
        private const double MaxFontSize = 40.0;
        private const double FontSizeStep = 1.0;

        public IDEPage(IDEPageViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
            SetupFileTreeViewEvents();
            SetupEditor();

            // Set initial font size
            CodeEditor.FontSize = _editorFontSize;

            if (ViewModel is INotifyPropertyChanged notifyViewModel)
            {
                notifyViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }

            // Subscribe to IdeSettings changes
            if (ViewModel.IdeSettings is INotifyPropertyChanged notifyIdeSettings)
            {
                notifyIdeSettings.PropertyChanged += IdeSettings_PropertyChanged;
            }

            // Ensure settings are applied after editor is loaded
            CodeEditor.Loaded += (s, e) =>
            {
                _editorLoaded = true;
                ApplyIdeSettings();
                SetupAutoSave();
                SetupFileWatcher();
            };

            // Add mouse wheel zoom (Ctrl+Wheel)
            CodeEditor.PreviewMouseWheel += CodeEditor_PreviewMouseWheel;
        }

        private void IdeSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!_editorLoaded) return;
            ApplyIdeSettings();
            if (e.PropertyName == nameof(ViewModel.IdeSettings.AutoSave) || e.PropertyName == nameof(ViewModel.IdeSettings.AutoSaveIntervalSeconds))
                SetupAutoSave();
            if (e.PropertyName == nameof(ViewModel.IdeSettings.AutoReloadExternalChanges))
                SetupFileWatcher();
        }

        private void ApplyIdeSettings()
        {
            if (!_editorLoaded) return;
            var s = ViewModel.IdeSettings;
            // Word wrap
            CodeEditor.WordWrap = s.WordWrap;
            // Show line numbers
            CodeEditor.ShowLineNumbers = s.ShowLineNumbers;
            // Tab size
            CodeEditor.Options.IndentationSize = s.TabSize;
            // Use spaces for tabs
            CodeEditor.Options.ConvertTabsToSpaces = s.UseSpacesForTabs;
            // Show whitespace
            CodeEditor.Options.ShowSpaces = s.ShowWhitespace;
            CodeEditor.Options.ShowTabs = s.ShowWhitespace;
            // Highlight current line
            if (s.HighlightCurrentLine)
                CodeEditor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(Color.FromArgb(32, 0, 120, 215));
            else
                CodeEditor.TextArea.TextView.CurrentLineBackground = null;
        }

        private void SetupAutoSave()
        {
            if (_autoSaveTimer != null)
            {
                _autoSaveTimer.Stop();
                _autoSaveTimer = null;
            }
            if (ViewModel.IdeSettings.AutoSave)
            {
                _autoSaveTimer = new System.Windows.Threading.DispatcherTimer();
                _autoSaveTimer.Interval = TimeSpan.FromSeconds(ViewModel.IdeSettings.AutoSaveIntervalSeconds);
                _autoSaveTimer.Tick += (s, e) =>
                {
                    if (ViewModel.HasUnsavedChanges)
                        ViewModel.SaveFile();
                };
                _autoSaveTimer.Start();
            }
        }

        private void SetupFileWatcher()
        {
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }
            if (ViewModel.IdeSettings.AutoReloadExternalChanges && !string.IsNullOrEmpty(ViewModel.SelectedFilePath))
            {
                _fileWatcher = new FileSystemWatcher(System.IO.Path.GetDirectoryName(ViewModel.SelectedFilePath) ?? "")
                {
                    Filter = System.IO.Path.GetFileName(ViewModel.SelectedFilePath),
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                };
                _fileWatcher.Changed += (s, e) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (!ViewModel.HasUnsavedChanges)
                        {
                            ViewModel.OpenFile(ViewModel.SelectedFilePath);
                        }
                    });
                };
                _fileWatcher.EnableRaisingEvents = true;
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
            if (e.PropertyName == nameof(ViewModel.SelectedFilePath))
            {
                SetupFileWatcher();
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

            // Add search functionality with Ctrl+F keyboard shortcut
            CodeEditor.KeyDown += (s, e) =>
            {
                if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    ShowSearchDialog();
                    e.Handled = true;
                }
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

        private void ShowSearchDialog()
        {
            var dialog = new IdeSearchDialog();
            void DoSearch()
            {
                var searchText = dialog.SearchText;
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    dialog.Results.Clear();
                    dialog.SetResultsCountText("No results");
                    return;
                }
                dialog.Results.Clear();
                var text = CodeEditor.Text;
                var comparison = dialog.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                int index = 0;
                while (index < text.Length)
                {
                    index = text.IndexOf(searchText, index, comparison);
                    if (index < 0) break;
                    // Whole word check
                    if (dialog.WholeWord)
                    {
                        bool isStart = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
                        bool isEnd = (index + searchText.Length == text.Length) || !char.IsLetterOrDigit(text[index + searchText.Length]);
                        if (!(isStart && isEnd))
                        {
                            index += searchText.Length;
                            continue;
                        }
                    }
                    var lineNumber = CodeEditor.Document.GetLineByOffset(index).LineNumber;
                    dialog.Results.Add(new IdeSearchDialog.SearchResult
                    {
                        LineNumber = lineNumber,
                        StartOffset = index,
                        Length = searchText.Length,
                        Preview = GetLinePreview(text, index, searchText.Length)
                    });
                    index += searchText.Length;
                }
                if (dialog.Results.Count > 0)
                {
                    dialog.SetSelectedIndex(0);
                }
                dialog.SetResultsCountText(dialog.Results.Count == 0 ? "No results" : $"{dialog.Results.Count} result{(dialog.Results.Count == 1 ? "" : "s")}");
            }
            dialog.FindRequested += (s, e) => DoSearch();
            dialog.SelectionChanged += (s, e) =>
            {
                if (dialog.SelectedResult != null)
                {
                    CodeEditor.Select(dialog.SelectedResult.StartOffset, dialog.SelectedResult.Length);
                    CodeEditor.ScrollToLine(dialog.SelectedResult.LineNumber);
                    CodeEditor.Focus();
                }
            };
            dialog.Show();
            dialog.FocusSearchBox();
        }

        private string GetLinePreview(string text, int offset, int length)
        {
            int lineStart = offset;
            while (lineStart > 0 && text[lineStart - 1] != '\n')
                lineStart--;

            int lineEnd = offset + length;
            while (lineEnd < text.Length && text[lineEnd] != '\r' && text[lineEnd] != '\n')
                lineEnd++;

            string line = text.Substring(lineStart, lineEnd - lineStart);

            // Highlight the match in the preview
            int matchStart = offset - lineStart;
            string highlighted = line.Substring(0, matchStart) +
                                "«" + line.Substring(matchStart, length) + "»" +
                                line.Substring(matchStart + length);

            return highlighted;
        }

        // Helper class for search results
        private class SearchResult
        {
            public int LineNumber { get; set; }
            public int StartOffset { get; set; }
            public int Length { get; set; }
            public string Preview { get; set; }

            public override string ToString() => $"Line {LineNumber}: {Preview}";
        }

        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ViewModel.SaveCommand.Execute(null);
        }

        private void SaveCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ViewModel.HasUnsavedChanges;
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            SetEditorFontSize(_editorFontSize + FontSizeStep);
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            SetEditorFontSize(_editorFontSize - FontSizeStep);
        }

        private void SetEditorFontSize(double newSize)
        {
            _editorFontSize = Math.Max(MinFontSize, Math.Min(MaxFontSize, newSize));
            CodeEditor.FontSize = _editorFontSize;
        }

        private void CodeEditor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (e.Delta > 0)
                    SetEditorFontSize(_editorFontSize + FontSizeStep);
                else if (e.Delta < 0)
                    SetEditorFontSize(_editorFontSize - FontSizeStep);
                e.Handled = true;
            }
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