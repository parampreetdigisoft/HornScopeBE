using HornScope.Dtos.CommonDto;

namespace HornScope.Dtos.ClientDto
{
    public class CompareProgramsRequestDto : PaginationRequest
    {
        public List<int> Programs { get; set; }
        public List<int> Kpis { get; set; } = new();
        public DateTime UpdatedAt { get; set; } = new DateTime(DateTime.Now.Year, 1, 1);
    }

}
