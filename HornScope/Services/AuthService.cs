using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HornScope.Common.Interface;
using HornScope.Common.Models;
using HornScope.Common.Models.settings;
using HornScope.Data;
using HornScope.Dtos.ProgramDto;
using HornScope.Dtos.ClientDto;
using HornScope.Dtos.EmailExistDto;
using HornScope.Dtos.UserDtos;
using HornScope.Enums;
using HornScope.IServices;
using HornScope.Models;
using HornScope.Views.EmailModels;

namespace HornScope.Services
{
    public class AuthService : IAuthService
    {
        #region  constructor
        private readonly ApplicationDbContext _context;
        private readonly AppSettings _appSettings;
        private readonly JwtSetting _jwtSetting;
        private readonly IEmailService _emailService;
        private readonly IAppLogger _appLogger;
        private readonly IWebHostEnvironment _env;
        public AuthService(ApplicationDbContext context, IOptions<AppSettings> appSettings, IEmailService emailService, IOptions<JwtSetting> jwtSetting, IAppLogger appLogger, IWebHostEnvironment env)
        {
            _context = context;
            _appSettings = appSettings.Value;
            _emailService = emailService;
            _jwtSetting = jwtSetting.Value;
            _appLogger = appLogger;
            _env = env;
        }
        #endregion

        #region IAuthService implemention

        public User Register(string fullName, string email, string phn, string password, UserRole role, Enums.TieredAccessPlan? tier = Enums.TieredAccessPlan.Pending)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(password);
            var user = new User
            {
                FullName = fullName,
                Email = email,
                Phone = phn,
                PasswordHash = hash,
                Role = role,
                IsEmailConfirmed = false,
                Tier = tier ?? TieredAccessPlan.Pending
            };
            _context.Users.Add(user);
            _context.SaveChanges();
            return user;
        }

        public User? GetByEmail(string email)
        {
            return _context.Users.FirstOrDefault(u => u.Email == email && !u.IsDeleted);
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            try
            {
                return await _context.Users.Where(u => u.Email == email && !u.IsDeleted).AsQueryable().FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("GetByEmailAysync", ex);
            }
            return null;
        }

        public bool VerifyPassword(string password, string hash)
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }

        public async Task<ResultResponseDto<object>> ForgotPassword(string email)
        {
            try
            {
                var user = GetByEmail(email);
                if (user == null)
                {
                    return ResultResponseDto<object>.Failure(new string[] { "User not exist." });
                }
                else
                {
                    var hash = BCrypt.Net.BCrypt.HashPassword(email);
                    var passwordToken = hash;
                    var token = passwordToken.Replace("+", " ");

                    var url = user.Role != UserRole.ProgramUser ? _appSettings.ApplicationUrl : _appSettings.PublicApplicationUrl;
                    string passwordResetLink = url + "/auth/reset-password?PasswordToken=" + token;

                    var sub = "Password Update Link – Veridian Climate Pulse Platform";
                    var model = new EmailInvitationSendRequestDto
                    {
                        ResetPasswordUrl = passwordResetLink,
                        Title = sub,
                        ApiUrl = _appSettings.ApiUrl,
                        ApplicationUrl = url,
                        MsgText= "A request was made to update the password for your Veridian Climate Pulse (VCP) account. To proceed, please use the secure link below:",
                        IsShowBtnText=true,
                        IsLoginBtn=false,
                        BtnText= "Update Password",
                        Mail=_appSettings.AdminMail,
                        DescriptionAboutBtnText = $"If you did not make this request, you may ignore this message and your account will remain unchanged."
                    };
                    var isMailSent = await _emailService.SendEmailAsync(email, sub, "~/Views/EmailTemplates/ChangePassword.cshtml", model);
                    if (isMailSent)
                    {
                        user.ResetToken = token;
                        user.ResetTokenDate = DateTime.Now;
                        _context.Users.Update(user);
                        await _context.SaveChangesAsync();
                    }
                    return ResultResponseDto<object>.Success(new { }, new string[] { "Please check your email for change password." });

                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("ForgotPassword", ex);
                return ResultResponseDto<object>.Failure(new string[] { "There is an error please try later" });

            }
        }

        public async Task<ResultResponseDto<object>> ChangePassword(string passwordToken, string password)
        {
            try
            {
                var user = await _context.Users.Where(u => u.ResetToken == passwordToken).FirstOrDefaultAsync();

                if (user == null)
                {
                    return ResultResponseDto<object>.Failure(new string[] { "User not exist." });
                }
                if (_appSettings.LinkValidHours >= (DateTime.Now - user.ResetTokenDate).Hours)
                {
                    var hash = BCrypt.Net.BCrypt.HashPassword(password);
                    user.PasswordHash = hash;
                    user.IsEmailConfirmed = true;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();

                    return ResultResponseDto<object>.Success(new { }, new string[] { "Password updated successfully" });
                }
                else
                {
                    return ResultResponseDto<object>.Failure(new string[] { "Link has been expired." });
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error change password", ex);
                return ResultResponseDto<object>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<UserResponseDto>> Login(string email, string password)
        {
            try
            {
                var user = await GetByEmailAsync(email);
                if (user == null || !VerifyPassword(password, user.PasswordHash))
                {
                    return ResultResponseDto<UserResponseDto>.Failure(new string[] { "Invalid request data." });
                }
                if (user.IsEmailConfirmed && !user.IsDeleted && user.Is2FAEnabled)
                {
                    var r = await SendTwoFactorOTPAsync(user);
                    if (r.Succeeded)
                    {
                        var sendOpt = new UserResponseDto {};
                        return ResultResponseDto<UserResponseDto>.Success(sendOpt,
                          new string[] { "We've sent a one-time verification code (OTP) to your registered email address. Please check your inbox and enter the OTP to continue." });
                    }
                    return ResultResponseDto<UserResponseDto>.Failure(new string[] { "Failed to send OTP Please try again." });
                }
                else
                {
                    var response = GetAuthorizedUserDetails(user);
                    return response;
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error login", ex);
                return ResultResponseDto<UserResponseDto>.Failure(new string[] { ex.Message });
            }
        }
        public ResultResponseDto<UserResponseDto> GetAuthorizedUserDetails(User user)
        {
            if (user == null)
            {
                return ResultResponseDto<UserResponseDto>.Failure(new string[] { "Invalid request" });
            }
            if (!user.IsEmailConfirmed || user.IsDeleted)
            {
                string message = string.Empty;

                if (user.Role != UserRole.ProgramUser)
                {
                    message = $"Your mail is not confirmed or de-activated by super {(user.Role == UserRole.Analyst ? "Admin" : "Analyst")}";
                }
                else
                {
                    message = "Your email is not verified. Please check your inbox and click the verification link. If the link has expired, you can reset your password to verify your account.";
                }

                return ResultResponseDto<UserResponseDto>.Failure(new string[] { message });
            }
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("Tier", user.Tier?.ToString() ?? ""),
                new Claim("UserId", user!.UserID.ToString())
            };
            var tokenExpired = DateTime.UtcNow.AddHours(1);
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSetting.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var securityToken = new JwtSecurityToken(
                issuer: _jwtSetting.Issuer,
                audience: _jwtSetting.Audience,
                claims: claims,
                expires: tokenExpired,
                signingCredentials: creds
            );
            var token = new JwtSecurityTokenHandler().WriteToken(securityToken);

            var response = new UserResponseDto
            {
                UserID = user.UserID,
                FullName = user.FullName,
                Phone = user.Phone,
                Email = user.Email,
                IsDeleted = user.IsDeleted,
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt,
                CreatedBy = user.CreatedBy,
                IsEmailConfirmed = user.IsEmailConfirmed,
                TokenExpirationDate = tokenExpired,
                ProfileImagePath = user.ProfileImagePath,
                Token = token,
                Tier = user.Tier
            };
            return ResultResponseDto<UserResponseDto>.Success(response, new string[] { "You have successfully logged in." });
        }

        public async Task<ResultResponseDto<object>> InviteUser(InviteUserDto inviteUser)
        {
            try
            {
                if (inviteUser == null || string.IsNullOrEmpty(inviteUser.Email) || string.IsNullOrEmpty(inviteUser.FullName))
                {
                    return ResultResponseDto<object>.Failure(new string[] { "Invalid request data." });
                }
                bool isExistingUser = true;
                var user = GetByEmail(inviteUser.Email);

                if (user == null || !user.IsDeleted)
                {
                    user = Register(inviteUser.FullName, inviteUser.Email, inviteUser.Phone, inviteUser.Password, inviteUser.Role, inviteUser.Tier);
                    if (user == null)
                    {
                        return ResultResponseDto<object>.Failure(new string[] { "Failed to register user." });
                    }
                    user.CreatedBy = inviteUser.InvitedUserID;
                    isExistingUser = false;
                }
                if (user.Role != inviteUser.Role)
                {
                    return ResultResponseDto<object>.Failure(new string[] { "User already have different role" });
                }

                var hash = BCrypt.Net.BCrypt.HashPassword(inviteUser.Email);
                var passwordToken = hash;
                var token = passwordToken.Replace("+", " ");
                string roleName = inviteUser.Role.ToString();

                if (inviteUser.Role == UserRole.ProgramUser)
                {
                    roleName = "Program User";
                }

                string sub = $"{roleName} Access Granted – Veridian Climate Pulse Platform";
                var url = _appSettings.ApplicationUrl;
                string passwordResetLink = url + "/auth/reset-password?PasswordToken=" + token;

                var programName = string.Join(", ",
                                         _context.ClimatePrograms
                                         .Where(c => inviteUser.ClimateProgramID.Contains(c.ClimateProgramID))
                                         .Select(c => c.ProgramName));
                var invitedUser = _context.Users.FirstOrDefault(x => x.UserID == inviteUser.InvitedUserID);

                var model = new EmailInvitationSendRequestDto
                {
                    ResetPasswordUrl = passwordResetLink,
                    ApiUrl = _appSettings.ApiUrl,
                    Title = sub,
                    ApplicationUrl = url,
                    Mail= _appSettings.AdminMail,
                    Name = "Dear" + " " +user.FullName
                };
                var viewNamePath = inviteUser.Role switch
                {
                    UserRole.Analyst => "~/Views/EmailTemplates/AnalystSendInvitation.cshtml",
                    UserRole.Evaluator => "~/Views/EmailTemplates/EvaluatorSendInvitation.cshtml",
                    UserRole.ProgramUser => "~/Views/EmailTemplates/ClientSendInvitation.cshtml",
                    _ => ""
                };

                var isMailSent = await _emailService.SendEmailAsync(inviteUser.Email, sub, viewNamePath, model);
                user.ResetToken = token;
                user.ResetTokenDate = DateTime.Now;
                user.IsDeleted = false;
                _context.Users.Update(user);
                if (inviteUser.Role != UserRole.ProgramUser)
                {
                    foreach (var id in inviteUser.ClimateProgramID)
                    {
                        var mapping = new StaffProgramMapping
                        {
                            UserID = user.UserID,
                            ClimateProgramID = id,
                            AssignedByUserId = inviteUser.InvitedUserID,
                            Role = user.Role
                        };
                        _context.StaffProgramMappings.Add(mapping);
                    }
                }

                await _context.SaveChangesAsync();
                if (inviteUser.Role == UserRole.ProgramUser)
                {
                    string tierName = inviteUser.Tier?.ToString();
                    var kpiPayload = new AddClientKpisProgramAndPillar
                    {
                        Programs = inviteUser.IsAllPrograms ? new List<int>() : (inviteUser.ClimateProgramID ?? new List<int>()),
                        Pillars = inviteUser.Pillars,
                        IsAllPrograms = inviteUser.IsAllPrograms
                    };
                    var response = await AddProgramUserKpisProgramAndPillar(kpiPayload, user.UserID, tierName);
                    if (!response.Succeeded)
                    {
                        return ResultResponseDto<object>.Failure(
                            response.Messages?.Length > 0
                                ? response.Messages
                                : new string[] { "There is an error please try later" });
                    }
                }
                if (isMailSent)
                {
                    string msg = string.Empty;

                    if (isExistingUser)
                    {
                        msg = "This user already exists. An invitation has been sent to confirm their email and access the assigned program.";
                    }
                    else
                    {
                        msg = "User added successfully. An invitation has been sent to access the assigned program.";
                    }
                    return ResultResponseDto<object>.Success(new { }, new string[] { msg });
                }
                return ResultResponseDto<object>.Failure(new string[] { "User created but invitation not send due to server error" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred", ex);
                return ResultResponseDto<object>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<object>> UpdateInviteUser(UpdateInviteUserDto inviteUser)
        {
            try
            {
                if (inviteUser == null || string.IsNullOrEmpty(inviteUser.Email) || string.IsNullOrEmpty(inviteUser.FullName))
                    return ResultResponseDto<object>.Failure(new[] { "Invalid request data." });

                var userList = await _context.Users
                    .Where(u => u.UserID == inviteUser.UserID || u.UserID == inviteUser.InvitedUserID)
                    .ToListAsync();

                var user = userList.FirstOrDefault(u => u.UserID == inviteUser.UserID);
                if (user == null)
                    return ResultResponseDto<object>.Failure(new[] { "User not found." });

                if (user.Role != inviteUser.Role)
                    return ResultResponseDto<object>.Failure(new[] { "User already have different role" });

                // Update basic user info
                user.FullName = inviteUser.FullName;
                user.Phone = inviteUser.Phone;
                user.CreatedBy = inviteUser.InvitedUserID;
                user.Email = inviteUser.Email;
                user.Tier = inviteUser.Tier;
                _context.Users.Update(user);

                // Determine added/deleted programs
                var (programsToAdd, programsToDelete) = await GetProgramMappingChangesAsync(
                    user.UserID,
                    inviteUser.InvitedUserID,
                    inviteUser.Role,
                    inviteUser.ClimateProgramID
                );

                // Handle program mappings based on role
                if (inviteUser.Role == UserRole.ProgramUser)
                {
                    if (inviteUser.Tier == TieredAccessPlan.Premium)
                    {
                        var allPillarIds = await _context.Pillars.Select(p => p.PillarID).ToListAsync();
                        inviteUser.Pillars = allPillarIds;

                        if (inviteUser.IsAllPrograms)
                        {
                            inviteUser.ClimateProgramID = await _context.ClimatePrograms
                                .Where(c => c.IsActive)
                                .Select(c => c.ClimateProgramID)
                                .ToListAsync();
                        }
                        else if (inviteUser.ClimateProgramID == null || inviteUser.ClimateProgramID.Count < 1)
                        {
                            return ResultResponseDto<object>.Failure(new[]
                            {
                                "Premium plan requires at least one program, or all programs."
                            });
                        }

                        // Recompute add/delete after Premium normalization
                        (programsToAdd, programsToDelete) = await GetProgramMappingChangesAsync(
                            user.UserID,
                            inviteUser.InvitedUserID,
                            inviteUser.Role,
                            inviteUser.ClimateProgramID
                        );
                    }

                    // Delete old programs
                    var existingPrograms = await _context.ClientProgramMappings
                        .Where(m => m.UserID == user.UserID)
                        .ToListAsync();
                    var programsToRemove = existingPrograms.Where(c => programsToDelete.Contains(c.ClimateProgramID)).ToList();
                    _context.ClientProgramMappings.RemoveRange(programsToRemove);

                    // Add new programs
                    var utcNow = DateTime.UtcNow;
                    var newPrograms = programsToAdd.Select(c => new ClientProgramMapping
                    {
                        UserID = user.UserID,
                        ClimateProgramID = c,
                        IsActive = true,
                        UpdatedAt = utcNow
                    });
                    await _context.ClientProgramMappings.AddRangeAsync(newPrograms);

                    // Update Pillars
                    if (inviteUser.Pillars != null)
                    {
                        var existingPillars = await _context.ClientPillarMappings
                            .Where(m => m.UserID == user.UserID)
                            .ToListAsync();
                        _context.ClientPillarMappings.RemoveRange(existingPillars);

                        var newPillars = inviteUser.Pillars.Select(p => new ClientPillarMapping
                        {
                            UserID = user.UserID,
                            PillarID = p,
                            IsActive = true,
                            UpdatedAt = utcNow
                        });
                        await _context.ClientPillarMappings.AddRangeAsync(newPillars);
                    }
                }
                else
                {
                    // Other roles use StaffProgramMappings
                    var existingMappings = _context.StaffProgramMappings
                        .Where(m => m.UserID == user.UserID && m.AssignedByUserId == inviteUser.InvitedUserID && !m.IsDeleted)
                        .ToList();

                    // Add missing
                    var addMappings = programsToAdd.Select(c => new StaffProgramMapping
                    {
                        UserID = user.UserID,
                        ClimateProgramID = c,
                        AssignedByUserId = inviteUser.InvitedUserID,
                        Role = user.Role
                    });
                    _context.StaffProgramMappings.AddRange(addMappings);

                    // Delete removed
                    var deleteMappings = existingMappings.Where(m => programsToDelete.Contains(m.ClimateProgramID)).ToList();
                    foreach (var m in deleteMappings)
                    {
                        m.IsDeleted = true;
                        _context.StaffProgramMappings.Update(m);
                    }
                }

                await _context.SaveChangesAsync();

                // Common email logic
                bool isMailSent = false;
                string msgText = "You are receiving this email because you haven't reset your password";
                string msg = "User updated successfully";

                var invitedUser = userList.FirstOrDefault(x => x.UserID == inviteUser.InvitedUserID);
                var mergedPrograms = (inviteUser.ClimateProgramID ?? new List<int>()).Concat(programsToAdd).ToList();
                var programDetails = await _context.ClimatePrograms
                    .Where(c => mergedPrograms.Contains(c.ClimateProgramID))
                    .ToListAsync();

                if (programsToAdd.Count > 0)
                {
                    isMailSent = true;
                    var addedNames = string.Join(", ", programDetails.Where(c => programsToAdd.Contains(c.ClimateProgramID)).Select(c => c.ProgramName));
                    msgText = $"You are receiving this email because {invitedUser?.FullName} recently requested program assignment ({addedNames}) for your VCP account.";
                }

                if (programsToDelete.Count > 0)
                {
                    var removedNames = string.Join(", ", programDetails.Where(c => programsToDelete.Contains(c.ClimateProgramID)).Select(c => c.ProgramName));
                    msgText = isMailSent
                        ? msgText + $" Additionally, you no longer have access to the programs ({removedNames}) for your VCP account."
                        : $"You are receiving this email because {invitedUser?.FullName} recently removed your access to the following programs ({removedNames}) for your VCP account.";
                    isMailSent = true;
                }

                if (!user.IsEmailConfirmed)
                {
                    var hash = BCrypt.Net.BCrypt.HashPassword(inviteUser.Email);
                    var token = hash.Replace("+", " ");
                    string roleName = inviteUser.Role == UserRole.ProgramUser ? "Program User" : inviteUser.Role.ToString();
                    string sub = $"{roleName} Access Granted – Veridian Climate Pulse Platform";
                    var url = user.Role != UserRole.ProgramUser ? _appSettings.ApplicationUrl : _appSettings.PublicApplicationUrl;
                    string passwordResetLink = url + "/auth/reset-password?PasswordToken=" + token;

                    var model = new EmailInvitationSendRequestDto
                    {
                        ResetPasswordUrl = passwordResetLink,
                        ApiUrl = _appSettings.ApiUrl,
                        ApplicationUrl = url,
                        Title = sub,
                        Mail = _appSettings.AdminMail,
                        Name = "Dear " + user.FullName
                    };

                    var viewNamePath = inviteUser.Role switch
                    {
                        UserRole.Analyst => "~/Views/EmailTemplates/AnalystSendInvitation.cshtml",
                        UserRole.Evaluator => "~/Views/EmailTemplates/EvaluatorSendInvitation.cshtml",
                        UserRole.ProgramUser => "~/Views/EmailTemplates/ClientSendInvitation.cshtml",
                        _ => ""
                    };

                    isMailSent = await _emailService.SendEmailAsync(inviteUser.Email, sub, viewNamePath, model);
                    user.ResetToken = token;
                    user.ResetTokenDate = DateTime.Now;
                    user.IsDeleted = false;

                    msg = $"User updated and invitation {(isMailSent ? "sent successfully" : "failed to send")}";
                    await _context.SaveChangesAsync();
                }

                return ResultResponseDto<object>.Success(new { }, new[] { msg });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in UpdateInviteUser", ex);
                return ResultResponseDto<object>.Failure(new[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<object>> DeleteUser(int userId)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(m => m.UserID == userId && !m.IsDeleted);

                if (user == null)
                {
                    return ResultResponseDto<object>.Failure(new string[] { "User not exist" });
                }

                // Soft delete user
                user.IsDeleted = true;
                _context.Users.Update(user);

                if (user.Role == UserRole.ProgramUser)
                {
                    var utcNow = DateTime.UtcNow;

                    // ?? Deactivate ClientProgramMappings
                    var clientMappings = await _context.ClientProgramMappings
                        .Where(x => x.UserID == userId && x.IsActive)
                        .ToListAsync();

                    foreach (var mapping in clientMappings)
                    {
                        mapping.IsActive = false;
                        mapping.UpdatedAt = utcNow;
                    }

                    _context.ClientProgramMappings.UpdateRange(clientMappings);

                    // ?? Deactivate ClientPillarMappings
                    var pillarMappings = await _context.ClientPillarMappings
                        .Where(x => x.UserID == userId && x.IsActive)
                        .ToListAsync();

                    foreach (var mapping in pillarMappings)
                    {
                        mapping.IsActive = false;
                        mapping.UpdatedAt = utcNow;
                    }

                    _context.ClientPillarMappings.UpdateRange(pillarMappings);
                }
                else
                {
                    // ?? Handle other roles (existing logic)

                    var userMappings = await _context.StaffProgramMappings
                        .Where(x => x.UserID == userId && !x.IsDeleted)
                        .ToListAsync();

                    foreach (var m in userMappings)
                    {
                        m.IsDeleted = true;
                        _context.StaffProgramMappings.Update(m);
                    }
                }

                await _context.SaveChangesAsync();

                return ResultResponseDto<object>.Success(new { }, new string[] { "User deleted successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in DeleteUser", ex);
                return ResultResponseDto<object>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<UserResponseDto>> RefreshToken(int userId)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(x => !x.IsDeleted && x.UserID == userId);
                if (user == null)
                {
                    return ResultResponseDto<UserResponseDto>.Failure(new string[] { "Invalid request data." });
                }
                var response = GetAuthorizedUserDetails(user);

                return await Task.FromResult(response);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure RefreshToken", ex);
                return ResultResponseDto<UserResponseDto>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<object>> CheckEmailExist(EmailExistRequestDto request)
        {
            try
            {
                var user = _context.Users.FirstOrDefault(u => u.Email == request.Email.Trim() && !u.IsDeleted);
                bool exists = user != null && user.UserID != request.UserID;

                if (exists)
                {
                    return ResultResponseDto<object>.Failure(
                        new[] { "Email Already Exists" },
                        isExist: true
                    );
                }

                return ResultResponseDto<object>.Success(
                    messages: new[] { "Email is Valid" }

                );
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in fetch emails", ex);
                return ResultResponseDto<object>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<object>> InviteBulkUser(InviteBulkUserDto inviteUserList)
        {
            try
            {
                if (inviteUserList?.users == null || !inviteUserList.users.Any())
                {
                    return ResultResponseDto<object>.Failure(new[] { "No users provided." });
                }

                // 1. Bulk fetch all users by email
                var emails = inviteUserList.users.Select(u => u.Email).ToList();
                var existingUsers = await _context.Users
                    .Where(u => emails.Contains(u.Email))
                    .ToDictionaryAsync(u => u.Email, u => u);

                // Collect new users & program mappings
                var newUsers = new List<User>();
                var newMappings = new List<StaffProgramMapping>();
                var emailTasks = new List<Task>();

                foreach (var inviteUser in inviteUserList.users)
                {
                    if (inviteUser == null || string.IsNullOrEmpty(inviteUser.Email) || string.IsNullOrEmpty(inviteUser.FullName))
                    {
                        return ResultResponseDto<object>.Failure(new[] { "Invalid request data." });
                    }

                    // 2. Try get existing user
                    existingUsers.TryGetValue(inviteUser.Email, out var user);

                    // 3. Register if not exists
                    if (user == null)
                    {
                        user = new User
                        {
                            FullName = inviteUser.FullName,
                            Email = inviteUser.Email,
                            Phone = inviteUser.Phone,
                            PasswordHash = BCrypt.Net.BCrypt.HashPassword(inviteUser.Password),
                            Role = inviteUser.Role,
                            CreatedBy = inviteUser.InvitedUserID,
                            IsDeleted = false,
                        };
                        _context.Users.Add(user);
                        existingUsers[inviteUser.Email] = user; // add to dictionary for later mapping
                        await _context.SaveChangesAsync();
                    }

                    if (user.Role != inviteUser.Role)
                    {
                        return ResultResponseDto<object>.Failure(new[] { $"User {inviteUser.Email} already has a different role." });
                    }

                    var existingProgramIds = _context.StaffProgramMappings
                        .Where(m => m.UserID == user.UserID && m.AssignedByUserId == inviteUser.InvitedUserID && !m.IsDeleted)
                        .Select(m => m.ClimateProgramID)
                        .ToList();

                    var programsToAdd = inviteUser.ClimateProgramID.Except(existingProgramIds).ToList();
                    foreach (var programId in programsToAdd)
                    {
                        newMappings.Add(new StaffProgramMapping
                        {
                            UserID = user.UserID,
                            ClimateProgramID = programId,
                            AssignedByUserId = inviteUser.InvitedUserID,
                            Role = user.Role
                        });
                    }

                    if (programsToAdd.Count() > 0)
                    {
                        // 5. Handle email invitation
                        var token = BCrypt.Net.BCrypt.HashPassword(inviteUser.Email).Replace("+", " ");
                        var url = user.Role != UserRole.ProgramUser ? _appSettings.ApplicationUrl : _appSettings.PublicApplicationUrl;

                        string resetLink = $"{url}/auth/reset-password?PasswordToken={token}";

                        var programName = string.Join(", ",
                         _context.ClimatePrograms
                         .Where(c => programsToAdd.Contains(c.ClimateProgramID))
                         .Select(c => c.ProgramName));
                        var invitedUser = _context.Users.FirstOrDefault(x => x.UserID == inviteUser.InvitedUserID);

                        string sub = $"{inviteUser.Role.ToString()} Access Granted – Veridian Climate Pulse Platform";
                        var model = new EmailInvitationSendRequestDto
                        {
                            ResetPasswordUrl = resetLink,
                            ApiUrl = _appSettings.ApiUrl,
                            ApplicationUrl = url,
                            Title = sub,
                            Mail = _appSettings.AdminMail
                        };
                        var viewNamePath = inviteUser.Role == UserRole.Analyst ? "~/Views/EmailTemplates/AnalystSendInvitation.cshtml" : "~/Views/EmailTemplates/EvaluatorSendInvitation.cshtml";

                        emailTasks.Add(_emailService.SendEmailAsync(
                            inviteUser.Email,
                            sub,
                            viewNamePath,
                            model
                        ));

                        user.ResetToken = token;
                        user.ResetTokenDate = DateTime.Now;
                        user.IsDeleted = false;
                    }

                }


                if (newMappings.Any()) await _context.StaffProgramMappings.AddRangeAsync(newMappings);
                await _context.SaveChangesAsync();

                // 8. Send all emails in parallel
                if (emailTasks.Any()) await Task.WhenAll(emailTasks);

                return ResultResponseDto<object>.Success(new { }, new[] { "Users will get invitation link to see assigned programs." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Invite Bulk User", ex);
                return ResultResponseDto<object>.Failure(new[] { ex.Message });
            }
        }

        public async Task<ResultResponseDto<string>> SendMailForEditAssessment(SendRequestMailToUpdateProgram request)
        {
            try
            {
                var users = _context.Users.Where(x => x.UserID == request.MailToUserID || x.UserID == request.UserID);

                var mailToUser = users.FirstOrDefault(x => x.UserID == request.MailToUserID);
                if (mailToUser == null)
                {
                    return ResultResponseDto<string>.Failure(new string[] { "User not exist." });
                }
                else
                {
                    var user = users.FirstOrDefault(x => x.UserID == request.UserID);
                    var assessment = await _context.Assessments.Include(x => x.StaffProgramMapping).FirstOrDefaultAsync(x => x.StaffProgramMappingID == request.StaffProgramMappingID);
                    if (assessment != null)
                    {
                        var program = _context.ClimatePrograms.FirstOrDefault(x => x.ClimateProgramID == assessment.StaffProgramMapping.ClimateProgramID);

                        var url = string.Empty;
                        if (mailToUser.Role == UserRole.Admin)
                        {
                            url = $"admin/assesment/2/{assessment.StaffProgramMapping.ClimateProgramID}";
                        }
                        else
                        {
                            url = $"analyst/evaluator-response/{request.UserID}/{assessment.StaffProgramMapping.ClimateProgramID}";
                        }

                        string passwordResetLink = _appSettings.ApplicationUrl+"/" + url;
                        var model = new EmailInvitationSendRequestDto
                        {
                            ResetPasswordUrl = passwordResetLink,
                            Title = "Request to update assessment",
                            ApiUrl = _appSettings.ApiUrl,
                            ApplicationUrl = _appSettings.ApplicationUrl,
                            MsgText = $"You are receiving this email because user {user?.FullName} recently requested to update assessment of {program?.ProgramName} from their Veridian Climate Pulse account.",
                            BtnText = "Give Access",
                            Mail = _appSettings.AdminMail
                        };
                        var isMailSent = await _emailService.SendEmailAsync(mailToUser.Email, "Request to update assessment", "~/Views/EmailTemplates/ChangePassword.cshtml", model);
                        if (isMailSent)
                        {
                            assessment.AssessmentPhase = AssessmentPhase.EditRequested;
                            _context.Assessments.Update(assessment);
                            await _context.SaveChangesAsync();
                            return ResultResponseDto<string>.Success("", new string[] { "You have requested to update the assessment" });
                        }
                    }
                    return ResultResponseDto<string>.Failure(new string[] { "There is an error please try again" });
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("ForgotPassword", ex);
                return ResultResponseDto<string>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<UserResponseDto>> ClientSignUp(ClientSignUpDto request)
        {
            try
            {
                // Check if the user already exists
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

                if (existingUser != null)
                {
                    return ResultResponseDto<UserResponseDto>.Failure(new[]
                    {
                        "An account with this email already exists. Please log in."
                    });
                }

                // Register new user
                var user = Register(request.FullName, request.Email, request.Phone, request.Password, request.Role);
                user.IsEmailConfirmed = request.IsConfrimed;
                user.Is2FAEnabled = request.Is2FAEnabled;
                bool isMailSend = false;
                // Send verification email
                if (!request.IsConfrimed)
                {
                    var hash = BCrypt.Net.BCrypt.HashPassword(request.Email);
                    var token = hash.Replace("+", " "); // Replace + to avoid URL issues
                    var passwordResetLink = $"{_appSettings.PublicApplicationUrl}/auth/confirm-mail?PasswordToken={token}";

                    var emailModel = new EmailInvitationSendRequestDto
                    {
                        ResetPasswordUrl = passwordResetLink,
                        Title = "Verify Your Email",
                        ApiUrl = _appSettings.ApiUrl,
                        ApplicationUrl = _appSettings.PublicApplicationUrl,
                        MsgText = "Thank you for signing up! Please verify your email to complete registration.",
                        Mail = _appSettings.AdminMail,
                        BtnText = "Verify",
                        DescriptionAboutBtnText = "Please verify your email address by clicking the button above.",
                        IsLoginBtn = false
                    };

                    isMailSend = await _emailService.SendEmailAsync(
                        request.Email,
                        "Verify Your Email",
                        "~/Views/EmailTemplates/ChangePassword.cshtml",
                        emailModel
                    );
                    if (isMailSend)
                    {
                        user.ResetToken = token;
                        user.ResetTokenDate = DateTime.Now;
                    }
                }
                user.TemporaryEmail = user.Email;

                _context.Users.Update(user);

                await _context.SaveChangesAsync();

                if (request.IsConfrimed)
                {
                    var response = GetAuthorizedUserDetails(user);
                    return response;
                }
                else if (isMailSend)
                {
                    return ResultResponseDto<UserResponseDto>.Success(new(), new[]
                    {
                        "We’ve sent you a verification link. Please check your email."
                    });
                }
                else
                {
                    return ResultResponseDto<UserResponseDto>.Success(new(), new[]
                    {
                        "Email could not be sent. Please use 'Forgot Password' to generate a new one."
                    });
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error during user signup", ex);
                return ResultResponseDto<UserResponseDto>.Failure(new[] { "Something went wrong. Please try again later." });
            }
        }

        public async Task<ResultResponseDto<object>> ConfirmMail(string passwordToken)
        {
            try
            {
                var user = await _context.Users
                    .Where(u => u.ResetToken == passwordToken)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return ResultResponseDto<object>.Failure(new string[] { "User not exist." });
                }

                if (_appSettings.LinkValidHours >= (DateTime.Now - user.ResetTokenDate).Hours)
                {
                    if (!string.IsNullOrEmpty(user.TemporaryEmail))
                    {
                        user.Email = user.TemporaryEmail;
                        user.TemporaryEmail = null;
                        user.IsEmailConfirmed = true;
                    }

                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();

                    return ResultResponseDto<object>.Success(
                        new { ResetToken = passwordToken },
                        new string[] { "Mail Confirmed Successfully." }
                    );
                }
                else
                {
                    return ResultResponseDto<object>.Failure(
                        new string[] { "Link has been expired. You can reset your password" });
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error change password", ex);
                return ResultResponseDto<object>.Failure(
                    new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<object>> ContactUs(ContactUsRequestDto requestDto)
        {
            try
            {
                var emailModel = new EmailInvitationSendRequestDto
                {
                    ResetPasswordUrl = "",
                    Title = $"{requestDto.Subject} - {requestDto.Email}",
                    ApiUrl = _appSettings.ApiUrl,
                    ApplicationUrl = _appSettings.PublicApplicationUrl,
                    MsgText = requestDto.Message,
                    DescriptionAboutBtnText
                        = $"This email was sent by {requestDto.Name} from {requestDto.Program}. You can reach them at: {requestDto.Email}.",
                    IsLoginBtn = false,
                    IsShowBtnText = false,
                    Mail = _appSettings.AdminMail
                };

                var isMailSend = await _emailService.SendEmailAsync(
                    _appSettings.ApplicationInfoMail,
                    requestDto.Subject,
                    "~/Views/EmailTemplates/ChangePassword.cshtml",
                    emailModel
                );

                if (isMailSend)
                {
                    return ResultResponseDto<object>.Success(
                        new { },
                        new string[] { "Thank you for contacting us. Our team will reach out to you shortly." }
                    );
                }
                else
                {
                    return ResultResponseDto<object>.Failure(new string[] { "Unable to send your message at the moment. Please try again later." });
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in ContactUs", ex);
                return ResultResponseDto<object>.Failure(
                    new string[] { "An unexpected error occurred. Please try again later." }
                );
            }
        }

        public async Task<ResultResponseDto<string>> SendTwoFactorOTPAsync(User user)
        {
            try
            {
                // 1?? Generate secure random 6-digit OTP
                var random = new Random();
                var otp = random.Next(100000, 999999).ToString();

                // 3?? Store hashed OTP + expiry
                user.ResetToken = otp;
                user.ResetTokenDate = DateTime.Now;

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                var url = user.Role != UserRole.ProgramUser ? _appSettings.ApplicationUrl : _appSettings.PublicApplicationUrl;
                // 4?? Send the OTP via email
                var model = new EmailInvitationSendRequestDto
                {
                    Title = "Two-Factor Authentication (2FA) Code",
                    ApiUrl = _appSettings.ApiUrl,
                    ApplicationUrl = url,
                    MsgText = $"Your one-time password (OTP) for login verification is ( {otp} ). " +
                               $"This code will expire in {_appSettings.OTPExpiryValidMinutes} minutes. " +
                               $"Please do not share this code with anyone.",
                    IsLoginBtn = false,
                    IsShowBtnText = false,
                    Mail = _appSettings.AdminMail,
                    DescriptionAboutBtnText = "You are receiving this email because a login attempt was made to your VCP account. " +
                               "If this was you, please use the above OTP to complete your sign-in. " +
                               "If you did not request this login, please secure your account immediately by resetting your password."
                };

                var isMailSent = await _emailService.SendEmailAsync(
                    user.Email,
                    "Your 2FA Verification Code",
                    "~/Views/EmailTemplates/ChangePassword.cshtml",
                    model
                );

                if (!isMailSent)
                    return ResultResponseDto<string>.Failure(new[] { "Failed to send OTP. Please try again." });

                return ResultResponseDto<string>.Success("", new[] { "OTP sent successfully to your email." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in SendTwoFactorOTPAsync", ex);
                return ResultResponseDto<string>.Failure(new[] { "There was an error while sending OTP. Please try again later." });
            }
        }
        public async Task<ResultResponseDto<UserResponseDto>> TwofaVerification(string email, int otp)
        {
            try
            {
                var user = await GetByEmailAsync(email);
                if (user == null)
                    return ResultResponseDto<UserResponseDto>.Failure(new[] { "User not found. Please check your email and try again." });

                if (string.IsNullOrEmpty(user.ResetToken) || !int.TryParse(user.ResetToken, out var existingOtp))
                    return ResultResponseDto<UserResponseDto>.Failure(new[] { "Invalid or missing OTP. Please request a new one." });

                if (existingOtp != otp)
                    return ResultResponseDto<UserResponseDto>.Failure(new[] { "Incorrect OTP. Please verify and try again." });

                var timeElapsed = (DateTime.Now - user.ResetTokenDate).TotalMinutes;
                if (timeElapsed > _appSettings.OTPExpiryValidMinutes)
                    return ResultResponseDto<UserResponseDto>.Failure(new[] { "OTP has expired. Please request a new one." });

                var response = GetAuthorizedUserDetails(user);
                return response;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error during 2FA verification", ex);
                return ResultResponseDto<UserResponseDto>.Failure(new[] { "An unexpected error occurred. Please try again later." });
            }
        }
        public async Task<ResultResponseDto<string>> ReSendLoginOtp(string email)
        {
            try
            {
                var user = await GetByEmailAsync(email);
                if (user == null)
                    return ResultResponseDto<string>.Failure(new[] { "User not found. Please check your email and try again." });
                return await SendTwoFactorOTPAsync(user);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in SendTwoFactorOTPAsync", ex);
                return ResultResponseDto<string>.Failure(new[] { "There was an error while sending OTP. Please try again later." });
            }
        }

        public async Task<ResultResponseDto<UpdateUserResponseDto>> UpdateUser(UpdateUserDto requestDto)
        {
            try
            {
                var user = await _context.Users.FindAsync(requestDto.UserID);
                if (user == null)
                    return ResultResponseDto<UpdateUserResponseDto>.Failure(new List<string>() { "Invalid request " });

                // Handle profile image upload
                if (requestDto.ProfileImage != null)
                {
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    // ?? Remove old image if exists
                    if (!string.IsNullOrEmpty(user.ProfileImagePath))
                    {
                        string oldFilePath = Path.Combine(_env.WebRootPath, user.ProfileImagePath.TrimStart('/'));
                        if (File.Exists(oldFilePath))
                        {
                            File.Delete(oldFilePath);
                        }
                    }

                    // Save new image
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(requestDto.ProfileImage.FileName);
                    string filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await requestDto.ProfileImage.CopyToAsync(stream);
                    }

                    user.ProfileImagePath = "/uploads/" + fileName;
                }

                bool isMailSent = false;
                if (requestDto.Email != user.Email)
                {
                    var existUser = _context.Users.FirstOrDefault(u => u.Email == requestDto.Email.Trim() && !u.IsDeleted && u.UserID != requestDto.UserID);
                    if (existUser != null)
                    {
                        return ResultResponseDto<UpdateUserResponseDto>.Failure(new List<string>() { "Email Already Exists" });
                    }
                    user.TemporaryEmail = requestDto.Email;
                    var hash = BCrypt.Net.BCrypt.HashPassword(requestDto.Email);
                    var token = hash.Replace("+", " ");
                    var passwordResetLink = $"{_appSettings.PublicApplicationUrl}/auth/confirm-mail?PasswordToken={token}";

                    var emailModel = new EmailInvitationSendRequestDto
                    {
                        ResetPasswordUrl = passwordResetLink,
                        Title = "Verify Your Email",
                        ApiUrl = _appSettings.ApiUrl,
                        ApplicationUrl = _appSettings.PublicApplicationUrl,
                        MsgText = "A request was made to update the Email for your Veridian Climate Pulse (VCP) account. Please verify your email or reset your password.",
                        Mail = _appSettings.AdminMail,
                        BtnText = "Verify",
                        DescriptionAboutBtnText = "Please verify your email address by clicking the button above."
                    };

                    isMailSent = await _emailService.SendEmailAsync(requestDto.Email, "Verify Your Email",
                        "~/Views/EmailTemplates/ChangePassword.cshtml", emailModel
                    );

                    if (isMailSent)
                    {
                        user.IsEmailConfirmed = false; // Require reconfirmation for new email                       
                        user.ResetToken = token;
                        user.ResetTokenDate = DateTime.Now;
                    }
                    else
                    {
                        return ResultResponseDto<UpdateUserResponseDto>.Failure(new List<string>()
                            { "Failed to send email confirmation. Please try again later." }
                        );
                    }
                }

                // Update fields
                user.FullName = requestDto.FullName;
                user.Phone = requestDto.Phone;
                user.Is2FAEnabled = requestDto.Is2FAEnabled;

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                var response = new UpdateUserResponseDto
                {
                    UserID = user.UserID,
                    FullName = user.FullName,
                    Phone = user.Phone,
                    Email = user.Email,
                    Is2FAEnabled = user.Is2FAEnabled,
                    ProfileImagePath = user.ProfileImagePath,
                    Tier = user.Tier ?? Enums.TieredAccessPlan.Pending
                };
                var messages = new List<string>();
                if (isMailSent)
                {
                    messages.Add("Confirmation Mail Sent and Details Updated Successfully");
                }
                else
                {
                    messages.Add("Updated Successfully");
                }
                return ResultResponseDto<UpdateUserResponseDto>.Success(response, messages);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure UpdateUser", ex);
                return ResultResponseDto<UpdateUserResponseDto>.Failure(new string[] { "There is an error please try later" });
            }
        }

        //public async Task<ResultResponseDto<string>> AddProgramUserKpisProgramAndPillar(AddClientKpisProgramAndPillar payload, int userId, string tierName)
        //{
        //    try
        //    {
        //        if (string.IsNullOrWhiteSpace(tierName))
        //            return ResultResponseDto<string>.Failure(new[] { "Access tier information is missing. Please log in again." });

        //        if (!Enum.TryParse<TieredAccessPlan>(tierName, true, out var tier))
        //            return ResultResponseDto<string>.Failure(new[] { "Invalid tier access. Please contact support team." });

        //        var tierLimits = tier switch
        //        {
        //            TieredAccessPlan.Basic => new { Min = 5, Max = 7, Name = "Basic" },
        //            TieredAccessPlan.Standard => new { Min = 8, Max = 12, Name = "Standard" },
        //            TieredAccessPlan.Premium => new { Min = 13, Max = 23, Name = "Premium" },
        //            _ => new { Min = 0, Max = 0, Name = "Unknown" }
        //        };

        //        if (tier != TieredAccessPlan.Premium)
        //        {
        //            bool isValid =
        //                payload.Programs.Count >= tierLimits.Min && payload.Programs.Count <= tierLimits.Max &&
        //                payload.Pillars.Count >= tierLimits.Min && payload.Pillars.Count <= tierLimits.Max;

        //            if (!isValid)
        //            {
        //                return ResultResponseDto<string>.Failure(new[]
        //                {
        //                    $"Your {tierLimits.Name} plan allows between {tierLimits.Min} and {tierLimits.Max} selections per category (Program, Pillar, and KPI). Please adjust your selections accordingly."
        //                });
        //            }
        //        }

        //        //  Remove existing mappings
        //        var existingPrograms = await _context.ClientProgramMappings
        //            .Where(m => m.UserID == userId)
        //            .ToListAsync();

        //        var existingPillars = await _context.ClientPillarMappings
        //            .Where(m => m.UserID == userId)
        //            .ToListAsync();

        //        _context.ClientProgramMappings.RemoveRange(existingPrograms);
        //        _context.ClientPillarMappings.RemoveRange(existingPillars);

        //        var utcNow = DateTime.UtcNow;

        //        var newProgramMappings = payload.Programs.Select(programId => new ClientProgramMapping
        //        {
        //            ClimateProgramID = programId,
        //            UserID = userId,
        //            IsActive = true,
        //            UpdatedAt = utcNow
        //        });

        //        var newPillarMappings = payload.Pillars.Select(pillarId => new ClientPillarMapping
        //        {
        //            PillarID = pillarId,
        //            UserID = userId,
        //            IsActive = true,
        //            UpdatedAt = utcNow
        //        });

        //        await _context.ClientProgramMappings.AddRangeAsync(newProgramMappings);
        //        await _context.ClientPillarMappings.AddRangeAsync(newPillarMappings);

        //        await _context.SaveChangesAsync();

        //        return ResultResponseDto<string>.Success("", new[] { "Your preferences have been saved successfully." });
        //    }
        //    catch (Exception ex)
        //    {
        //        await _appLogger.LogAsync("Error occurred in AddProgramUserKpisProgramAndPillar", ex);
        //        return ResultResponseDto<string>.Failure(new[]
        //        {
        //            "Something went wrong while saving your selections. Please try again later."
        //        });
        //    }
        //}

        public async Task<ResultResponseDto<string>> AddProgramUserKpisProgramAndPillar(AddClientKpisProgramAndPillar payload, int userId, string tierName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tierName))
                    return ResultResponseDto<string>.Failure(new[] { "Access tier information is missing. Please log in again." });

                if (!Enum.TryParse<TieredAccessPlan>(tierName, true, out var tier))
                    return ResultResponseDto<string>.Failure(new[] { "Invalid tier access. Please contact support team." });

                var allPillarIds = await _context.Pillars.Select(p => p.PillarID).ToListAsync();
                var allProgramIds = await _context.ClimatePrograms.Where(c => c.IsActive).Select(c => c.ClimateProgramID).ToListAsync();

                if (tier == TieredAccessPlan.Premium)
                {
                    // Premium always receives every pillar
                    payload.Pillars = allPillarIds;

                    if (payload.IsAllPrograms)
                    {
                        payload.Programs = allProgramIds;
                    }
                    else if (payload.Programs == null || payload.Programs.Count < 1)
                    {
                        return ResultResponseDto<string>.Failure(new[]
                        {
                            "Premium plan requires at least one program, or all programs."
                        });
                    }
                }
                else
                {
                    var pillarLimits = tier switch
                    {
                        TieredAccessPlan.Basic => new { Min = 1, Max = 7, Name = "Basic" },
                        TieredAccessPlan.Standard => new { Min = 1, Max = 12, Name = "Standard" },
                        _ => new { Min = 0, Max = 0, Name = "Unknown" }
                    };

                    var programCount = payload.Programs?.Count ?? 0;
                    var pillarCount = payload.Pillars?.Count ?? 0;
                    var programsOk = programCount >= 1;
                    var pillarsOk = pillarCount >= pillarLimits.Min && pillarCount <= pillarLimits.Max;

                    if (!programsOk || !pillarsOk)
                    {
                        return ResultResponseDto<string>.Failure(new[]
                        {
                            $"Your {pillarLimits.Name} plan requires at least 1 program and between {pillarLimits.Min} and {pillarLimits.Max} pillars."
                        });
                    }
                }

                //  Remove existing mappings
                var existingPrograms = await _context.ClientProgramMappings
                    .Where(m => m.UserID == userId)
                    .ToListAsync();

                var existingPillars = await _context.ClientPillarMappings
                    .Where(m => m.UserID == userId)
                    .ToListAsync();

                _context.ClientProgramMappings.RemoveRange(existingPrograms);
                _context.ClientPillarMappings.RemoveRange(existingPillars);

                var utcNow = DateTime.UtcNow;

                var newProgramMappings = payload.Programs.Select(programId => new ClientProgramMapping
                {
                    ClimateProgramID = programId,
                    UserID = userId,
                    IsActive = true,
                    UpdatedAt = utcNow
                });

                var newPillarMappings = payload.Pillars.Select(pillarId => new ClientPillarMapping
                {
                    PillarID = pillarId,
                    UserID = userId,
                    IsActive = true,
                    UpdatedAt = utcNow
                });

                await _context.ClientProgramMappings.AddRangeAsync(newProgramMappings);
                await _context.ClientPillarMappings.AddRangeAsync(newPillarMappings);

                await _context.SaveChangesAsync();

                return ResultResponseDto<string>.Success("", new[] { "Your preferences have been saved successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in AddProgramUserKpisProgramAndPillar", ex);
                return ResultResponseDto<string>.Failure(new[]
                {
                    "Something went wrong while saving your selections. Please try again later."
                });
            }
        }

        private async Task<(List<int> programsToAdd, List<int> programsToDelete)> GetProgramMappingChangesAsync(int userId, int assignedByUserId,
            UserRole role, List<int> newProgramIds)
        {
            List<int> existingProgramIds;

            if (role == UserRole.ProgramUser)
            {
                var existingPrograms = await _context.ClientProgramMappings
                    .Where(m => m.UserID == userId && m.IsActive)
                    .ToListAsync();

                existingProgramIds = existingPrograms.Select(x => x.ClimateProgramID).ToList();

                var updatePrograms = existingPrograms.Where(x => x.IsDeleted && newProgramIds.Contains(x.ClimateProgramID));
                foreach (var c in updatePrograms)
                {
                    c.IsDeleted = false;
                    _context.Update(c);
                }
            }
            else
            {
                var existingPrograms = _context.StaffProgramMappings
                    .Where(m => m.UserID == userId && m.AssignedByUserId == assignedByUserId)
                    .ToList();
                existingProgramIds = existingPrograms.Select(x => x.ClimateProgramID).ToList();

                var updatePrograms = existingPrograms.Where(x => x.IsDeleted && newProgramIds.Contains(x.ClimateProgramID));
                foreach (var c in updatePrograms)
                {
                    c.IsDeleted = false;
                    _context.Update(c);
                }
            }
            newProgramIds ??= new List<int>();

            var programsToAdd = newProgramIds.Except(existingProgramIds).ToList();
            var programsToDelete = existingProgramIds.Except(newProgramIds).ToList();

            return (programsToAdd, programsToDelete);
        }

        #endregion
    }
}
