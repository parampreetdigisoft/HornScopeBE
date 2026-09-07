namespace HornScope.Models
{
    public class DashboardModeKPIMapping
    {
        public int DashboardModeKPIMappingID { get; set; }
        public int DashboardModeID { get; set; }
        public int LayerID { get; set; }
        public string? Description { get; set; }
        public int PriorityLevel { get; set; } = 1;
        public int? DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;

        public DashboardMode DashboardMode { get; set; } = default!;
    }
}
