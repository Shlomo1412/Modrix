using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Modrix.ViewModels.Pages;
using Modrix.Views.Windows;

namespace Modrix.Views.Pages
{
    /// <summary>
    /// Interaction logic for LanguageControlPage.xaml
    /// </summary>
    public partial class LanguageControlPage : Page
    {
        public LanguageControlPageViewModel ViewModel { get; private set; }
        
        public LanguageControlPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }
        
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is LanguageControlPageViewModel existingViewModel)
            {
                ViewModel = existingViewModel;
                return;
            }
            
            var workspace = Application.Current.Windows
                .OfType<ProjectWorkspace>()
                .FirstOrDefault();
                
            if (workspace?.ViewModel?.CurrentProject != null)
            {
                string projectPath = workspace.ViewModel.CurrentProject.Location;
                string modId = workspace.ViewModel.CurrentProject.ModId;
                
                ViewModel = new LanguageControlPageViewModel(projectPath, modId);
                DataContext = ViewModel;
                
                // Ensure filter is applied when loaded
                ViewModel.ApplyFilterCommand.Execute(null);
            }
        }
        
        public void Refresh()
        {
            var workspace = Application.Current.Windows
                .OfType<ProjectWorkspace>()
                .FirstOrDefault();
                
            if (workspace?.ViewModel?.CurrentProject != null)
            {
                string projectPath = workspace.ViewModel.CurrentProject.Location;
                string modId = workspace.ViewModel.CurrentProject.ModId;
                
                ViewModel = new LanguageControlPageViewModel(projectPath, modId);
                DataContext = ViewModel;
                
                // Ensure filter is applied when refreshed
                ViewModel.ApplyFilterCommand.Execute(null);
            }
        }
    }
}
