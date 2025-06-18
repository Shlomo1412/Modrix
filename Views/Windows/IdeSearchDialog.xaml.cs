using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace Modrix.Views.Windows
{
    public partial class IdeSearchDialog : FluentWindow
    {
        public class SearchResult
        {
            public int LineNumber { get; set; }
            public int StartOffset { get; set; }
            public int Length { get; set; }
            public string Preview { get; set; }
        }

        public ObservableCollection<SearchResult> Results { get; } = new();
        public string SearchText => SearchBox.Text;
        public bool MatchCase => MatchCaseCheck.IsChecked == true;
        public bool WholeWord => WholeWordCheck.IsChecked == true;
        public int SelectedIndex => ResultsListView.SelectedIndex;
        public SearchResult? SelectedResult => ResultsListView.SelectedItem as SearchResult;

        public IdeSearchDialog()
        {
            InitializeComponent();
            ResultsListView.ItemsSource = Results;
            ResultsListView.SelectionChanged += ResultsListView_SelectionChanged;
            SearchBox.KeyDown += SearchBox_KeyDown;
            FindButton.Click += (s, e) => RaiseFindRequested();
            CloseButton.Click += (s, e) => Close();
            PrevButton.Click += (s, e) =>
            {
                if (ResultsListView.SelectedIndex > 0)
                    ResultsListView.SelectedIndex--;
            };
            NextButton.Click += (s, e) =>
            {
                if (ResultsListView.SelectedIndex < Results.Count - 1)
                    ResultsListView.SelectedIndex++;
            };
            MatchCaseCheck.Checked += (s, e) => RaiseFindRequested();
            MatchCaseCheck.Unchecked += (s, e) => RaiseFindRequested();
            WholeWordCheck.Checked += (s, e) => RaiseFindRequested();
            WholeWordCheck.Unchecked += (s, e) => RaiseFindRequested();
        }

        private void WindowDrag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        public event EventHandler? FindRequested;
        public event EventHandler? SelectionChanged;

        private void RaiseFindRequested() => FindRequested?.Invoke(this, EventArgs.Empty);
        private void ResultsListView_SelectionChanged(object sender, SelectionChangedEventArgs e) => SelectionChanged?.Invoke(this, EventArgs.Empty);
        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                RaiseFindRequested();
            }
            else if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        public void SetResultsCountText(string text) => ResultsCountText.Text = text;
        public void FocusSearchBox() => SearchBox.Focus();
        public void SetSelectedIndex(int idx)
        {
            if (idx >= 0 && idx < Results.Count)
                ResultsListView.SelectedIndex = idx;
        }
    }
}
