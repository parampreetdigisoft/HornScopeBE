using HornScope.Dtos.CommonDto;
using HornScope.Models;

namespace HornScope.Dtos.kpiDto
{
    public class GetAnalyticalLayerRequestDto : PaginationRequest
    {
        public int? CountryID { get; set; }
        public int? LayerID { get; set; }
        public int Year { get; set; } = DateTime.Now.Year;
    }
}
