namespace HornScope.Dtos.ProgramDto
{
    public class GetProgramsProgressAdminDto
    {
        public int ClimateProgramID { get; set; }
        public string ProgramName { get; set; }
        public string Location { get; set; }        
        public int PillarID { get; set; }
        public string PillarName { get; set; } 
        public int DisplayOrder { get; set; }
        public int TotalScore { get; set; }
        public int TotalAns { get; set; }
        public decimal PillarProgress { get; set; }
        public decimal AIPillarProgress { get; set; }
        public decimal AIProgramProgress { get; set; }
    }
}
