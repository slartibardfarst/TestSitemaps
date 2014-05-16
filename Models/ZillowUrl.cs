using AddressCommon.DataStructures;


namespace TestSitemaps.Models
{
    public enum ZillowToMoveMatchType
    {
        invalid,
        exact,
        primaryOnly,
        noZip
    }

    public class ZillowUrl
    {
        public int id { get; set; }
        public string zillow_url { get; set; }
        public string addressString { get; set; }
        public Address Address { get { return ConstructAddress(); } }

        public string address_line;
        public string street_number;
        public string street_direction;
        public string street_name;
        public string street_suffix;
        public string street_post_direction;
        public string unit_number;
        public string zip;
        public string city;
        public string county;
        public string state_code;

        private Address ConstructAddress()
        {
            return new Address
            {
                address_line = address_line,
                street_no = street_number,
                street_direction = street_direction,
                street = street_name,
                street_suffix = street_suffix,
                street_post_direction = street_post_direction,
                unit = unit_number,
                zip = zip,
                city = city,
                county = county,
                state = state_code,
            };
        }
    }
}
