namespace HornScope.Dtos.ProgramDto
{
    public class AddUpdateProgramDto
    {
        public int ClimateProgramID { get; set; }
        public string ProgramName { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
        public IFormFile? ImageFile { get; set; }
        public string? ImageUrl { get; set; }     
        public DateTime? StartAt { get; set; }     
        public DateTime? EndAt { get; set; }     
        public string? Status { get; set; }     
        public int Year { get; set; }     
        public bool isActive { get; set; }
        public List<int>? PeerProgramIDs { get; set; }

    }
    public class BulkAddProgramDto
    {
        public List<AddUpdateProgramDto> Programs { get; set; }
    }
}
