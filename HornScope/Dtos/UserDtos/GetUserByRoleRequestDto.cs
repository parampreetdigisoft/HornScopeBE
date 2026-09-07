using HornScope.Dtos.CommonDto;
using HornScope.Models;

namespace HornScope.Dtos.UserDtos
{
    public class GetUserByRoleRequestDto : PaginationRequest
    {
        public UserRole? GetUserRole { get; set; }
        public int UserID { get; set; }
    }
}
