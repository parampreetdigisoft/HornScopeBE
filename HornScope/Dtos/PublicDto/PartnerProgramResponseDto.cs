

namespace HornScope.Dtos.PublicDto
{
    public class PartnerProgramResponseDto : PartnerProgramHistoryResponseDtoBase
    {
        public int ClimateProgramID { get; set; }
        public string ProgramName { get; set; }
        public string? Location { get; set; }
        public int? Year { get; set; }
        public string? Image { get; set; }
    }

    public class PartnerProgramHistoryResponseDtoBase
    {
        public decimal Score { get; set; }
        public decimal HighScore { get; set; }
        public decimal LowerScore { get; set; }
        public decimal Progress { get; set; }
        public decimal AiScore { get; set; }

    }
}
