using HornScope.Common.Implementation;
using HornScope.Common.Interface;
using HornScope.Common.Models;
using HornScope.Data;
using HornScope.Dtos.AssessmentDto;
using HornScope.Dtos.CommonDto;
using HornScope.Dtos.CountryDto;
using HornScope.Dtos.UserDtos;
using HornScope.Enums;
using HornScope.IServices;
using HornScope.Models;
using Microsoft.EntityFrameworkCore;
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
        public async Task<PaginationResponse<GetUserByRoleResponse>> GetUserByRoleWithAssignedCountry(GetUserByRoleRequestDto request, int userid, UserRole userRole)
        {
            try
            {
                var filteredMappings =
                    _context.UserCountryMappings
                        .Where(x => !x.IsDeleted &&
                               (x.AssignedByUserId == request.UserID || userRole == UserRole.Admin));

                Expression<Func<User, bool>> predicate = userRole switch
                {
                    UserRole.Admin => x => !x.IsDeleted && (request.GetUserRole.HasValue
                                        ? x.Role == request.GetUserRole
                                        : (x.Role == UserRole.Evaluator)),
                    _ => x => !x.IsDeleted && x.Role == UserRole.Evaluator
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
                        Countries = new List<AddUpdateCountryDto>(),
                        Pillars = new List<int>()  // initialize empty list
                    };

                var response = await query.ApplyPaginationAsync(
                    request,
                    x => string.IsNullOrEmpty(request.SearchText) ||
                         x.Email.Contains(request.SearchText) ||
                         x.FullName.Contains(request.SearchText));

                var userIds = response.Data.Select(x => x.UserID).Distinct().ToList();

                if (request.GetUserRole == UserRole.CountryUser)
                {
                    // Fetch countries from PublicUserCountryMappings
                    var countryMap = await _context.PublicUserCountryMappings
                        .Where(x => x.IsActive && userIds.Contains(x.UserID))
                        .Join(_context.Countries,
                            cm => cm.CountryID,
                            c => c.CountryID,
                            (cm, c) => new { cm.UserID, Country = new AddUpdateCountryDto { CountryID = c.CountryID, CountryName = c.CountryName, Region = c.Region, Continent = c.Continent } })
                        .ToListAsync();

                    // Fetch pillar IDs from CountryUserPillarMappings
                    var pillarMap = await _context.CountryUserPillarMappings
                        .Where(x => x.IsActive && userIds.Contains(x.UserID))
                        .Select(x => new { x.UserID, x.PillarID })
                        .ToListAsync();

                    var countriesGrouped = countryMap.GroupBy(x => x.UserID)
                        .ToDictionary(g => g.Key, g => g.Select(x => x.Country).ToList());

                    var pillarsGrouped = pillarMap.GroupBy(x => x.UserID)
                        .ToDictionary(g => g.Key, g => g.Select(x => x.PillarID).ToList());

                    foreach (var item in response.Data)
                    {
                        countriesGrouped.TryGetValue(item.UserID, out var countries);
                        pillarsGrouped.TryGetValue(item.UserID, out var pillars);

                        item.Countries = countries ?? new List<AddUpdateCountryDto>();
                        item.Pillars = pillars ?? new List<int>();
                    }
                }
                else
                {
                    // For Evaluator / Analyst, keep your existing logic for countries
                    var countryMap = await _context.UserCountryMappings
                        .Where(x => !x.IsDeleted &&
                               userIds.Contains(x.UserID) &&
                               (x.AssignedByUserId == request.UserID || userRole == UserRole.Admin))
                        .Join(_context.Countries,
                            cm => cm.CountryID,
                            c => c.CountryID,
                            (cm, c) => new { cm.UserID, Country = new AddUpdateCountryDto { CountryID = c.CountryID, CountryName = c.CountryName, Region = c.Region, Continent = c.Continent } })
                        .ToListAsync();

                    var countriesGrouped = countryMap.GroupBy(x => x.UserID)
                        .ToDictionary(g => g.Key, g => g.Select(x => x.Country).ToList());

                    foreach (var item in response.Data)
                    {
                        countriesGrouped.TryGetValue(item.UserID, out var countries);
                        item.Countries = countries ?? new List<AddUpdateCountryDto>();
                        item.Pillars = new List<int>(); // no pillars for other roles
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetUserByRoleWithAssignedCity", ex);
                return new PaginationResponse<GetUserByRoleResponse>();
            }
        }
        public async Task<ResultResponseDto<List<PublicUserResponse>>> GetEvaluatorByAnalyst(GetAssignUserDto request)
        {
            try
            {
                var query =
                    from uc in _context.UserCountryMappings
                    where !uc.IsDeleted
                          && uc.AssignedByUserId == request.UserID
                          && (!request.SearchedUserID.HasValue || uc.UserID == request.SearchedUserID.Value)
                          && (!request.CountryID.HasValue || uc.CountryID == request.CountryID.Value)
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

        public async Task<ResultResponseDto<List<GetAssessmentResponseDto>>> GetUsersAssignedToCountry(int countryId)
        {
            try
            {
                var year = DateTime.Now.Year;
                var users = await _commonService.GetUserDetailsAssignedToCountry(year, countryId);
                return ResultResponseDto<List<GetAssessmentResponseDto>>.Success(users, new[] { "User fetched successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetUsersAssignedToCity", ex);
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