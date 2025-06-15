using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Modrix.Services;
using Modrix.Views.Windows;

namespace Modrix.Views.Pages
{
    public partial class FeedbackPage : Page, INotifyPropertyChanged
    {
        private bool _isLoadingChecklist;
        private string _roadmapContent = "Loading roadmap...";
        private readonly HttpClient _httpClient;
        private readonly string _checklistUrl = "https://raw.githubusercontent.com/Shlomo1412/Modrix/master/Checklist.md";
        private readonly string _roadmapSectionAnchor = "#roadmap-1";
        private readonly string _issueUrl = "https://github.com/Shlomo1412/Modrix/issues/new";
        private readonly string _roadmapUrl = "https://github.com/Shlomo1412/Modrix/blob/master/Checklist.md#roadmap-1";

        public bool IsLoadingChecklist
        {
            get => _isLoadingChecklist;
            set { _isLoadingChecklist = value; OnPropertyChanged(); }
        }

        private void ExploreSourceCode_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var dialog = new ExploreSourceCodeDialog();
                dialog.Owner = Application.Current.Windows.Count > 0 ? Application.Current.Windows[0] : null;
                dialog.ShowDialog();
            });
        }
        public string RoadmapContent
        {
            get => _roadmapContent;
            set { _roadmapContent = value; OnPropertyChanged(); }
        }

        public FeedbackPage()
        {
            InitializeComponent();
            DataContext = this;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ModrixApp/1.0");
            LoadRoadmapAsync();
        }

        private async void LoadRoadmapAsync()
        {
            IsLoadingChecklist = true;
            try
            {
                var content = await _httpClient.GetStringAsync(_checklistUrl);
                RoadmapContent = ExtractRoadmapSection(content);
            }
            catch (Exception ex)
            {
                RoadmapContent = $"Failed to load roadmap.\n\nError: {ex.Message}";
            }
            finally
            {
                IsLoadingChecklist = false;
            }
        }

        private string ExtractRoadmapSection(string markdown)
        {
            // Extracts the section starting with "# Roadmap" or "## Roadmap" and stops at the next top-level heading
            var lines = markdown.Split('\n');
            bool inRoadmap = false;
            System.Text.StringBuilder sb = new();
            foreach (var line in lines)
            {
                if (!inRoadmap && (line.Trim().Equals("# Roadmap") || line.Trim().Equals("## Roadmap") || line.Trim().Equals("## Roadmap", StringComparison.OrdinalIgnoreCase) || line.Trim().Equals("# Roadmap", StringComparison.OrdinalIgnoreCase)))
                {
                    inRoadmap = true;
                    sb.AppendLine(line);
                    continue;
                }
                if (inRoadmap)
                {
                    if (line.StartsWith("# ") && !line.Trim().Equals("# Roadmap", StringComparison.OrdinalIgnoreCase))
                        break;
                    sb.AppendLine(line);
                }
            }
            var result = sb.ToString().Trim();
            return string.IsNullOrWhiteSpace(result) ? "Roadmap section not found." : result;
        }

        private void RefreshRoadmapButton_Click(object sender, RoutedEventArgs e)
        {
            LoadRoadmapAsync();
        }

        private void ViewOnGitHubButton_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo(_roadmapUrl) { UseShellExecute = true }); }
            catch { }
        }

        private void OpenGitHubIssueButton_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo(_issueUrl) { UseShellExecute = true }); }
            catch { }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
