
using System.Windows.Controls;
using Modrix.ViewModels.Pages;

namespace Modrix.Views.Pages
{
    public partial class ResourcesPage : Page
    {
        public ResourcesPage(ResourcesPageViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
