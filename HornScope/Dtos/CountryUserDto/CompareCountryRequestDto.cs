using HornScope.Dtos.CommonDto;

namespace HornScope.Dtos.CountryUserDto
{
    public class CompareCountryRequestDto : PaginationRequest
    {
        public List<int> Countries { get; set; }
        public List<int> Kpis { get; set; } = new();
        public DateTime UpdatedAt { get; set; } = new DateTime(DateTime.Now.Year, 1, 1);
    }

}
