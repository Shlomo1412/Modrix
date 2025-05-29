using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Modrix.ViewModels.Windows;

namespace Modrix.ViewModels.Pages
{
    public partial class ResourcesPageViewModel : ObservableObject
    {
        private readonly ProjectWorkspaceViewModel _workspaceVm;

        public ResourcesPageViewModel(ProjectWorkspaceViewModel workspaceVm)
        {
            _workspaceVm = workspaceVm;
            Textures = new ObservableCollection<string>();
            Models = new ObservableCollection<string>();
            Sounds = new ObservableCollection<string>();

            ChangeIconCommand = new RelayCommand(ChangeIcon);
            RemoveIconCommand = new RelayCommand(RemoveIcon);

            LoadResources();
        }

        [ObservableProperty] private ObservableCollection<string> _textures;
        [ObservableProperty] private ObservableCollection<string> _models;
        [ObservableProperty] private ObservableCollection<string> _sounds;
        [ObservableProperty] private string _iconPath = "";

        public IRelayCommand ChangeIconCommand { get; }
        public IRelayCommand RemoveIconCommand { get; }

        private void LoadResources()
        {
            var proj = _workspaceVm.CurrentProject;
            if (proj == null) return;

            var assets = Path.Combine(proj.Location,
                                      "src", "main", "resources", "assets", proj.ModId);

            // Textures
            var texDir = Path.Combine(assets, "textures");
            if (Directory.Exists(texDir))
            {
                foreach (var file in Directory.GetFiles(texDir, "*.*", SearchOption.AllDirectories)
                                              .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg")))
                {
                    Textures.Add(file);
                }
            }

            // Models
            var modelDir = Path.Combine(assets, "models");
            if (Directory.Exists(modelDir))
            {
                foreach (var file in Directory.GetFiles(modelDir, "*.json", SearchOption.AllDirectories))
                    Models.Add(file);
            }

            // Sounds
            var soundDir = Path.Combine(assets, "sounds");
            if (Directory.Exists(soundDir))
            {
                foreach (var file in Directory.GetFiles(soundDir, "*.*", SearchOption.AllDirectories)
                                              .Where(f => f.EndsWith(".ogg") || f.EndsWith(".wav")))
                {
                    Sounds.Add(file);
                }
            }

            // Icon
            IconPath = Path.Combine(proj.Location, proj.IconPath ?? "");
        }

        private void ChangeIcon()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PNG Files|*.png",
                Title = "Select new icon"
            };
            if (dlg.ShowDialog() != true) return;

            var proj = _workspaceVm.CurrentProject!;
            var dest = Path.Combine(proj.Location,
                                    "src", "main", "resources", "assets", proj.ModId, "icon.png");
            File.Copy(dlg.FileName, dest, true);
            IconPath = dest;
        }

        private void RemoveIcon()
        {
            if (string.IsNullOrEmpty(IconPath) || !File.Exists(IconPath))
                return;

            File.Delete(IconPath);
            IconPath = "";
        }

        [RelayCommand]
        private void PlaySound(string path)
        {
            var player = new System.Media.SoundPlayer(path);
            player.Play();
        }
    }
}
