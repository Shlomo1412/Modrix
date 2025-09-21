using System.Windows;
using System.Windows.Controls;
using Modrix.Models;
using Modrix.Services;

namespace Modrix.Views.Controls
{
    public partial class WikiTooltip : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(WikiTooltip), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string), typeof(WikiTooltip), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty CategoryProperty =
            DependencyProperty.Register(nameof(Category), typeof(string), typeof(WikiTooltip), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty WikiIdProperty =
            DependencyProperty.Register(nameof(WikiId), typeof(string), typeof(WikiTooltip), new PropertyMetadata(string.Empty, OnWikiIdChanged));

        public static readonly DependencyProperty KeywordsProperty =
            DependencyProperty.Register(nameof(Keywords), typeof(string), typeof(WikiTooltip), new PropertyMetadata(string.Empty));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public string Category
        {
            get => (string)GetValue(CategoryProperty);
            set => SetValue(CategoryProperty, value);
        }

        public string WikiId
        {
            get => (string)GetValue(WikiIdProperty);
            set => SetValue(WikiIdProperty, value);
        }

        public string Keywords
        {
            get => (string)GetValue(KeywordsProperty);
            set => SetValue(KeywordsProperty, value);
        }

        public WikiTooltip()
        {
            InitializeComponent();
        }

        private static void OnWikiIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WikiTooltip tooltip && !string.IsNullOrEmpty(tooltip.WikiId))
            {
                // Register this tooltip with the wiki service
                var wikiService = WikiService.Instance;
                wikiService.RegisterWikiEntry(new WikiEntry
                {
                    Id = tooltip.WikiId,
                    Title = tooltip.Title,
                    Description = tooltip.Description,
                    Category = tooltip.Category,
                    Keywords = tooltip.Keywords?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>()
                });
            }
        }
    }
}