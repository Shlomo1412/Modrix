using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Modrix.Models;
using Modrix.ViewModels.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace Modrix.Views.Windows
{
    public partial class ProjectWorkspace : FluentWindow, INavigationWindow
    {
        public ProjectWorkspaceViewModel ViewModel { get; }
        private SnackbarPresenter _snackbarPresenter;

        public ProjectWorkspace(
            ProjectWorkspaceViewModel viewModel,
            INavigationViewPageProvider navigationViewPageProvider,
            INavigationService navigationService
        )
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
            SetPageService(navigationViewPageProvider);

            navigationService.SetNavigationControl(RootNavigation);

            Loaded += ProjectWorkspace_Loaded;
        }

        private void ProjectWorkspace_Loaded(object sender, RoutedEventArgs e)
        {
            _snackbarPresenter = this.SnackbarPresenter;
        }

       

        public void LoadProject(ModProjectData project) => ViewModel.LoadProject(project);

        public INavigationView GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(INavigationViewPageProvider provider)
        {
            RootNavigation.SetPageProviderService(provider);
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            // Not needed
        }

        private async void BuildButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.CurrentProject == null)
            {
                ShowSnackbar("Please open a project first", "No project loaded");
                return;
            }

            var project = ViewModel.CurrentProject;
            var projectDir = project.Location;

            if (!Directory.Exists(projectDir))
            {
                ShowSnackbar("Could not build project", "Project directory not found");
                return;
            }

            try
            {

                
                // Show progress snackbar
                var progressSnackbar = new Snackbar(_snackbarPresenter)
                {
                    Title = "Building Project",
                    Content = "Preparing build environment...",
                    Timeout = TimeSpan.Zero, // No timeout
                    Appearance = ControlAppearance.Info
                };
                progressSnackbar.Show();

                // Ensure correct JDK is available
                var jdkHelper = new JdkHelper();
                var progress = new Progress<(string Message, int Progress)>(report =>
                {
                    progressSnackbar.Content = report.Message;
                });

                string jdkHome = await jdkHelper.EnsureRequiredJdkAsync(
                    project.MinecraftVersion,
                    progress
                );

                // Run Gradle build
                await RunGradleBuildAsync(projectDir, "build", jdkHome, progress);

                // Update snackbar to show success
                progressSnackbar.Content = "Build succeeded!";
                progressSnackbar.Appearance = ControlAppearance.Success;
                progressSnackbar.Timeout = TimeSpan.FromSeconds(3);
            }
            catch (Exception ex)
            {
                ShowSnackbar(ex.Message, "Build Failed", ControlAppearance.Danger);
            }
        }

        private async Task RunGradleBuildAsync(
            string projectDir,
            string gradleTasks,
            string jdkHome,
            IProgress<(string Message, int Progress)> progress)
        {
            // Determine Gradle wrapper based on OS
            var wrapper = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                "gradlew.bat" : "gradlew";
            var wrapperPath = Path.Combine(projectDir, wrapper);

            if (!File.Exists(wrapperPath))
            {
                throw new FileNotFoundException($"Gradle wrapper not found at {wrapperPath}");
            }

            progress.Report(("Starting Gradle build...", 10));

            var startInfo = new ProcessStartInfo
            {
                FileName = wrapperPath,
                Arguments = gradleTasks,
                WorkingDirectory = projectDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Set JAVA_HOME if we have it
            if (!string.IsNullOrEmpty(jdkHome))
            {
                startInfo.EnvironmentVariables["JAVA_HOME"] = jdkHome;
            }

            using var process = new Process { StartInfo = startInfo };

            // Capture output
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    progress.Report(($"[Gradle] {e.Data}", 0));
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    progress.Report(($"[Gradle][ERR] {e.Data}", 0));
                }
            };

            // Start process
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for completion
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Gradle build failed with exit code {process.ExitCode}");
            }

            progress.Report(("Build completed successfully", 100));
        }

        private void ShowSnackbar(string message, string title,
                                 ControlAppearance appearance = ControlAppearance.Info)
        {
            if (_snackbarPresenter == null) return;

            var snackbar = new Snackbar(_snackbarPresenter)
            {
                Title = title,
                Content = message,
                Timeout = TimeSpan.FromSeconds(3),
                Appearance = appearance
            };
            snackbar.Show();
        }

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();
    }

    public static class ProcessExtensions
    {
        public static Task WaitForExitAsync(this Process process)
        {
            var tcs = new TaskCompletionSource();
            process.EnableRaisingEvents = true;
            process.Exited += (s, e) => tcs.TrySetResult();

            if (process.HasExited)
            {
                tcs.TrySetResult();
            }

            return tcs.Task;
        }
    }
}