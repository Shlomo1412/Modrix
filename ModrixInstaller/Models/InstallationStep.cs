using Wpf.Ui.Controls;

namespace ModrixInstaller.Models
{
    public class InstallationStep
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public SymbolRegular Icon { get; set; }
        public Type PageType { get; set; } = null!;
        public bool IsCompleted { get; set; } = false;
        public bool IsActive { get; set; } = false;
        public bool IsEnabled { get; set; } = true;
    }
}