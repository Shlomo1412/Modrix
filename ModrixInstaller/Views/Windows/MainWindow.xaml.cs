using ModrixInstaller.ViewModels.Windows;
using Wpf.Ui.Appearance;
using System.Windows;
using System.Windows.Media.Animation;
using System.ComponentModel;

namespace ModrixInstaller.Views.Windows;

public partial class MainWindow
{
    public MainWindowViewModel ViewModel { get; }
    private int _previousStepIndex = 0;
    private bool _isAnimating = false;

    public MainWindow(MainWindowViewModel vm)
    {
        ViewModel = vm;
        DataContext = this;
        SystemThemeWatcher.Watch(this);
        InitializeComponent();
        
        // Subscribe to property changes to detect navigation
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        _previousStepIndex = ViewModel.CurrentStepIndex;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CurrentStepIndex))
        {
            HandleStepIndexChange();
        }
    }

    private void HandleStepIndexChange()
    {
        if (_isAnimating) return; // Prevent overlapping animations
        
        int currentIndex = ViewModel.CurrentStepIndex;
        bool isForward = currentIndex > _previousStepIndex;
        
        // Start the appropriate animation
        if (isForward)
        {
            StartSlideAnimation("SlideOutToLeft", "SlideInFromRight");
        }
        else
        {
            StartSlideAnimation("SlideOutToRight", "SlideInFromLeft");
        }
        
        _previousStepIndex = currentIndex;
    }

    private void StartSlideAnimation(string slideOutKey, string slideInKey)
    {
        _isAnimating = true;
        
        // Find the storyboards
        var slideOut = (Storyboard)FindResource(slideOutKey);
        var slideIn = (Storyboard)FindResource(slideInKey);
        
        if (slideOut != null && slideIn != null)
        {
            // Set up completion handler for slide out
            slideOut.Completed += (s, e) =>
            {
                // Start slide in animation after slide out completes
                slideIn.Begin();
            };
            
            // Set up completion handler for slide in
            slideIn.Completed += (s, e) =>
            {
                _isAnimating = false;
            };
            
            // Start the slide out animation
            slideOut.Begin();
        }
        else
        {
            // Fallback if animations not found
            _isAnimating = false;
        }
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
