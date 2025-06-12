using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Modrix.Models;
using Modrix.ViewModels.Windows;
using Modrix.Views.Windows;
using Wpf.Ui.Controls;
using System.Threading.Tasks;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxButton = Wpf.Ui.Controls.MessageBoxButton;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace Modrix.ViewModels.Pages
{
    public partial class IDEPageViewModel : ObservableObject
    {
        private readonly ProjectWorkspaceViewModel _workspaceViewModel;

        [ObservableProperty]
        private ObservableCollection<FileTreeItem> _fileTree = new();

        [ObservableProperty]
        private string _selectedFilePath;

        [ObservableProperty]
        private string _selectedFileContent;

        [ObservableProperty]
        private string _selectedFileName;

        [ObservableProperty]
        private bool _hasUnsavedChanges;

        public IDEPageViewModel(ProjectWorkspaceViewModel workspaceViewModel)
        {
            _workspaceViewModel = workspaceViewModel;
            SaveCommand = new RelayCommand(SaveFile, () => HasUnsavedChanges);
            LoadFileTree();
        }

        public ICommand SaveCommand { get; }

        public void LoadFileTree()
        {
            if (_workspaceViewModel?.CurrentProject == null) return;

            FileTree.Clear();
            var projectDir = _workspaceViewModel.CurrentProject.Location;
            if (Directory.Exists(projectDir))
            {
                var rootItem = new FileTreeItem(projectDir, true);
                PopulateFileTree(rootItem);
                FileTree.Add(rootItem);
            }
        }

        private async Task ShowErrorAsync(string message, string title = "Error")
        {
            var errorBox = new MessageBox
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK"
            };
            await errorBox.ShowDialogAsync();
        }

        private async Task ShowWarningAsync(string message, string title = "Warning")
        {
            var warningBox = new MessageBox
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK"
            };
            await warningBox.ShowDialogAsync();
        }

        private void PopulateFileTree(FileTreeItem item)
        {
            try
            {
                // Add subdirectories
                foreach (var dir in Directory.GetDirectories(item.FullPath))
                {
                    var dirItem = new FileTreeItem(dir, true);
                    item.Children.Add(dirItem);
                    PopulateFileTree(dirItem);
                }

                // Add files
                foreach (var file in Directory.GetFiles(item.FullPath))
                {
                    item.Children.Add(new FileTreeItem(file, false));
                }
            }
            catch (Exception ex)
            {
                ShowErrorAsync($"Error accessing directory: {ex.Message}");
            }
        }

        public void OpenFile(string filePath)
        {
            try
            {
                SelectedFilePath = filePath;
                SelectedFileName = Path.GetFileName(filePath);
                SelectedFileContent = File.ReadAllText(filePath);
                HasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                ShowErrorAsync($"Error opening file: {ex.Message}");
            }
        }

        public void SaveFile()
        {
            if (string.IsNullOrEmpty(SelectedFilePath) || string.IsNullOrEmpty(SelectedFileContent))
                return;

            try
            {
                File.WriteAllText(SelectedFilePath, SelectedFileContent);
                HasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                ShowErrorAsync($"Error saving file: {ex.Message}", "Save Error");
            }
        }

        public void UpdateContent(string newContent)
        {
            if (SelectedFileContent != newContent)
            {
                SelectedFileContent = newContent;
                HasUnsavedChanges = true;
            }
        }

        public void CreateNewFile(string parentPath)
        {
            try
            {
                var dialog = new CreateFileDialog
                {
                    Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive)
                };

                if (dialog.ShowDialog() == true)
                {
                    var fileName = dialog.FileName;
                    var newFilePath = Path.Combine(parentPath, fileName);

                    if (File.Exists(newFilePath))
                    {
                        ShowWarningAsync($"A file named '{fileName}' already exists.", "File Exists");
                        return;
                    }

                    File.Create(newFilePath).Dispose();
                    RefreshFileTree();
                }
            }
            catch (Exception ex)
            {
                ShowErrorAsync($"Error creating file: {ex.Message}");
            }
        }

        public void CreateNewFolder(string parentPath)
        {
            try
            {
                var dialog = new CreateFolderDialog
                {
                    Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive)
                };

                if (dialog.ShowDialog() == true)
                {
                    var folderName = dialog.FolderName;
                    var newFolderPath = Path.Combine(parentPath, folderName);

                    if (Directory.Exists(newFolderPath))
                    {
                        ShowWarningAsync($"A folder named '{folderName}' already exists.", "Folder Exists");
                        return;
                    }

                    Directory.CreateDirectory(newFolderPath);
                    RefreshFileTree();
                }
            }
            catch (Exception ex)
            {
                ShowErrorAsync($"Error creating folder: {ex.Message}");
            }
        }

        public async Task DeleteItemAsync(string path)
        {
            try
            {
                var isDirectory = Directory.Exists(path);
                var message = $"Are you sure you want to delete this {(isDirectory ? "folder" : "file")}?";

                var messageBox = new MessageBox
                {
                    Title = "Confirm Delete",
                    Content = message,
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel"
                };
                var result = await messageBox.ShowDialogAsync();
                if (result == MessageBoxResult.Primary)
                {
                    if (isDirectory)
                    {
                        Directory.Delete(path, true);
                    }
                    else
                    {
                        File.Delete(path);
                    }
                    RefreshFileTree();
                }
            }
            catch (Exception ex)
            {
                await ShowErrorAsync($"Error deleting item: {ex.Message}");
            }
        }

        public void RenameItem(string path)
        {
            try
            {
                var isDirectory = Directory.Exists(path);
                var oldName = Path.GetFileName(path);

                var dialog = new Views.Windows.RenameDialog(oldName, !isDirectory)
                {
                    Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive)
                };

                if (dialog.ShowDialog() == true)
                {
                    var newName = dialog.NewName;
                    if (newName == oldName) return;

                    var directory = Path.GetDirectoryName(path);
                    if (directory == null) return;

                    var newPath = Path.Combine(directory, newName);

                    if (File.Exists(newPath) || Directory.Exists(newPath))
                    {
                        ShowWarningAsync($"An item named '{newName}' already exists.", "Item Exists");
                        return;
                    }

                    if (isDirectory)
                    {
                        Directory.Move(path, newPath);
                    }
                    else
                    {
                        // If this is the currently open file, close it first
                        if (path == SelectedFilePath)
                        {
                            SelectedFilePath = null;
                            SelectedFileName = null;
                            SelectedFileContent = null;
                        }

                        File.Move(path, newPath);
                    }
                    RefreshFileTree();
                }
            }
            catch (Exception ex)
            {
                ShowErrorAsync($"Error renaming item: {ex.Message}");
            }
        }

        public async Task MoveItemAsync(string sourcePath, string targetDirectory)
        {
            try
            {
                var isDirectory = Directory.Exists(sourcePath);
                var itemName = Path.GetFileName(sourcePath);
                var newPath = Path.Combine(targetDirectory, itemName);

                if (string.Equals(sourcePath, newPath, StringComparison.OrdinalIgnoreCase))
                    return;

                if (File.Exists(newPath) || Directory.Exists(newPath))
                {
                    await ShowWarningAsync($"An item named '{itemName}' already exists in the target folder.", "Item Exists");
                    return;
                }

                var message = $"Are you sure you want to move this {(isDirectory ? "folder" : "file")} to '{targetDirectory}'?";
                var messageBox = new MessageBox
                {
                    Title = "Confirm Move",
                    Content = message,
                    PrimaryButtonText = "Move",
                    CloseButtonText = "Cancel"
                };
                var result = await messageBox.ShowDialogAsync();
                if (result == MessageBoxResult.Primary)
                {
                    if (isDirectory)
                    {
                        Directory.Move(sourcePath, newPath);
                    }
                    else
                    {
                        // If this is the currently open file, close it first
                        if (sourcePath == SelectedFilePath)
                        {
                            SelectedFilePath = null;
                            SelectedFileName = null;
                            SelectedFileContent = null;
                        }
                        File.Move(sourcePath, newPath);
                    }
                    RefreshFileTree();
                }
            }
            catch (Exception ex)
            {
                await ShowErrorAsync($"Error moving item: {ex.Message}");
            }
        }

        private void RefreshFileTree()
        {
            LoadFileTree();
        }
    }

    public class FileTreeItem : ObservableObject
    {
        private readonly bool _isDirectory;
        private string _name;
        private ObservableCollection<FileTreeItem> _children;

        public FileTreeItem(string path, bool isDirectory)
        {
            FullPath = path;
            _isDirectory = isDirectory;
            _name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(_name)) _name = path;

            if (_isDirectory)
            {
                _children = new ObservableCollection<FileTreeItem>();
            }
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string FullPath { get; }

        public ObservableCollection<FileTreeItem> Children
        {
            get => _children;
            set => SetProperty(ref _children, value);
        }

        public bool IsDirectory => _isDirectory;
    }
}