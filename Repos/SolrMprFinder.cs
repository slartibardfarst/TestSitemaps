using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AddressCommon.DataStructures;
using ConsoleApplication1.ServiceProxies;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TestSitemaps.Models;
using log4net;

namespace TestSitemaps.Repos
{
    public class MprIdAndHasDQ
    {
        public long MprId;
        public bool HasDQ;
    }


    public class SolrMprFinder
    {
        private readonly WebRequestCommunicator _communicator;
        private readonly ILog _log;
        private readonly string _addressSolrUrl;
        private const string AddressSolrQueryTemplate = "select?q={0}&wt=json";


        public SolrMprFinder(string addressSolrUrl, ILog log)
        {
            _addressSolrUrl = addressSolrUrl;
            _log = log;
            _communicator = new WebRequestCommunicator(_log);
        }

        public List<SolrMprRecord> GetMprMatchFromSolr(IAddress address)
        {
            //Solr cannot do the search when address line in empty
            if (String.IsNullOrEmpty(address.address_line) || string.IsNullOrEmpty(AddressLinePopulator.GetFormatedAddressLine(address)))
                return new List<SolrMprRecord>();

            string addressSolrQuery = "";
            string addressSolrResponse = "";
            string solrConditions = "";

            try
            {
                solrConditions = AddSolrCondition(solrConditions, "StreetNumber", address.street_no);
                solrConditions = AddSolrCondition(solrConditions, "StreetDirection", address.street_direction);
                solrConditions = AddSolrCondition(solrConditions, "StreetName", address.street);
                solrConditions = AddSolrCondition(solrConditions, "StreetSuffix", address.street_suffix);
                solrConditions = AddSolrCondition(solrConditions, "StreetPostDirection", address.street_post_direction);
                solrConditions = AddSolrCondition(solrConditions, "Unit", address.unit);
                solrConditions = AddSolrCondition(solrConditions, "PostalCode", address.zip);
                solrConditions = AddSolrCondition(solrConditions, "State", address.state);

                solrConditions = System.Web.HttpUtility.UrlEncode(solrConditions.Remove(0, 4));
                addressSolrQuery = _addressSolrUrl + "/" + string.Format(AddressSolrQueryTemplate, solrConditions);
                addressSolrResponse = _communicator.GetContent(addressSolrQuery);
                var solrResponse = JObject.Parse(addressSolrResponse);
                var response = (JObject)solrResponse["response"];

                return response["docs"].Select(doc => JsonConvert.DeserializeObject<SolrMprRecord>(doc.ToString())).ToList();
            }
            catch (Exception ex)
            {
                _log.ErrorFormat("Got exception in GetMprMatchFromSolr(). Solr query: {0}. Solr response: {1}. Exception details: {2}",
                    addressSolrQuery == null ? "null" : addressSolrQuery,
                    addressSolrResponse ?? "null",
                    ex);


                throw;
            }
        }

        private static string AddSolrCondition(string solrQuery, string schemaFieldName, string value)
        {
            if (!string.IsNullOrEmpty(value))
                solrQuery += String.Format(" AND {0}:\"{1}\"", schemaFieldName, value);
            return solrQuery;
        }

    }
}
