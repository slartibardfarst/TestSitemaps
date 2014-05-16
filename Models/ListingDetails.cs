using AddressCommon.DataStructures;

namespace TestSitemaps.Models
{
    public class ListingDetails
    {
        public Address Address { get { return ConstructAddress(); } }
        public int ListingId { get { return listingId; } }

        public bool is_rental;
        public bool has_tax_record;

        public int listingId;
        public string country;
        public string state_code;
        public string city;
        public string zip;
        public string address_line;
        public string house_number;
        public string street_direction;
        public string street_name;
        public string street_suffix;
        public string street_post_direction;
        public string unit_number;
        public double latitude;
        public double longitude;
        public string county;
        public string zip_plus_four;

        private Address ConstructAddress()
        {
            return new Address
            {
                address_line = address_line,
                city = city,
                country = country,
                county = county,
                state = state_code,
                street = street_name,
                street_direction = street_direction,
                street_no = house_number,
                street_post_direction = street_post_direction,
                street_suffix = street_suffix,
                unit = unit_number,
                zip = zip,
                zip_plus_four = zip_plus_four
            };
        }
    }
}
