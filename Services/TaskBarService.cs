using System;
using System.Collections.Generic;
using System.Windows.Shell;
using Modrix.Models;

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
                    ApplicationPath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName,
                    IconResourcePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName,
                    IconResourceIndex = 0
                };
                jumpList.JumpItems.Add(jumpTask);
            }
            JumpList.SetJumpList(System.Windows.Application.Current, jumpList);
        }
    }
}
