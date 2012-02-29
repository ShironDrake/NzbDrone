﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common;
using NzbDrone.Core.Providers;
using NzbDrone.Core.Providers.Core;
using NzbDrone.Core.Repository;
using NzbDrone.Core.Repository.Quality;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;
using NzbDrone.Test.Common.AutoMoq;

namespace NzbDrone.Core.Test.ProviderTests
{
    // ReSharper disable InconsistentNaming
    public class DiskScanProviderTest : CoreTest
    {
        [Test]
        public void scan_series_should_update_the_last_scan_date()
        {

            
            Mocker.GetMock<SeriesProvider>()
                .Setup(c => c.UpdateSeries(It.Is<Series>(s => s.LastDiskSync != null))).Verifiable();

            Mocker.GetMock<EpisodeProvider>()
                .Setup(c => c.GetEpisodeBySeries(It.IsAny<long>()))
                .Returns(new List<Episode> { new Episode() });

            Mocker.GetMock<DiskProvider>()
                .Setup(c => c.FolderExists(It.IsAny<string>()))
                .Returns(true);

            Mocker.GetMock<MediaFileProvider>()
                .Setup(c => c.GetSeriesFiles(It.IsAny<int>()))
                .Returns(new List<EpisodeFile>());

            Mocker.Resolve<DiskScanProvider>().Scan(new Series());

            Mocker.VerifyAllMocks();

        }

        [Test]
        public void cleanup_should_skip_existing_files()
        {
            WithStrictMocker();
            var episodes = Builder<EpisodeFile>.CreateListOfSize(10).Build();

            Mocker.GetMock<DiskProvider>()
                .Setup(e => e.FileExists(It.IsAny<String>()))
                .Returns(true);


            //Act
            Mocker.Resolve<DiskScanProvider>().CleanUp(episodes);

            //Assert
            Mocker.VerifyAllMocks();
        }

        [Test]
        public void cleanup_should_delete_none_existing_files()
        {
            WithStrictMocker();
            var episodes = Builder<EpisodeFile>.CreateListOfSize(10).Build();

            Mocker.GetMock<DiskProvider>()
                .Setup(e => e.FileExists(It.IsAny<String>()))
                .Returns(false);

            Mocker.GetMock<EpisodeProvider>()
                .Setup(e => e.GetEpisodesByFileId(It.IsAny<int>()))
                .Returns(new List<Episode>());

            Mocker.GetMock<MediaFileProvider>()
                .Setup(e => e.Delete(It.IsAny<int>()));


            //Act
            Mocker.Resolve<DiskScanProvider>().CleanUp(episodes);

            //Assert
            Mocker.VerifyAllMocks();

            Mocker.GetMock<EpisodeProvider>()
               .Verify(e => e.GetEpisodesByFileId(It.IsAny<int>()), Times.Exactly(10));

            Mocker.GetMock<MediaFileProvider>()
                .Verify(e => e.Delete(It.IsAny<int>()), Times.Exactly(10));

        }

        [Test]
        public void cleanup_should_delete_none_existing_files_remove_links_to_episodes()
        {
            WithStrictMocker();
            var episodes = Builder<EpisodeFile>.CreateListOfSize(10).Build();

            Mocker.GetMock<DiskProvider>()
                .Setup(e => e.FileExists(It.IsAny<String>()))
                .Returns(false);

            Mocker.GetMock<EpisodeProvider>()
                .Setup(e => e.GetEpisodesByFileId(It.IsAny<int>()))
                .Returns(new List<Episode> { new Episode { EpisodeFileId = 10 }, new Episode { EpisodeFileId = 10 } });

            Mocker.GetMock<EpisodeProvider>()
                .Setup(e => e.UpdateEpisode(It.IsAny<Episode>()));

            Mocker.GetMock<MediaFileProvider>()
                .Setup(e => e.Delete(It.IsAny<int>()));

            Mocker.GetMock<ConfigProvider>()
                .SetupGet(s => s.AutoIgnorePreviouslyDownloadedEpisodes)
                .Returns(true);

            //Act
            Mocker.Resolve<DiskScanProvider>().CleanUp(episodes);

            //Assert
            Mocker.VerifyAllMocks();

            Mocker.GetMock<EpisodeProvider>()
               .Verify(e => e.GetEpisodesByFileId(It.IsAny<int>()), Times.Exactly(10));

            Mocker.GetMock<EpisodeProvider>()
                .Verify(e => e.UpdateEpisode(It.Is<Episode>(g=>g.EpisodeFileId == 0)), Times.Exactly(20));

            Mocker.GetMock<MediaFileProvider>()
                .Verify(e => e.Delete(It.IsAny<int>()), Times.Exactly(10));

            Mocker.GetMock<MediaFileProvider>()
                .Verify(e => e.Delete(It.IsAny<int>()), Times.Exactly(10));

        }

        [Test]
        public void scan_series_should_log_warning_if_path_doesnt_exist_on_disk()
        {
            //Setup
            WithStrictMocker();

            var series = Builder<Series>.CreateNew()
                .With(s => s.Path = @"C:\Test\TV\SeriesName\")
                .Build();


            Mocker.GetMock<MediaFileProvider>()
                    .Setup(c => c.CleanUpDatabase());
   

            Mocker.GetMock<DiskProvider>()
                .Setup(c => c.FolderExists(series.Path))
                .Returns(false);

            //Act
            Mocker.Resolve<DiskScanProvider>().Scan(series, series.Path);

            //Assert
            Mocker.VerifyAllMocks();
            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void move_should_not_move_file_if_source_and_destination_are_the_same_path()
        {
            var fakeSeries = Builder<Series>.CreateNew()
                    .With(s => s.SeriesId = 5)
                    .With(s => s.Title = "30 Rock")
                    .Build();

            var fakeEpisode = Builder<Episode>.CreateListOfSize(1)
                    .All()
                    .With(e => e.SeriesId = fakeSeries.SeriesId)
                    .With(e => e.SeasonNumber = 1)
                    .With(e => e.EpisodeNumber = 1)
                    .Build();

            const string filename = @"30 Rock - S01E01 - TBD";
            var fi = new FileInfo(Path.Combine(@"C:\Test\TV\30 Rock\Season 01\", filename + ".avi"));

            var file = Builder<EpisodeFile>.CreateNew()
                    .With(f => f.SeriesId = fakeSeries.SeriesId)
                    .With(f => f.Path = fi.FullName)
                    .Build();

            Mocker.GetMock<SeriesProvider>()
                .Setup(e => e.GetSeries(fakeSeries.SeriesId))
                .Returns(fakeSeries);

            Mocker.GetMock<EpisodeProvider>()
                .Setup(e => e.GetEpisodesByFileId(file.EpisodeFileId))
                .Returns(fakeEpisode);

            Mocker.GetMock<MediaFileProvider>()
                .Setup(e => e.GetNewFilename(fakeEpisode, fakeSeries.Title, It.IsAny<QualityTypes>(), It.IsAny<bool>()))
                .Returns(filename);

            Mocker.GetMock<MediaFileProvider>()
                .Setup(e => e.CalculateFilePath(It.IsAny<Series>(), fakeEpisode.First().SeasonNumber, filename, ".avi"))
                .Returns(fi);

            //Act
            var result = Mocker.Resolve<DiskScanProvider>().MoveEpisodeFile(file, false);

            //Assert
            result.Should().BeFalse();
        }
    }
}
