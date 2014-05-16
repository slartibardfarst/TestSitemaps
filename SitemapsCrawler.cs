using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using HtmlAgilityPack;

namespace TestSitemaps
{
    class SitemapsCrawler
    {
        public void Go()
        {
            HtmlNodeCollection nodes;

            //string url = "http://www.realtor.com/realestateandhomes-detail/foo_foo_WA_98107_M27384-36473";
            string url = "http://www.realtor.com/realestateandhomes-detail/928-Nw-52Nd-St-Unit-A_Seattle_WA_98107_M27384-36473";
            try
            {
                TestSitemaps(url);
                //HtmlDocument doc = new HtmlDocument();
                //string s = Download(url);
                //doc.LoadHtml(s);

                //nodes = doc.DocumentNode.SelectNodes("//meta[@property='og:zillow_fb:address']");
                //string addressString = nodes[0].GetAttributeValue("content", "");
                //return addressString;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to scrape address for url: {0}", url);
                throw;
            }
        }


        private string Download(string uri)
        {
            WebClient client = new WebClient();

            Stream data = client.OpenRead(uri);
            StreamReader reader = new StreamReader(data);
            string s = reader.ReadToEnd();
            data.Close();
            reader.Close();
            return s;
        }

        private void TestSitemaps(string uri)
        {
            try
            {
                HttpWebRequest myHttpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
                //myHttpWebRequest.Method = "HEAD";
                myHttpWebRequest.MaximumAutomaticRedirections=1;
                myHttpWebRequest.AllowAutoRedirect = false;
                Stopwatch sw = new Stopwatch();
                sw.Start();
                HttpWebResponse myHttpWebResponse = (HttpWebResponse)myHttpWebRequest.GetResponse();
                sw.Stop();

                long ms = sw.ElapsedMilliseconds;

                if(myHttpWebResponse.StatusCode  == HttpStatusCode.MovedPermanently)
                {
                    string s = myHttpWebResponse.Headers["Location"];
                }
                else if(myHttpWebResponse.StatusCode  == HttpStatusCode.OK)
                {
                    HtmlDocument doc = new HtmlDocument();
                    doc.Load(myHttpWebResponse.GetResponseStream());
                }
                int i = 1;
            }
            catch (Exception ex)
            {
                int i = 1;
                throw;
            }
        }

    }
}
