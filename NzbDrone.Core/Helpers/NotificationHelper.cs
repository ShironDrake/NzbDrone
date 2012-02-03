using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NzbDrone.Core.Providers;
using SignalR.Hubs;

namespace NzbDrone.Core.Helpers
{
    public class NotificationHelper : Hub
    {
        public static void SendNotification(string message, params object[] parameters)
        {
            var formattedMessage = String.Format(message, parameters);
            GetClients<NotificationHelper>().notify(formattedMessage);
        }
    }
}
