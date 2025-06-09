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
                // Handle any directory access errors
                MessageBox.Show($"Error accessing directory: {ex.Message}");
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
                MessageBox.Show($"Error opening file: {ex.Message}");
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
                MessageBox.Show($"Error saving file: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        MessageBox.Show($"A file named '{fileName}' already exists.", "File Exists", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    File.Create(newFilePath).Dispose();
                    RefreshFileTree();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        MessageBox.Show($"A folder named '{folderName}' already exists.", "Folder Exists", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    Directory.CreateDirectory(newFolderPath);
                    RefreshFileTree();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void DeleteItem(string path)
        {
            try
            {
                var isDirectory = Directory.Exists(path);
                var message = $"Are you sure you want to delete this {(isDirectory ? "folder" : "file")}?";
                var result = MessageBox.Show(message, "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
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
                MessageBox.Show($"Error deleting item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        MessageBox.Show($"An item named '{newName}' already exists.", "Item Exists", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show($"Error renaming item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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