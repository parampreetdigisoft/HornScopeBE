using AssessmentPlatform.Models;

namespace HornScope.Models
{
    public class ClimateProgram
    {
        public int ClimateProgramID { get; set; }
        public string ProgramName { get; set; }       
        public int Year { get; set; }      
        public string? Description { get; set; }
        public bool IsActive { get; set; }  = true;
        public DateTime? StartAt { get; set; } = DateTime.UtcNow;
        public DateTime? EndAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;        
        public string? Image { get; set; }
        public string? Location { get; set; }
        public string? Status { get; set; }
        public ICollection<ProgramPeer>? ProgramPeers { get; set; }
    }
}
