﻿using System;
using System.Collections.Generic;
using NzbDrone.Core.Repository.Quality;
using PetaPoco;

namespace NzbDrone.Core.Repository
{
    [TableName("EpisodeFiles")]
    [PrimaryKey("EpisodeFileId", autoIncrement = true)]
    public class EpisodeFile
    {
        public int EpisodeFileId { get; set; }

        public int SeriesId { get; set; }
        public int SeasonNumber { get; set; }
        public string Path { get; set; }
        public QualityTypes Quality { get; set; }
        public bool Proper { get; set; }
        public long Size { get; set; }
        public DateTime DateAdded { get; set; }

        [Ignore]
        public Model.Quality QualityWrapper
        {
            get
            {
                return new Model.Quality(Quality, Proper);
            }
            set
            {
                Quality = value.QualityType;
                Proper = value.Proper;
            }
        }
    }
}