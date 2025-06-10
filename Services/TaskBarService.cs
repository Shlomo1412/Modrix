using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Shell;
using System.Windows;
using Modrix.Models;
using Wpf.Ui.Controls;

namespace Modrix.Services
{
    public interface IModrixTaskBarService
    {
        void UpdateJumpListWithProjects(IEnumerable<ModProjectData> projects);
    }

    public class ModrixTaskBarService : IModrixTaskBarService
    {
        public void UpdateJumpListWithProjects(IEnumerable<ModProjectData> projects)
        {
            var jumpList = new JumpList();
            jumpList.ShowRecentCategory = false;
            jumpList.ShowFrequentCategory = false;

            var appPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";

            // Get the resource URI for the Apps16 symbol icon
            var iconUri = new Uri("pack://application:,,,/Wpf.Ui;component/Resources/Icons/Regular/Apps16.svg");

            foreach (var project in projects)
            {
                if (string.IsNullOrEmpty(project.Location) || string.IsNullOrEmpty(project.Name))
                    continue;

                var args = $"--open-project=\"{project.Location}\"";
                var jumpTask = new JumpTask
                {
                    Title = project.Name,
                    Arguments = args,
                    Description = $"Open {project.Name} workspace",
                    ApplicationPath = appPath,
                    IconResourcePath = appPath,
                    IconResourceIndex = 0,
                    CustomCategory = "Projects"
                };

                jumpList.JumpItems.Add(jumpTask);
            }

            try
            {
                JumpList.SetJumpList(Application.Current, jumpList);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set JumpList: {ex.Message}");
            }
        }
    }
}
