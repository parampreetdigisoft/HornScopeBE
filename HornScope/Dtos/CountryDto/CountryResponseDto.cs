using HornScope.Models;

namespace HornScope.Dtos.CountryDto
{
    public class CountryResponseDto : Country
    {
        public string? AssignedBy { get; set; }
        public decimal? Score { get; set; }// highest score have top rank
        public decimal? AiScore { get; set; }
    }
    public class UserCountryMappingResponseDto : CountryResponseDto
    {
        public int UserCountryMappingID { get; set; }
        public AssessmentPhase? AssessmentPhase { get; set; } = Models.AssessmentPhase.InProgress;
        public double? Distance { get; set; }
        public List<int>? PeerCountryIDs { get; set; }
    }
}
