using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestSitemaps.Config
{
    public class ConnectionStrings
    {
        public string MPRRedirectDB
        {
            get
            {
                if (ConfigurationManager.ConnectionStrings["MPRRedirectDB"] == null)
                    throw new ConfigurationErrorsException("MPRRedirectDB is a required connection string and needs to be defined in the .config file.");
                return ConfigurationManager.ConnectionStrings["MPRRedirectDB"].ConnectionString;
            }
        }

        public string GeoService
        {
            get
            {
                if (ConfigurationManager.ConnectionStrings["GeoService"] == null)
                    throw new ConfigurationErrorsException("GeoService is a required connection string and needs to be defined in the .config file.");
                return ConfigurationManager.ConnectionStrings["GeoService"].ConnectionString;
            }
        }

        public string AddressParser
        {
            get
            {
                if (ConfigurationManager.ConnectionStrings["AddressParser"] == null)
                    throw new ConfigurationErrorsException("AddressParser is a required connection string and needs to be defined in the .config file.");
                return ConfigurationManager.ConnectionStrings["AddressParser"].ConnectionString;
            }
        }

        public string TestResults
        {
            get
            {
                if (ConfigurationManager.ConnectionStrings["TestResults"] == null)
                    throw new ConfigurationErrorsException("TestResults is a required connection string and needs to be defined in the .config file.");
                return ConfigurationManager.ConnectionStrings["TestResults"].ConnectionString;
            }
        }

    }
}
