namespace HornScope.Models
{
    public class StaffProgramMapping
    {
        public int StaffProgramMappingID { get; set; }
        public int UserID { get; set; }
        public UserRole Role { get; set; }
        public int ClimateProgramID { get; set; }
        public int AssignedByUserId { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
