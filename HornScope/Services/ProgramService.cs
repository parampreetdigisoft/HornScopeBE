using AssessmentPlatform.Models;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data;
using System.Linq.Expressions;
using HornScope.Backgroundjob;
using HornScope.Common.Implementation;
using HornScope.Common.Interface;
using HornScope.Common.Models;
using HornScope.Common.Models.settings;
using HornScope.Data;
using HornScope.Dtos.AssessmentDto;
using HornScope.Dtos.CommonDto;
using HornScope.Dtos.ProgramDto;
using HornScope.IServices;
using HornScope.Models;

namespace HornScope.Services
{
    public class ProgramService : IProgramService
    {
        #region constructor

        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly IWebHostEnvironment _env;
        private readonly ICommonService _commonService;
        private readonly Download _download;
        private readonly AppSettings _appSettings;
        public ProgramService(ApplicationDbContext context, IAppLogger appLogger, IWebHostEnvironment env, ICommonService commonService, Download download, IOptions<AppSettings> appSettings)
        {
            _context = context;
            _appLogger = appLogger;
            _env = env;
            _commonService = commonService;
            _download = download;
            _appSettings = appSettings.Value;
        }

        #endregion

        #region  methods Implementations
        public async Task<ResultResponseDto<string>> AddUpdateProgram(AddUpdateProgramDto q)
        {
            try
            {
                string image = string.Empty;
                if (q.ImageFile != null)
                {
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "assets/programs");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    // ?? Remove old image if exists
                    if (!string.IsNullOrEmpty(q.ImageUrl))
                    {
                        string oldFilePath = Path.Combine(_env.WebRootPath, q.ImageUrl.TrimStart('/'));
                        if (File.Exists(oldFilePath))
                        {
                            File.Delete(oldFilePath);
                        }
                    }

                    // Save new image
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(q.ImageFile.FileName);
                    string filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await q.ImageFile.CopyToAsync(stream);
                    }

                    image = "/assets/programs/" + fileName;
                }
                if(q.ClimateProgramID > 0)
                {
                    var existProgram = await _context.ClimatePrograms.FirstOrDefaultAsync(x => x.IsActive && !x.IsDeleted && x.ProgramName == q.ProgramName && x.ClimateProgramID != q.ClimateProgramID);
                    if (existProgram != null)
                    {
                        return ResultResponseDto<string>.Failure(new string[] { "Program already exists" });
                    }

                    var existing = await _context.ClimatePrograms.FindAsync(q.ClimateProgramID);
                    if (existing == null) return ResultResponseDto<string>.Failure(new string[] { "Program not exists" });
                    existing.ProgramName = q.ProgramName;
                    existing.UpdatedAt = DateTime.Now;
                    existing.Location = q.Location;
                    existing.Status = q.Status;
                    existing.Description = q.Description;
                    existing.StartAt = q.StartAt;
                    existing.EndAt = q.EndAt;
                    existing.IsActive = q.isActive;
                    if (!string.IsNullOrEmpty(image))
                    {
                        existing.Image = image;
                    }                               
                    _context.ClimatePrograms.Update(existing);
                    await _context.SaveChangesAsync();
                    await UpdatePeerPrograms(existing.ClimateProgramID, q.PeerProgramIDs ?? new List<int>());

                    return ResultResponseDto<string>.Success("", new string[] { "Program edited Successfully" });
                }
                else
                {
                    var payload = new BulkAddProgramDto { Programs = new() { q } };
                    var response = await AddBulkProgramsAsync(payload, image);
                    return response;
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in AddUpdateProgram", ex);
                return ResultResponseDto<string>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task UpdatePeerPrograms(int climateProgramID, List<int> peerProgramIDs)
        {
            if (peerProgramIDs == null)
                peerProgramIDs = new List<int>();

            // Remove self and duplicates
            peerProgramIDs = peerProgramIDs
                .Where(x => x != climateProgramID)
                .Distinct()
                .ToList();

            var existingPeers = await _context.ProgramPeers
                .Where(x => x.ClimateProgramID == climateProgramID && !x.IsDeleted)
                .ToListAsync();

            var existingPeerIds = existingPeers
                .Select(x => x.PeerProgramID)
                .ToList();

            // Soft delete removed peers
            var removePeers = existingPeers
                .Where(x => !peerProgramIDs.Contains(x.PeerProgramID))
                .ToList();

            foreach (var peer in removePeers)
            {
                peer.IsDeleted = true;
                peer.IsActive = false;
                peer.UpdatedAt = DateTime.UtcNow;
            }

            // Add new peers
            var newPeers = peerProgramIDs
                .Where(x => !existingPeerIds.Contains(x))
                .ToList();

            foreach (var peerId in newPeers)
            {
                await _context.ProgramPeers.AddAsync(new ProgramPeer
                {
                    ClimateProgramID = climateProgramID,
                    PeerProgramID = peerId,
                    IsActive = true,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            if (newPeers.Count > 0 || removePeers.Count > 0)
            {
                _download.InsertAnalyticalLayerResults(climateProgramID);
            }

            await _context.SaveChangesAsync();
        }
        //public async Task<ResultResponseDto<string>> AddBulkProgramsAsync(BulkAddProgramDto request, string image = "")
        //{
        //    try
        //    {
        //        // ? Keep ORIGINAL values for storage, compute keys only for dedup
        //        var inputPrograms = request.Programs
        //            .Select(c => new
        //            {
        //                // -- Original values (used when creating entities) --
        //                ProgramName = c.ProgramName.Trim(),          // preserve casing
        //                Location = c.Location.Trim(),
        //                StartAt = c.StartAt,
        //                EndAt = c.EndAt,
        //                Description = c.Description,
        //                Status = c.Status,
        //                PeerPrograms = c.PeerPrograms,
        //                Year = c.Year,
        //                // -- Normalized keys (used only for dedup lookups) --
        //                DedupKey = $"{c.ProgramName.Trim().ToLowerInvariant()}_{c.Location.Trim().ToLowerInvariant()}"
        //            })
        //            .GroupBy(c => c.DedupKey)        // deduplicate within the request itself
        //            .Select(g => g.First())
        //            .ToList();

        //        // ? Load existing Programs once
        //        var existingSet = new HashSet<string>(
        //            await _context.ClimatePrograms
        //                .Where(x => x.IsActive && !x.IsDeleted)
        //                .Select(x => x.ProgramName.ToLower() + "_" + x.Location.ToLower())
        //                .ToListAsync(),
        //            StringComparer.OrdinalIgnoreCase
        //        );

        //        // ? Split into new vs already-existing
        //        var newPrograms = inputPrograms.Where(c => !existingSet.Contains(c.DedupKey)).ToList();
        //        var alreadyExisting = inputPrograms.Where(c => existingSet.Contains(c.DedupKey))
        //                                              .Select(c => $"{c.ProgramName}, {c.Location}")
        //                                              .ToList();

        //        // ? Build entities from ORIGINAL (properly-cased) values
        //        var programEntities = newPrograms.Select(c => new ClimateProgram
        //        {
        //            ProgramName = c.ProgramName,   // "Algeria", not "algeria"
        //            Location = c.Location,
        //            Description = c.Description,
        //            Status = c.Status,
        //            StartAt = c.StartAt,
        //            EndAt = c.EndAt,
        //            Year = c.Year,
        //            Image = image,
        //            IsActive = true,
        //            IsDeleted = false,
        //            UpdatedAt = DateTime.UtcNow
        //        }).ToList();

        //        if (programEntities.Any())
        //        {
        //            await _context.ClimatePrograms.AddRangeAsync(programEntities);
        //            await _context.SaveChangesAsync();
        //        }

        //        // ? Peer Programs (index-aligned with newPrograms / ProgramEntities)
        //        var ProgramPeers = newPrograms
        //            .SelectMany((dto, i) =>
        //                dto.PeerPrograms?.Any() == true
        //                    ? dto.PeerPrograms.Select(peerId => new ProgramPeer
        //                    {
        //                        ClimateProgramID = programEntities[i].ClimateProgramID,
        //                        PeerProgramID = peerId,
        //                        IsActive = true,
        //                        IsDeleted = false,
        //                        UpdatedAt = DateTime.UtcNow
        //                    })
        //                    : Enumerable.Empty<ProgramPeer>())
        //            .ToList();

        //        if (ProgramPeers.Any())
        //        {
        //            await _context.ProgramPeers.AddRangeAsync(ProgramPeers);
        //            await _context.SaveChangesAsync();
        //        }

        //        // ? Response
        //        return (alreadyExisting.Any(), newPrograms.Any()) switch
        //        {
        //            (true, true) => ResultResponseDto<string>.Success("",
        //                                new[] { $"{string.Join(", ", alreadyExisting)} already exist" }),
        //            (true, false) => ResultResponseDto<string>.Failure(
        //                                new[] { $"{string.Join(", ", alreadyExisting)} already exist" }),
        //            _ => ResultResponseDto<string>.Success("",
        //                                new[] { "Program added successfully" })
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        await _appLogger.LogAsync("Error Occurred in AddBulkProgramsAsync", ex);
        //        return ResultResponseDto<string>.Failure(new[] { "There is an error please try later" });
        //    }
        //}
        public async Task<ResultResponseDto<string>> AddBulkProgramsAsync(BulkAddProgramDto request, string image = "")
        {
            try
            {
                // ✅ Guard: no programs sent at all
                if (request?.Programs == null || !request.Programs.Any())
                {
                    return ResultResponseDto<string>.Failure(new[] { "No programs were provided to add." });
                }

                // ✅ Guard: reject rows missing required fields before touching them
                var invalidRows = request.Programs
                    .Where(c => string.IsNullOrWhiteSpace(c.ProgramName) || string.IsNullOrWhiteSpace(c.Location))
                    .ToList();

                if (invalidRows.Any())
                {
                    return ResultResponseDto<string>.Failure(
                        new[] { $"{invalidRows.Count} program(s) are missing a required Program Name or Location." });
                }

                // ✅ Keep ORIGINAL values for storage, compute keys only for dedup
                var inputPrograms = request.Programs
                    .Select(c => new
                    {
                        ProgramName = c.ProgramName.Trim(),
                        Location = c.Location.Trim(),
                        StartAt = c.StartAt,
                        EndAt = c.EndAt,
                        Description = c.Description,
                        Status = c.Status,
                        PeerPrograms = c.PeerProgramIDs,
                        Year = c.Year,
                        isActive = c.isActive,
                        DedupKey = $"{c.ProgramName.Trim().ToLowerInvariant()}_{c.Location.Trim().ToLowerInvariant()}"
                    }).GroupBy(c => c.DedupKey).Select(g => g.First()).ToList();

                // ✅ Load existing Programs once
                var existingSet = new HashSet<string>(
                    await _context.ClimatePrograms
                        .Where(x => x.IsActive && !x.IsDeleted)
                        .Select(x => x.ProgramName.ToLower() + "_" + x.Location.ToLower())
                        .ToListAsync(),
                    StringComparer.OrdinalIgnoreCase
                );

                var newPrograms = inputPrograms.Where(c => !existingSet.Contains(c.DedupKey)).ToList();
                var alreadyExisting = inputPrograms.Where(c => existingSet.Contains(c.DedupKey))
                                                      .Select(c => $"{c.ProgramName}, {c.Location}")
                                                      .ToList();

                var programEntities = newPrograms.Select(c => new ClimateProgram
                {
                    ProgramName = c.ProgramName,
                    Location = c.Location,
                    Description = c.Description,
                    Status = c.Status,
                    StartAt = c.StartAt,
                    EndAt = c.EndAt,
                    Year = c.Year,
                    Image = image,
                    IsActive = c.isActive,
                    IsDeleted = false,
                    UpdatedAt = DateTime.UtcNow
                }).ToList();

                // ✅ Use the execution strategy so the transaction is retry-safe
                var strategy = _context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        if (programEntities.Any())
                        {
                            await _context.ClimatePrograms.AddRangeAsync(programEntities);
                            await _context.SaveChangesAsync();
                        }

                        var programPeers = newPrograms
                            .SelectMany((dto, i) =>
                                dto.PeerPrograms?.Any() == true
                                    ? dto.PeerPrograms.Select(peerId => new ProgramPeer
                                    {
                                        ClimateProgramID = programEntities[i].ClimateProgramID,
                                        PeerProgramID = peerId,
                                        IsActive = true,
                                        IsDeleted = false,
                                        UpdatedAt = DateTime.UtcNow
                                    })
                                    : Enumerable.Empty<ProgramPeer>())
                            .ToList();

                        if (programPeers.Any())
                        {
                            await _context.ProgramPeers.AddRangeAsync(programPeers);
                            await _context.SaveChangesAsync();
                        }

                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                // ✅ Response
                return (alreadyExisting.Any(), newPrograms.Any()) switch
                {
                    (true, true) => ResultResponseDto<string>.Success("",
                        new[] { $"{newPrograms.Count} program(s) added successfully. {string.Join(", ", alreadyExisting)} already exist and were skipped." }),
                    (true, false) => ResultResponseDto<string>.Failure(
                        new[] { $"{string.Join(", ", alreadyExisting)} already exist" }),
                    (false, true) => ResultResponseDto<string>.Success("",
                        new[] { "Program added successfully" }),
                    (false, false) => ResultResponseDto<string>.Failure(
                        new[] { "No programs were added." })
                };
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in AddBulkProgramsAsync", ex);
                return ResultResponseDto<string>.Failure(new[] { "There is an error, please try again later" });
            }
        }

        public async Task<ResultResponseDto<bool>> DeleteProgramAsync(int id)
        {
            try
            {
                var q = await _context.ClimatePrograms.FindAsync(id);
                if (q == null) return ResultResponseDto<bool>.Failure(new string[] { "Program not exists" });

                q.IsActive = false;
                q.IsDeleted = true;
                q.UpdatedAt = DateTime.UtcNow;
                _context.ClimatePrograms.Update(q);

                await _context.ProgramPeers.Where(x => x.ClimateProgramID == id).ForEachAsync(x =>
                {
                    x.IsActive = false;
                    x.IsDeleted = true;
                    x.UpdatedAt = DateTime.UtcNow;
                });

                await _context.ProgramPeers.Where(x => x.PeerProgramID == id).ForEachAsync(x =>
                {
                    x.IsActive = false;
                    x.IsDeleted = true;
                    x.UpdatedAt = DateTime.UtcNow;
                });

                await _context.StaffProgramMappings.Where(x => x.ClimateProgramID == id).ForEachAsync(x =>
                {
                    x.IsDeleted = true;
                });

                await _context.SaveChangesAsync();
                return ResultResponseDto<bool>.Success(true, new string[] { "Program deleted Successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in DeleteProgramAsync", ex);
                return ResultResponseDto<bool>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<ClimateProgram>> EditProgramAsync(int id, AddUpdateProgramDto q)
        {

            try
            {
                var existProgram = await _context.ClimatePrograms.FirstOrDefaultAsync(x => x.IsActive && !x.IsDeleted && q.ProgramName == x.ProgramName && x.ClimateProgramID != id);
                if (existProgram != null)
                {
                    return ResultResponseDto<ClimateProgram>.Failure(new string[] { "Program already exists" });
                }
                var existing = await _context.ClimatePrograms.FindAsync(id);
                if (existing == null) return ResultResponseDto<ClimateProgram>.Failure(new string[] { "Program not exists" });
                existing.ProgramName = q.ProgramName;
                existing.UpdatedAt = DateTime.UtcNow;              
                _context.ClimatePrograms.Update(existing);
                await _context.SaveChangesAsync();

                return ResultResponseDto<ClimateProgram>.Success(existing, new string[] { "Program edited Successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in EditProgramAsync", ex);
                return ResultResponseDto<ClimateProgram>.Failure(new string[] { "There is an error please try later" });
            }
        }

        #region GetProgramsAsync
        public async Task<PaginationResponse<StaffProgramMappingResponseDto>> GetProgramsAsync(ProgramPaginationRequest request, UserRole role)
        {
            try
            {
                IQueryable<StaffProgramMappingResponseDto> query = role == UserRole.Admin
                    ? GetAdminProgramQuery()
                    : GetUserProgramQuery(request.UserId);

                // ?? Search
                if (!string.IsNullOrWhiteSpace(request.SearchText))
                {
                    string search = request.SearchText.Trim();
                    query = query.Where(x =>
                        x.ProgramName.Contains(search));
                }

                // ?? Filter by ClimateProgramID
                if (request.ClimateProgramID.HasValue)
                {
                    query = query.Where(x => x.ClimateProgramID == request.ClimateProgramID);
                }

                query = query.OrderBy(x => x.ClimateProgramID);
                // ?? Pagination (DB level)
                var response = await query.ApplyPaginationAsync(request);

                // ?? Manual Score Calculation (Non-Program User)
                if (role != UserRole.ProgramUser && response.Data.Any())
                {
                    await ApplyManualScoresAsync(response, request, role);
                }

                return response;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in GetProgramsAsync", ex);
                return new PaginationResponse<StaffProgramMappingResponseDto>();
            }
        }

        private IQueryable<StaffProgramMappingResponseDto> GetAdminProgramQuery()
        {
            return ( 
                from c in _context.ClimatePrograms.AsNoTracking()
                where !c.IsDeleted
                join ai in _context.AIProgramScores.Where(x => x.IsVerified)
                on c.ClimateProgramID equals ai.ClimateProgramID into aiJoin
                from ai in aiJoin.DefaultIfEmpty()
                select new StaffProgramMappingResponseDto
                {
                    ClimateProgramID = c.ClimateProgramID,
                    ProgramName = c.ProgramName,
                    Year = c.Year,
                    Image = c.Image,
                    Location = c.Location,
                    IsActive = c.IsActive,
                    StartAt = c.StartAt,
                    EndAt = c.EndAt,
                    UpdatedAt = c.UpdatedAt,
                    IsDeleted = c.IsDeleted,
                    AiScore = ai.AIProgress ?? 0,
                    Score = 0,
                    Status = c.Status,
                    Description = c.Description,
                    ProgramPeers = c.ProgramPeers,
                });
        }

        private IQueryable<StaffProgramMappingResponseDto> GetUserProgramQuery(long? userId)
        {
            return
                from c in _context.ClimatePrograms.AsNoTracking()
                join cm in _context.StaffProgramMappings
                        .Where(x => !x.IsDeleted && x.UserID == userId)
                    on c.ClimateProgramID equals cm.ClimateProgramID
                join u in _context.Users
                    on cm.AssignedByUserId equals u.UserID
                join ai in _context.AIProgramScores
                .Where(x => x.IsVerified)
                on c.ClimateProgramID equals ai.ClimateProgramID into aiJoin
                from ai in aiJoin.DefaultIfEmpty()
                where !c.IsDeleted
                select new StaffProgramMappingResponseDto
                {
                    ClimateProgramID = c.ClimateProgramID,
                    ProgramName = c.ProgramName,
                    Location = c.Location,
                    IsActive = c.IsActive,
                    StartAt = c.StartAt,
                    EndAt = c.EndAt,
                    UpdatedAt = c.UpdatedAt,
                    IsDeleted = c.IsDeleted,
                    AssignedBy = u.FullName,
                    StaffProgramMappingID = cm.StaffProgramMappingID,
                    Score = 0,
                    AiScore = ai.AIProgress,
                    ProgramPeers = c.ProgramPeers,
                    Year = c.Year
                };
        }
        private async Task ApplyManualScoresAsync(PaginationResponse<StaffProgramMappingResponseDto> response,PaginationRequest request, UserRole role)
        {
            var scores = await _commonService.GetProgramProgressAsync(request.UserId.GetValueOrDefault(),(int)role);
            int pillarCount = (await _commonService.GetPillars()).Count;

            var scoreMap = scores
                .GroupBy(x => x.ClimateProgramID)
                .ToDictionary(
                    g => g.Key,
                    g => Math.Round((g.Sum(x => (decimal?)x.ScoreProgress) ?? 0) / (decimal)pillarCount, 2));

            foreach (var program in response.Data)
            {
                program.PeerProgramIDs = program.ProgramPeers?.Where(x => x.IsActive && !x.IsDeleted).Select(x => x.PeerProgramID).ToList() ?? new List<int>();
                if (scoreMap.TryGetValue(program.ClimateProgramID, out var score))
                {
                    program.Score = score;
                }
            }

            // ? Correct dynamic sorting
            response.Data = request.SortDirection?.ToLower() == "desc"
                ? response.Data.OrderByDescending(x => x.Score)
                : response.Data.OrderBy(x => x.Score);
        }

        #endregion
        public async Task<ResultResponseDto<List<StaffProgramMappingResponseDto>>> GetAllProgramsByUserId(int userId, UserRole userRole)
        {
            try
            {
                IQueryable<StaffProgramMappingResponseDto> programQuery;
                int pillarCount = (await _commonService.GetPillars()).Count;
                if (userRole == UserRole.Admin)
                {
                    programQuery = GetAdminProgramQuery();
                }
                else
                {
                    programQuery = GetUserProgramQuery(userId);
                }
                var result = await programQuery.ToListAsync();

                if (userRole != UserRole.ProgramUser)
                {
                    var scores = await _commonService.GetProgramProgressAsync(userId, (int)userRole);

                    var scoreMap = scores
                  .GroupBy(x => x.ClimateProgramID)
                  .ToDictionary(
                      g => g.Key,
                      g => Math.Round((g.Sum(x => (decimal?)x.ScoreProgress) ?? 0) / (decimal)pillarCount, 2));

                        foreach (var program in result)
                        {
                            if (scoreMap.TryGetValue(program.ClimateProgramID, out var score))
                            {
                                program.Score = score;
                            }
                        }

                    }
                result = (userRole == UserRole.ProgramUser ? result.OrderByDescending(x => x.AiScore) : result.OrderByDescending(x => x.Score)).ToList();

                return ResultResponseDto<List<StaffProgramMappingResponseDto>>.Success(result, new string[] { "get successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in getAllProgramsByUserId", ex);
                return ResultResponseDto<List<StaffProgramMappingResponseDto>>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<ClimateProgram>> GetByIdAsync(int id)
        {
            try
            {
                var d = await _context.ClimatePrograms.FirstAsync(x => x.ClimateProgramID == id);
                return await Task.FromResult(ResultResponseDto<ClimateProgram>.Success(d, new string[] { "get successfully" }));
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetByIdAsync", ex);
                return ResultResponseDto<ClimateProgram>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<object>> AssignProgramToUser(int userId, int climateProgramID, int assignedByUserId)
        {
            try
            {
                if (_context.StaffProgramMappings.Any(x => x.UserID == userId && x.ClimateProgramID == climateProgramID && x.AssignedByUserId == assignedByUserId && !x.IsDeleted))
                {
                    return await Task.FromResult(ResultResponseDto<object>.Failure(new string[] { "Program already assigned to user" }));
                }
                var user = _context.Users.Find(userId);

                if (user == null)
                {
                    return await Task.FromResult(ResultResponseDto<object>.Failure(new string[] { "Invalid request data." }));
                }
                var mapping = new StaffProgramMapping
                {
                    UserID = userId,
                    ClimateProgramID = climateProgramID,
                    AssignedByUserId = assignedByUserId,
                    Role = user.Role
                };
                _context.StaffProgramMappings.Add(mapping);

                await _context.SaveChangesAsync();

                return await Task.FromResult(ResultResponseDto<object>.Success(new { }, new string[] { "Program assigned successfully" }));
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in AssignProgramToUser", ex);
                return ResultResponseDto<object>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<object>> EditAssignProgram(int id, int userId, int climateProgramID, int assignedByUserId)
        {
            try
            {

                if (_context.StaffProgramMappings.Any(x => x.UserID == userId && x.ClimateProgramID == climateProgramID && x.AssignedByUserId == assignedByUserId))
                {
                    return ResultResponseDto<object>.Failure(new string[] { "Program already assigned to user" });
                }
                var userMapping = _context.StaffProgramMappings.Find(id);

                if (userMapping == null)
                {
                    return ResultResponseDto<object>.Failure(new string[] { "Invalid request data." });
                }

                userMapping.UserID = userId;
                userMapping.ClimateProgramID = climateProgramID;
                userMapping.AssignedByUserId = assignedByUserId;
                _context.StaffProgramMappings.Update(userMapping);
                await _context.SaveChangesAsync();

                return ResultResponseDto<object>.Success(new { }, new string[] { "Assigned Program updated successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure", ex);
                return ResultResponseDto<object>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<object>> UnAssignProgram(StaffProgramUnMappingRequestDto requestDto)
        {
            try
            {
                var userMapping = _context.StaffProgramMappings.Where(x => x.UserID == requestDto.UserId && x.AssignedByUserId == requestDto.AssignedByUserId && !x.IsDeleted).ToList();
                if (userMapping == null && userMapping?.Count == 0)
                {
                    return await Task.FromResult(ResultResponseDto<object>.Failure(new string[] { "user has no assign Program" }));
                }
                foreach (var m in userMapping)
                {
                    m.IsDeleted = true;
                    _context.StaffProgramMappings.Update(m);
                }

                await _context.SaveChangesAsync();

                return ResultResponseDto<object>.Success(new { }, new string[] { "Assigned Program deleted successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in UnAssignProgram", ex);
                return ResultResponseDto<object>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<List<StaffProgramMappingResponseDto>>> GetProgramByUserIdForAssessment(int userId)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(x => x.UserID == userId);

                if (user == null)
                {
                    return ResultResponseDto<List<StaffProgramMappingResponseDto>>.Failure(new string[] { "Invalid user" });
                }

                Expression<Func<Assessment, bool>>  predicate = a => 
                !a.StaffProgramMapping.IsDeleted 
                && a.StaffProgramMapping.UserID == userId 
                && (a.AssessmentPhase == AssessmentPhase.Completed || a.AssessmentPhase == AssessmentPhase.EditRejected || a.AssessmentPhase == AssessmentPhase.EditRequested);

                // Get distinct staffProgramMapping which are not show to user
                var staffProgramMappingIds = _context.Assessments
                    .Where(predicate)
                    .Select(a => a.StaffProgramMappingID)
                    .Distinct();

                // Project into response DTO
                var ProgramQuery =
                     from c in _context.ClimatePrograms
                     join cm in _context.StaffProgramMappings
                         .Where(x => !x.IsDeleted && x.UserID == userId && !staffProgramMappingIds.Contains(x.StaffProgramMappingID))
                         on c.ClimateProgramID equals cm.ClimateProgramID
                     join u in _context.Users on cm.AssignedByUserId equals u.UserID
                     select new StaffProgramMappingResponseDto
                     {
                         ClimateProgramID = c.ClimateProgramID,
                         ProgramName = c.ProgramName,
                         Location = c.Location,
                         IsActive = c.IsActive,
                         StartAt = c.StartAt,
                         EndAt = c.EndAt,
                         UpdatedAt = c.UpdatedAt,
                         IsDeleted = c.IsDeleted,
                         AssignedBy = u.FullName,
                         StaffProgramMappingID = cm.StaffProgramMappingID,
                         Year = c.Year
                     };

                var result = await ProgramQuery.OrderByDescending(x => x.EndAt).ThenBy(x => x.UpdatedAt).ToListAsync();

                if (!result.Any())
                {
                    return ResultResponseDto<List<StaffProgramMappingResponseDto>>.Failure(new string[] { "No Program is found for assessment" });
                }

                return ResultResponseDto<List<StaffProgramMappingResponseDto>>.Success(result, new string[] { "Retrieved successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetProgramByUserIdForAssessment", ex);
                return ResultResponseDto<List<StaffProgramMappingResponseDto>>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<ProgramHistoryDto>> GetProgramHistory(int userID, UserRole userRole)
        {
            try
            {
                var programHistory = new ProgramHistoryDto();

                Expression<Func<StaffProgramMapping, bool>> predicate;

                if (userRole == UserRole.Analyst || userRole == UserRole.Evaluator)
                    predicate = x => !x.IsDeleted && x.UserID == userID;
                else
                    predicate = x => !x.IsDeleted;
            
                var programQuery = await (
                    from c in _context.ClimatePrograms
                    where !c.IsDeleted && c.IsActive
                    join uc in _context.StaffProgramMappings.Where(predicate)
                        on c.ClimateProgramID equals uc.ClimateProgramID into ucGroup
                    from uc in ucGroup.DefaultIfEmpty()
                    join a in _context.Assessments.Where(x => x.IsActive)
                        on uc.StaffProgramMappingID equals a.StaffProgramMappingID into programAssessments
                    from a in programAssessments.DefaultIfEmpty()
                    select new
                    {
                        c.ClimateProgramID,
                        HasMapping = uc != null,
                        IsCompleted = a != null && a.AssessmentPhase == AssessmentPhase.Completed
                    }
                ).ToListAsync();

                // First, extract the list of ClimateProgramIDs from your programQuery
                var climateProgramIDs = programQuery.Select(c => c.ClimateProgramID).Distinct().ToList();

                // Then, get all AIProgramScores for those programs
                var aiProgram = await _context.AIProgramScores
                    .Where(x => climateProgramIDs.Contains(x.ClimateProgramID))
                    .ToListAsync();

                if (userRole == UserRole.Admin)
                {
                    programHistory.TotalProgram = programQuery  
                        .Select(x => x.ClimateProgramID)
                        .Distinct()
                        .Count();
                }
                else
                {       
                    programHistory.TotalProgram = programQuery
                        .Where(x => x.HasMapping)
                        .Select(x => x.ClimateProgramID)
                        .Distinct()
                        .Count();
                }
                programHistory.ActiveProgram = programQuery.Where(x => x.HasMapping).Select(x => x.ClimateProgramID).Distinct().Count();
                programHistory.CompeleteProgram = programQuery.Where(x => x.IsCompleted).Select(x => x.ClimateProgramID).Distinct().Count();
                programHistory.InprocessProgram = programHistory.ActiveProgram - programHistory.CompeleteProgram;
                programHistory.TotalAccessProgram = programHistory.ActiveProgram - programHistory.CompeleteProgram;
                programHistory.FinalizeProgram = aiProgram.Where(x=>x.IsVerified).Count();
                programHistory.UnFinalizeProgram = aiProgram.Where(x => !x.IsVerified).Count();

                // 2?? Get evaluators & analysts in a single query
                var userCounts = await _context.Users
                    .Where(u => !u.IsDeleted && (u.Role == UserRole.Evaluator || u.Role == UserRole.Analyst))
                    .GroupBy(u => u.Role)
                    .Select(g => new { Role = g.Key, Count = g.Count() })
                    .ToListAsync();
                if(userRole == UserRole.Admin)
                {
                    programHistory.TotalEvaluator = userCounts.FirstOrDefault(x => x.Role == UserRole.Evaluator)?.Count ?? 0;
                    programHistory.TotalAnalyst = userCounts.FirstOrDefault(x => x.Role == UserRole.Analyst)?.Count ?? 0;
                }

                return ResultResponseDto<ProgramHistoryDto>.Success(programHistory, new List<string> { "Get history successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetProgramHistory", ex);
                return ResultResponseDto<ProgramHistoryDto>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<List<GetProgramsSubmissionHistoryResponseDto>>> GetProgramsProgressByUserId(int userID, UserRole userRole)
        {
            try
            {
                // Get total pillars and questions (independent query)
                var pillarStats = await _context.Pillars.Where(x => x.IsActive && !x.IsDeleted)
                    .Select(p => new { QuestionsCount = p.Questions.Count(x=>!x.IsDeleted) })
                    .ToListAsync();

                int totalPillars = pillarStats.Count;
                int totalQuestions = pillarStats.Sum(p => p.QuestionsCount);

                Expression<Func<StaffProgramMapping, bool>> predicate;

                if (userRole == UserRole.Analyst)
                    predicate = x => !x.IsDeleted && (x.AssignedByUserId == userID || x.UserID == userID);
                else
                    predicate = x => !x.IsDeleted && x.UserID == userID;


                var programRaw = await (
                    from uc in _context.StaffProgramMappings.Where(predicate)
                    join c in _context.ClimatePrograms.Where(c => !c.IsDeleted && c.IsActive)
                        on uc.ClimateProgramID equals c.ClimateProgramID
                    join a in _context.AIProgramScores
                        on c.ClimateProgramID equals a.ClimateProgramID into AIProgramScores
                    from a in AIProgramScores.DefaultIfEmpty()
                    select new
                    {
                        c.ClimateProgramID,
                        c.ProgramName,
                        a.AIProgress
                    }
                ).AsNoTracking().Distinct().ToListAsync();  // ?? force materialization first

                var manualAssessmentList = await _commonService.GetProgramProgressAsync(userID, (int)userRole);

                // Now do grouping/aggregation in memory (LINQ to Objects)
                var programSubmission = programRaw
                    .Select(g =>
                    {
                        var allPillars = manualAssessmentList.Where(x => x.ClimateProgramID == g.ClimateProgramID);

                        var manualScore = allPillars.Any()? allPillars.Average(x => x?.ScoreProgress ?? 0): 0;

                        return new GetProgramsSubmissionHistoryResponseDto
                        {
                            ClimateProgramID = g.ClimateProgramID,
                            ProgramName = g.ProgramName,
                            Score = Math.Round(manualScore,2),
                            AnsQuestion = allPillars.Any() ? allPillars.Sum(x=>x?.TotalAns ?? 0) : 0,
                            ScoreProgress = Math.Round(manualScore, 2),
                            AIScore = g.AIProgress ?? 0
                        };
                    }).ToList();

                return ResultResponseDto<List<GetProgramsSubmissionHistoryResponseDto>>.Success(programSubmission ?? new(), new List<string> { "Get Programs history successfully" });

            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetProgramsProgressByUserId", ex);
                return ResultResponseDto<List<GetProgramsSubmissionHistoryResponseDto>>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<List<StaffProgramMappingResponseDto>>> GetAiAccessProgram(int userId, UserRole userRole)
        {
            try
            {
                IQueryable<StaffProgramMappingResponseDto> programQuery;
                // Step 1??: Fetch Program score averages as a dictionary
                var programScoresQuery =
                   from ar in _context.AIProgramScores
                   group ar by ar.ClimateProgramID into g
                   select new
                   {
                       ClimateProgramID = g.Key,
                       Score = g.Average(x => (decimal?)x.AIProgress) ?? 0
                   };

                programQuery =
                    from c in _context.ClimatePrograms
                    join cm in _context.AIEvaluatorProgramMappings
                        .Where(x => x.IsActive && x.UserID == userId)
                        on c.ClimateProgramID equals cm.ClimateProgramID
                    join u in _context.Users on cm.AssignBy equals u.UserID
                    join cs in programScoresQuery on cm.ClimateProgramID equals cs.ClimateProgramID into scoreGroup
                    from cs in scoreGroup.DefaultIfEmpty()
                    select new StaffProgramMappingResponseDto
                    {
                        ClimateProgramID = c.ClimateProgramID,
                        Location = c.Location,
                        ProgramName = c.ProgramName,
                        IsActive = c.IsActive,
                        StartAt = c.StartAt,
                        UpdatedAt = c.UpdatedAt,
                        IsDeleted = c.IsDeleted,
                        AssignedBy = u.FullName,
                        StaffProgramMappingID = cm.AIEvaluatorProgramMappingID,
                        Score = cs.Score,
                    };
                var result = await programQuery
                    .OrderByDescending(x => x.Score)
                                .ToListAsync();

                return ResultResponseDto<List<StaffProgramMappingResponseDto>>.Success(result, new string[] { "get successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GetAiAccessProgram", ex);
                return ResultResponseDto<List<StaffProgramMappingResponseDto>>.Failure(new string[] { "There is an error please try later" });
            }
        }       
        public async Task<ResultResponseDto<byte[]>> ExportPrograms(ExportProgramsWithOptionDto request, int userId, UserRole userRole)
        {
            try
            {
                var programs = await _commonService.GetProgramProgressForAdmin(userId, (int)userRole);

                if (programs == null) return ResultResponseDto<byte[]>.Failure(new string[] { "There is an error please try later" });
                IEnumerable<IGrouping<(int ClimateProgramID, string ProgramName, string Location), GetProgramsProgressAdminDto>>
                result =
                    programs.Where(x => request.ClimateProgramIDs==null  || request.ClimateProgramIDs.Count==0 || request.ClimateProgramIDs.Contains(x.ClimateProgramID) || request.IsAllProgram==true)
                    .GroupBy(x => (
                        x.ClimateProgramID,
                        x.ProgramName,
                        x.Location                       
                    )).OrderByDescending(x => x.Average(y => y.PillarProgress));

                var byteRes = MakeProgramPillarSheet(request, result);

                return ResultResponseDto<byte[]>.Success(byteRes, new string[] { "get successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in ExportPrograms", ex);
                return ResultResponseDto<byte[]>.Failure(new string[] { "There is an error please try later" });
            }
        }
        private byte[] MakeProgramPillarSheet(
            ExportProgramsWithOptionDto request,
            IEnumerable<IGrouping<(int ClimateProgramID, string ProgramName, string Location), GetProgramsProgressAdminDto>> programGroups)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Programs Score Report");

            bool isRanking = request.IsRanking == true;

            // ---------------- Header ----------------
            int totalColumns = isRanking ? 5 : 10;

            ws.Range(1, 1, 1, totalColumns).Merge().Value = "Programs Score Report";
            ws.Range(2, 1, 2, totalColumns).Merge().Value = $"Generated On: {DateTime.UtcNow:dd-MMM-yyyy HH:mm}";

            var headerRange = ws.Range(1, 1, 2, totalColumns);
            headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(57, 123, 103);
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            int row = 5;

            // ---------------- Column Header ----------------
            ws.Cell(row, 1).Value = "S.No.";
            ws.Cell(row, 2).Value = "Program Name";
            ws.Cell(row, 3).Value = "Location";           

            if (isRanking)
            {
                ws.Cell(row, 5).Value = "Evaluated - AI Program Score";
            }
            else
            {
                ws.Cell(row, 5).Value = "Pillar Name";
                ws.Cell(row, 6).Value = "Total Score";
                ws.Cell(row, 7).Value = "Total Answers";
                ws.Cell(row, 8).Value = "Evaluated Pillar Score";
                ws.Cell(row, 9).Value = "AI Pillar Score";
                ws.Cell(row, 10).Value = "Evaluated - AI Program Score";
            }

            var header = ws.Range(row, 1, row, totalColumns);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromArgb(57, 123, 103);
            header.Style.Font.FontColor = XLColor.White;

            row++;
            int sno = 1;

            // ---------------- Data ----------------
            foreach (var programGroup in programGroups)
            {
                var programData = programGroup.First();
                var pillars = programGroup.OrderBy(x => x.DisplayOrder).ToList();

                var ProgramProgress = pillars.Average(x => x.PillarProgress);

                // ========================
                // ? RANK-WISE (1 ROW ONLY)
                // ========================
                if (isRanking)
                {
                    ws.Cell(row, 1).Value = sno++;
                    ws.Cell(row, 2).Value = programData.ProgramName;
                    ws.Cell(row, 3).Value = programData.Location;                    
                    ws.Cell(row, 5).Value = $"{ProgramProgress:F2} - {programData.AIProgramProgress:F2}";

                    row++;
                    continue;
                }

                // ========================
                // ? PILLAR-WISE
                // ========================
                int startRow = row;
                bool first = true;

                foreach (var pillar in pillars)
                {
                    ws.Cell(row, 1).Value = sno++;
                    ws.Cell(row, 2).Value = programData.ProgramName;
                    ws.Cell(row, 3).Value = programData.Location;                    

                    ws.Cell(row, 5).Value = pillar.PillarName;
                    ws.Cell(row, 6).Value = pillar.TotalScore;
                    ws.Cell(row, 7).Value = pillar.TotalAns;
                    ws.Cell(row, 8).Value = $"{pillar.PillarProgress:F2}";
                    ws.Cell(row, 9).Value = $"{pillar.AIPillarProgress:F2}";

                    if (first)
                    {
                        ws.Cell(row, 10).Value = $"{ProgramProgress:F2} - {programData.AIProgramProgress:F2}";
                        first = false;
                    }

                    row++;
                }

                int endRow = row - 1;

                // Merge only for pillar mode
                if (endRow > startRow)
                {
                    ws.Range(startRow, 2, endRow, 2).Merge();
                    ws.Range(startRow, 3, endRow, 3).Merge();
                    ws.Range(startRow, 4, endRow, 4).Merge();
                    ws.Range(startRow, 10, endRow, 10).Merge();
                }
            }

            // ---------------- Formatting ----------------
            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(5);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
        #endregion
    }
}
