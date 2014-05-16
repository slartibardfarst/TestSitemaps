using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using log4net;

namespace ConsoleApplication1.ServiceProxies
{
    public class WebRequestCommunicator
    {
        private readonly ILog _log;

        public WebRequestCommunicator(ILog log)
        {
            _log = log;
        }

        public string GetContent(string serviceUrl)
        {
            try
            {
                using (var client = new WebClient { Proxy = null })
                {
                    client.Encoding = Encoding.UTF8;
                    return client.DownloadString(serviceUrl);
                }
            }
            catch (Exception ex)
            {
                if (null != _log)
                    _log.Error(String.Format("Error when getting response. Exception: {0}. Message: {1}. ", ex.GetType(), ex.Message));

                throw;
            }
        }

        public string PostRequest(string serviceUrl, string postMessageDetailsJson)
        {
            try
            {
                using (var client = new WebClient { Proxy = null })
                {
                    client.Encoding = Encoding.UTF8;
                    client.Headers.Add("Content-Type", "application/json");
                    string response = client.UploadString(serviceUrl, "POST", postMessageDetailsJson);
                    return response;
                }
            }
            catch (Exception ex)
            {
                _log.Error(String.Format("Error when getting response. Exception: {0}. Message: {1}. ", ex.GetType(), ex.Message));
                throw;
            }
        }

    }
}
