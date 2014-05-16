using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Office.Interop.Excel;

namespace ConsoleApplication1
{
    class RdcUrlComponents
    {
        public string root { get; set; }
        public string addressLine { get; set; }
        public string city { get; set; }
        public string zip { get; set; }
        public string state { get; set; }
        public string MPR { get; set; }
    }

    class SitemapResultsExcelAnalyzer
    {
        enum SitemapAction
        {
            invalid,
            categorizedRedirectCauses
        }

        enum MismatchReason
        {
            invalid,
            noMismatch,
            MPR,
            zip,
            state,
            city,
            addressLine,
            addressLineDashes,
            addressLineNull,
            addressLineCasing,
            exception
        }


        public void Go()
        {
            var action = GetActionFromUser();
            switch (action)
            {
                case SitemapAction.categorizedRedirectCauses:
                    ProcessExcelFile("");
                    break;

                default:
                    throw new NotImplementedException("invalid action");
            }
        }


        private SitemapAction GetActionFromUser()
        {
            return SitemapAction.categorizedRedirectCauses;
        }


        private void ProcessExcelFile(string excelFilepath)
        {
            var appExcel = new Microsoft.Office.Interop.Excel.Application();
            var aggregateResults = InitializeAggregateResults();

            excelFilepath = @"C:\Users\awatkins\Downloads\realtor-sitemap-fah1-ct-1-output-AWEdits.xlsx";

            if (File.Exists(excelFilepath))
            {
                var workbook = appExcel.Workbooks.Open(excelFilepath, false, false);
                Worksheet worksheet = workbook.Sheets["realtor-sitemap-fah1-ct-1-outpu"];

                int totalRecords = ProcessExcelRecords(worksheet, aggregateResults);

                workbook.Save();
                workbook.Close();

                PrintAggregateResult(aggregateResults, totalRecords);
            }
        }




        private Dictionary<MismatchReason, List<int>> InitializeAggregateResults()
        {
            var aggregateResults = new Dictionary<MismatchReason, List<int>>();
            foreach (var val in Enum.GetValues(typeof(MismatchReason)))
            {
                aggregateResults[(MismatchReason)val] = new List<int>();
            }

            return aggregateResults;
        }


        private int ProcessExcelRecords(Worksheet worksheet, Dictionary<MismatchReason, List<int>> aggregateResults)
        {
            int numRows = worksheet.UsedRange.Rows.Count;
            for (int iRow = 13; iRow < numRows; iRow++)
            //for (int iRow = 2; iRow < 10; iRow++)
            {
                try
                {
                    string targetURL = getValue(worksheet, string.Format("A{0}", iRow));
                    string lastLocation = getValue(worksheet, string.Format("B{0}", iRow));
                    string responseCode = getValue(worksheet, string.Format("C{0}", iRow));

                    if (!responseCode.Contains("301"))
                        break;

                    RdcUrlComponents target = DeconstructUrl(targetURL);
                    RdcUrlComponents lastLoc = DeconstructUrl(lastLocation);
                    var diff = CompareUrls(target, lastLoc);

                    aggregateResults[diff].Add(iRow);
                    worksheet.Cells[iRow, 7 + (int)diff] = 1;

                }
                catch (Exception ex)
                {
                    //Console.WriteLine("Got exception processing element {0}: {1}", i, ex.Message);
                    aggregateResults[MismatchReason.exception].Add(iRow);
                    //throw;
                }
            }

            return numRows;
        }


        private void PrintAggregateResult(Dictionary<MismatchReason, List<int>> aggregateResults, int totalRecords)
        {
            foreach (var val in Enum.GetValues(typeof(MismatchReason)))
            {
                int count = aggregateResults[(MismatchReason)val].Count;
                Console.WriteLine(string.Format("{0}: {1}   ({2:0.00}%)", val.ToString(), count, (100.0 * ((double)count / (double)totalRecords))));
            }
        }


        private string getValue(Worksheet worksheet, string cellname)
        {
            string value = string.Empty;
            try
            {
                value = worksheet.get_Range(cellname).get_Value().ToString();
            }
            catch
            {
                value = "";
            }

            return value;
        }

        private RdcUrlComponents DeconstructUrl(string url)
        {
            string[] parts = url.Split(new char[] { '/' });
            string resourceId = parts[parts.Length - 1];
            string[] resourceIdParts = resourceId.Split(new char[] { '_' });
            var numResourceIdParts = resourceIdParts.Length;
            if ((numResourceIdParts < 4) || (numResourceIdParts > 5))
                throw new Exception(string.Format("unexpected number of address parts: {0}", numResourceIdParts));

            //if (url.Contains("---"))
            //    throw new Exception(string.Format("unexpected URL: {0}", url));


            var result = new RdcUrlComponents()
            {
                MPR = resourceIdParts[numResourceIdParts - 1],
                zip = resourceIdParts[numResourceIdParts - 2],
                state = resourceIdParts[numResourceIdParts - 3],
                city = resourceIdParts[numResourceIdParts - 4],
            };

            if (numResourceIdParts >= 5)
                result.addressLine = resourceIdParts[numResourceIdParts - 5];

            return result;
        }


        private MismatchReason CompareUrls(RdcUrlComponents url1, RdcUrlComponents url2)
        {
            if (url1.MPR != url2.MPR)
                return MismatchReason.MPR;

            if (url1.zip != url2.zip)
                return MismatchReason.zip;

            if (url1.state != url2.state)
                return MismatchReason.state;

            if (url1.city != url2.city)
                return MismatchReason.city;

            if (url1.addressLine != url2.addressLine)
            {
                if (string.IsNullOrEmpty(url1.addressLine) != string.IsNullOrEmpty(url2.addressLine))
                    return MismatchReason.addressLineNull;

                if (Regex.Matches(url1.addressLine, "--").Count != Regex.Matches(url2.addressLine, "--").Count)
                    return MismatchReason.addressLineDashes;

                if (url1.addressLine.ToLower() == url2.addressLine.ToLower())
                    return MismatchReason.addressLineCasing;

                return MismatchReason.addressLine;
            }

            return MismatchReason.noMismatch;
        }
    }
}
