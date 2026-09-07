using Microsoft.EntityFrameworkCore;
using HornScope.Common.Implementation;
using HornScope.Common.Interface;
using HornScope.Common.Models;
using HornScope.Data;
using HornScope.Dtos.AssessmentDto;
using HornScope.Dtos.CommonDto;
using HornScope.Dtos.ProgramDto;
using HornScope.Dtos.UserDtos;
using HornScope.Enums;
using HornScope.IServices;
using HornScope.Models;
using System.Linq.Expressions;

namespace HornScope.Services
{
    public class UserService : IUserService
    {
        private readonly IAppLogger _appLogger;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ICommonService _commonService;
        public UserService(ApplicationDbContext context, IAppLogger appLogger, IWebHostEnvironment env, ICommonService commonService)
        {
            _context = context;
            _appLogger = appLogger;
            _env = env;
            _commonService = commonService;
        }
        public User? GetByEmail(string email)
        {
            return _context.Users.FirstOrDefault(u => u.Email == email);
        }
        public async Task<PaginationResponse<GetUserByRoleResponse>> GetUserByRoleWithAssignedProgram(GetUserByRoleRequestDto request, int userid, UserRole userRole)
        {
            try
            {
                var filteredMappings =
                    _context.StaffProgramMappings
                        .Where(x => !x.IsDeleted &&
                               (x.AssignedByUserId == request.UserID || userRole == UserRole.Admin));

                Expression<Func<User, bool>> predicate = userRole switch
                {
                    UserRole.Admin => x => !x.IsDeleted && (request.GetUserRole.HasValue
                                        ? x.Role == request.GetUserRole
                                        : (x.Role == UserRole.Evaluator)),
                    _ => x => !x.IsDeleted && x.Role == UserRole.Evaluator && x.CreatedBy == userid
                };

                var query =
                    from u in _context.Users.Where(predicate)
                    from uc in filteredMappings
                                .Where(m => m.UserID == u.UserID)
                                .Take(1).DefaultIfEmpty()
                    from ab in _context.Users
                                .Where(p => uc != null && p.UserID == uc.AssignedByUserId)
                                .DefaultIfEmpty()
                    select new GetUserByRoleResponse
                    {
                        UserID = u.UserID,
                        FullName = u.FullName,
                        Email = u.Email,
                        Phone = u.Phone,
                        Role = u.Role.ToString(),
                        CreatedBy = uc != null ? uc.AssignedByUserId : null,
                        IsDeleted = u.IsDeleted,
                        IsEmailConfirmed = u.IsEmailConfirmed,
                        CreatedAt = u.CreatedAt,
                        CreatedByName = ab != null ? ab.FullName : null,
                        Tier = u.Tier,
                        ClimatePrograms = new List<AddUpdateProgramDto>(),
                        Pillars = new List<int>()  // initialize empty list
                    };

                var response = await query.ApplyPaginationAsync(
                    request,
                    x => string.IsNullOrEmpty(request.SearchText) ||
                         x.Email.Contains(request.SearchText) ||
                         x.FullName.Contains(request.SearchText));

                var userIds = response.Data.Select(x => x.UserID).Distinct().ToList();

                if (request.GetUserRole == UserRole.ProgramUser)
                {
                    // Fetch programs from PublicStaffProgramMappings
                    var programMap = await _context.ClientProgramMappings
                        .Where(x => x.IsActive && userIds.Contains(x.UserID))
                        .Join(_context.ClimatePrograms,
                            cm => cm.ClimateProgramID,
                            p => p.ClimateProgramID,
                            (cm, p) => new { cm.UserID, Program = new AddUpdateProgramDto { ClimateProgramID = p.ClimateProgramID, ProgramName = p.ProgramName } })
                        .ToListAsync();

                    // Fetch pillar IDs from ClientPillarMappings
                    var pillarMap = await _context.ClientPillarMappings
                        .Where(x => x.IsActive && userIds.Contains(x.UserID))
                        .Select(x => new { x.UserID, x.PillarID })
                        .ToListAsync();

                    var programsGrouped = programMap.GroupBy(x => x.UserID)
                        .ToDictionary(g => g.Key, g => g.Select(x => x.Program).ToList());

                    var pillarsGrouped = pillarMap.GroupBy(x => x.UserID)
                        .ToDictionary(g => g.Key, g => g.Select(x => x.PillarID).ToList());

                    foreach (var item in response.Data)
                    {
                        programsGrouped.TryGetValue(item.UserID, out var programs);
                        pillarsGrouped.TryGetValue(item.UserID, out var pillars);

                        item.ClimatePrograms = programs ?? new List<AddUpdateProgramDto>();
                        item.Pillars = pillars ?? new List<int>();
                    }
                }
                else
                {
                    // For Evaluator / Analyst, keep your existing logic for programs
                    var programMap = await _context.StaffProgramMappings
                        .Where(x => !x.IsDeleted &&
                               userIds.Contains(x.UserID) &&
                               (x.AssignedByUserId == request.UserID || userRole == UserRole.Admin))
                        .Join(_context.ClimatePrograms,
                            cm => cm.ClimateProgramID,
                            p => p.ClimateProgramID,
                            (cm, p) => new { cm.UserID, Program = new AddUpdateProgramDto { ClimateProgramID = p.ClimateProgramID, ProgramName = p.ProgramName } })
                        .ToListAsync();

                    var programsGrouped = programMap.GroupBy(x => x.UserID)
                        .ToDictionary(g => g.Key, g => g.Select(x => x.Program).ToList());

                    foreach (var item in response.Data)
                    {
                        programsGrouped.TryGetValue(item.UserID, out var programs);
                        item.ClimatePrograms = programs ?? new List<AddUpdateProgramDto>();
                        item.Pillars = new List<int>(); // no pillars for other roles
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetUserByRoleWithAssignedProgram", ex);
                return new PaginationResponse<GetUserByRoleResponse>();
            }
        }

        public async Task<ResultResponseDto<List<PublicUserResponse>>> GetEvaluatorByAnalyst(GetAssignUserDto request)
        {
            try
            {
                var query =
                    from uc in _context.StaffProgramMappings
                    where !uc.IsDeleted
                          && uc.AssignedByUserId == request.UserID
                          && (!request.SearchedUserID.HasValue || uc.UserID == request.SearchedUserID.Value)
                          && (!request.ClimateProgramID.HasValue || uc.ClimateProgramID == request.ClimateProgramID.Value)
                    join u in _context.Users
                        .Where(x => !x.IsDeleted)
                        on uc.UserID equals u.UserID
                    select new PublicUserResponse
                    {
                        UserID = u.UserID,
                        FullName = u.FullName,
                        Email = u.Email,
                        Phone = u.Phone,
                        Role = u.Role.ToString(),
                        CreatedBy = uc.AssignedByUserId,
                        IsDeleted = u.IsDeleted,
                        IsEmailConfirmed = u.IsEmailConfirmed,
                        CreatedAt = u.CreatedAt
                    };

                var users = await query
                    .Distinct()
                    .OrderBy(x => x.FullName)
                    .ToListAsync();

                return ResultResponseDto<List<PublicUserResponse>>
                    .Success(users, new[] { "User fetched successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetEvaluatorByAnalyst", ex);
                return ResultResponseDto<List<PublicUserResponse>>
                    .Failure(new[] { "There is an error, please try later" });
            }
        }

        public async Task<ResultResponseDto<List<GetAssessmentResponseDto>>> GetUsersAssignedToProgram(int climateProgramID)
        {
            try
            {
                var users = await _commonService.GetUserDetailsAssignedToProgram(climateProgramID);
                return ResultResponseDto<List<GetAssessmentResponseDto>>.Success(users, new[] { "User fetched successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetUsersAssignedToProgram", ex);
                return ResultResponseDto<List<GetAssessmentResponseDto>>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<UpdateUserResponseDto>> GetUserInfo(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return ResultResponseDto<UpdateUserResponseDto>.Failure(new List<string>() { "Invalid request " });

                var response = new UpdateUserResponseDto
                {
                    UserID = user.UserID,
                    FullName = user.FullName,
                    Phone = user.Phone,
                    Email = user.Email,
                    ProfileImagePath = user?.ProfileImagePath,
                    Is2FAEnabled = user?.Is2FAEnabled ?? false,
                    Tier = user?.Tier ?? Enums.TieredAccessPlan.Pending
                };

                return ResultResponseDto<UpdateUserResponseDto>.Success(response, new List<string> { "Updated successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure UpdateUser", ex);
                return ResultResponseDto<UpdateUserResponseDto>.Failure(new string[] { "There is an error please try later" });
            }
        }
    }
}