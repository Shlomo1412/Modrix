using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Input;
using Modrix.Services;

namespace Modrix.Views.Pages
{
    public partial class NewsPage : Page
    {
        public ObservableCollection<CommitInfo> LatestCommits { get; set; } = new();
        public IRelayCommand<string> OpenCommitCommand { get; }

        public NewsPage()
        {
            DataContext = this;
            OpenCommitCommand = new RelayCommand<string>(OpenCommit);
            InitializeComponent();

            // Use the connectivity service to check for internet
            var connectivity = new ConnectivityService();
            if (!connectivity.IsInternetAvailable())
            {
                NoInternetGrid.Visibility = Visibility.Visible;
                ContentScrollViewer.Visibility = Visibility.Collapsed;
            }
            else
            {
                NoInternetGrid.Visibility = Visibility.Collapsed;
                ContentScrollViewer.Visibility = Visibility.Visible;
                _ = LoadCommitsAsync();
            }
        }

        private async Task LoadCommitsAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ModrixApp/1.0");
                var url = "https://api.github.com/repos/Shlomo1412/Modrix/commits?per_page=3";
                var json = await client.GetStringAsync(url);
                var doc = JsonDocument.Parse(json);
                LatestCommits.Clear();
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    var commit = item.GetProperty("commit");
                    var message = commit.GetProperty("message").GetString();
                    var author = commit.GetProperty("author").GetProperty("name").GetString();
                    var htmlUrl = item.GetProperty("html_url").GetString();
                    LatestCommits.Add(new CommitInfo
                    {
                        Message = message,
                        Author = author,
                        Url = htmlUrl
                    });
                }
            }
            catch
            {
                // Optionally handle errors (e.g., show a message)
            }
        }

        private void OpenCommit(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }

        public class CommitInfo
        {
            public string Message { get; set; }
            public string Author { get; set; }
            public string Url { get; set; }
        }
    }
}