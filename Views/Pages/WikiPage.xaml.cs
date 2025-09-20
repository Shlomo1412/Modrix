using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Modrix.Services;

namespace Modrix.Views.Pages
{
    public partial class WikiPage : Page
    {
        public WikiPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            WikiService.EntriesChanged += WikiService_EntriesChanged;
            CategoryFilter.SelectionChanged += CategoryFilter_SelectionChanged;
            SearchBox.TextChanged += SearchBox_TextChanged;

            // Initialize category filter
            var cats = WikiService.GetCategories().ToList();
            cats.Insert(0, "All");
            CategoryFilter.ItemsSource = cats;
            if (CategoryFilter.Items.Count > 0)
                CategoryFilter.SelectedIndex = 0;

            RefreshResults();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            WikiService.EntriesChanged -= WikiService_EntriesChanged;
            CategoryFilter.SelectionChanged -= CategoryFilter_SelectionChanged;
            SearchBox.TextChanged -= SearchBox_TextChanged;
        }

        private void WikiService_EntriesChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(RefreshResults);
        }

        private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshResults();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshResults();
        }

        private void RefreshResults()
        {
            var cat = CategoryFilter.SelectedItem as string;
            var text = SearchBox.Text;
            var results = WikiService.Search(text, cat).ToList();
            ResultsList.ItemsSource = results;
        }

        private void Locate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is WikiEntry entry)
            {
                if (entry.SourceElement != null && entry.SourceElement.TryGetTarget(out var src))
                {
                    // Bring containing Window to front and focus control
                    var win = Window.GetWindow(src);
                    if (win != null)
                    {
                        if (win.WindowState == WindowState.Minimized)
                            win.WindowState = WindowState.Normal;
                        win.Activate();
                    }
                    src.Focus();
                }
            }
        }
    }
}
