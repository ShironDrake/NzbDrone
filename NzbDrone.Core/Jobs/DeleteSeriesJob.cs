using System.Linq;
using System;
using Ninject;
using NLog;
using NzbDrone.Core.Helpers;
using NzbDrone.Core.Providers;

namespace NzbDrone.Core.Jobs
{
    public class DeleteSeriesJob : IJob
    {
        private readonly SeriesProvider _seriesProvider;

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        [Inject]
        public DeleteSeriesJob(SeriesProvider seriesProvider)
        {
            _seriesProvider = seriesProvider;
        }

        public string Name
        {
            get { return "Delete Series"; }
        }

        public TimeSpan DefaultInterval
        {
            get { return TimeSpan.FromTicks(0); }
        }

        public virtual void Start(int targetId, int secondaryTargetId)
        {
            DeleteSeries(targetId);
        }

        private void DeleteSeries(int seriesId)
        {
            Logger.Warn("Deleting Series [{0}]", seriesId);

            try
            {
                var title = _seriesProvider.GetSeries(seriesId).Title;

                NotificationHelper.SendNotification("Deleting '{0}' from database", title);

                _seriesProvider.DeleteSeries(seriesId);

                NotificationHelper.SendNotification("Successfully deleted '{0}' from database", title);
            }
            catch (Exception e)
            {
                Logger.ErrorException("An error has occurred while deleting series: " + seriesId, e);
                throw;
            }
        }
    }
}