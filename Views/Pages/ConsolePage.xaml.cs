using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Path = System.IO.Path;

namespace Modrix.Views.Pages
{
    public partial class ConsolePage : Page
    {
        public static (string ProjectDir, string Tasks, string JdkHome)? PendingBuild;

        private int _lineNumber;
        private bool _showIndex = false;
        private bool _autoScroll = true;
        private bool _isBuildRunning = false;
        private Process? _currentProcess;

        public ConsolePage()
        {
            InitializeComponent();
            Loaded += ConsolePage_Loaded;
        }

        private void ConsolePage_Loaded(object sender, RoutedEventArgs e)
        {
            StartPendingBuildIfAny();
        }

        /// <summary>
        /// Call this to start a pending build if one is set. Safe to call multiple times.
        /// </summary>
        public void StartPendingBuildIfAny()
        {
            if (PendingBuild is { } info)
            {
                // If a build is running, kill it and clean up
                if (_currentProcess != null && !_currentProcess.HasExited)
                {
                    try { _currentProcess.Kill(true); } catch { }
                    _currentProcess.Dispose();
                    _currentProcess = null;
                }
                PendingBuild = null;
                _lineNumber = 0;
                ConsoleOutput.Document.Blocks.Clear();
                _isBuildRunning = true;
                _ = StartGradleBuild(info.ProjectDir, info.Tasks, info.JdkHome);
            }
        }

        public async Task StartGradleBuild(string projectDir, string gradleTasks, string jdkHome)
        {
            // Ensure correct JDK version for the project
            string requiredVersion = GetRequiredJavaVersionFromProject(projectDir);
            AppendLine($"[DEBUG] Required Java version: {requiredVersion}", Brushes.Yellow);
            AppendLine($"[DEBUG] Current JDK path: {jdkHome}", Brushes.Yellow);
            
            if (!string.IsNullOrEmpty(requiredVersion) && !string.IsNullOrEmpty(jdkHome) && !jdkHome.Contains($"jdk-{requiredVersion}"))
            {
                AppendLine($"[DEBUG] JDK version mismatch detected, searching for correct JDK...", Brushes.Yellow);
                // Try to find the correct JDK again
                var jdkHelper = new JdkHelper();
                var correctJdk = jdkHelper.GetInstalledJdks()
                    .FirstOrDefault(j => j.Version.StartsWith(requiredVersion));
                if (correctJdk != null)
                {
                    jdkHome = correctJdk.Path;
                    AppendLine($"[DEBUG] Found correct JDK: {jdkHome}", Brushes.Yellow);
                }
                else
                {
                    AppendLine($"[DEBUG] No matching JDK found for version {requiredVersion}", Brushes.Yellow);
                }
            }
            else
            {
                AppendLine($"[DEBUG] Using JDK path as provided", Brushes.Yellow);
            }

            // Check if build.gradle needs Java version fixes
            await FixBuildGradleJavaVersion(projectDir, requiredVersion);

            // Kill any previous process if still running
            if (_currentProcess != null && !_currentProcess.HasExited)
            {
                try { _currentProcess.Kill(true); } catch { }
                _currentProcess.Dispose();
                _currentProcess = null;
            }

            var wrapper = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                          ? "gradlew.bat"
                          : "gradlew";
            var wrapperPath = Path.Combine(projectDir, wrapper);

            if (!File.Exists(wrapperPath))
            {
                AppendLine($"[ERROR] Gradle wrapper not found at {wrapperPath}", Brushes.Red);
                _isBuildRunning = false;
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = wrapperPath,
                Arguments = gradleTasks,
                WorkingDirectory = projectDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (!string.IsNullOrEmpty(jdkHome))
                psi.EnvironmentVariables["JAVA_HOME"] = jdkHome;

            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _currentProcess = proc;

            proc.OutputDataReceived += (s, e) => {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    AppendLine(e.Data, Brushes.White);
                }
            };
            proc.ErrorDataReceived += (s, e) => {
                if (!string.IsNullOrEmpty(e.Data))
                    AppendLine(e.Data, Brushes.OrangeRed);
            };
            proc.Exited += (s, e) =>
            {
                var success = proc.ExitCode == 0;
                AppendLine(
                  success
                    ? $"[BUILD SUCCEEDED - exit {proc.ExitCode}]"
                    : $"[BUILD FAILED - exit {proc.ExitCode}]",
                  success ? Brushes.LimeGreen : Brushes.Red
                );
                _isBuildRunning = false;
                // Clean up
                try { proc.Dispose(); } catch { }
                if (_currentProcess == proc) _currentProcess = null;
            };

            AppendLine($"$ {wrapperPath} {gradleTasks}", Brushes.Gray);

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
        }

        private async Task FixBuildGradleJavaVersion(string projectDir, string requiredVersion)
        {
            if (string.IsNullOrEmpty(requiredVersion)) return;

            var buildGradlePath = Path.Combine(projectDir, "build.gradle");
            if (!File.Exists(buildGradlePath)) return;

            try
            {
                var content = await File.ReadAllTextAsync(buildGradlePath);
                bool modified = false;

                // Fix sourceCompatibility and targetCompatibility
                if (content.Contains("sourceCompatibility") || content.Contains("targetCompatibility"))
                {
                    content = Regex.Replace(content, 
                        @"sourceCompatibility\s*=\s*JavaVersion\.VERSION_\d+", 
                        $"sourceCompatibility = JavaVersion.VERSION_{requiredVersion}");
                    content = Regex.Replace(content, 
                        @"targetCompatibility\s*=\s*JavaVersion\.VERSION_\d+", 
                        $"targetCompatibility = JavaVersion.VERSION_{requiredVersion}");
                    modified = true;
                }

                // Fix java toolchain if present
                if (content.Contains("java.toolchain.languageVersion"))
                {
                    content = Regex.Replace(content,
                        @"java\.toolchain\.languageVersion\s*=\s*JavaLanguageVersion\.of\(\d+\)",
                        $"java.toolchain.languageVersion = JavaLanguageVersion.of({requiredVersion})");
                    modified = true;
                }

                // Fix compileJava options if present
                if (content.Contains("compileJava"))
                {
                    content = Regex.Replace(content,
                        @"options\.release\s*=\s*\d+",
                        $"options.release = {requiredVersion}");
                    modified = true;
                }

                if (modified)
                {
                    await File.WriteAllTextAsync(buildGradlePath, content);
                    AppendLine($"[DEBUG] Fixed build.gradle Java version to {requiredVersion}", Brushes.Yellow);
                }
            }
            catch (Exception ex)
            {
                AppendLine($"[DEBUG] Failed to fix build.gradle: {ex.Message}", Brushes.Yellow);
            }
        }

        private string GetRequiredJavaVersionFromProject(string projectDir)
        {
            // Try to read modrix.config for MinecraftVersion
            string configPath = Path.Combine(projectDir, "modrix.config");
            string mcVersion = null;
            if (File.Exists(configPath))
            {
                try
                {
                    var lines = File.ReadAllLines(configPath);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("MinecraftVersion="))
                        {
                            mcVersion = line.Substring("MinecraftVersion=".Length).Trim();
                            break;
                        }
                    }
                }
                catch { }
            }
            // Fallback: try gradle.properties
            if (mcVersion == null)
            {
                string gradleProps = Path.Combine(projectDir, "gradle.properties");
                if (File.Exists(gradleProps))
                {
                    try
                    {
                        var lines = File.ReadAllLines(gradleProps);
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("minecraft_version="))
                            {
                                mcVersion = line.Substring("minecraft_version=".Length).Trim();
                                break;
                            }
                        }
                    }
                    catch { }
                }
            }
            if (string.IsNullOrEmpty(mcVersion)) return null;
            // Parse version: 1.20.x => 17, 1.21.x => 21
            var parts = mcVersion.Split('.');
            if (parts.Length < 2) return null;
            if (!int.TryParse(parts[1], out int minor)) return null;
            return minor >= 21 ? "21" : minor >= 17 ? "17" : "8";
        }

        private void AppendLine(string text, Brush defaultColor)
        {
            Debug.WriteLine($"[ConsoleOutput] {text}"); // Log every line for debugging
            Dispatcher.Invoke(() =>
            {
                _lineNumber++;
                var display = _showIndex
                  ? $"{_lineNumber:000} | {text}"
                  : text;

                var color = defaultColor;
                if (text.StartsWith("> Task")) color = Brushes.CornflowerBlue;
                else if (text.StartsWith("> Configure")) color = Brushes.MediumPurple;
                else if (text.Contains("INFO")) color = Brushes.ForestGreen;
                else if (text.Contains("WARN")) color = Brushes.Orange;
                else if (text.Contains("ERROR") || text.Contains("FAIL")) color = Brushes.Red;
                else if (text.StartsWith("$")) color = Brushes.Gray;

                var run = new Run(display) { Foreground = color };
                var para = new Paragraph(run) { Margin = new Thickness(0) };
                ConsoleOutput.Document.Blocks.Add(para);

                if (_autoScroll)
                    ConsoleOutput.ScrollToEnd();
            });
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _lineNumber = 0;
            ConsoleOutput.Document.Blocks.Clear();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            var textRange = new TextRange(
                ConsoleOutput.Document.ContentStart,
                ConsoleOutput.Document.ContentEnd);
            Clipboard.SetText(textRange.Text);
        }

        private void ShareButton_Click(object sender, RoutedEventArgs e)
        {
            var tempPath = Path.Combine(
                Path.GetTempPath(),
                $"ModrixConsole_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            var textRange = new TextRange(
                ConsoleOutput.Document.ContentStart,
                ConsoleOutput.Document.ContentEnd);
            File.WriteAllText(tempPath, textRange.Text);
            Process.Start("explorer.exe", $"/select,\"{tempPath}\"");
        }

        private void ChkLineIndex_Changed(object sender, RoutedEventArgs e)
        {
            _showIndex = ChkLineIndex.IsChecked == true;
        }

        private void ChkAutoScroll_Changed(object sender, RoutedEventArgs e)
        {
            _autoScroll = ChkAutoScroll.IsChecked == true;
        }
    }
}