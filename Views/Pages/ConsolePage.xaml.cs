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
        // שדה סטטי שבו נשמור מה לבנות ברגע שנגיע לעמוד
        public static (string ProjectDir, string Tasks, string JdkHome)? PendingBuild;

        public ConsolePage()
        {
            InitializeComponent();
            Loaded += ConsolePage_Loaded;
        }

        private void ConsolePage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (PendingBuild is { } info)
            {
                PendingBuild = null; // כדי שלא ירוץ שוב אם נטען מחדש
                StartGradleBuild(info.ProjectDir, info.Tasks, info.JdkHome);
            }
        }

        public void StartGradleBuild(string projectDir, string gradleTasks, string jdkHome)
        {
            // בחר את הסקריפט הנכון
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

            // קישורים ישירים לפלט
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

            // שורה אפורה שמציגה בדיוק מה רצינו להריץ
            AppendLine($"$ {wrapperPath} {gradleTasks}", Brushes.Gray);

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
        }

        private void AppendLine(string text, Brush color)
        {
            Dispatcher.Invoke(() =>
            {
                var run = new Run(text) { Foreground = color };
                var para = new Paragraph(run) { Margin = new System.Windows.Thickness(0) };
                ConsoleOutput.Document.Blocks.Add(para);
                ConsoleOutput.ScrollToEnd();
            });
        }
    }
}
