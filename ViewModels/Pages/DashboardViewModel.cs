using Modrix.Models;
using Modrix.Services;
using Modrix.Views.Windows;
using Modrix;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

public partial class DashboardViewModel : ObservableObject
{
    private readonly TemplateManager _templateManager;

    [ObservableProperty]
    private ObservableCollection<ModProjectData> _projects;

    public DashboardViewModel()
    {
        _templateManager = new TemplateManager();
        _projects = new ObservableCollection<ModProjectData>();
        LoadProjects();
    }

    private void LoadProjects()
    {
        var loadedProjects = TemplateManager.LoadAllProjects();
        _projects.Clear();
        foreach (var project in loadedProjects)
        {
            _projects.Add(project);
        }
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
            LoadProjects();
            RefreshProjects();
        }
    }

    [RelayCommand]
    private void RefreshProjects()
    {
        LoadProjects();
    }

    [RelayCommand]
    public async Task DeleteProject(ModProjectData project)
    {
        try
        {
            if (project != null)
            {
                string projectDir = project.Location;
                if (Directory.Exists(projectDir))
                {
                    // קודם נשחרר את כל הקבצים והתיקיות מ-read-only באופן רקורסיבי
                    foreach (var file in Directory.EnumerateFiles(projectDir, "*.*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }
                    foreach (var dir in Directory.EnumerateDirectories(projectDir, "*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(dir, FileAttributes.Normal);  // שימוש ב-File.SetAttributes במקום Directory.SetAttributes
                    }
                    // הסרת read-only מהתיקייה הראשית
                    File.SetAttributes(projectDir, FileAttributes.Normal);

                    // ננסה למחוק את התיקייה באופן רקורסיבי
                    try
                    {
                        Directory.Delete(projectDir, true);
                    }
                    catch
                    {
                        // אם נכשל, ננסה למחוק קודם את הקבצים ואז את התיקיות
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

                    _projects.Remove(project);
                    LoadProjects();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error deleting project: {ex.Message}");
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
}