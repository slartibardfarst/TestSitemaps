using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;

namespace TestSitemaps.Config
{
    public static class ConfigUtils
    {
        /// <summary>
        /// Converts a string containing a comma-separated list of state codes into a hash set. If the string contains ' ', then the blank state code
        /// ([space][space]) will be added to the hash set. The special value '*' is used to mean 'match any state'
        /// </summary>
        public static HashSet<string> ParseStateCodesListString(string statesForNewParser)
        {
            bool unused;
            return ParseStateCodesListString(statesForNewParser, out unused);
        }

        /// <summary>
        /// Converts a string containing a comma-separated list of state codes into a hash set. If the string contains ' ', then the blank state code
        /// ([space][space]) will be added to the hash set. The special value '*' is used to mean 'match any state'
        /// </summary>
        public static HashSet<string> ParseStateCodesListString(string statesForNewParser, out bool matchAnyState)
        {
            string[] states = statesForNewParser.Split(new[] { ',' });

            //first trim whitespace, if any
            for (int i = 0; i < states.Length; i++)
            {
                states[i] = states[i].Trim();
                states[i] = states[i].Trim(new[] { '\'' });

                if (states[i] == " ") //if user accidentally listed state code as one space, fix this up to be two spaces
                    states[i] = "  ";
            }

            matchAnyState = states.Contains("*");

            var result = new HashSet<string>();
            foreach (var state in states)
                result.Add(state);

            return result;
        }


        internal static List<int> ParseParititionIdsString(string partitions)
        {
            if (partitions.Contains("-1"))
                partitions = "2,3,4,5";

            string[] idStrings = partitions.Split(new[] { ',' });

            return idStrings.Select(int.Parse).ToList();
        }

        public static HashSet<string> LoadStatesForNewParserFromDatabase()
        {
            string dbConnectionString = AppConfig.ConnectionStrings.MPRRedirectDB;

            var result = new HashSet<string>();
            using (var connection = new SqlConnection(dbConnectionString))
            {
                connection.Open();

                const string query = "SELECT state_code FROM MPRRedirect.dbo.PLC_enabled_states";
                var states = connection.Query<string>(query);
                foreach (string state in states)
                    result.Add(state.ToUpper());
            }

            return result;
        }
    }
}
