using System;
using System.Collections.Generic;
using System.Linq;
using Ninject;
using NLog;
using NzbDrone.Core.Helpers;
using NzbDrone.Core.Providers;
using NzbDrone.Core.Providers.Core;
using NzbDrone.Core.Repository;

namespace NzbDrone.Core.Jobs
{
    public class DiskScanJob : IJob
    {
        private readonly SeriesProvider _seriesProvider;
        private readonly DiskScanProvider _diskScanProvider;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        [Inject]
        public DiskScanJob(SeriesProvider seriesProvider, DiskScanProvider diskScanProvider)
        {
            _seriesProvider = seriesProvider;
            _diskScanProvider = diskScanProvider;
        }

        public DiskScanJob()
        {
        }

        public string Name
        {
            get { return "Media File Scan"; }
        }

        public TimeSpan DefaultInterval
        {
            get { return TimeSpan.FromHours(6); }
        }

        public virtual void Start(int targetId, int secondaryTargetId)
        {
            IList<Series> seriesToScan;
            if (targetId == 0)
            {
                seriesToScan = _seriesProvider.GetAllSeries().OrderBy(o => SortHelper.SkipArticles(o.Title)).ToList();
            }
            else
            {
                seriesToScan = new List<Series>() { _seriesProvider.GetSeries(targetId) };
            }

            foreach (var series in seriesToScan)
            {
                try
                {
                    NotificationHelper.SendNotification("Scanning disk for '{0}'", series.Title);
                    _diskScanProvider.Scan(series);
                    NotificationHelper.SendNotification("Disk Scan completed for '{0}'", series.Title);
                }
                catch (Exception e)
                {
                    Logger.ErrorException("An error has occurred while scanning " + series.Title, e);
                }
            }
        }
    }
}
