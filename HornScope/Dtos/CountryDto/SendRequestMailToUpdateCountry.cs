using HornScope.Models;

namespace HornScope.Dtos.CountryDto
{
    public class SendRequestMailToUpdateCountry
    {
        public int UserID { get; set; }
        public int MailToUserID { get; set; }
        public int UserCountryMappingID { get; set; }
    }
}
