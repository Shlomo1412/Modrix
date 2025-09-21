using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Modrix.Models;
using Modrix.Services;
using Wpf.Ui.Controls;

namespace Modrix.Views.Pages
{
    public partial class WikiPage : Page
    {
        private readonly WikiService _wikiService;

        public WikiPage()
        {
            InitializeComponent();
            _wikiService = WikiService.Instance;
            Loaded += WikiPage_Loaded;
        }

        private void WikiPage_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshContent();
        }

        private void RefreshContent()
        {
            ContentPanel.Children.Clear();
            
            if (!_wikiService.Categories.Any())
            {
                ShowEmptyState();
                return;
            }

            foreach (var category in _wikiService.Categories.OrderBy(c => c.Name))
            {
                var categoryCard = CreateCategoryCard(category);
                ContentPanel.Children.Add(categoryCard);
            }
        }

        private UIElement CreateCategoryCard(WikiCategory category)
        {
            var border = new Border
            {
                Background = (System.Windows.Media.Brush)FindResource("ControlFillColorDefaultBrush"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 16),
                BorderBrush = (System.Windows.Media.Brush)FindResource("ControlStrokeColorDefaultBrush"),
                BorderThickness = new Thickness(1)
            };

            var stackPanel = new StackPanel();

            // Category header
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            
            var categoryIcon = new SymbolIcon
            {
                Symbol = GetCategoryIcon(category.Name),
                Width = 20,
                Height = 20,
                Margin = new Thickness(0, 0, 8, 0),
                Foreground = (System.Windows.Media.Brush)FindResource("SystemAccentColorSecondaryBrush")
            };
            
            var categoryTitle = new System.Windows.Controls.TextBlock
            {
                Text = category.Name,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };

            var entryCount = new System.Windows.Controls.TextBlock
            {
                Text = $"({category.Entries.Count} entries)",
                FontSize = 12,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush")
            };

            headerPanel.Children.Add(categoryIcon);
            headerPanel.Children.Add(categoryTitle);
            headerPanel.Children.Add(entryCount);
            stackPanel.Children.Add(headerPanel);

            // Category description
            if (!string.IsNullOrEmpty(category.Description))
            {
                var description = new System.Windows.Controls.TextBlock
                {
                    Text = category.Description,
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 0, 16),
                    Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush"),
                    TextWrapping = TextWrapping.Wrap
                };
                stackPanel.Children.Add(description);
            }

            // Entries
            foreach (var entry in category.Entries.OrderBy(e => e.Title))
            {
                var entryCard = CreateEntryCard(entry);
                stackPanel.Children.Add(entryCard);
            }

            border.Child = stackPanel;
            return border;
        }

        private UIElement CreateEntryCard(WikiEntry entry)
        {
            var border = new Border
            {
                Background = (System.Windows.Media.Brush)FindResource("ControlFillColorSecondaryBrush"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var stackPanel = new StackPanel();

            // Entry title
            var title = new System.Windows.Controls.TextBlock
            {
                Text = entry.Title,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            stackPanel.Children.Add(title);

            // Entry description
            var description = new System.Windows.Controls.TextBlock
            {
                Text = entry.Description,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 18,
                Margin = new Thickness(0, 0, 0, 8)
            };
            stackPanel.Children.Add(description);

            // Keywords
            if (entry.Keywords.Any())
            {
                var keywordsPanel = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
                
                foreach (var keyword in entry.Keywords)
                {
                    var keywordBorder = new Border
                    {
                        Background = (System.Windows.Media.Brush)FindResource("ControlFillColorTertiaryBrush"),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 2, 6, 2),
                        Margin = new Thickness(0, 0, 4, 2)
                    };

                    var keywordText = new System.Windows.Controls.TextBlock
                    {
                        Text = keyword.Trim(),
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush")
                    };

                    keywordBorder.Child = keywordText;
                    keywordsPanel.Children.Add(keywordBorder);
                }
                
                stackPanel.Children.Add(keywordsPanel);
            }

            border.Child = stackPanel;
            return border;
        }

        private void ShowEmptyState()
        {
            var emptyState = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 60, 0, 0)
            };

            var icon = new SymbolIcon
            {
                Symbol = SymbolRegular.BookInformation24,
                Width = 64,
                Height = 64,
                Margin = new Thickness(0, 0, 0, 16),
                Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorTertiaryBrush"),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var message = new System.Windows.Controls.TextBlock
            {
                Text = "No wiki entries available yet.",
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush")
            };

            var subtitle = new System.Windows.Controls.TextBlock
            {
                Text = "Wiki entries will appear here as you explore the application.",
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0),
                Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorTertiaryBrush")
            };

            emptyState.Children.Add(icon);
            emptyState.Children.Add(message);
            emptyState.Children.Add(subtitle);

            ContentPanel.Children.Add(emptyState);
        }

        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var searchTerm = SearchBox.Text;
            
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                RefreshContent();
                return;
            }

            var searchResults = _wikiService.SearchEntries(searchTerm);
            ShowSearchResults(searchResults, searchTerm);
        }

        private void ShowSearchResults(List<WikiEntry> results, string searchTerm)
        {
            ContentPanel.Children.Clear();

            // Search results header
            var headerPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
            
            var resultsTitle = new System.Windows.Controls.TextBlock
            {
                Text = $"Search Results for \"{searchTerm}\"",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            };

            var resultsCount = new System.Windows.Controls.TextBlock
            {
                Text = $"{results.Count} entries found",
                FontSize = 12,
                Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush")
            };

            headerPanel.Children.Add(resultsTitle);
            headerPanel.Children.Add(resultsCount);
            ContentPanel.Children.Add(headerPanel);

            // Results
            if (!results.Any())
            {
                var noResults = new System.Windows.Controls.TextBlock
                {
                    Text = "No matching entries found.",
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 40, 0, 0),
                    Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush")
                };
                ContentPanel.Children.Add(noResults);
                return;
            }

            foreach (var entry in results)
            {
                var entryCard = CreateEntryCard(entry);
                ContentPanel.Children.Add(entryCard);
            }
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = string.Empty;
            RefreshContent();
        }

        private static SymbolRegular GetCategoryIcon(string categoryName)
        {
            return categoryName switch
            {
                "Models" => SymbolRegular.Cube24,
                "Textures" => SymbolRegular.Image24,
                "Projects" => SymbolRegular.Folder24,
                "Tools" => SymbolRegular.Wrench24,
                "General" => SymbolRegular.Info24,
                _ => SymbolRegular.BookInformation24
            };
        }
    }
}