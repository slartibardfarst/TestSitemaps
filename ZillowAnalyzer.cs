using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Xml;
using AddressCommon.DataStructures;
using ConsoleApplication1.Repos;
using ConsoleApplication1.ServiceProxies;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using SDS.Providers.MPRRouter;
using TestSitemaps.Models;
using TestSitemaps.Repos;
using TestSitemaps.ServiceProxies;

namespace ConsoleApplication1
{
    internal class ZillowAnalyzer
    {
        enum ZillowAction
        {
            invalid,
            processSitemapUrls,
            justCountSitemapUrls,
            logSitemapUrls,
            processUrlsFromSql,
            processSample10K = 10000,
            processSample100K = 100000,
            processSample1MM = 1000000,
            MatchZillowAddressesToMoveProperties
        }


        private string _addressParserServiceUrl;
        private WebRequestCommunicator _webRequestor;
        private SqlRepository _dataDestRepo;
        private BlockingCollection<string> _messageQueue;
        private int _everyNthUrlToProcess = 1;
        private int _everyNthGZipFileToProcess = 1;
        //private int _SqlParitionIdToWriteResults = 2;
        //private bool _logUrlsOnly = false;
        private int _SqlCommandCode = 0;
        private SolrMprFinder _solrMprFinder;



        public ZillowAnalyzer()
        {
            MPRRedirect dataDestMprRedirect = new MPRRedirect(ConfigurationManager.ConnectionStrings["DestMprRedirectDB"].ConnectionString);

            _addressParserServiceUrl = ConfigurationManager.ConnectionStrings["AddressParser"].ConnectionString;
            _webRequestor = new WebRequestCommunicator(null);
            _dataDestRepo = new SqlRepository(dataDestMprRedirect);

            int bufferSize = int.Parse(ConfigurationManager.AppSettings["MessageBufferSize"]);
            _messageQueue = new BlockingCollection<string>(bufferSize);

            var addressSolrUrl = ConfigurationManager.ConnectionStrings["AddressSolrServer"].ConnectionString;
            _solrMprFinder = new SolrMprFinder(addressSolrUrl, null);
        }

        public void Go()
        {
            int sqlParitionIdToWriteResults;

            string sitemapFilenameSearchPattern;
            var action = GetActionFromUser(out sitemapFilenameSearchPattern, out sqlParitionIdToWriteResults);
            switch (action)
            {
                //case ZillowAction.processSitemapUrls:
                //    _logUrlsOnly = false;
                //    ProcessSitemapUrls(_SqlParitionIdToWriteResults, "http://sitemap.zillow.com/sitemap.xml", sitemapFilenameSearchPattern);
                //    break;

                //case ZillowAction.processUrlsFromSql:
                //    ProcessSqlUrlsMatchingCode(_SqlParitionIdToWriteResults, _SqlCommandCode);
                //    break;

                case ZillowAction.justCountSitemapUrls:
                    int numZipfileMatchingPattern;
                    int numUrls = CountSitemapUrls("http://sitemap.zillow.com/sitemap.xml", sitemapFilenameSearchPattern, out numZipfileMatchingPattern);
                    Console.WriteLine("There are '{0}' zipfile matching pattern {1} with a combined total of {2} URLs", numZipfileMatchingPattern, sitemapFilenameSearchPattern, numUrls);
                    break;

                //case ZillowAction.logSitemapUrls:
                //    _logUrlsOnly = true;
                //    ProcessSitemapUrls(_SqlParitionIdToWriteResults, "http://sitemap.zillow.com/sitemap.xml", sitemapFilenameSearchPattern);
                //    break;

                case ZillowAction.processSample10K:
                case ZillowAction.processSample100K:
                    ProcessSampleFromZillowUrls(sqlParitionIdToWriteResults, action);
                    break;

                case ZillowAction.MatchZillowAddressesToMoveProperties:
                    MatchZillowAddressesToMoveProperties();
                    break;

                default:
                    throw new NotImplementedException("invalid action");
            }
        }




        private ZillowAction GetActionFromUser(out string sitemapFilenameSearchPattern, out int sqlParitionIdToWriteResults)
        {
            sitemapFilenameSearchPattern = "";
            ZillowAction result = ZillowAction.invalid;

            bool done = false;
            while (!done)
            {
                //Console.Write("Do you want to (c)ount, (l)og, (p)rocess URLs from sitemaps or process URLs from (S)QL, process (10k) sample: ");
                Console.Write("Do you want to process (10K), (100K) or (1MM) Zillow URLs or (M)atch zillow addresses to Move properties?: ");
                switch (Console.ReadLine().ToLower())
                {
                    //case "c":
                    //    result = ZillowAction.justCountSitemapUrls;
                    //    done = true;
                    //    break;

                    //case "p":
                    //    result = ZillowAction.processSitemapUrls;
                    //    done = true;
                    //    break;

                    //case "l":
                    //    result = ZillowAction.logSitemapUrls;
                    //    done = true;
                    //    break;

                    //case "s":
                    //    result = ZillowAction.processUrlsFromSql;
                    //    done = true;
                    //    break;

                    case "10k":
                        result = ZillowAction.processSample10K;
                        done = true;
                        break;

                    case "100k":
                        result = ZillowAction.processSample100K;
                        done = true;
                        break;

                    case "1mm":
                        result = ZillowAction.processSample1MM;
                        done = true;
                        break;

                    case "m":
                        result = ZillowAction.MatchZillowAddressesToMoveProperties;
                        done = true;
                        break;


                }
            }

            Console.Write("Enter SQL partition ID to write results to: (-1 for local sql, -2 for local txt) ");
            if (!int.TryParse(Console.ReadLine(), out sqlParitionIdToWriteResults))
                sqlParitionIdToWriteResults = 2;

            if ((result == ZillowAction.processSitemapUrls) || (result == ZillowAction.logSitemapUrls) || (result == ZillowAction.logSitemapUrls))
            {
                Console.Write("Enter substring to search for in sitemap filenames: ");
                sitemapFilenameSearchPattern = Console.ReadLine();
                if (string.IsNullOrEmpty(sitemapFilenameSearchPattern))
                    sitemapFilenameSearchPattern = "hdp_";
            }

            if ((result == ZillowAction.processSitemapUrls) || (result == ZillowAction.logSitemapUrls))
            {
                Console.Write("Enter every nth .gz file to process: ");
                if (!int.TryParse(Console.ReadLine(), out _everyNthGZipFileToProcess))
                    _everyNthGZipFileToProcess = 1;


                Console.Write("Enter every nth URL to process: ");
                if (!int.TryParse(Console.ReadLine(), out _everyNthUrlToProcess))
                    _everyNthUrlToProcess = 1;
            }


            if (result == ZillowAction.processUrlsFromSql)
            {
                Console.Write("Enter command code for URLs to process: ");
                if (!int.TryParse(Console.ReadLine(), out _SqlCommandCode))
                    _SqlCommandCode = 0;
            }

            return result;
        }

        //private void ProcessSitemapUrls(int sqlPartitionId, string sitemapXmlUrl, string sitemapFilenameSearchPattern)
        //{
        //    int numTasks = (sqlPartitionId == -2) ? 1 : int.Parse(ConfigurationManager.AppSettings["NumZillowUrlProcessingThreads"]);
        //    Task[] tasks = StartProcessingTasks(numTasks);

        //    var sitemapZipUrls = GetSitemaps(sitemapXmlUrl, sitemapFilenameSearchPattern);

        //    int zipfilesSoFar = 0;
        //    foreach (var zipfileUrl in sitemapZipUrls)
        //    {
        //        if (zipfilesSoFar++ % _everyNthGZipFileToProcess != 0)
        //            continue;

        //        Console.WriteLine("Processing URLs in: " + zipfileUrl);
        //        var sitemapXml = DownloadAndUnzipXml(zipfileUrl);
        //        var urls = ExtractUrlsFromSitemapXml(sitemapXml);

        //        int processedSoFar = 0;
        //        int enqueuedSoFar = 0;
        //        foreach (var zillowUrl in urls)
        //        {
        //            if (processedSoFar++ % _everyNthUrlToProcess != 0)
        //                continue;

        //            //adding a listing to the queue will unblock one of the processing tasks
        //            _messageQueue.Add(zillowUrl);

        //            if (enqueuedSoFar++ % 100 == 0)
        //                Console.Write(".");
        //        }
        //    }

        //    _messageQueue.CompleteAdding();
        //    Task.WaitAll(tasks);

        //}


        private void ProcessSampleFromZillowUrls(int sqlPartitionId, ZillowAction zillowAction)
        {
            int bufferSize = int.Parse(ConfigurationManager.AppSettings["MessageBufferSize"]);
            var messageQueue = new BlockingCollection<ZillowUrl>(bufferSize);

            int numTasks = int.Parse(ConfigurationManager.AppSettings["NumZillowProcessingThreads"]);
            object[] parms = new object[] { messageQueue, zillowAction, sqlPartitionId };
            Task[] tasks = StartProcessingTasks(numTasks, ProcessZillowUrls2, (object)parms);


            IEnumerable<ZillowUrl> zillowUrls = _dataDestRepo.GetTableSampleFromZillowUrls(sqlPartitionId, (int)zillowAction);
            int soFar = 0;
            foreach (var zillowUrl in zillowUrls)
            {
                messageQueue.Add(zillowUrl);

                if (soFar++ % 100 == 0)
                    Console.Write(".");
            }

            messageQueue.CompleteAdding();
            Task.WaitAll(tasks);
        }


        private void ProcessZillowUrls2(object state)
        {
            object[] parms = (object[])state;
            var messageQueue = (BlockingCollection<ZillowUrl>)parms[0];
            var zillowAction = (ZillowAction)parms[1];
            var parititionId = (int)parms[2];

            bool keepProcessing = true;
            while (keepProcessing)
            {
                ZillowUrl zillowUrl = messageQueue.Take();

                try
                {
                    if (string.IsNullOrEmpty(zillowUrl.addressString))
                    {
                        //we don't already know the address string for this url, need to query it from zillow 
                        zillowUrl.addressString = ScrapeAddressFromZillowUrl(zillowUrl.zillow_url);
                        _dataDestRepo.SaveAddressForZillowUrl(parititionId, zillowUrl.id, zillowUrl.addressString);
                    }

                    var parserResponse = CallBuildAddressParser(zillowUrl.addressString);

                    string flagColName;
                    bool flagColValue;
                    switch (zillowAction)
                    {
                        case ZillowAction.processSample10K:
                            flagColName = "sample_10k";
                            flagColValue = true;
                            break;

                        case ZillowAction.processSample100K:
                            flagColName = "sample_100k";
                            flagColValue = true;
                            break;

                        default:
                            throw new NotImplementedException();
                    }
                    _dataDestRepo.SaveAddressParserResponseForZillowAddress(parititionId, zillowUrl.id, parserResponse, flagColName, flagColValue);
                }
                catch (InvalidOperationException)
                {
                    // An InvalidOperationException means that Take() was called on a completed collection
                    keepProcessing = false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ignoring unexpected exception in ProcessListings: {0}", ex.Message);
                }
            }
        }


        //private void ProcessSqlUrlsMatchingCode(int sqlParitionId, int commandCode)
        //{
        //    int numTasks = (sqlParitionId == -2) ? 1 : int.Parse(ConfigurationManager.AppSettings["NumZillowUrlProcessingThreads"]);
        //    Task[] tasks = StartProcessingTasks(numTasks);

        //    IEnumerable<string> zillowUrlsToProcess = _dataDestRepo.GetZillowUrlsGivenCommandCode(sqlParitionId, commandCode);
        //    int enqueuedSoFar = 0;
        //    foreach (var zillowUrl in zillowUrlsToProcess)
        //    {
        //        //adding a listing to the queue will unblock one of the processing tasks
        //        _messageQueue.Add(zillowUrl);

        //        if (enqueuedSoFar++ % 100 == 0)
        //            Console.Write(".");
        //    }

        //    _messageQueue.CompleteAdding();
        //    Task.WaitAll(tasks);
        //}


        private Task[] StartProcessingTasks(int numTasks, Action<object> action, object state)
        {
            List<Task> taskList = new List<Task>();

            for (int i = 0; i < numTasks; i++)
                taskList.Add(Task.Factory.StartNew(action, state));

            return taskList.ToArray();
        }


        //private void ProcessZillowUrls()
        //{
        //    bool keepProcessing = true;

        //    while (keepProcessing)
        //    {
        //        string zillowUrl = _messageQueue.Take();

        //        try
        //        {
        //            if (_logUrlsOnly)
        //            {
        //                //_dataDestRepo.UpsertZillowUrl(_SqlParitionIdToWriteResults, zillowUrl);
        //                _dataDestRepo.AddZillowUrl(_SqlParitionIdToWriteResults, zillowUrl);
        //            }
        //            else
        //            {
        //                var addressString = ScrapeAddressFromZillowUrl(zillowUrl);
        //                var parserResponse = CallBuildAddressParser(addressString);
        //                _dataDestRepo.UpsertZillowQuery(_SqlParitionIdToWriteResults, zillowUrl, addressString, parserResponse);
        //            }

        //        }
        //        catch (InvalidOperationException)
        //        {
        //            // An InvalidOperationException means that Take() was called on a completed collection
        //            keepProcessing = false;
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine("Ignoring unexpected exception in ProcessListings: {0}", ex.Message);
        //        }
        //    }
        //}

        private int CountSitemapUrls(string sitemapXmlUrl, string sitemapFilenameSearchPattern, out int numZipfileMatchingPattern)
        {
            int totalUrls = 0;

            var sitemapZipUrls = GetSitemaps(sitemapXmlUrl, sitemapFilenameSearchPattern);
            foreach (var zipfileUrl in sitemapZipUrls)
            {
                Console.WriteLine("Processing URLs in: " + zipfileUrl);
                var sitemapXml = DownloadAndUnzipXml(zipfileUrl);
                var urls = ExtractUrlsFromSitemapXml(sitemapXml);
                totalUrls += urls.Count;
            }

            numZipfileMatchingPattern = sitemapZipUrls.Count;
            return totalUrls;
        }


        private List<string> GetSitemaps(string sitemapsURL, string matchPattern)
        {
            List<string> result = new List<string>();

            using (var client = new WebClient { Proxy = null })
            {
                client.Encoding = Encoding.UTF8;
                string sitemapsText = client.DownloadString(sitemapsURL);

                XmlDocument xml = new XmlDocument();
                xml.LoadXml(sitemapsText);

                XmlNodeList sitemapLocationNodes = xml.GetElementsByTagName("loc");
                foreach (XmlElement loc in sitemapLocationNodes)
                {
                    string sitemapZipUrl = loc.InnerText;
                    if (sitemapZipUrl.Contains(matchPattern))
                        result.Add(sitemapZipUrl);
                }

            }

            return result;
        }


        public XmlDocument DownloadAndUnzipXml(string URL)
        {
            XmlDocument result = new XmlDocument();

            WebClient client = new WebClient();
            Stream data = client.OpenRead(URL);
            using (GZipStream zipStream = new GZipStream(data, CompressionMode.Decompress))
            {
                using (MemoryStream memStream = new MemoryStream())
                {
                    byte[] tempBytes = new byte[4096];
                    int i;
                    while ((i = zipStream.Read(tempBytes, 0, tempBytes.Length)) != 0)
                    {
                        memStream.Write(tempBytes, 0, i);
                    }

                    using (var tempReader = new StreamReader(memStream))
                    {
                        memStream.Position = 0;
                        string s = tempReader.ReadToEnd();
                        result.LoadXml(s);
                    }
                }

            }

            return result;
        }


        private List<string> ExtractUrlsFromSitemapXml(XmlDocument sitemapXml)
        {
            List<string> result = new List<string>();

            XmlNodeList urls = sitemapXml.GetElementsByTagName("loc");
            foreach (XmlElement url in urls)
            {
                result.Add(url.InnerText);
            }

            return result;
        }

        private string ScrapeAddressFromZillowUrl(string url)
        {
            HtmlNodeCollection nodes;

            try
            {
                HtmlDocument doc = new HtmlDocument();
                string s = Download(url);
                doc.LoadHtml(s);

                nodes = doc.DocumentNode.SelectNodes("//meta[@property='og:zillow_fb:address']");
                string addressString = nodes[0].GetAttributeValue("content", "");
                return addressString;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to scrape address for url: {0}", url);
                throw;
            }
        }

        public string Download(string uri)
        {
            WebClient client = new WebClient();

            Stream data = client.OpenRead(uri);
            StreamReader reader = new StreamReader(data);
            string s = reader.ReadToEnd();
            data.Close();
            reader.Close();
            return s;
        }


        private AddressParserVerifiedAddress CallBuildAddressParser(string addressString)
        {
            string buildLdaQueryTemplate = "v1/address/parse?address={0}&debug=0&client_id=ZillowLogger&legacy_support=1";

            try
            {
                addressString = System.Web.HttpUtility.UrlEncode(addressString);
                string request = _addressParserServiceUrl + "/" + string.Format(buildLdaQueryTemplate, addressString);
                string response = _webRequestor.GetContent(request);

                AddressParserVerifiedAddress result = null;

                if (!string.IsNullOrEmpty(response) && response != "null")
                {
                    var parserResponseJobj = JObject.Parse(response);

                    if (parserResponseJobj["results"].HasValues)
                    {
                        var validatedAddressJobj = (JObject)parserResponseJobj["results"][0]["validated_address"];

                        result = validatedAddressJobj.ToObject<AddressParserVerifiedAddress>();

                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                throw new AddressParserException(ex);
            }
        }


        private void MatchZillowAddressesToMoveProperties()
        {
            int numTries = 0;
            int numMatches = 0;
            int numMatchesAfterDroppingUnit = 0;

            try
            {

                IEnumerable<ZillowUrl> zillowAddresses = _dataDestRepo.GetCurrentZillowAddresses(4);
                foreach (var zillowAddress in zillowAddresses)
                {
                    numTries++;
                    if (numTries % 100 == 0)
                        Console.Write(".");

                    List<SolrMprRecord> result = _solrMprFinder.GetMprMatchFromSolr(zillowAddress.Address);
                    if (CanSaveResult(result, zillowAddress.id, ZillowToMoveMatchType.exact))
                        numMatches++;
                    else if (!string.IsNullOrEmpty(zillowAddress.unit_number))
                    {
                        //we couldn't match primary and secondary, see if we can just match primary 
                        zillowAddress.unit_number = null;
                        result = _solrMprFinder.GetMprMatchFromSolr(zillowAddress.Address);
                        if (CanSaveResult(result, zillowAddress.id, ZillowToMoveMatchType.primaryOnly))
                            numMatchesAfterDroppingUnit++;
                    }
                }
            }
            catch (Exception ex)
            {
                int i = 1;
                throw;
            }

        }


        private bool CanSaveResult(List<SolrMprRecord> result, int zillowUrlId, ZillowToMoveMatchType matchType)
        {
            if ((result != null) && (result.Count > 0))
            {
                _dataDestRepo.SaveMprListForZillowAddress(4, zillowUrlId, matchType, result.Select(x => x.MprID).ToArray());
                return true;
            }

            return false;
        }

    }
}
