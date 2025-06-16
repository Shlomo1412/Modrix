using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Modrix.Models
{
    public class AppConfig
    {
        public string ConfigurationsFolder { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Modrix"
        );

        public string AppPropertiesFileName { get; set; } = "appsettings.json";

        public string ProjectsFileName { get; set; } = "projects.json";

        //Theme
        public string Theme { get; set; } = "Dark";

        public ObservableCollection<ModProjectData> Projects { get; set; } = new();

        // IDE Settings
        public IdeSettings IdeSettings { get; set; } = new();
    }

    public partial class IdeSettings : ObservableObject
    {
        // Editor Settings
        [ObservableProperty]
        private bool _autoSave = true;

        [ObservableProperty]
        private int _autoSaveIntervalSeconds = 30;

        [ObservableProperty]
        private bool _wordWrap = false;

        [ObservableProperty]
        private int _tabSize = 4;

        [ObservableProperty]
        private bool _useSpacesForTabs = true;

        [ObservableProperty]
        private LineEndingType _lineEndings = LineEndingType.CRLF;

        [ObservableProperty]
        private int _fontSize = 14;

        [ObservableProperty]
        private string _fontFamily = "Cascadia Code";

        [ObservableProperty]
        private bool _showWhitespace = false;

        [ObservableProperty]
        private bool _showLineEndings = false;

        [ObservableProperty]
        private bool _autoIndent = true;

        [ObservableProperty]
        private bool _enableCodeFolding = true;

        // File Settings
        [ObservableProperty]
        private string _defaultEncoding = "UTF-8";

        [ObservableProperty]
        private bool _autoReloadExternalChanges = true;

        [ObservableProperty]
        private long _maxFileSize = 10 * 1024 * 1024; // 10MB

        [ObservableProperty]
        private bool _createBackupFiles = true;

        // Visual Settings
        [ObservableProperty]
        private bool _showLineNumbers = true;

        [ObservableProperty]
        private bool _showMinimap = true;

        [ObservableProperty]
        private bool _showIndentGuides = true;

        [ObservableProperty]
        private bool _highlightCurrentLine = true;

        [ObservableProperty]
        private bool _highlightMatchingBrackets = true;

        // On Open Settings
        [ObservableProperty]
        private bool _closeMainWindowOnOpen = false;

        [ObservableProperty]
        private string _onOpenNavigateTab = "Workspace";
    }

    public enum LineEndingType
    {
        CRLF,
        LF
    }
}
