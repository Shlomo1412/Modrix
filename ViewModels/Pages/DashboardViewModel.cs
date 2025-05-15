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
            // טען מחדש את הרשימה מיד לאחר יצירת הפרויקט
            LoadProjects();
        }
    }

    [RelayCommand] // הוספת פקודת ריענון
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
                // מחיקת תיקיית הפרויקט המלאה
                string projectDir = project.Location; // זה הנתיב המלא לתיקיית הפרויקט
                if (Directory.Exists(projectDir))
                {
                    // ננסה למחוק את כל הקבצים (גם אם הם read-only)
                    foreach (string file in Directory.GetFiles(projectDir, "*.*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }

                    Directory.Delete(projectDir, true);
                    _projects.Remove(project);
                    // רענון הרשימה
                    LoadProjects();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error deleting project: {ex.Message}");
            // אפשר להוסיף פה הודעת שגיאה למשתמש
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
