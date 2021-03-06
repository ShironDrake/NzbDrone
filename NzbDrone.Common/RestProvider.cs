﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using NLog;
using Newtonsoft.Json;
using Ninject;
using NzbDrone.Common.Contract;

namespace NzbDrone.Common
{

    public class RestProvider
    {

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly EnviromentProvider _enviromentProvider;


        [Inject]
        public RestProvider(EnviromentProvider enviromentProvider)
        {
            _enviromentProvider = enviromentProvider;
        }

        public RestProvider()
        {

        }

        private const int TIMEOUT = 15000;
        private const string METHOD = "POST";

        public virtual void PostData(string url, ReportBase reportBase)
        {
            reportBase.UGuid = EnviromentProvider.UGuid;
            reportBase.Version = _enviromentProvider.Version.ToString();
            reportBase.IsProduction = EnviromentProvider.IsProduction;

            PostData(url, reportBase as object);
        }


        private static void PostData(string url, object message)
        {
            try
            {
                var json = JsonConvert.SerializeObject(message);

                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = TIMEOUT;

                request.Proxy = WebRequest.DefaultWebProxy;

                request.KeepAlive = false;
                request.ProtocolVersion = HttpVersion.Version10;
                request.Method = METHOD;
                request.ContentType = "application/json";

                byte[] postBytes = Encoding.UTF8.GetBytes(json);
                request.ContentLength = postBytes.Length;

                var requestStream = request.GetRequestStream();
                requestStream.Write(postBytes, 0, postBytes.Length);
                requestStream.Close();

                var response = (HttpWebResponse)request.GetResponse();
                var streamreader = new StreamReader(response.GetResponseStream());
                streamreader.Close();
            }
            catch (Exception e)
            {
                e.Data.Add("URL", url);
                throw;
            }
        }
    }
}
