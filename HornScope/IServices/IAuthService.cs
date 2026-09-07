using HornScope.Common.Models;
using HornScope.Dtos.ProgramDto;
using HornScope.Dtos.EmailExistDto;
using HornScope.Dtos.UserDtos;
using HornScope.Models;

namespace HornScope.IServices
{
    public interface IAuthService
    {
        User Register(string fullName, string email, string phn, string password, UserRole role, Enums.TieredAccessPlan? tier = Enums.TieredAccessPlan.Pending);
        User GetByEmail(string email);
        Task<User?> GetByEmailAsync(string email);
        bool VerifyPassword(string password, string hash);
        Task<ResultResponseDto<UserResponseDto>> Login(string email, string password);
        Task<ResultResponseDto<object>> ForgotPassword(string email);
        Task<ResultResponseDto<object>> ChangePassword(string passwordToken, string password);
        Task<ResultResponseDto<object>> InviteUser(InviteUserDto inviteUser);
        Task<ResultResponseDto<object>> InviteBulkUser(InviteBulkUserDto inviteUser);
        Task<ResultResponseDto<object>> CheckEmailExist(EmailExistRequestDto request);
        Task<ResultResponseDto<object>> UpdateInviteUser(UpdateInviteUserDto inviteUser);
        Task<ResultResponseDto<object>> DeleteUser(int userId);
        Task<ResultResponseDto<UserResponseDto>> RefreshToken(int userId);
        Task<ResultResponseDto<string>> SendMailForEditAssessment(SendRequestMailToUpdateProgram request);
        Task<ResultResponseDto<UserResponseDto>> ClientSignUp(ClientSignUpDto request);
        Task<ResultResponseDto<object>> ConfirmMail(string passwordToken);
        Task<ResultResponseDto<object>> ContactUs(ContactUsRequestDto passwordToken);
        Task<ResultResponseDto<UserResponseDto>> TwofaVerification(string email, int otp);
        Task<ResultResponseDto<string>> ReSendLoginOtp(string email);
        Task<ResultResponseDto<UpdateUserResponseDto>> UpdateUser(UpdateUserDto requestDto);
    }
}
