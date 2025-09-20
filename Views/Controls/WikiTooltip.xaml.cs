using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Modrix.Services;

namespace Modrix.Views.Controls
{
    public partial class WikiTooltip : UserControl
    {
        public WikiTooltip()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public static readonly DependencyProperty IdProperty = DependencyProperty.Register(
            nameof(Id), typeof(string), typeof(WikiTooltip), new PropertyMetadata(null, OnMetaChanged));

        public static readonly DependencyProperty CategoryProperty = DependencyProperty.Register(
            nameof(Category), typeof(string), typeof(WikiTooltip), new PropertyMetadata("General", OnMetaChanged));

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(WikiTooltip), new PropertyMetadata("Help", OnMetaChanged));

        public static readonly DependencyProperty SummaryProperty = DependencyProperty.Register(
            nameof(Summary), typeof(string), typeof(WikiTooltip), new PropertyMetadata(string.Empty, OnMetaChanged));

        public static readonly DependencyProperty KeywordsProperty = DependencyProperty.Register(
            nameof(Keywords), typeof(string[]), typeof(WikiTooltip), new PropertyMetadata(Array.Empty<string>(), OnMetaChanged));

        // For later highlighting/search matching state
        public static readonly DependencyProperty HighlightTextProperty = DependencyProperty.Register(
            nameof(HighlightText), typeof(string), typeof(WikiTooltip), new PropertyMetadata(null));

        public string? Id
        {
            get => (string?)GetValue(IdProperty);
            set => SetValue(IdProperty, value);
        }

        public string Category
        {
            get => (string)GetValue(CategoryProperty);
            set => SetValue(CategoryProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Summary
        {
            get => (string)GetValue(SummaryProperty);
            set => SetValue(SummaryProperty, value);
        }

        public string[] Keywords
        {
            get => (string[])GetValue(KeywordsProperty);
            set => SetValue(KeywordsProperty, value);
        }

        public string? HighlightText
        {
            get => (string?)GetValue(HighlightTextProperty);
            set => SetValue(HighlightTextProperty, value);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RegisterEntry();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(Id))
            {
                WikiService.Unregister(Id);
            }
        }

        private static void OnMetaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WikiTooltip wt && wt.IsLoaded)
            {
                wt.RegisterEntry();
            }
        }

        private void RegisterEntry()
        {
            var id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString() : Id!;
            Id = id; // ensure set

            var sourceView = TryGetViewName(this);
            var entry = new WikiEntry
            {
                Id = id,
                Category = Category,
                Title = Title,
                Summary = Summary,
                Keywords = Keywords ?? Array.Empty<string>(),
                SourceView = sourceView,
                SourceElement = new WeakReference<FrameworkElement>(this)
            };
            WikiService.RegisterOrUpdate(entry);
        }

        private static string? TryGetViewName(FrameworkElement element)
        {
            DependencyObject current = element;
            while (current != null)
            {
                if (current is Page page) return page.GetType().Name;
                if (current is Window win) return win.GetType().Name;
                current = LogicalTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
