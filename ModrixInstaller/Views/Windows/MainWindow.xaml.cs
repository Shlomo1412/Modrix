using ModrixInstaller.ViewModels.Windows;
using Wpf.Ui.Appearance;

namespace ModrixInstaller.Views.Windows;

public partial class MainWindow
{
    public MainWindowViewModel ViewModel { get; }

    public MainWindow(MainWindowViewModel vm)
    {
        ViewModel = vm;
        DataContext = this;
        SystemThemeWatcher.Watch(this);
        InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Application.Current.Shutdown();
    }
}
