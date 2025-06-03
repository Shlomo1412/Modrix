using Modrix.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Modrix.Views.Pages
{
    public partial class TextureEditorPage : INavigableView<TextureEditorViewModel>
    {
        public TextureEditorViewModel ViewModel { get; }

        public TextureEditorPage(TextureEditorViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }
    }
}