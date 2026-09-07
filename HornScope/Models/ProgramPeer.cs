namespace AssessmentPlatform.Models
{
    public class ProgramPeer
    {
        public int ProgramPeerID { get; set; }
        public int ClimateProgramID { get; set; }
        public int PeerProgramID { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
