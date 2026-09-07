using HornScope.Dtos.CommonDto;

namespace HornScope.Dtos.CountryDto
{
    public class CountryPaginationRequest: PaginationRequest
    {
        public int? CountryID { get; set; }
    }
}
