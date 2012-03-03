﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Ninject;
using NzbDrone.Core.Model;
using NzbDrone.Core.Model.Notification;
using NzbDrone.Core.Providers.DecisionEngine;
using NzbDrone.Core.Repository;

namespace NzbDrone.Core.Providers
{
    public class SearchProvider
    {
        //Season and Episode Searching
        private readonly EpisodeProvider _episodeProvider;
        private readonly DownloadProvider _downloadProvider;
        private readonly SeriesProvider _seriesProvider;
        private readonly IndexerProvider _indexerProvider;
        private readonly SceneMappingProvider _sceneMappingProvider;
        private readonly UpgradePossibleSpecification _upgradePossibleSpecification;
        private readonly AllowedDownloadSpecification _allowedDownloadSpecification;

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        [Inject]
        public SearchProvider(EpisodeProvider episodeProvider, DownloadProvider downloadProvider, SeriesProvider seriesProvider,
                                IndexerProvider indexerProvider, SceneMappingProvider sceneMappingProvider,
                                UpgradePossibleSpecification upgradePossibleSpecification, AllowedDownloadSpecification allowedDownloadSpecification)
        {
            _episodeProvider = episodeProvider;
            _downloadProvider = downloadProvider;
            _seriesProvider = seriesProvider;
            _indexerProvider = indexerProvider;
            _sceneMappingProvider = sceneMappingProvider;
            _upgradePossibleSpecification = upgradePossibleSpecification;
            _allowedDownloadSpecification = allowedDownloadSpecification;
        }

        public SearchProvider()
        {
        }

        public virtual bool SeasonSearch(ProgressNotification notification, int seriesId, int seasonNumber)
        {
            var series = _seriesProvider.GetSeries(seriesId);

            if (series == null)
            {
                Logger.Error("Unable to find an series {0} in database", seriesId);
                return false;
            }

            //Return false if the series is a daily series (we only support individual episode searching
            if (series.IsDaily)
                return false;

            notification.CurrentMessage = String.Format("Searching for {0} Season {1}", series.Title, seasonNumber);

            var reports = PerformSearch(notification, series, seasonNumber);

            Logger.Debug("Finished searching all indexers. Total {0}", reports.Count);

            if (reports.Count == 0)
                return false;

            Logger.Debug("Getting episodes from database for series: {0} and season: {1}", seriesId, seasonNumber);
            var episodeNumbers = _episodeProvider.GetEpisodeNumbersBySeason(seriesId, seasonNumber);

            if (episodeNumbers == null || episodeNumbers.Count == 0)
            {
                Logger.Warn("No episodes in database found for series: {0} and season: {1}.", seriesId, seasonNumber);
                return false;
            }

            notification.CurrentMessage = "Processing search results";

            reports.Where(p => p.FullSeason && p.SeasonNumber == seasonNumber).ToList().ForEach(
                e => e.EpisodeNumbers = episodeNumbers.ToList()
                );

            var downloadedEpisodes = ProcessSearchResults(notification, reports, series, seasonNumber);

            downloadedEpisodes.Sort();
            episodeNumbers.ToList().Sort();

            //Returns true if the list of downloaded episodes matches the list of episode numbers
            //(either a full season release was grabbed or all individual episodes)
            return (downloadedEpisodes.SequenceEqual(episodeNumbers));
        }

        public virtual List<int> PartialSeasonSearch(ProgressNotification notification, int seriesId, int seasonNumber)
        {
            //This method will search for episodes in a season in groups of 10 episodes S01E0, S01E1, S01E2, etc 

            var series = _seriesProvider.GetSeries(seriesId);

            if (series == null)
            {
                Logger.Error("Unable to find an series {0} in database", seriesId);
                return new List<int>();
            }

            //Return empty list if the series is a daily series (we only support individual episode searching
            if (series.IsDaily)
                return new List<int>();

            notification.CurrentMessage = String.Format("Searching for {0} Season {1}", series.Title, seasonNumber);

            var episodes = _episodeProvider.GetEpisodesBySeason(seriesId, seasonNumber);

            var reports = PerformSearch(notification, series, seasonNumber, episodes);

            Logger.Debug("Finished searching all indexers. Total {0}", reports.Count);

            if (reports.Count == 0)
                return new List<int>();

            notification.CurrentMessage = "Processing search results";

            return ProcessSearchResults(notification, reports, series, seasonNumber);
        }

        public virtual bool EpisodeSearch(ProgressNotification notification, int episodeId)
        {
            var episode = _episodeProvider.GetEpisode(episodeId);

            if (episode == null)
            {
                Logger.Error("Unable to find an episode {0} in database", episodeId);
                return false;
            }

            //Check to see if an upgrade is possible before attempting
            if (!_upgradePossibleSpecification.IsSatisfiedBy(episode))
            {
                Logger.Info("Search for {0} was aborted, file in disk meets or exceeds Profile's Cutoff", episode);
                notification.CurrentMessage = String.Format("Skipping search for {0}, file you have is already at cutoff", episode);
                return false;
            }

            notification.CurrentMessage = "Looking for " + episode;

            if (episode.Series.IsDaily && !episode.AirDate.HasValue)
            {
                Logger.Warn("AirDate is not Valid for: {0}", episode);
                return false;
            }

            var reports = PerformSearch(notification, episode.Series, episode.SeasonNumber, new List<Episode> { episode });

            Logger.Debug("Finished searching all indexers. Total {0}", reports.Count);
            notification.CurrentMessage = "Processing search results";
            int EpNumber = episode.Series.AbsoluteNumbering ? episode.AbsoluteNumber : episode.EpisodeNumber;
            if (!episode.Series.IsDaily && ProcessSearchResults(notification, reports, episode.Series, episode.SeasonNumber,EpNumber).Count == 1)
                return true;

            if (episode.Series.IsDaily && ProcessSearchResults(notification, reports, episode.Series, episode.AirDate.Value))
                return true;

            Logger.Warn("Unable to find {0} in any of indexers.", episode);

            if (reports.Any())
            {
                notification.CurrentMessage = String.Format("Sorry, couldn't find {0} in a non-sucky quality. (by your standards)", episode);
            }
            else
            {
                notification.CurrentMessage = String.Format("Sorry, couldn't find you {0} in any of indexers.", episode);
            }


            return false;
        }

        public List<EpisodeParseResult> PerformSearchAbsolute(ProgressNotification notification, Series series, int seasonNumber, IList<Episode> episodes)
        {

            var indexers = _indexerProvider.GetEnabledIndexers();
            var reports = new List<EpisodeParseResult>();

            var title = _sceneMappingProvider.GetSceneName(series.SeriesId);

            if (string.IsNullOrWhiteSpace(title))
            {
                title = series.Title;
            }

            foreach (var indexer in indexers)
            {
                try
                {
                    if (episodes == null)
                    {
                        //fetch whole season
                        foreach (Episode ep in _episodeProvider.GetEpisodesBySeason(series.SeriesId,seasonNumber))
                            reports.AddRange(indexer.FetchAbsoluteEpisode(title,ep.AbsoluteNumber));
                    }

                    //Treat as list of episodes
                    else
                    {
                        foreach (Episode ep in episodes)
                            reports.AddRange(indexer.FetchAbsoluteEpisode(title, ep.AbsoluteNumber));
                    }
                }

                catch (Exception e)
                {
                    Logger.ErrorException("An error has occurred while fetching items from " + indexer.Name, e);
                }
            }

            return reports;
        }

        public List<EpisodeParseResult> PerformSearch(ProgressNotification notification, Series series, int seasonNumber, IList<Episode> episodes = null)
        {
            //If single episode, do a single episode search, if full season then do a full season search, otherwise, do a partial search
            if (series.AbsoluteNumbering)
            {
                return PerformSearchAbsolute(notification, series, seasonNumber, episodes);
            }
            var indexers = _indexerProvider.GetEnabledIndexers();
            var reports = new List<EpisodeParseResult>();

            var title = _sceneMappingProvider.GetSceneName(series.SeriesId);

            if (string.IsNullOrWhiteSpace(title))
            {
                title = series.Title;
            }

            foreach (var indexer in indexers)
            {
                try
                {
                    if (episodes == null)
                        reports.AddRange(indexer.FetchSeason(title, seasonNumber));

                    //Treat as single episode
                    else if (episodes.Count == 1)
                    {
                        if (!series.IsDaily)
                            reports.AddRange(indexer.FetchEpisode(title, seasonNumber, episodes.First().EpisodeNumber));

                        //Daily Episode
                        else
                            reports.AddRange(indexer.FetchDailyEpisode(title, episodes.First().AirDate.Value));
                    }

                    //Treat as Partial Season
                    else
                    {
                        var prefixes = GetEpisodeNumberPrefixes(episodes.Select(s => s.EpisodeNumber));

                        foreach (var episodePrefix in prefixes)
                        {
                            reports.AddRange(indexer.FetchPartialSeason(title, seasonNumber, episodePrefix));
                        }
                    }
                }

                catch (Exception e)
                {
                    Logger.ErrorException("An error has occurred while fetching items from " + indexer.Name, e);
                }
            }

            return reports;
        }

        public List<int> ProcessSearchResults(ProgressNotification notification, IEnumerable<EpisodeParseResult> reports, Series series, int seasonNumber, int? episodeNumber = null)
        {
            var successes = new List<int>();

            foreach (var episodeParseResult in reports.OrderByDescending(c => c.Quality).ThenBy(c => c.Age))
            {
                try
                {
                    Logger.Trace("Analysing report " + episodeParseResult);

                    //Get the matching series
                    episodeParseResult.Series = _seriesProvider.FindSeries(episodeParseResult.CleanTitle);

                    //If series is null or doesn't match the series we're looking for return
                    if (episodeParseResult.Series == null || episodeParseResult.Series.SeriesId != series.SeriesId)
                    {
                        Logger.Trace("Unexpected series for search: {0}. Skipping.", episodeParseResult.CleanTitle);
                        continue;
                    }

                    //If SeasonNumber doesn't match or episode is not in the in the list in the parse result, skip the report.
                    if (episodeParseResult.SeasonNumber != seasonNumber && !series.AbsoluteNumbering)
                    {
                        Logger.Trace("Season number does not match searched season number, skipping.");
                        continue;
                    }

                    //If the EpisodeNumber was passed in and it is not contained in the parseResult, skip the report.
                    if (episodeNumber.HasValue && !episodeParseResult.EpisodeNumbers.Contains(episodeNumber.Value))
                    {
                        Logger.Trace("Searched episode number is not contained in post, skipping.");
                        continue;
                    }

                    //Make sure we haven't already downloaded a report with this episodenumber, if we have, skip the report.
                    if (successes.Intersect(episodeParseResult.EpisodeNumbers).Any())
                    {
                        Logger.Trace("Episode has already been downloaded in this search, skipping.");
                        continue;
                    }

                    if (_allowedDownloadSpecification.IsSatisfiedBy(episodeParseResult))
                    {
                        Logger.Debug("Found '{0}'. Adding to download queue.", episodeParseResult);
                        try
                        {
                            if (_downloadProvider.DownloadReport(episodeParseResult))
                            {
                                notification.CurrentMessage = String.Format("{0} Added to download queue", episodeParseResult);

                                //Add the list of episode numbers from this release
                                successes.AddRange(episodeParseResult.EpisodeNumbers);
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.ErrorException("Unable to add report to download queue." + episodeParseResult, e);
                            notification.CurrentMessage = String.Format("Unable to add report to download queue. {0}", episodeParseResult);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.ErrorException("An error has occurred while processing parse result items from " + episodeParseResult, e);
                }
            }

            return successes;
        }

        public bool ProcessSearchResults(ProgressNotification notification, IEnumerable<EpisodeParseResult> reports, Series series, DateTime airDate)
        {
            foreach (var episodeParseResult in reports.OrderByDescending(c => c.Quality))
            {
                try
                {
                    Logger.Trace("Analysing report " + episodeParseResult);

                    //Get the matching series
                    episodeParseResult.Series = _seriesProvider.FindSeries(episodeParseResult.CleanTitle);

                    //If series is null or doesn't match the series we're looking for return
                    if (episodeParseResult.Series == null || episodeParseResult.Series.SeriesId != series.SeriesId)
                        continue;

                    //If parse result doesn't have an air date or it doesn't match passed in airdate, skip the report.
                    if (!episodeParseResult.AirDate.HasValue || episodeParseResult.AirDate.Value.Date != airDate.Date)
                        continue;

                    if (_allowedDownloadSpecification.IsSatisfiedBy(episodeParseResult))
                    {
                        Logger.Debug("Found '{0}'. Adding to download queue.", episodeParseResult);
                        try
                        {
                            if (_downloadProvider.DownloadReport(episodeParseResult))
                            {
                                notification.CurrentMessage =
                                        String.Format("{0} - {1} {2} Added to download queue",
                                                      episodeParseResult.Series.Title, episodeParseResult.AirDate.Value.ToShortDateString(), episodeParseResult.Quality);

                                return true;
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.ErrorException("Unable to add report to download queue." + episodeParseResult, e);
                            notification.CurrentMessage = String.Format("Unable to add report to download queue. {0}", episodeParseResult);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.ErrorException("An error has occurred while processing parse result items from " + episodeParseResult, e);
                }
            }
            return false;
        }

        private List<int> GetEpisodeNumberPrefixes(IEnumerable<int> episodeNumbers)
        {
            var results = new List<int>();

            foreach (var i in episodeNumbers)
            {
                results.Add(i / 10);
            }

            return results.Distinct().ToList();
        }
    }
}
