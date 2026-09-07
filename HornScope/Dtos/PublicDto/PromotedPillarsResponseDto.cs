namespace HornScope.Dtos.PublicDto
{
    public class PromotedPillarsResponseDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public string ImagePath { get; set; }
        public int DisplayOrder { get; set; }
        public List<PromotedProgramResponseDto> Programs { get; set; }
    }

    public class PromotedProgramResponseDto
    {
        public int ClimateProgramID { get; set; }        
        public string ProgramName { get; set; }
        public string Location { get; set; }
        public string? Image { get; set; }
        public decimal? ScoreProgress { get; set; }
        public string Description { get; set; }
    }
}
