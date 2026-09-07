namespace HornScope.Dtos.PublicDto
{
    public class PillarDmiResultDto
    {
        //public int ClimateProgramID { get; set; }
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public int DisplayOrder { get; set; }

        public decimal PEMDM_t { get; set; }
        public decimal Angle { get; set; }
    }
}
