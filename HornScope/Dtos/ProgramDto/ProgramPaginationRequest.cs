using HornScope.Dtos.CommonDto;

namespace HornScope.Dtos.ProgramDto
{
    public class ProgramPaginationRequest: PaginationRequest
    {
        public int? ClimateProgramID { get; set; }
    }
}
