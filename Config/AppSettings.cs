using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestSitemaps.Config
{
    public class AppSettings
    {
        private static string DefaultPartitionIdsToProcess = "-1";

        /// <summary>
        /// Returns a set of state codes to process. If set contains '*' then all states should be matched. [space][space] is also a valid state code
        /// </summary>
        public List<int> PartitionIdsToProcess
        {
            get
            {
                var value = ConfigurationManager.AppSettings["PartitionIdsToProcess"];
                string partitions = string.IsNullOrEmpty(value) ? DefaultPartitionIdsToProcess : value;
                return ConfigUtils.ParseParititionIdsString(partitions);
            }
        }

    }
}
