using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Modrix.Models;

namespace Modrix.Views.Controls
{
    public partial class ProjectCard : UserControl
    {
        public static readonly DependencyProperty ProjectDataProperty =
            DependencyProperty.Register(
                "ProjectData",
                typeof(ModProjectData),
                typeof(ProjectCard),
                new PropertyMetadata(null, OnProjectDataChanged));

        public ModProjectData ProjectData
        {
            get => (ModProjectData)GetValue(ProjectDataProperty);
            set => SetValue(ProjectDataProperty, value);
        }

        public event RoutedEventHandler EditClicked
        {
            add => EditButton.Click += value;
            remove => EditButton.Click -= value;
        }

        public event RoutedEventHandler DeleteClicked
        {
            add => DeleteButton.Click += value;
            remove => DeleteButton.Click -= value;
        }

        public ProjectCard()
        {
            InitializeComponent();
        }

        private static void OnProjectDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ProjectCard card && e.NewValue is ModProjectData data)
            {
                card.UpdateCard(data);
            }
        }

        private void UpdateCard(ModProjectData data)
        {
            ProjectNameText.Text = data.Name;
            ModTypeText.Text = data.ModType;
            VersionText.Text = data.MinecraftVersion;

            if (!string.IsNullOrEmpty(data.IconPath))
            {
                ProjectIconImage.Source = new BitmapImage(new Uri(data.IconPath));
            }
            else
            {
                ProjectIconImage.Source = null;
            }
        }
    }

}

