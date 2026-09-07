namespace HornScope.Dtos.UserDtos
{
    public class GetAssignUserDto : UserIdDto
    {
        public int? SearchedUserID { get; set; }
        public int? ClimateProgramID { get; set; }
    }
    public class UserIdDto
    {
        public int UserID { get; set; }
    }
}
