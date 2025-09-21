namespace Modrix.Models
{
    public class WikiEntry
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string[] Keywords { get; set; } = Array.Empty<string>();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class WikiCategory
    {
        public string Name { get; set; } = string.Empty;
        public List<WikiEntry> Entries { get; set; } = new List<WikiEntry>();
        public string Description { get; set; } = string.Empty;
    }
}