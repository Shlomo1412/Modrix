using System.IO;
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

        public event RoutedEventHandler EditClicked;
        public event RoutedEventHandler DeleteClicked;

        public ProjectCard()
        {
            InitializeComponent();

            // הוספת טיפול באירועי הלחיצה
            EditButton.Click += (s, e) => EditClicked?.Invoke(this, e);
            DeleteButton.Click += (s, e) => DeleteClicked?.Invoke(this, e);
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
            ModTypeText.Text = data.ModType switch
            {
                "Fabric Mod" => "Fabric Mod",
                "Forge Mod" => "Forge Mod",
                _ => "Unknown Mod Type"
            };
            VersionText.Text = data.MinecraftVersion;

            string fullIconPath = Path.Combine(data.Location, data.IconPath ?? "");

            if (!string.IsNullOrEmpty(data.IconPath) && File.Exists(fullIconPath))
            {
                ProjectIconImage.Source = new BitmapImage(new Uri(fullIconPath));
                ProjectIconImage.Visibility = Visibility.Visible;
                DefaultIconText.Visibility = Visibility.Collapsed;
            }
            else
            {
                // אין אייקון - נציג את האות הראשונה של שם הפרויקט
                ProjectIconImage.Source = null;
                ProjectIconImage.Visibility = Visibility.Collapsed;
                DefaultIconText.Visibility = Visibility.Visible;

                // מקבל את האות הראשונה של השם ומעביר אותה לאותיות גדולות
                DefaultIconText.Text = !string.IsNullOrEmpty(data.Name)
                    ? data.Name.Substring(0, 1).ToUpper()
                    : "?";
            }
        }
    }



}

