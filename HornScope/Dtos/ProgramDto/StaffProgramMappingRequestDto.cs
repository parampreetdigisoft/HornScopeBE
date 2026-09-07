namespace HornScope.Dtos.ProgramDto
{
    public class StaffProgramMappingRequestDto
    {
        public int UserId { get; set; }
        public int ClimateProgramID { get; set; }
        public int AssignedByUserId { get; set; }
    }
    public class StaffProgramUnMappingRequestDto
    {
        public int UserId { get; set; }
        public int AssignedByUserId { get; set; }
    }
}
