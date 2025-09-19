using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace Modrix.Services
{
    public class NotificationService : INotifyPropertyChanged
    {
        private static NotificationService _instance;
        public static NotificationService Instance => _instance ??= new NotificationService();

        private readonly ObservableCollection<NotificationItem> _notifications = new();
        public ObservableCollection<NotificationItem> Notifications => _notifications;

        public event PropertyChangedEventHandler PropertyChanged;

        private NotificationService() { }

        public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info, int autoHideSeconds = 5)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var notification = new NotificationItem
                {
                    Id = Guid.NewGuid(),
                    Title = title,
                    Message = message,
                    Type = type,
                    Timestamp = DateTime.Now,
                    IsVisible = true
                };

                _notifications.Insert(0, notification);

                // Auto-hide after specified seconds
                if (autoHideSeconds > 0)
                {
                    var timer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(autoHideSeconds)
                    };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        HideNotification(notification.Id);
                    };
                    timer.Start();
                }

                // Keep only last 10 notifications
                while (_notifications.Count > 10)
                {
                    _notifications.RemoveAt(_notifications.Count - 1);
                }
            });
        }

        public void ShowValidationNotification(int errorCount, int warningCount, int missingMappings)
        {
            if (errorCount == 0 && warningCount == 0 && missingMappings == 0)
            {
                ShowNotification(
                    "Validation Complete",
                    "All models are valid with no issues found.",
                    NotificationType.Success
                );
            }
            else
            {
                var message = $"Found {errorCount} errors, {warningCount} warnings";
                if (missingMappings > 0)
                {
                    message += $", and {missingMappings} missing texture mappings";
                }

                ShowNotification(
                    "Model Validation Results",
                    message,
                    errorCount > 0 ? NotificationType.Error : NotificationType.Warning,
                    10 // Show longer for validation results
                );
            }
        }

        public void HideNotification(Guid id)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var notification = _notifications.FirstOrDefault(n => n.Id == id);
                if (notification != null)
                {
                    _notifications.Remove(notification);
                }
            });
        }

        public void ClearAll()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                _notifications.Clear();
            });
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class NotificationItem
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public NotificationType Type { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsVisible { get; set; }
    }

    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }
}