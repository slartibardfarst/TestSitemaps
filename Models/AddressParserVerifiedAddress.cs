using TestSitemaps.ServiceProxies;

namespace TestSitemaps.Models
{
    public class AddressParserVerifiedAddress : QasVerifiedAddress
    {
        public string qas_response_code
        {
            set { original_quality_code = value; }
            get { return original_quality_code; }
        }
    }
}
