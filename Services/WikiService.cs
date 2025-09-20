using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Windows;

namespace Modrix.Services
{
    public class WikiEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Category { get; set; } = "General";
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string[] Keywords { get; set; } = Array.Empty<string>();
        public string? SourceView { get; set; }
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

        // Weak reference to source control for navigation/focus from Wiki page
        public WeakReference<FrameworkElement>? SourceElement { get; set; }
    }

    public static class WikiService
    {
        private static readonly ReaderWriterLockSlim _lock = new();
        private static readonly ObservableCollection<WikiEntry> _entries = new();
        public static ReadOnlyObservableCollection<WikiEntry> Entries { get; } = new(_entries);

        public static event EventHandler? EntriesChanged;

        public static void RegisterOrUpdate(WikiEntry entry)
        {
            if (entry == null) return;
            _lock.EnterWriteLock();
            try
            {
                var existing = _entries.FirstOrDefault(e => string.Equals(e.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    _entries.Add(entry);
                }
                else
                {
                    // Update fields
                    existing.Category = entry.Category;
                    existing.Title = entry.Title;
                    existing.Summary = entry.Summary;
                    existing.Details = entry.Details;
                    existing.Keywords = entry.Keywords;
                    existing.SourceView = entry.SourceView;
                    existing.SourceElement = entry.SourceElement;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
            EntriesChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void Unregister(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            _lock.EnterWriteLock();
            try
            {
                var found = _entries.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
                if (found != null)
                {
                    _entries.Remove(found);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
            EntriesChanged?.Invoke(null, EventArgs.Empty);
        }

        public static IEnumerable<string> GetCategories()
        {
            _lock.EnterReadLock();
            try
            {
                return _entries.Select(e => e.Category).Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToArray();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public static IEnumerable<WikiEntry> Search(string? text, string? category = null)
        {
            _lock.EnterReadLock();
            try
            {
                IEnumerable<WikiEntry> query = _entries;
                if (!string.IsNullOrWhiteSpace(category) && !string.Equals(category, "All", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(e => string.Equals(e.Category, category, StringComparison.OrdinalIgnoreCase));
                }
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var t = text!.Trim();
                    query = query.Where(e =>
                        (!string.IsNullOrEmpty(e.Title) && e.Title.Contains(t, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(e.Summary) && e.Summary.Contains(t, StringComparison.OrdinalIgnoreCase)) ||
                        (e.Keywords?.Any(k => k.Contains(t, StringComparison.OrdinalIgnoreCase)) == true));
                }
                return query.OrderBy(e => e.Category).ThenBy(e => e.Title).ToArray();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }
}
