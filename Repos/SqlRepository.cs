using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AddressCommon.DataStructures;
using Dapper;
using SDS.Providers.MPRRouter;
using TestSitemaps.Config;
using TestSitemaps.Models;

namespace ConsoleApplication1.Repos
{
    public class SqlRepository
    {
        private readonly MPRRedirect _mprRedirect;
        private readonly List<string> _partitionConnectionStrings;

        public SqlRepository(MPRRedirect mprRedirect)
        {
            _mprRedirect = mprRedirect;
            _partitionConnectionStrings = SqlConnectionStrings();
        }

        public List<int> GetListingsForMpr(long MPR)
        {
            ConcurrentBag<int> bag = new ConcurrentBag<int>();
            Parallel.ForEach(_partitionConnectionStrings, cs => GetListingsForMpr(MPR, cs).ForEach(l => bag.Add(l)));

            return bag.ToList();
        }

        public Address GetListingAddress(int listingId, string stateCode)
        {
            const string sql = @"SELECT 
                                       --l.raw_address_line as address_line,
                                       l.address_line as address_line,
                                       l.city as city,
                                       l.country as country,
                                       l.county as county,
                                       l.state_code as state,
	                                   l.street_name as street,
	                                   l.site_dir as street_direction,
	                                   l.house_nbr as street_no,
	                                   l.street_post_dir as street_post_direction,
	                                   l.site_suf as street_suffix,
	                                   l.unit_val as unit,
	                                   l.zip as zip,
	                                   l.zip_plus_four as zip_plus_four
                                FROM Property.dataagg.listings l (NOLOCK)
                                WHERE l.listing_id = @listing_id AND
                                      l.state_code = @state_code";

            var connectionString = _mprRedirect.GetConnectionStringByStateCode(stateCode, "Property");

            using (var dbConnection = new SqlConnection(connectionString))
            {
                dbConnection.Open();

                var result = dbConnection.Query<Address>(sql, new { listing_id = listingId, state_code = stateCode }, commandTimeout: 9800).FirstOrDefault();
                return result;
            }
        }

        public IEnumerable<ListingDetails> GetListings(int partitionId, string prefixClause, string whereClause)
        {
            string queryTemplate = @"select {0} l.listing_id as listingId, 
                                    l.country, 
                                    l.state_code, 
                                    l.city, 
                                    l.zip, 
                                    --l.raw_address_line as address_line, 
                                    l.address_line as address_line, 
                                    l.house_nbr as house_number, 
                                    l.site_dir as street_direction, 
                                    l.street_name, 
                                    l.site_suf as street_suffix, 
                                    l.street_post_dir as street_post_direction,
                                    l.unit_val as unit_number, 
                                    l.latitude, 
                                    l.longitude, 
                                    l.county,
                                    l.zip_plus_four
                                    from Property.dataagg.listings AS l WITH (NOLOCK)
                                    {1}";

            string query = string.Format(queryTemplate, prefixClause, whereClause);
            string connectionString = _mprRedirect.GetConnectionStringByPartitionId(partitionId, "Property");
            using (var dbConnection = new SqlConnection(connectionString))
            {
                dbConnection.Open();
                var listings = dbConnection.Query<ListingDetails>(query, commandTimeout: 9800);
                foreach (var listing in listings)
                {
                    yield return listing;
                }
            }
        }


        private List<int> GetListingsForMpr(long MPR, string connectionString)
        {
            string sql = @" SELECT top 5 listing_id
                            FROM Property.dataagg.listings l  (NOLOCK)
                            INNER JOIN MasterPropertyRecord.dbo.property_external_ids pei ON l.property_id = substring(pei.property_id, 3, 16)
                            where pei.mpr_id = @mpr";

            using (var dbConnection = new SqlConnection(connectionString))
            {
                dbConnection.Open();

                var result = dbConnection.Query<int>(sql, new { mpr = MPR }, commandTimeout: 9800);
                return result.ToList();
            }
        }

        private List<string> SqlConnectionStrings()
        {
            List<string> result = new List<string>();

            if (null != AppConfig.AppSettings)
            {
                var pids = AppConfig.AppSettings.PartitionIdsToProcess;
                foreach (var pid in pids)
                {
                    var connectionString = _mprRedirect.GetConnectionStringByPartitionId(pid, SDSDatabaseNames.Property);
                    result.Add(connectionString);
                }
            }

            return result;
        }

//        public void LogBuildLdaStats(int listingId, string listingStateCode, string listingAddressString, Address LDA, BuildDisplayAddressDiagnostics diag)
//        {
//            const string updateSqlTemplate = @"BEGIN TRAN
//                                               UPDATE   MasterPropertyRecord.dbo.zzz_analyze_listing_display_address_components
//                                               SET 
//                                                        [listing_address_string]	= @listing_address_string,
//
//	                                                    [lda_address_line]		    = @lda_address_line,
//	                                                    [lda_street_number]		    = @lda_street_number,
//	                                                    [lda_street_direction]	    = @lda_street_direction,
//	                                                    [lda_street_name]		    = @lda_street_name,
//	                                                    [lda_street_suffix]		    = @lda_street_suffix,
//	                                                    [lda_street_post_direction] = @lda_street_post_direction,
//	                                                    [lda_unit_value]		    = @lda_unit_value,
//	                                                    [lda_zip]				    = @lda_zip,
//	                                                    [lda_city]				    = @lda_city,
//	                                                    [lda_county]			    = @lda_county,
//	                                                    [lda_state_code]		    = @lda_state_code,
//
//	                                                    [aa_street_number]          = @aa_street_number,
//	                                                    [aa_street_direction]       = @aa_street_direction,
//	                                                    [aa_street_name]            = @aa_street_name,
//	                                                    [aa_street_suffix]          = @aa_street_suffix,
//	                                                    [aa_street_post_direction]  = @aa_street_post_direction,
//	                                                    [aa_unit_value]			    = @aa_unit_value,
//	                                                    [aa_zip]				    = @aa_zip,
//	                                                    [aa_city]                   = @aa_city,
//	                                                    [aa_county]                 = @aa_county,
//	                                                    [aa_state_code]             = @aa_state_code,
//
//	                                                    [ip_address_line]		    = @ip_address_line,
//	                                                    [ip_street_number]		    = @ip_street_number,
//	                                                    [ip_street_direction]	    = @ip_street_direction,
//	                                                    [ip_street_name]		    = @ip_street_name,
//	                                                    [ip_street_suffix]		    = @ip_street_suffix,
//	                                                    [ip_street_post_direction]  = @ip_street_post_direction,
//	                                                    [ip_unit_value]		        = @ip_unit_value,
//	                                                    [ip_zip]				    = @ip_zip,
//	                                                    [ip_city]				    = @ip_city,
//	                                                    [ip_county]			        = @ip_county,
//	                                                    [ip_state_code]		        = @ip_state_code,
//                                                        [ip_verification_code]		= @ip_verification_code,
//
//	                                                    [qas_address_line]		    = @qas_address_line,
//	                                                    [qas_street_number]		    = @qas_street_number,
//	                                                    [qas_street_direction]	    = @qas_street_direction,
//	                                                    [qas_street_name]		    = @qas_street_name,
//	                                                    [qas_street_suffix]		    = @qas_street_suffix,
//	                                                    [qas_street_post_direction] = @qas_street_post_direction,
//	                                                    [qas_unit_value]		    = @qas_unit_value,
//	                                                    [qas_zip]				    = @qas_zip,
//	                                                    [qas_city]				    = @qas_city,
//	                                                    [qas_county]			    = @qas_county,
//	                                                    [qas_state_code]		    = @qas_state_code,
//                                                        [qas_verification_code]		= @qas_verification_code,
//
//                                                        [fix_city_raw_city]		    = @fix_city_raw_city,
//                                                        [fix_city_fixed_city]		= @fix_city_fixed_city,
//                                                        [fix_city_fix_type]		    = @fix_city_fix_type,
//
//                                                        [transformations]		    = @transformations,
//
//                                                        [use_qas_for_display]		= @use_qas_for_display
//
//                                               WHERE listing_id         = @listing_id AND
//                                                     listing_state_code = @listing_state_code
//
//                                               IF @@ROWCOUNT = 0
//                                               BEGIN
//                                                  INSERT INTO MasterPropertyRecord.dbo.zzz_analyze_listing_display_address_components
//                                                  (
//	                                                    [listing_id],
//                                                        [listing_state_code],
//                                                        [listing_address_string],
//
//	                                                    [lda_address_line],
//	                                                    [lda_street_number],
//	                                                    [lda_street_direction],
//	                                                    [lda_street_name],
//	                                                    [lda_street_suffix],
//	                                                    [lda_street_post_direction],
//	                                                    [lda_unit_value],
//	                                                    [lda_zip],
//	                                                    [lda_city],
//	                                                    [lda_county],
//	                                                    [lda_state_code],
//
//	                                                    [aa_street_number],
//	                                                    [aa_street_direction],
//	                                                    [aa_street_name],
//	                                                    [aa_street_suffix],
//	                                                    [aa_street_post_direction],
//	                                                    [aa_unit_value],
//	                                                    [aa_zip],
//	                                                    [aa_city],
//	                                                    [aa_county],
//	                                                    [aa_state_code],
//
//	                                                    [ip_address_line],
//	                                                    [ip_street_number],
//	                                                    [ip_street_direction],
//	                                                    [ip_street_name],
//	                                                    [ip_street_suffix],
//	                                                    [ip_street_post_direction],
//	                                                    [ip_unit_value],
//	                                                    [ip_zip],
//	                                                    [ip_city],
//	                                                    [ip_county],
//	                                                    [ip_state_code],
//                                                        [ip_verification_code],
//
//	                                                    [qas_address_line],
//	                                                    [qas_street_number],
//	                                                    [qas_street_direction],
//	                                                    [qas_street_name],
//	                                                    [qas_street_suffix],
//	                                                    [qas_street_post_direction],
//	                                                    [qas_unit_value],
//	                                                    [qas_zip],
//	                                                    [qas_city],
//	                                                    [qas_county],
//	                                                    [qas_state_code],
//                                                        [qas_verification_code],
//
//                                                        [fix_city_raw_city],
//                                                        [fix_city_fixed_city],
//                                                        [fix_city_fix_type],
//
//                                                        [transformations],
//
//                                                        [use_qas_for_display]
//                                                  )
//                                                  VALUES
//                                                  (
//                                                        @listing_id,
//                                                        @listing_state_code,
//                                                        @listing_address_string,
//
//                                                        @lda_address_line,
//                                                        @lda_street_number,
//                                                        @lda_street_direction,
//                                                        @lda_street_name,
//                                                        @lda_street_suffix,
//                                                        @lda_street_post_direction,
//                                                        @lda_unit_value,
//                                                        @lda_zip,
//                                                        @lda_city,
//                                                        @lda_county,
//                                                        @lda_state_code,
//
//	                                                    @aa_street_number,
//	                                                    @aa_street_direction,
//	                                                    @aa_street_name,
//	                                                    @aa_street_suffix,
//	                                                    @aa_street_post_direction,
//	                                                    @aa_unit_value,
//	                                                    @aa_zip,
//	                                                    @aa_city,
//	                                                    @aa_county,
//	                                                    @aa_state_code,
//
//	                                                    @ip_address_line,
//	                                                    @ip_street_number,
//	                                                    @ip_street_direction,
//	                                                    @ip_street_name,
//	                                                    @ip_street_suffix,
//	                                                    @ip_street_post_direction,
//	                                                    @ip_unit_value,
//	                                                    @ip_zip,
//	                                                    @ip_city,
//	                                                    @ip_county,
//	                                                    @ip_state_code,
//                                                        @ip_verification_code,
//
//	                                                    @qas_address_line,
//	                                                    @qas_street_number,
//	                                                    @qas_street_direction,
//	                                                    @qas_street_name,
//	                                                    @qas_street_suffix,
//	                                                    @qas_street_post_direction,
//	                                                    @qas_unit_value,
//	                                                    @qas_zip,
//	                                                    @qas_city,
//	                                                    @qas_county,
//	                                                    @qas_state_code,
//                                                        @qas_verification_code,
//
//                                                        @fix_city_raw_city,
//                                                        @fix_city_fixed_city,
//                                                        @fix_city_fix_type,
//
//                                                        @transformations,
//
//                                                        @use_qas_for_display
//                                                   )
//                                               END
//                                            COMMIT TRAN";

//            try
//            {
//                string connectionString = _mprRedirect.GetConnectionStringByStateCode(listingStateCode, "MasterPropertyRecord");
//                using (var dbConnection = new SqlConnection(connectionString))
//                {
//                    dbConnection.Open();
//                    SqlCommand cmd = new SqlCommand(updateSqlTemplate, dbConnection);
//                    cmd.Parameters.AddWithValue("listing_id", listingId);
//                    cmd.Parameters.AddWithValue("listing_state_code", listingStateCode);
//                    cmd.Parameters.AddWithValue("listing_address_string", listingAddressString);

//                    cmd.Parameters.AddWithValue("lda_address_line", ((object)LDA.address_line) ?? DBNull.Value);
//                    cmd.Parameters.AddWithValue("lda_street_number", ((object)LDA.street_no) ?? DBNull.Value);
//                    cmd.Parameters.AddWithValue("lda_street_direction", ((object)LDA.street_direction) ?? DBNull.Value);
//                    cmd.Parameters.AddWithValue("lda_street_name", ((object)LDA.street) ?? DBNull.Value);
//                    cmd.Parameters.AddWithValue("lda_street_suffix", ((object)LDA.street_suffix) ?? DBNull.Value);
//                    cmd.Parameters.AddWithValue("lda_street_post_direction", ((object)LDA.street_post_direction) ?? DBNull.Value);
//                    cmd.Parameters.AddWithValue("lda_unit_value", ((object)LDA.unit) ?? DBNull.Value);
//                    cmd.Parameters.AddWithValue("lda_zip", ((object)LDA.zip) ?? DBNull.Value);
//                    cmd.Parameters.AddWithValue("lda_city", ((object)LDA.city) ?? DBNull.Value);
//                    cmd.Parameters.AddWithValue("lda_county", ((object)LDA.county) ?? DBNull.Value);
//                    cmd.Parameters.AddWithValue("lda_state_code", ((object)LDA.state) ?? DBNull.Value);

//                    if (null != diag.RawAnalyzedAddress)
//                    {
//                        cmd.Parameters.AddWithValue("aa_street_number", ((object)diag.RawAnalyzedAddress.street_no) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("aa_street_direction", ((object)diag.RawAnalyzedAddress.street_direction) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("aa_street_name", ((object)diag.RawAnalyzedAddress.street) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("aa_street_suffix", ((object)diag.RawAnalyzedAddress.street_suffix) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("aa_street_post_direction", ((object)diag.RawAnalyzedAddress.street_post_direction) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("aa_unit_value", ((object)diag.RawAnalyzedAddress.unit) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("aa_zip", ((object)diag.RawAnalyzedAddress.zip) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("aa_city", ((object)diag.RawAnalyzedAddress.city) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("aa_county", ((object)diag.RawAnalyzedAddress.county) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("aa_state_code", ((object)diag.RawAnalyzedAddress.state) ?? DBNull.Value);
//                    }
//                    else
//                    {
//                        cmd.Parameters.AddWithValue("aa_street_number", DBNull.Value);
//                        cmd.Parameters.AddWithValue("aa_street_direction", DBNull.Value);
//                        cmd.Parameters.AddWithValue("aa_street_name", DBNull.Value);
//                        cmd.Parameters.AddWithValue("aa_street_suffix", DBNull.Value);
//                        cmd.Parameters.AddWithValue("aa_street_post_direction", DBNull.Value);
//                        cmd.Parameters.AddWithValue("aa_unit_value", DBNull.Value);
//                        cmd.Parameters.AddWithValue("aa_zip", DBNull.Value);
//                        cmd.Parameters.AddWithValue("aa_city", DBNull.Value);
//                        cmd.Parameters.AddWithValue("aa_county", DBNull.Value);
//                        cmd.Parameters.AddWithValue("aa_state_code", DBNull.Value);
//                    }

//                    if (null != diag.InternalParsedAddress)
//                    {
//                        cmd.Parameters.AddWithValue("ip_address_line", ((object)diag.InternalParsedAddress.address.address_line) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("ip_street_number", ((object)diag.InternalParsedAddress.address.street_no) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("ip_street_direction", ((object)diag.InternalParsedAddress.address.street_direction) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("ip_street_name", ((object)diag.InternalParsedAddress.address.street) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("ip_street_suffix", ((object)diag.InternalParsedAddress.address.street_suffix) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("ip_street_post_direction", ((object)diag.InternalParsedAddress.address.street_post_direction) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("ip_unit_value", ((object)diag.InternalParsedAddress.address.unit) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("ip_zip", ((object)diag.InternalParsedAddress.address.zip) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("ip_city", ((object)diag.InternalParsedAddress.address.city) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("ip_county", ((object)diag.InternalParsedAddress.address.county) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("ip_state_code", ((object)diag.InternalParsedAddress.address.state) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("ip_verification_code", ((object)diag.InternalParsedAddress.verification_code) ?? DBNull.Value);
//                    }
//                    else
//                    {
//                        cmd.Parameters.AddWithValue("ip_address_line", DBNull.Value);
//                        cmd.Parameters.AddWithValue("ip_street_number", DBNull.Value);
//                        cmd.Parameters.AddWithValue("ip_street_direction", DBNull.Value);
//                        cmd.Parameters.AddWithValue("ip_street_name", DBNull.Value);
//                        cmd.Parameters.AddWithValue("ip_street_suffix", DBNull.Value);
//                        cmd.Parameters.AddWithValue("ip_street_post_direction", DBNull.Value);
//                        cmd.Parameters.AddWithValue("ip_unit_value", DBNull.Value);
//                        cmd.Parameters.AddWithValue("ip_zip", DBNull.Value);
//                        cmd.Parameters.AddWithValue("ip_city", DBNull.Value);
//                        cmd.Parameters.AddWithValue("ip_county", DBNull.Value);
//                        cmd.Parameters.AddWithValue("ip_state_code", DBNull.Value);
//                        cmd.Parameters.AddWithValue("ip_verification_code", DBNull.Value);
//                    }

//                    if (null != diag.QasParsedAddress)
//                    {
//                        cmd.Parameters.AddWithValue("qas_address_line", ((object)diag.QasParsedAddress.address.address_line) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("qas_street_number", ((object)diag.QasParsedAddress.address.street_no) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("qas_street_direction", ((object)diag.QasParsedAddress.address.street_direction) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("qas_street_name", ((object)diag.QasParsedAddress.address.street) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("qas_street_suffix", ((object)diag.QasParsedAddress.address.street_suffix) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("qas_street_post_direction", ((object)diag.QasParsedAddress.address.street_post_direction) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("qas_unit_value", ((object)diag.QasParsedAddress.address.unit) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("qas_zip", ((object)diag.QasParsedAddress.address.zip) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("qas_city", ((object)diag.QasParsedAddress.address.city) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("qas_county", ((object)diag.QasParsedAddress.address.county) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("qas_state_code", ((object)diag.QasParsedAddress.address.state) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("qas_verification_code", ((object)diag.QasParsedAddress.verification_code) ?? DBNull.Value);
//                    }
//                    else
//                    {
//                        cmd.Parameters.AddWithValue("qas_address_line", DBNull.Value);
//                        cmd.Parameters.AddWithValue("qas_street_number", DBNull.Value);
//                        cmd.Parameters.AddWithValue("qas_street_direction", DBNull.Value);
//                        cmd.Parameters.AddWithValue("qas_street_name", DBNull.Value);
//                        cmd.Parameters.AddWithValue("qas_street_suffix", DBNull.Value);
//                        cmd.Parameters.AddWithValue("qas_street_post_direction", DBNull.Value);
//                        cmd.Parameters.AddWithValue("qas_unit_value", DBNull.Value);
//                        cmd.Parameters.AddWithValue("qas_zip", DBNull.Value);
//                        cmd.Parameters.AddWithValue("qas_city", DBNull.Value);
//                        cmd.Parameters.AddWithValue("qas_county", DBNull.Value);
//                        cmd.Parameters.AddWithValue("qas_state_code", DBNull.Value);
//                        cmd.Parameters.AddWithValue("qas_verification_code", DBNull.Value);
//                    }

//                    if (null != diag.FixCityResults)
//                    {
//                        cmd.Parameters.AddWithValue("@fix_city_raw_city", ((object)diag.FixCityResults.ListingCity) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("@fix_city_fixed_city", ((object)diag.FixCityResults.FixedCity) ?? DBNull.Value);
//                        cmd.Parameters.AddWithValue("@fix_city_fix_type", ((object)diag.FixCityResults.FixTypeString) ?? DBNull.Value);
//                    }
//                    else
//                    {
//                        cmd.Parameters.AddWithValue("@fix_city_raw_city", DBNull.Value);
//                        cmd.Parameters.AddWithValue("@fix_city_fixed_city", DBNull.Value);
//                        cmd.Parameters.AddWithValue("@fix_city_fix_type", DBNull.Value);
//                    }

//                    cmd.Parameters.AddWithValue("@transformations", ((object)diag.RawToQasTransformations) ?? DBNull.Value);

//                    cmd.Parameters.AddWithValue("@use_qas_for_display", ((object)diag.UseQasForDisplayAddress) ?? DBNull.Value);

//                    cmd.ExecuteNonQuery();
//                }
//            }
//            catch (Exception ex)
//            {
//                int i = listingId;
//                throw;
//            }

//        }




        public void UpsertZillowQuery(int sqlPartitionId, string zillowUrl, string addressString, AddressParserVerifiedAddress parserResponse)
        {
            const string updateSqlTemplate = @"BEGIN TRAN
                                               UPDATE   MasterPropertyRecord.dbo.zzz_analyze_zillow_addresses
                                               SET 
                                                        [zillow_url]	            = @zillow_url,
                                                        [address_string]	        = @address_string,

	                                                    [ap_address_line]		    = @ap_address_line,
	                                                    [ap_street_number]		    = @ap_street_number,
	                                                    [ap_street_direction]	    = @ap_street_direction,
	                                                    [ap_street_name]		    = @ap_street_name,
	                                                    [ap_street_suffix]		    = @ap_street_suffix,
	                                                    [ap_street_post_direction]  = @ap_street_post_direction,
	                                                    [ap_unit_value]		        = @ap_unit_value,
	                                                    [ap_zip]				    = @ap_zip,
	                                                    [ap_city]				    = @ap_city,
	                                                    [ap_county]			        = @ap_county,
	                                                    [ap_state_code]		        = @ap_state_code,
                                                        [ap_verification_code]		= @ap_verification_code,
                                                        [qas_response_code]			= @qas_response_code

                                               WHERE zillow_url         = @zillow_url

                                               IF @@ROWCOUNT = 0
                                               BEGIN
                                                  INSERT INTO MasterPropertyRecord.dbo.zzz_analyze_zillow_addresses
                                                  (
                                                        [zillow_url],
                                                        [address_string],

	                                                    [ap_address_line],
	                                                    [ap_street_number],
	                                                    [ap_street_direction],
	                                                    [ap_street_name],
	                                                    [ap_street_suffix],
	                                                    [ap_street_post_direction],
	                                                    [ap_unit_value],
	                                                    [ap_zip],
	                                                    [ap_city],
	                                                    [ap_county],
	                                                    [ap_state_code],
                                                        [ap_verification_code],
                                                        [qas_response_code]
                                                  )
                                                  VALUES
                                                  (
                                                        @zillow_url,
                                                        @address_string,

	                                                    @ap_address_line,
	                                                    @ap_street_number,
	                                                    @ap_street_direction,
	                                                    @ap_street_name,
	                                                    @ap_street_suffix,
	                                                    @ap_street_post_direction,
	                                                    @ap_unit_value,
	                                                    @ap_zip,
	                                                    @ap_city,
	                                                    @ap_county,
	                                                    @ap_state_code,
                                                        @ap_verification_code,
                                                        @qas_response_code
                                                   )
                                               END
                                            COMMIT TRAN";

            try
            {
                string connectionString = _mprRedirect.GetConnectionStringByPartitionId(sqlPartitionId, "MasterPropertyRecord");
                using (var dbConnection = new SqlConnection(connectionString))
                {
                    dbConnection.Open();
                    SqlCommand cmd = new SqlCommand(updateSqlTemplate, dbConnection);
                    cmd.Parameters.AddWithValue("zillow_url", zillowUrl);
                    cmd.Parameters.AddWithValue("address_string", addressString);


                    if (null != parserResponse)
                    {
                        cmd.Parameters.AddWithValue("ap_address_line", ((object)parserResponse.address.address_line) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_number", ((object)parserResponse.address.street_no) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_direction", ((object)parserResponse.address.street_direction) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_name", ((object)parserResponse.address.street) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_suffix", ((object)parserResponse.address.street_suffix) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_post_direction", ((object)parserResponse.address.street_post_direction) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_unit_value", ((object)parserResponse.address.unit) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_zip", ((object)parserResponse.address.zip) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_city", ((object)parserResponse.address.city) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_county", ((object)parserResponse.address.county) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_state_code", ((object)parserResponse.address.state) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_verification_code", ((object)parserResponse.verification_code) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("qas_response_code", ((object)parserResponse.qas_response_code) ?? DBNull.Value);
                    }
                    else
                    {
                        cmd.Parameters.AddWithValue("ap_address_line", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_number", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_direction", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_name", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_suffix", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_post_direction", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_unit_value", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_zip", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_city", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_county", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_state_code", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_verification_code", DBNull.Value);
                        cmd.Parameters.AddWithValue("qas_response_code", DBNull.Value);
                    }

                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public void LogZillowQuery(int sqlPartitionId, string zillowUrl, string addressString, AddressParserVerifiedAddress parserResponse)
        {
            const string updateSqlTemplate = @"
                                                  INSERT INTO MasterPropertyRecord.dbo.zzz_analyze_zillow_addresses
                                                  (
                                                        [zillow_url],
                                                        [address_string],

	                                                    [ap_address_line],
	                                                    [ap_street_number],
	                                                    [ap_street_direction],
	                                                    [ap_street_name],
	                                                    [ap_street_suffix],
	                                                    [ap_street_post_direction],
	                                                    [ap_unit_value],
	                                                    [ap_zip],
	                                                    [ap_city],
	                                                    [ap_county],
	                                                    [ap_state_code],
                                                        [ap_verification_code],
                                                        [qas_response_code]
                                                  )
                                                  VALUES
                                                  (
                                                        @zillow_url,
                                                        @address_string,

	                                                    @ap_address_line,
	                                                    @ap_street_number,
	                                                    @ap_street_direction,
	                                                    @ap_street_name,
	                                                    @ap_street_suffix,
	                                                    @ap_street_post_direction,
	                                                    @ap_unit_value,
	                                                    @ap_zip,
	                                                    @ap_city,
	                                                    @ap_county,
	                                                    @ap_state_code,
                                                        @ap_verification_code,
                                                        @qas_response_code
                                                   )";

            try
            {
                string connectionString = _mprRedirect.GetConnectionStringByPartitionId(sqlPartitionId, "MasterPropertyRecord");
                using (var dbConnection = new SqlConnection(connectionString))
                {
                    dbConnection.Open();
                    SqlCommand cmd = new SqlCommand(updateSqlTemplate, dbConnection);
                    cmd.Parameters.AddWithValue("zillow_url", zillowUrl);
                    cmd.Parameters.AddWithValue("address_string", addressString);


                    if (null != parserResponse)
                    {
                        cmd.Parameters.AddWithValue("ap_address_line", ((object)parserResponse.address.address_line) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_number", ((object)parserResponse.address.street_no) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_direction", ((object)parserResponse.address.street_direction) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_name", ((object)parserResponse.address.street) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_suffix", ((object)parserResponse.address.street_suffix) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_post_direction", ((object)parserResponse.address.street_post_direction) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_unit_value", ((object)parserResponse.address.unit) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_zip", ((object)parserResponse.address.zip) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_city", ((object)parserResponse.address.city) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_county", ((object)parserResponse.address.county) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_state_code", ((object)parserResponse.address.state) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_verification_code", ((object)parserResponse.verification_code) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("qas_response_code", ((object)parserResponse.qas_response_code) ?? DBNull.Value);
                    }
                    else
                    {
                        cmd.Parameters.AddWithValue("ap_address_line", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_number", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_direction", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_name", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_suffix", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_post_direction", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_unit_value", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_zip", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_city", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_county", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_state_code", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_verification_code", DBNull.Value);
                        cmd.Parameters.AddWithValue("qas_response_code", DBNull.Value);
                    }

                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public void UpsertZillowUrl(int sqlPartitionId, string zillowUrl)
        {
            const string updateSqlTemplate = @"BEGIN TRAN
                                               UPDATE   MasterPropertyRecord.dbo.zzz_analyze_zillow_addresses
                                               SET 
                                                        [zillow_url] = @zillow_url
                                               WHERE zillow_url = @zillow_url

                                               IF @@ROWCOUNT = 0
                                               BEGIN
                                                  INSERT INTO MasterPropertyRecord.dbo.zzz_analyze_zillow_addresses
                                                  (
                                                        [zillow_url]
                                                  )
                                                  VALUES
                                                  (
                                                        @zillow_url
                                                  )
                                               END
                                            COMMIT TRAN";

            try
            {
                string connectionString;
                if (sqlPartitionId == -1)
                {
                    connectionString = ConfigurationManager.ConnectionStrings["LocalDB"].ConnectionString;
                }
                else
                {
                    connectionString = _mprRedirect.GetConnectionStringByPartitionId(sqlPartitionId, "MasterPropertyRecord");
                }
                using (var dbConnection = new SqlConnection(connectionString))
                {
                    dbConnection.Open();
                    SqlCommand cmd = new SqlCommand(updateSqlTemplate, dbConnection);
                    cmd.Parameters.AddWithValue("zillow_url", zillowUrl);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }


        public IEnumerable<string> GetZillowUrlsGivenCommandCode(int sqlParitionId, int commandCode)
        {
            string sql = @" SELECT zillow_url
                            FROM [MasterPropertyRecord].[dbo].[zzz_analyze_zillow_addresses]
                            WHERE command = @commandCode";
            string connectionString = _mprRedirect.GetConnectionStringByPartitionId(sqlParitionId, "MasterPropertyRecord");
            using (var dbConnection = new SqlConnection(connectionString))
            {
                dbConnection.Open();
                var zillowUrls = dbConnection.Query<string>(sql, new { commandCode }, commandTimeout: 9800);
                foreach (var zillowUrl in zillowUrls)
                {
                    yield return zillowUrl;
                }
            }
        }

        public void AddZillowUrl(int sqlPartitionId, string zillowUrl)
        {
            if (sqlPartitionId == -2)
            {
                LogZillowUrlToTextFile(zillowUrl);
                return;
            }

            const string updateSqlTemplate = @"INSERT INTO MasterPropertyRecord.dbo.zzz_analyze_zillow_addresses
                                                  (
                                                        [zillow_url]
                                                  )
                                                  VALUES
                                                  (
                                                        @zillow_url
                                                  )";
            try
            {
                string connectionString;
                if (sqlPartitionId == -1)
                {
                    connectionString = ConfigurationManager.ConnectionStrings["LocalDB"].ConnectionString;
                }
                else
                {
                    connectionString = _mprRedirect.GetConnectionStringByPartitionId(sqlPartitionId, "MasterPropertyRecord");
                }
                using (var dbConnection = new SqlConnection(connectionString))
                {
                    dbConnection.Open();
                    SqlCommand cmd = new SqlCommand(updateSqlTemplate, dbConnection);
                    cmd.Parameters.AddWithValue("zillow_url", zillowUrl);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private StreamWriter _fileWriter = null;
        private void LogZillowUrlToTextFile(string zillowUrl)
        {
            //File.AppendAllText(".\\ZillowUrls.log", zillowUrl);
            if (null == _fileWriter)
            {
                _fileWriter = File.AppendText(".\\ZillowUrls.log");
            }

            _fileWriter.WriteLine(zillowUrl);
        }

        public IEnumerable<ZillowUrl> GetKnownZillowAddressStrings(int sqlParitionId)
        {
            string sql = @" SELECT zillow_url, address_string as address
                            FROM [MasterPropertyRecord].[dbo].[zzz_analyze_zillow_addresses]
                            WHERE address_string is not null";
            string connectionString = _mprRedirect.GetConnectionStringByPartitionId(sqlParitionId, "MasterPropertyRecord");
            using (var dbConnection = new SqlConnection(connectionString))
            {
                dbConnection.Open();
                var zillowUrls = dbConnection.Query<ZillowUrl>(sql, commandTimeout: 9800);
                foreach (var zillowUrl in zillowUrls)
                {
                    yield return zillowUrl;
                }
            }
        }

        public void UpdateZillowAddressString(int sqlPartitionId, ZillowUrl zillowAddress)
        {
            //            const string updateSqlTemplate = @"UPDATE   MasterPropertyRecord.dbo.ZillowUrls
            //                                               SET   address = @address
            //                                               WHERE zillow_url = @zillow_url";

            const string updateSqlTemplate = @"INSERT INTO MasterPropertyRecord.dbo.zzz_aw_temp
                                                  (
                                                        [zillow_url], [address]
                                                  )
                                                  VALUES
                                                  (
                                                        @zillow_url, @address
                                                  )";


            try
            {
                string connectionString = _mprRedirect.GetConnectionStringByPartitionId(sqlPartitionId, "MasterPropertyRecord");
                using (var dbConnection = new SqlConnection(connectionString))
                {
                    dbConnection.Open();
                    SqlCommand cmd = new SqlCommand(updateSqlTemplate, dbConnection);
                    cmd.Parameters.AddWithValue("zillow_url", zillowAddress.zillow_url);
                    cmd.Parameters.AddWithValue("address", zillowAddress.addressString);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public IEnumerable<ZillowUrl> GetTableSampleFromZillowUrls(int sqlPartitionId, int sampleSize)
        {
            string sqlTemplate = @" SELECT [id], [zillow_url], [address]
                                    FROM MasterPropertyRecord.dbo.ZillowUrls
                                    TABLESAMPLE ({0} ROWS)
                                    ORDER BY NEWID()";

            string sql = string.Format(sqlTemplate, sampleSize);
            string connectionString = _mprRedirect.GetConnectionStringByPartitionId(sqlPartitionId, "MasterPropertyRecord");
            using (var dbConnection = new SqlConnection(connectionString))
            {
                dbConnection.Open();
                var zillowUrls = dbConnection.Query<ZillowUrl>(sql, commandTimeout: 9800);
                foreach (var zillowUrl in zillowUrls)
                {
                    yield return zillowUrl;
                }
            }
        }

        public void SaveAddressForZillowUrl(int sqlPartitionId, int zillowUrlId, string zillowAddress)
        {
            const string updateSqlTemplate = @"UPDATE  MasterPropertyRecord.dbo.ZillowUrls
                                                       SET   address = @address
                                                       WHERE id = @id";

            try
            {
                string connectionString = _mprRedirect.GetConnectionStringByPartitionId(sqlPartitionId, "MasterPropertyRecord");
                using (var dbConnection = new SqlConnection(connectionString))
                {
                    dbConnection.Open();
                    SqlCommand cmd = new SqlCommand(updateSqlTemplate, dbConnection);
                    cmd.Parameters.AddWithValue("address", zillowAddress);
                    cmd.Parameters.AddWithValue("id", zillowUrlId);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }


        public void SaveAddressParserResponseForZillowAddress(int sqlPartitionId, int zillowUrlId, AddressParserVerifiedAddress parserResponse, string flagColName, bool flagValue)
        {
            const string updateSqlTemplate = @"BEGIN TRAN
                                               UPDATE   MasterPropertyRecord.dbo.zzz_analyze_zillow_addresses
                                               SET 
	                                                    [ap_address_line]		    = @ap_address_line,
	                                                    [ap_street_number]		    = @ap_street_number,
	                                                    [ap_street_direction]	    = @ap_street_direction,
	                                                    [ap_street_name]		    = @ap_street_name,
	                                                    [ap_street_suffix]		    = @ap_street_suffix,
	                                                    [ap_street_post_direction]  = @ap_street_post_direction,
	                                                    [ap_unit_value]		        = @ap_unit_value,
	                                                    [ap_zip]				    = @ap_zip, 
	                                                    [ap_city]				    = @ap_city,
	                                                    [ap_county]			        = @ap_county,
	                                                    [ap_state_code]		        = @ap_state_code,
                                                        [ap_verification_code]		= @ap_verification_code,
                                                        [qas_response_code]			= @qas_response_code,
                                                        [{0}]                       = @flagValue

                                               WHERE zillow_url_id = @zillow_url_id

                                               IF @@ROWCOUNT = 0
                                               BEGIN
                                                  INSERT INTO MasterPropertyRecord.dbo.zzz_analyze_zillow_addresses
                                                  (
                                                        [zillow_url_id],

	                                                    [ap_address_line],
	                                                    [ap_street_number],
	                                                    [ap_street_direction],
	                                                    [ap_street_name],
	                                                    [ap_street_suffix],
	                                                    [ap_street_post_direction],
	                                                    [ap_unit_value],
	                                                    [ap_zip],
	                                                    [ap_city],
	                                                    [ap_county],
	                                                    [ap_state_code],
                                                        [ap_verification_code],
                                                        [qas_response_code],
                                                        [{0}]
                                                  )
                                                  VALUES
                                                  (
                                                        @zillow_url_id,

	                                                    @ap_address_line,
	                                                    @ap_street_number,
	                                                    @ap_street_direction,
	                                                    @ap_street_name,
	                                                    @ap_street_suffix,
	                                                    @ap_street_post_direction,
	                                                    @ap_unit_value,
	                                                    @ap_zip,
	                                                    @ap_city,
	                                                    @ap_county,
	                                                    @ap_state_code,
                                                        @ap_verification_code,
                                                        @qas_response_code,
                                                        @flagValue
                                                   )
                                               END
                                            COMMIT TRAN";

            try
            {
                string sqlQuery = string.Format(updateSqlTemplate, flagColName);
                string connectionString = _mprRedirect.GetConnectionStringByPartitionId(sqlPartitionId, "MasterPropertyRecord");
                using (var dbConnection = new SqlConnection(connectionString))
                {
                    dbConnection.Open();
                    SqlCommand cmd = new SqlCommand(sqlQuery, dbConnection);
                    cmd.Parameters.AddWithValue("zillow_url_id", zillowUrlId);
                    cmd.Parameters.AddWithValue("flagValue", flagValue);

                    if (null != parserResponse)
                    {
                        cmd.Parameters.AddWithValue("ap_address_line", ((object)parserResponse.address.address_line) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_number", ((object)parserResponse.address.street_no) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_direction", ((object)parserResponse.address.street_direction) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_name", ((object)parserResponse.address.street) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_suffix", ((object)parserResponse.address.street_suffix) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_post_direction", ((object)parserResponse.address.street_post_direction) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_unit_value", ((object)parserResponse.address.unit) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_zip", ((object)parserResponse.address.zip) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_city", ((object)parserResponse.address.city) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_county", ((object)parserResponse.address.county) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_state_code", ((object)parserResponse.address.state) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_verification_code", ((object)parserResponse.verification_code) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("qas_response_code", ((object)parserResponse.qas_response_code) ?? DBNull.Value);
                    }
                    else
                    {
                        cmd.Parameters.AddWithValue("ap_address_line", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_number", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_direction", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_name", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_suffix", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_street_post_direction", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_unit_value", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_zip", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_city", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_county", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_state_code", DBNull.Value);
                        cmd.Parameters.AddWithValue("ap_verification_code", DBNull.Value);
                        cmd.Parameters.AddWithValue("qas_response_code", DBNull.Value);
                    }

                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sqlPartitionId"></param>
        /// <returns></returns>
        public IEnumerable<ZillowUrl> GetCurrentZillowAddresses(int sqlPartitionId)
        {
            const string sql = @"SELECT --TOP 5
                                    z.id as id, 
		                                    z.zillow_url as zillow_url,
		                                    z.address as addressString,
		                                    a.ap_address_line as address_line,
		                                    a.ap_street_number as street_number,
		                                    a.ap_street_direction as street_direction,
		                                    a.ap_street_name as street_name,
		                                    a.ap_street_suffix as street_suffix,
		                                    a.ap_street_post_direction as street_post_direction,
		                                    a.ap_unit_value as unit_number,
		                                    a.ap_zip as zip,
		                                    a.ap_city as city,
		                                    a.ap_county as county,
		                                    a.ap_state_code as state_code
                                    FROM [MasterPropertyRecord].[dbo].[zzz_analyze_zillow_addresses] a
                                    JOIN [MasterPropertyRecord].[dbo].[ZillowUrls] z on a.zillow_url_id = z.id
                                    WHERE a.sample_10k = 1 OR a.sample_100k = 1";


            string connectionString = _mprRedirect.GetConnectionStringByPartitionId(sqlPartitionId, "MasterPropertyRecord");
            using (var dbConnection = new SqlConnection(connectionString))
            {
                dbConnection.Open();
                var listings = dbConnection.Query<ZillowUrl>(sql, commandTimeout: 9800);
                foreach (var listing in listings)
                {
                    yield return listing;
                }
            }
        }


        public void SaveMprListForZillowAddress(int sqlPartitionId, int zillow_url_id, ZillowToMoveMatchType matchType, string[] matchingMprs)
        {
            const string updateSqlTemplate = @"UPDATE  [MasterPropertyRecord].[dbo].[zzz_analyze_zillow_addresses]
                                                       SET  match_type = @match_type,
                                                            num_matching_mprs = @num_matching_mprs,
                                                            matching_mprs_list = @matching_mprs_list
                                                       WHERE zillow_url_id = @zillow_url_id";

            try
            {
                string connectionString = _mprRedirect.GetConnectionStringByPartitionId(sqlPartitionId, "MasterPropertyRecord");
                using (var dbConnection = new SqlConnection(connectionString))
                {
                    dbConnection.Open();
                    SqlCommand cmd = new SqlCommand(updateSqlTemplate, dbConnection);
                    cmd.Parameters.AddWithValue("match_type", Enum.GetName(typeof(ZillowToMoveMatchType), matchType));
                    cmd.Parameters.AddWithValue("num_matching_mprs", matchingMprs.Length);
                    cmd.Parameters.AddWithValue("matching_mprs_list", string.Join(",", matchingMprs));
                    cmd.Parameters.AddWithValue("zillow_url_id", zillow_url_id);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }


        public void UpdateLdaRentalAndTaxRecordFlags(int p1, string p2, bool p3, bool p4)
        {
            throw new NotImplementedException();
        }
    }
}
