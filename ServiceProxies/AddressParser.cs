using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AddressCommon.DataStructures;

namespace TestSitemaps.ServiceProxies
{
    public abstract class AddressParser
    {
        private static readonly IList<string> StatesThatNeedCountryCode = new[] { "DE" };

        public static string PrepareAddressForParser(IAddress address)
        {

            var addressToParse = AddressLinePopulator.GetFormatedFullAddress(address);

            return addressToParse + (StatesThatNeedCountryCode.Contains(address.state) ? " US" : String.Empty);
        }

        public abstract QasVerifiedAddress ParseAddress(IAddress address);
    }

    /// <summary>
    /// Wrapper class for exceptions throw by an Address Parser
    /// </summary>
    public class AddressParserException : Exception
    {
        public AddressParserException(Exception ex)
            : base("AddressParserException", ex)
        { }

        public AddressParserException(string message, Exception ex)
            : base(message, ex)
        { }

    }
}
