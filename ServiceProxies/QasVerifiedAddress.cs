using AddressCommon.DataStructures;

namespace TestSitemaps.ServiceProxies
{
    /// <summary>
    /// This class represents an address that have been verified by the Experian QAS address verification service.
    /// The QAS verification code is stored in the  base class's verification_code field
    /// 
    /// This class stores the QAS verification code (R9 or R5) in the 'original_quality_code' field
    /// The base class's 'verification_code' field stores a value that comes from QAS and which is ignored

    /// </summary>
    public class QasVerifiedAddress : ValidatedAddress
    {
        public int validation_source;
        public string original_quality_code;
        public string extended_info;
        private ValidatedAddressWithVendorInfo validatedAddressWithVendorInfo;

        public virtual string GetQasVerificationCode()
        {
            return original_quality_code;
        }

        public QasVerifiedAddress()
        {
            //this class is specific to QAS and the id for QAS is 1
            validation_source = 1;
        }

        public QasVerifiedAddress(ValidatedAddressWithVendorInfo validatedAddressWithVendorInfo)
            : this()
        {
            if (null != validatedAddressWithVendorInfo)
            {
                this.address = validatedAddressWithVendorInfo.address;
                this.verification_code = validatedAddressWithVendorInfo.verification_code;
                this.validatedAddressWithVendorInfo = validatedAddressWithVendorInfo;
            }
        }
    }
}
