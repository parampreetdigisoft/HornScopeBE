namespace HornScope.Models
{
    public class ClientProgramMapping
    {
        public int ClientProgramMappingID { get; set; }
        public int UserID { get; set; }
        public int ClimateProgramID { get; set; }
        public ClimateProgram? Program { get; set; }
        public User? User { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
    }
}
