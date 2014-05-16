using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestSitemaps;

namespace ConsoleApplication1
{
    class Program
    {
        private static void Main(string[] args)
        {
            bool done = false;
            while (!done)
            {
                Console.WriteLine("What do you want to do:");
                Console.WriteLine("1: Generate LDAs from listing addresses for analyis");
                Console.WriteLine("2: Process Zillow addresses for analysis");
                Console.WriteLine("3: Analyze RDC Sitemap response codes");
                Console.Write("Enter option: ");

                int choice;
                if (int.TryParse(Console.ReadLine(), out choice))
                {
                    switch (choice)
                    {
                        case 1:
                            ZillowAnalyzer zillowAnalyzer = new ZillowAnalyzer();
                            zillowAnalyzer.Go();
                            done = true;
                            break;

                        case 2:
                            SitemapResultsExcelAnalyzer sitemapAnalyzer = new SitemapResultsExcelAnalyzer();
                            sitemapAnalyzer.Go();
                            done = true;
                            break;

                        case 3:
                            SitemapsCrawler sitemapsCrawler = new SitemapsCrawler();
                            sitemapsCrawler.Go();
                            done = true;
                            break;
                    }
                }
            }
        }
    }
}
