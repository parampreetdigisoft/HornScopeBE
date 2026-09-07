using HornScope.Dtos.ProgramDto;

namespace HornScope.Dtos.UserDtos
{
    public class GetUserByRoleResponse : PublicUserResponse
    {
        public List<AddUpdateProgramDto> ClimatePrograms { get; set; } = new();
    }
}
