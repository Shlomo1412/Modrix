using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modrix.Models;
using Modrix.ViewModels.Windows;

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