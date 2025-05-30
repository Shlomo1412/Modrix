using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using Modrix;
using Modrix.Models;
using Modrix.Services;
using Modrix.Views.Windows;

public partial class DashboardViewModel : ObservableObject
{
    private readonly TemplateManager _templateManager;

    [ObservableProperty]
    private ObservableCollection<ModProjectData> _allProjects = new();

    [ObservableProperty]
    private ICollectionView _filteredProjects;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _selectedGameVersion = "All";

    [ObservableProperty]
    private string _selectedModLoader = "All";

    public List<string> GameVersions { get; } = new List<string>();
    public List<string> ModLoaders { get; } = new List<string> { "All", "Fabric", "Forge" };

    public DashboardViewModel()
    {
        _templateManager = new TemplateManager();

        // Initialize filtered projects first
        FilteredProjects = CollectionViewSource.GetDefaultView(AllProjects);
        FilteredProjects.Filter = FilterProject;

        // Then load projects
        LoadProjects();
    }

    private bool FilterProject(object obj)
    {
        if (obj is ModProjectData project)
        {
            // Check search text
            bool matchesSearch = string.IsNullOrWhiteSpace(SearchText) ||
                                project.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                project.ModId.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                project.Package.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

            // Check game version
            bool matchesGameVersion = SelectedGameVersion == "All" ||
                                      project.MinecraftVersion == SelectedGameVersion;

            // Check mod loader
            bool matchesModLoader = SelectedModLoader == "All" ||
                                    (SelectedModLoader == "Fabric" && project.ModType == "Fabric Mod") ||
                                    (SelectedModLoader == "Forge" && project.ModType == "Forge Mod");

            return matchesSearch && matchesGameVersion && matchesModLoader;
        }
        return false;
    }

    private void LoadProjects()
    {
        var loadedProjects = TemplateManager.LoadAllProjects();
        AllProjects.Clear();

        // Collect unique game versions
        var versions = new HashSet<string>();
        foreach (var project in loadedProjects)
        {
            AllProjects.Add(project);
            if (!string.IsNullOrEmpty(project.MinecraftVersion))
            {
                versions.Add(project.MinecraftVersion);
            }
        }

        // Update game versions list
        GameVersions.Clear();
        GameVersions.Add("All");
        GameVersions.AddRange(versions.OrderByDescending(v => v));
        OnPropertyChanged(nameof(GameVersions));

        // Only refresh if FilteredProjects is initialized
        FilteredProjects?.Refresh();
    }

    [RelayCommand]
    private async Task CreateNewProject()
    {
        var newProjectWindow = new NewProject
        {
            Owner = App.Current.MainWindow
        };

        if (newProjectWindow.ShowDialog() == true && newProjectWindow.ProjectData != null)
        {
            // Just reload projects
            LoadProjects();
        }
    }

    [RelayCommand]
    public void RefreshProjects()
    {
        LoadProjects();
    }

    [RelayCommand]
    public async Task DeleteProject(ModProjectData project)
    {
        try
        {
            if (project != null && AllProjects.Contains(project))
            {
                string projectDir = project.Location;
                if (Directory.Exists(projectDir))
                {
                    // Normalize file attributes
                    foreach (var file in Directory.EnumerateFiles(projectDir, "*.*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }
                    foreach (var dir in Directory.EnumerateDirectories(projectDir, "*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(dir, FileAttributes.Normal);
                    }
                    File.SetAttributes(projectDir, FileAttributes.Normal);

                    // Attempt to delete
                    try
                    {
                        Directory.Delete(projectDir, true);
                    }
                    catch
                    {
                        // Fallback deletion method
                        foreach (var file in Directory.EnumerateFiles(projectDir, "*.*", SearchOption.AllDirectories))
                        {
                            File.Delete(file);
                        }
                        foreach (var dir in Directory.EnumerateDirectories(projectDir, "*", SearchOption.TopDirectoryOnly).Reverse())
                        {
                            Directory.Delete(dir, true);
                        }
                        Directory.Delete(projectDir);
                    }

                    // Remove from collection
                    AllProjects.Remove(project);
                    FilteredProjects?.Refresh();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error deleting project: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenProjectFolder(ModProjectData project)
    {
        if (project != null && Directory.Exists(project.Location))
        {
            Process.Start("explorer.exe", project.Location);
        }
    }

    [RelayCommand]
    private void OpenProjectsFolder()
    {
        var projectsPath = TemplateManager.ProjectsBasePath;
        if (Directory.Exists(projectsPath))
        {
            Process.Start("explorer.exe", projectsPath);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        FilteredProjects?.Refresh();
    }

    partial void OnSelectedGameVersionChanged(string value)
    {
        FilteredProjects?.Refresh();
    }

    partial void OnSelectedModLoaderChanged(string value)
    {
        FilteredProjects?.Refresh();
    }
}