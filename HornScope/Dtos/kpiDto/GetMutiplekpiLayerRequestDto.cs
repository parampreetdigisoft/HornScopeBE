namespace HornScope.Dtos.kpiDto
{
    public class GetMutiplekpiLayerRequestDto
    {
        public int LayerID { get; set; }
        public List<int> ClimateProgramIDs { get; set; }
    }
}
