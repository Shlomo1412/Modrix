using CommunityToolkit.Mvvm.ComponentModel;
using System.Text;

namespace ModrixInstaller.ViewModels.Pages
{
    public partial class ShortcutsViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _createDesktopShortcut = true;

        [ObservableProperty]
        private bool _createStartMenuShortcut = true;

        [ObservableProperty]
        private bool _createQuickLaunchShortcut = false;

        [ObservableProperty]
        private bool _pinToTaskbar = false;

        [ObservableProperty]
        private bool _associateProjectFiles = true;

        [ObservableProperty]
        private bool _addToSystemPath = false;

        [ObservableProperty]
        private string _shortcutSummary = "";

        public ShortcutsViewModel()
        {
            // Subscribe to property changes to update summary
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(ShortcutSummary))
                {
                    UpdateSummary();
                }
            };

            UpdateSummary();
        }

        private void UpdateSummary()
        {
            var summary = new StringBuilder();
            var actions = new List<string>();

            if (CreateDesktopShortcut)
                actions.Add("Desktop shortcut");

            if (CreateStartMenuShortcut)
                actions.Add("Start Menu entry");

            if (CreateQuickLaunchShortcut)
                actions.Add("Quick Launch toolbar");

            if (PinToTaskbar)
                actions.Add("Taskbar pin");

            if (AssociateProjectFiles)
                actions.Add("File associations for .modrix files");

            if (AddToSystemPath)
                actions.Add("System PATH entry");

            if (actions.Count == 0)
            {
                summary.Append("No shortcuts will be created. You can manually launch Modrix from the installation directory.");
            }
            else
            {
                summary.Append("The following will be created during installation:");
                summary.AppendLine();
                
                foreach (var action in actions)
                {
                    summary.AppendLine($"• {action}");
                }

                summary.AppendLine();
                summary.Append("Note: Some features may require administrator privileges.");
            }

            ShortcutSummary = summary.ToString();
        }
    }
}