using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Model.Notification;
using SignalR.Hubs;

namespace NzbDrone.Core.Providers
{
    public class NotificationProvider : Hub
    {
        private static ProgressNotification _currentNotification;

        public virtual ProgressNotification GetCurrent()
        {
            if (_currentNotification == null || _currentNotification.CompletedTime < DateTime.Now.AddSeconds(-6))
            {
                return null;
            }

            return _currentNotification;
        }

        public virtual void Register(ProgressNotification notification)
        {
            _currentNotification = notification;
        }
    }
}