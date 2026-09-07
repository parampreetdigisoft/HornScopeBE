namespace HornScope.Dtos.PublicDto
{
    public class PartnerProgramFilterResponse
    {
        public List<string> Programs { get; set; }
        public List<string> Regions { get; set; }
        public List<PartnerProgramDto> PartnerPrograms { get; set; }
    }

    public class PartnerProgramDto
    {
        public int ClimateProgramID { get; set; }
        public string ProgramName { get; set; }
    }
}
