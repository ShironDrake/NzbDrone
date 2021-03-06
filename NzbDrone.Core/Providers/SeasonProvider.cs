using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Ninject;
using NLog;
using NzbDrone.Core.Datastore.PetaPoco;
using NzbDrone.Core.Model;
using NzbDrone.Core.Repository;
using PetaPoco;
using TvdbLib.Data;

namespace NzbDrone.Core.Providers
{
    public class SeasonProvider
    {

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        //this will remove (1),(2) from the end of multi part episodes.
        private static readonly Regex multiPartCleanupRegex = new Regex(@"\(\d+\)$", RegexOptions.Compiled);

        private readonly IDatabase _database;

        [Inject]
        public SeasonProvider(IDatabase database)
        {
            _database = database;
        }

        public SeasonProvider()
        {
        }

        public virtual void Add(int seriesId, int seasonNumber, bool isIgnored = false)
        {
            _database.Insert(new Season
            {
                SeriesId = seriesId,
                SeasonNumber = seasonNumber,
                Ignored = isIgnored
            });
        }

        public virtual void Delete(int seriesId, int seasonNumber)
        {
            _database.Delete<Season>("WHERE SeriesId = @seriesId AND SeasonNumber = @seasonNumber",
                                     new {seriesId, seasonNumber});
        }

        public virtual IList<int> GetSeasons(int seriesId)
        {
            return _database.Fetch<Int32>("SELECT DISTINCT SeasonNumber FROM Seasons WHERE SeriesId=seriesId", new { seriesId }).OrderBy(c => c).ToList();
        }

        public virtual void SetIgnore(int seriesId, int seasonNumber, bool isIgnored)
        {
            logger.Info("Setting ignore flag on Series:{0} Season:{1} to {2}", seriesId, seasonNumber, isIgnored);

            _database.Execute(@"UPDATE Seasons SET Ignored = @isIgnored
                                WHERE SeriesId = @seriesId AND SeasonNumber = @seasonNumber AND Ignored = @InversedIgnored",
                new { isIgnored, seriesId, seasonNumber, InversedIgnored = !isIgnored });

            logger.Info("Ignore flag for Series:{0} Season:{1} successfully set to {2}", seriesId, seasonNumber, isIgnored);
        }

        public virtual bool IsIgnored(int seriesId, int seasonNumber)
        {
            var season = _database.SingleOrDefault<Season>("WHERE SeriesId = @seriesId AND SeasonNumber = @seasonNumber",
                                                           new { seriesId, seasonNumber });

            if (season == null)
            {
                if (seasonNumber == 0)
                {
                    Add(seriesId, seasonNumber, true);
                    return true;
                }

                //Don't check for a previous season if season is 1
                if (seasonNumber == 1)
                {
                    Add(seriesId, seasonNumber, false);
                    return false;
                }

                //else
                var lastSeason = _database.SingleOrDefault<Season>("WHERE SeriesId = @seriesId AND SeasonNumber = @LastSeasonNumber",
                                                          new { seriesId, LastSeasonNumber = seasonNumber - 1 });

                if (lastSeason == null)
                {
                    Add(seriesId, seasonNumber, false);
                    return false;
                }

                Add(seriesId, seasonNumber, lastSeason.Ignored);
                return lastSeason.Ignored;
            }

            return season.Ignored;
        }

        public virtual List<Season> All(int seriesId)
        {
            var seasons = _database.Fetch<Season, Episode, EpisodeFile, Season>(
            new EpisodeSeasonRelator().MapIt, 
            @"SELECT * FROM Seasons
                INNER JOIN Episodes ON Episodes.SeriesId = Seasons.SeriesId
                AND Episodes.SeasonNumber = Seasons.SeasonNumber
                LEFT OUTER JOIN EpisodeFiles ON EpisodeFiles.EpisodeFileId = Episodes.EpisodeFileId
                WHERE Seasons.SeriesId = @0", seriesId
            );

            return seasons;
        }
    }
}                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      