using System;
using System.Text;
using System.Windows;
using System.Windows.Shell;

using Wpf.Ui.Controls;

namespace Modrix.Views.Windows
{
    public partial class LoadingProjectWindow : FluentWindow
    {
        private readonly StringBuilder _logs = new();
        private readonly TaskbarItemInfo _taskbarProgress;
        private Action _cancelAction;

        public LoadingProjectWindow()
        {
            InitializeComponent();

            // Setup taskbar progress
            _taskbarProgress = new TaskbarItemInfo();
            TaskbarItemInfo = _taskbarProgress;
        }

        public void UpdateStatus(string message, int progress)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateStatus(message, progress));
                return;
            }

            CurrentTaskText.Text = message;
            TaskProgressBar.Value = progress;

            // Update taskbar progress
            _taskbarProgress.ProgressState = TaskbarItemProgressState.Normal;
            _taskbarProgress.ProgressValue = progress / 100.0;

            // Add to logs
            _logs.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
            //LogsText.Text = _logs.ToString();
        }

        public void SetCancelAction(Action cancelAction)
        {
            _cancelAction = cancelAction;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cancelAction?.Invoke();
            Close();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement stop functionality
            
        }

        public void ShowSuccess(string message)
        {
            _taskbarProgress.ProgressState = TaskbarItemProgressState.None;
            //var notification = new NotifyIcon
            //{
            //    IconSource = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/wpfui-icon-256.png")),
            //    Title = "Success",
            //    Message = message
            //};
            //notification.Show();
        }

        public void ShowError(string message)
        {
            _taskbarProgress.ProgressState = TaskbarItemProgressState.Error;
            //var notification = new NotifyIcon
            //{
            //    IconSource = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/wpfui-icon-256.png")),
            //    Title = "Error",
            //    Message = message
            //};
            //notification.Show();
        }
    }
}
