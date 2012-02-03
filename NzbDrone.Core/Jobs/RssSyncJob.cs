using System;
using System.Collections.Generic;
using System.Linq;
using Ninject;
using NLog;
using NzbDrone.Core.Helpers;
using NzbDrone.Core.Model;
using NzbDrone.Core.Providers;
using NzbDrone.Core.Providers.Indexer;

namespace NzbDrone.Core.Jobs
{
    public class RssSyncJob : IJob
    {
        private readonly IEnumerable<IndexerBase> _indexers;
        private readonly InventoryProvider _inventoryProvider;
        private readonly DownloadProvider _downloadProvider;
        private readonly IndexerProvider _indexerProvider;


        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        [Inject]
        public RssSyncJob(IEnumerable<IndexerBase> indexers, InventoryProvider inventoryProvider, DownloadProvider downloadProvider, IndexerProvider indexerProvider)
        {
            _indexers = indexers;
            _inventoryProvider = inventoryProvider;
            _downloadProvider = downloadProvider;
            _indexerProvider = indexerProvider;
        }

        public string Name
        {
            get { return "RSS Sync"; }
        }

        public TimeSpan DefaultInterval
        {
            get { return TimeSpan.FromMinutes(25); }
        }

        public virtual void Start(int targetId, int secondaryTargetId)
        {
            var reports = new List<EpisodeParseResult>();

            foreach (var indexer in _indexers.Where(i => _indexerProvider.GetSettings(i.GetType()).Enable))
            {
                try
                {
                    NotificationHelper.SendNotification("Fetching RSS from {0}", indexer.Name);
                    reports.AddRange(indexer.FetchRss());
                }
                catch (Exception e)
                {
                    Logger.ErrorException("An error has occurred while fetching items from " + indexer.Name, e);
                }
            }

            Logger.Debug("Finished fetching reports from all indexers. Total {0}", reports.Count);
            NotificationHelper.SendNotification("Processing downloaded RSS");

            foreach (var episodeParseResult in reports)
            {
                try
                {
                    if (_inventoryProvider.IsMonitored(episodeParseResult) && _inventoryProvider.IsQualityNeeded(episodeParseResult))
                    {
                        _downloadProvider.DownloadReport(episodeParseResult);
                    }
                }
                catch (Exception e)
                {
                    Logger.ErrorException("An error has occurred while processing parse result items from " + episodeParseResult, e);
                }
            }

            NotificationHelper.SendNotification("RSS Sync Completed");

        }
    }
}