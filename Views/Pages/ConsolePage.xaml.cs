using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace Modrix.Views.Pages
{
    public partial class ConsolePage : Page
    {
        public static (string ProjectDir, string Tasks, string JdkHome)? PendingBuild;

        private int _lineNumber;
        private bool _showIndex = false;
        private bool _autoScroll = true;
        private bool _showStats = true;

        private Process _minecraftProcess;
        private CancellationTokenSource _monitoringCts;
        private bool _isMinecraftRunning = false;

        // Performance monitoring
        private readonly List<PerformanceData> _performanceHistory = new List<PerformanceData>();
        private const int MaxHistoryPoints = 60; // One minute of data at 1-second intervals

        // Chart drawing
        private readonly Brush _cpuBrush = Brushes.DodgerBlue;
        private readonly Brush _memoryBrush = Brushes.LimeGreen;
        private readonly Pen _cpuPen;
        private readonly Pen _memoryPen;

        public ConsolePage()
        {
            InitializeComponent();
            Loaded += ConsolePage_Loaded;
            Unloaded += ConsolePage_Unloaded;

            // Initialize pens for chart drawing
            _cpuPen = new Pen(_cpuBrush, 2);
            _memoryPen = new Pen(_memoryBrush, 2);
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

            // Initialize UI based on current settings
            UpdateStatsPanelVisibility();
        }

        private void ConsolePage_Unloaded(object sender, RoutedEventArgs e)
        {
            StopPerformanceMonitoring();
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
                {
                    AppendLine(e.Data, Brushes.White);

                    // More robust trigger for Minecraft process detection
                    if (e.Data.Contains("Loading Minecraft", StringComparison.OrdinalIgnoreCase) ||
                        e.Data.Contains("Setting user:", StringComparison.OrdinalIgnoreCase) ||
                        e.Data.Contains("Backend library:", StringComparison.OrdinalIgnoreCase) ||
                        e.Data.Contains("Minecraft", StringComparison.OrdinalIgnoreCase)) // fallback: any line with 'Minecraft'
                    {
                        DetectMinecraftProcess();
                    }
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
            };

            AppendLine($"$ {wrapperPath} {gradleTasks}", Brushes.Gray);

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
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

        private void ChkShowStats_Changed(object sender, RoutedEventArgs e)
        {
            _showStats = ChkShowStats.IsChecked == true;
            UpdateStatsPanelVisibility();
        }

        private void UpdateStatsPanelVisibility()
        {
            // Check if UI elements have been initialized
            if (StatsPanel == null || ConsoleSplitter == null || 
                ConsoleColumn == null || StatsColumn == null)
            {
                // Elements not initialized yet, log and return
                Debug.WriteLine("UI elements not initialized yet in UpdateStatsPanelVisibility");
                return;
            }

            if (_showStats && _isMinecraftRunning)
            {
                // Show stats panel
                StatsPanel.Visibility = Visibility.Visible;
                ConsoleSplitter.Visibility = Visibility.Visible;

                // Adjust column widths for split view
                ConsoleColumn.Width = new GridLength(2, GridUnitType.Star);
                StatsColumn.Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                // Hide stats panel
                StatsPanel.Visibility = Visibility.Collapsed;
                ConsoleSplitter.Visibility = Visibility.Collapsed;

                // Adjust column widths for full console view
                ConsoleColumn.Width = new GridLength(1, GridUnitType.Star);
                StatsColumn.Width = new GridLength(0, GridUnitType.Star);
            }
        }

        private void DetectMinecraftProcess()
        {
            Task.Run(async () =>
            {
                try
                {
                    const int maxAttempts = 10;
                    const int delayMs = 1000;
                    for (int attempt = 0; attempt < maxAttempts; attempt++)
                    {
                        // Look for Java processes that might be Minecraft
                        var javaProcesses = Process.GetProcessesByName("java");
                        Debug.WriteLine($"[DetectMinecraftProcess] Attempt {attempt + 1}/{maxAttempts}: Found {javaProcesses.Length} java processes");
                        foreach (var process in javaProcesses)
                        {
                            try
                            {
                                var commandLine = GetCommandLine(process.Id);
                                Debug.WriteLine($"[DetectMinecraftProcess] PID {process.Id} CommandLine: {commandLine}");
                                if (commandLine.Contains("minecraft", StringComparison.OrdinalIgnoreCase) ||
                                    commandLine.Contains("runClient", StringComparison.OrdinalIgnoreCase) ||
                                    commandLine.Contains("net.minecraft", StringComparison.OrdinalIgnoreCase))
                                {
                                    _minecraftProcess = process;
                                    Dispatcher.Invoke(() =>
                                    {
                                        AppendLine($"[INFO] Detected Minecraft process (PID: {process.Id})", Brushes.LimeGreen);
                                        if (GameStatusText != null)
                                        {
                                            GameStatusText.Text = "Running";
                                        }
                                        _isMinecraftRunning = true;
                                        UpdateStatsPanelVisibility();
                                        if (_minecraftProcess != null)
                                            StartPerformanceMonitoring();
                                    });
                                    return;
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[DetectMinecraftProcess] Exception: {ex.Message}");
                            }
                        }
                        await Task.Delay(delayMs);
                    }
                    Dispatcher.Invoke(() =>
                    {
                        AppendLine("[WARN] Could not detect Minecraft process", Brushes.Orange);
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        AppendLine($"[ERROR] Error detecting Minecraft process: {ex.Message}", Brushes.Red);
                    });
                }
            });
        }

        private string GetCommandLine(int processId)
        {
            // This is a simplified approach - in a real implementation,
            // you would use platform-specific code to get the command line
            try
            {
                string wmiQuery = $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}";
                using var searcher = new System.Management.ManagementObjectSearcher(wmiQuery);
                using var results = searcher.Get();

                foreach (var result in results)
                {
                    return result["CommandLine"]?.ToString() ?? string.Empty;
                }
            }
            catch
            {
                // Fall back to just process name if WMI query fails
            }

            return string.Empty;
        }

        private void StartPerformanceMonitoring()
        {
            // Stop any existing monitoring
            StopPerformanceMonitoring();

            if (_minecraftProcess == null)
            {
                AppendLine("[ERROR] Minecraft process not found for monitoring.", Brushes.Red);
                return;
            }

            _monitoringCts = new CancellationTokenSource();
            var token = _monitoringCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    _performanceHistory.Clear();

                    // Process counter setup
                    var cpuCounter = new PerformanceCounter("Process", "% Processor Time", _minecraftProcess.ProcessName);
                    var ramCounter = new PerformanceCounter("Process", "Working Set", _minecraftProcess.ProcessName);

                    // Get the first reading (this is needed to initialize the counter)
                    cpuCounter.NextValue();
                    ramCounter.NextValue();

                    // Wait a second for the CPU counter to be valid
                    await Task.Delay(1000, token);

                    while (!token.IsCancellationRequested && !_minecraftProcess.HasExited)
                    {
                        try
                        {
                            // Get current values
                            var cpuUsage = cpuCounter.NextValue() / Environment.ProcessorCount;
                            var ramUsage = ramCounter.NextValue() / (1024 * 1024); // Convert to MB

                            var data = new PerformanceData
                            {
                                Timestamp = DateTime.Now,
                                CpuUsage = cpuUsage,
                                MemoryUsageMB = ramUsage
                            };

                            // Add to history and keep only the last N points
                            _performanceHistory.Add(data);
                            if (_performanceHistory.Count > MaxHistoryPoints)
                                _performanceHistory.RemoveAt(0);

                            // Update UI
                            Dispatcher.Invoke(() => UpdatePerformanceUI(data));

                            // Wait for next reading
                            await Task.Delay(1000, token);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                AppendLine($"[ERROR] Performance monitoring error: {ex.Message}", Brushes.Red);
                            });
                            await Task.Delay(5000, token); // Wait longer after an error
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal cancellation
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        AppendLine($"[ERROR] Performance monitoring failed: {ex.Message}", Brushes.Red);
                    });
                }
                finally
                {
                    Dispatcher.Invoke(() =>
                    {
                        // Check if UI elements are initialized
                        if (GameStatusText != null)
                        {
                            GameStatusText.Text = "Not Running";
                        }
                        
                        _isMinecraftRunning = false;
                        UpdateStatsPanelVisibility();
                    });
                }
            }, token);
        }

        private void StopPerformanceMonitoring()
        {
            if (_monitoringCts != null)
            {
                _monitoringCts.Cancel();
                _monitoringCts.Dispose();
                _monitoringCts = null;
            }

            if (_minecraftProcess != null)
            {
                _minecraftProcess = null;
            }
        }

        private void UpdatePerformanceUI(PerformanceData data)
        {
            // Check if UI elements are initialized
            if (CpuProgressBar == null || MemoryProgressBar == null ||
                CpuUsageText == null || MemoryUsageText == null ||
                PerformanceChart == null || AdditionalStatsPanel == null)
            {
                Debug.WriteLine("UI elements not initialized yet in UpdatePerformanceUI");
                return;
            }

            // Update progress bars
            CpuProgressBar.Value = data.CpuUsage;
            MemoryProgressBar.Value = Math.Min(data.MemoryUsageMB / 10, 100); // Scale memory usage

            // Update text values
            CpuUsageText.Text = $"{data.CpuUsage:0.0}%";
            MemoryUsageText.Text = $"{data.MemoryUsageMB:0} MB";

            // Draw performance chart
            DrawPerformanceChart();

            // Update additional stats
            UpdateAdditionalStats(data);
        }

        private void DrawPerformanceChart()
        {
            if (_performanceHistory.Count < 2)
                return;

            PerformanceChart.Children.Clear();

            double chartWidth = PerformanceChart.ActualWidth;
            double chartHeight = PerformanceChart.ActualHeight;

            // Draw background grid
            DrawChartGrid(chartWidth, chartHeight);

            // Draw CPU line
            var cpuPoints = new PointCollection();
            for (int i = 0; i < _performanceHistory.Count; i++)
            {
                double x = i * (chartWidth / (MaxHistoryPoints - 1));
                double y = chartHeight - (_performanceHistory[i].CpuUsage * chartHeight / 100);
                cpuPoints.Add(new Point(x, y));
            }

            var cpuPolyline = new Polyline
            {
                Points = cpuPoints,
                Stroke = _cpuBrush,
                StrokeThickness = 2
            };
            PerformanceChart.Children.Add(cpuPolyline);

            // Draw memory line
            var memPoints = new PointCollection();
            for (int i = 0; i < _performanceHistory.Count; i++)
            {
                double x = i * (chartWidth / (MaxHistoryPoints - 1));
                double y = chartHeight - (Math.Min(_performanceHistory[i].MemoryUsageMB / 10, 100) * chartHeight / 100);
                memPoints.Add(new Point(x, y));
            }

            var memPolyline = new Polyline
            {
                Points = memPoints,
                Stroke = _memoryBrush,
                StrokeThickness = 2
            };
            PerformanceChart.Children.Add(memPolyline);
        }

        private void DrawChartGrid(double width, double height)
        {
            // Draw horizontal lines at 25%, 50%, 75%
            for (int i = 1; i < 4; i++)
            {
                double y = height * i / 4;
                var line = new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = width,
                    Y2 = y,
                    Stroke = Brushes.DimGray,
                    StrokeThickness = 0.5
                };
                PerformanceChart.Children.Add(line);
            }
        }

        private void UpdateAdditionalStats(PerformanceData data)
        {
            AdditionalStatsPanel.Children.Clear();

            // Calculate additional stats
            double avgCpu = 0;
            double peakCpu = 0;
            double avgMem = 0;
            double peakMem = 0;

            foreach (var point in _performanceHistory)
            {
                avgCpu += point.CpuUsage;
                avgMem += point.MemoryUsageMB;
                peakCpu = Math.Max(peakCpu, point.CpuUsage);
                peakMem = Math.Max(peakMem, point.MemoryUsageMB);
            }

            if (_performanceHistory.Count > 0)
            {
                avgCpu /= _performanceHistory.Count;
                avgMem /= _performanceHistory.Count;
            }

            // Add stats to panel
            AddStatTextBlock("Process ID", _minecraftProcess?.Id.ToString() ?? "Unknown");
            AddStatTextBlock("Process Name", _minecraftProcess?.ProcessName ?? "Unknown");
            AddStatTextBlock("Peak CPU Usage", $"{peakCpu:0.0}%");
            AddStatTextBlock("Average CPU Usage", $"{avgCpu:0.0}%");
            AddStatTextBlock("Peak Memory Usage", $"{peakMem:0} MB");
            AddStatTextBlock("Average Memory Usage", $"{avgMem:0} MB");
            AddStatTextBlock("Running Time",
                _minecraftProcess != null ?
                (DateTime.Now - _minecraftProcess.StartTime).ToString(@"hh\:mm\:ss") :
                "00:00:00");
            AddStatTextBlock("Monitoring Started", _performanceHistory.Count > 0 ?
                _performanceHistory[0].Timestamp.ToString("HH:mm:ss") :
                "N/A");
        }

        private void AddStatTextBlock(string label, string value)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

            panel.Children.Add(new TextBlock
            {
                Text = label + ": ",
                FontWeight = FontWeights.Bold,
                Width = 150
            });

            panel.Children.Add(new TextBlock { Text = value });

            AdditionalStatsPanel.Children.Add(panel);
        }
    }

    public class PerformanceData
    {
        public DateTime Timestamp { get; set; }
        public float CpuUsage { get; set; }
        public float MemoryUsageMB { get; set; }
    }
}