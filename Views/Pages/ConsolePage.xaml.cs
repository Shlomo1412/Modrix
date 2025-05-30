using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Modrix.Views.Pages
{
    public partial class ConsolePage : Page
    {
        
        public static (string ProjectDir, string Tasks, string JdkHome)? PendingBuild;

        private int _lineNumber;
        private bool _showIndex = false;
        private bool _autoScroll = true;

        public ConsolePage()
        {
            InitializeComponent();
            Loaded += ConsolePage_Loaded;
        }

        private void ConsolePage_Loaded(object sender, RoutedEventArgs e)
        {
            // reset console
            _lineNumber = 0;
            ConsoleOutput.Document.Blocks.Clear();

            if (PendingBuild is { } info)
            {
                PendingBuild = null;
                StartGradleBuild(info.ProjectDir, info.Tasks, info.JdkHome);
            }
        }

        public async Task StartGradleBuild(string projectDir, string gradleTasks, string jdkHome)
        {
            
            var wrapper = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                          ? "gradlew.bat"
                          : "gradlew";
            var wrapperPath = Path.Combine(projectDir, wrapper);

            if (!File.Exists(wrapperPath))
            {
                AppendLine($"[ERROR] Gradle wrapper not found at {wrapperPath}", Brushes.Red);
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

            
            proc.OutputDataReceived += (s, e) => {
                if (!string.IsNullOrEmpty(e.Data))
                    AppendLine(e.Data, Brushes.White);
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
            };

            AppendLine($"$ {wrapperPath} {gradleTasks}", Brushes.Gray);

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
        }

        private void AppendLine(string text, Brush defaultColor)
        {
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
