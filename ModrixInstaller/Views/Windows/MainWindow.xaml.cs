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

    private void Community_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new ModrixInstaller.Views.Dialogs.JoinDiscordDialog();
        dialog.Owner = this;
        dialog.ShowDialog();
    }

    private void SourceCode_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new ModrixInstaller.Views.Dialogs.ExploreSourceCodeDialog();
        dialog.Owner = this;
        dialog.ShowDialog();
    }

    private void Donate_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new ModrixInstaller.Views.Dialogs.DonateDialog();
        dialog.Owner = this;
        dialog.ShowDialog();
    }
}
