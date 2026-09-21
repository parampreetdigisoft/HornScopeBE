namespace API.Views.EmailModels
{
  public class UserEmailConfirmationRequestDto
  {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string EmailConfirmationUrl { get; set; } = string.Empty;
        public string ApiUrl { get; set; } = string.Empty;
    }
}
