using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Modrix.Views.Pages
{
    public partial class ConsolePage : Page
    {
        public ConsolePage()
        {
            InitializeComponent();
            Loaded += ConsolePage_Loaded;
        }

        private void ConsolePage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            RunGradleTask("build");
        }

        private void RunGradleTask(string task)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C gradlew {task}",
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.OutputDataReceived += (s, e) => AppendConsoleLine(e.Data, Brushes.White);
            process.ErrorDataReceived += (s, e) => AppendConsoleLine(e.Data, Brushes.Red);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        private void AppendConsoleLine(string text, Brush color)
        {
            if (string.IsNullOrEmpty(text)) return;

            Dispatcher.Invoke(() =>
            {
                var para = new Paragraph(new Run(text)) { Foreground = color, Margin = new Thickness(0) };
                ConsoleOutput.Document.Blocks.Add(para);
                ConsoleOutput.ScrollToEnd();
            });
        }
    }
}
