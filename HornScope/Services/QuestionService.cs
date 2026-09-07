using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using HornScope.Common.Implementation;
using HornScope.Common.Interface;
using HornScope.Common.Models;
using HornScope.Data;
using HornScope.Dtos.AssessmentDto;
using HornScope.Dtos.CommonDto;
using HornScope.Dtos.PillarDto;
using HornScope.Dtos.QuestionDto;
using HornScope.Enums;
using HornScope.IServices;
using HornScope.Models;

namespace HornScope.Services
{
    public class QuestionService : IQuestionService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly ICommonService _commonService;

        public QuestionService(ApplicationDbContext context, IAppLogger appLogger, ICommonService commonService)
        {
            _context = context;
            _appLogger = appLogger;
            _commonService = commonService;
        }

        public async Task<List<GetPillarDTO>> GetPillarsAsync()
        {
            try
            {
                return await _commonService.GetPillars();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetPillarsAsync", ex);
                return new List<GetPillarDTO>();
            }
        }

        public async Task<PaginationResponse<GetQuestionResponse>> GetQuestionsAsync(GetQuestionRequestDto request)
        {
            try
            {
                var query =
                from q in _context.Questions
                    .Include(q => q.Pillar)
                    .Include(o => o.QuestionOptions)
                where !q.IsDeleted
                   && (!request.PillarID.HasValue || q.PillarID == request.PillarID.Value)
                select new
                {
                    q.QuestionID,
                    q.QuestionText,
                    q.PillarID,
                    q.Weight,
                    PillarName = q.Pillar.PillarName,
                    q.DisplayOrder,
                    QuestionOptions = q.QuestionOptions.ToList()
                };

                // Get paginated data
                var pagedData = await query.ApplyPaginationAsync(request);

                // Map to response DTO and calculate WeightID from Weight
                var response = new PaginationResponse<GetQuestionResponse>
                {
                    Data = pagedData.Data.Select(q => new GetQuestionResponse
                    {
                        QuestionID = q.QuestionID,
                        QuestionText = q.QuestionText,
                        PillarID = q.PillarID,
                        Weight = q.Weight,
                        WeightID = QuestionWeightTierExtensions.GetWeightIdFromWeight(q.Weight),
                        PillarName = q.PillarName,
                        DisplayOrder = q.DisplayOrder,
                        QuestionOptions = q.QuestionOptions
                    }).ToList(),
                    TotalRecords = pagedData.TotalRecords,
                    PageNumber = pagedData.PageNumber,
                    PageSize = pagedData.PageSize
                };

                return response;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetQuestionsAsync", ex);
                return new PaginationResponse<GetQuestionResponse>();
            }
        }

        public async Task<Question> AddQuestionAsync(Question q)
        {
            try
            {
                _context.Questions.Add(q);
                await _context.SaveChangesAsync();
                return q;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in AddQuestionAsync", ex);
                return new Question();
            }
        }

        public async Task<Question> EditQuestionAsync(int id, Question q)
        {
            try
            {
                var existing = await _context.Questions.FindAsync(id);
                if (existing == null) return null;
                existing.QuestionText = q.QuestionText;     
                existing.PillarID = q.PillarID;
                existing.DisplayOrder = q.DisplayOrder;
                await _context.SaveChangesAsync();
                return existing;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure", ex);
                return new Question();
            }
        }

        public async Task<bool> DeleteQuestionAsync(int id)
        {
            try
            {
                var q = await _context.Questions.FindAsync(id);
                if (q == null) return false;

                q.IsDeleted = true;
                _context.Questions.Update(q);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure", ex);
                return true;
            }
        }
        public async Task<ResultResponseDto<string>> AddUpdateQuestion(AddUpdateQuestionDto q)
        {
            try
            {
                var pillarQuestions = await _context.Questions
                .Include(x => x.QuestionOptions)
                .Where(x => x.PillarID == q.PillarID)
                .ToListAsync();

                var totalQuestions = pillarQuestions.Count;
                var question = _context.Questions
                    .Include(x => x.QuestionOptions)
                    .FirstOrDefault(x => x.QuestionID == q.QuestionID) ?? new Question();
                if (question.QuestionID > 0 && !pillarQuestions.Select(x => x.QuestionID).Contains(q.QuestionID))
                {
                    question.DisplayOrder = totalQuestions + 1;
                }

                // Common properties
                question.IsDeleted = false;
                question.QuestionText = q.QuestionText;
                question.PillarID = q.PillarID;

                // Get Weight value 
                var tier = (QuestionWeightTier)q.WeightID;
                question.Weight = tier.GetWeight();

                // Sync options (Add / Update / Delete)
                var incomingOptions = q.QuestionOptions ?? new List<QuestionOption>();


                foreach (var o in incomingOptions)
                {
                    var option = question.QuestionOptions.FirstOrDefault(x => x.OptionID == o.OptionID && x.OptionID > 0);

                    if (option == null) // new option
                    {
                        option = new QuestionOption
                        {
                            OptionText = o.OptionText,
                            DisplayOrder = GetDisplayOrder(o),
                            ScoreValue = o.ScoreValue,
                            Question = question
                        };
                        question.QuestionOptions.Add(option);
                    }
                    else // update existing
                    {
                        option.OptionText = o.OptionText;
                        option.DisplayOrder = GetDisplayOrder(o);
                        option.ScoreValue = o.ScoreValue;
                    }
                }

                var optionIdsFromDto = incomingOptions.Select(x => x.OptionID).ToHashSet();
                var optionsToRemove = question.QuestionOptions
                    .Where(x => x.OptionID > 0 && x.ScoreValue != null && !optionIdsFromDto.Contains(x.OptionID))
                    .ToList();

                foreach (var remove in optionsToRemove)
                {
                    _context.QuestionOptions.Remove(remove);
                }

                // Save Question
                if (question.QuestionID > 0)
                    _context.Questions.Update(question);
                else
                    _context.Questions.Add(question);

                await _context.SaveChangesAsync();

                return ResultResponseDto<string>.Success("", new[] { q.QuestionID > 0 ? "Question updated successfully" : "Question saved successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in AddUpdateQuestion", ex);
                return ResultResponseDto<string>.Failure(new string[] { "There is an error please try later" });
            }
        }

        private int GetDisplayOrder(QuestionOption option)
        {
            // Handle special options by OptionText
            if (option.ScoreValue == "N/A")
                return (int)ScoreValue.NA;

            if (option.ScoreValue == "Indeterminate")
                return (int)ScoreValue.Indeterminate;

            // Try to find the ScoreValue using enum extension method
            if (!string.IsNullOrEmpty(option.ScoreValue))
            {
                var displayOrder = ScoreValueExtensions.GetDisplayOrderByScore(option.ScoreValue);
                if (displayOrder.HasValue)
                    return displayOrder.Value;
            }

            // If not found, return max value + 1
            return ScoreValueExtensions.GetMaxDisplayOrder() + 1;
        }

        public async Task<ResultResponseDto<string>> AddBulkQuestion(AddBulkQuestionsDto payload)
        {
            try
            {
                var newQuestions = new List<Question>();
                var pillarIds = payload.Questions.Select(x => x.PillarID).Distinct().ToList();
                var pillarQuestions = await _context.Questions
                    .Where(x => pillarIds.Contains(x.PillarID) && !x.IsDeleted)
                    .ToListAsync();

                // Create a dictionary to track the max display order per pillar
                var pillarQuestionCounts = pillarQuestions
                    .GroupBy(q => q.PillarID)
                    .ToDictionary(g => g.Key, g => g.Count());

                foreach (var q in payload.Questions)
                {
                    if (pillarQuestions.Any(x => x.QuestionText == q.QuestionText && x.PillarID == q.PillarID))
                    {
                        continue;
                    }

                    var question = new Question
                    {
                        IsDeleted = false,
                        QuestionText = q.QuestionText,
                        PillarID = q.PillarID,
                        DisplayOrder = pillarQuestionCounts[q.PillarID]++,
                        QuestionOptions = new List<QuestionOption>()
                    };
                    var tier = (QuestionWeightTier)q.WeightID;
                    question.Weight = tier.GetWeight();
                    // Add provided options
                    foreach (var o in q.QuestionOptions)
                    {
                        var option = new QuestionOption
                        {
                            OptionText = o.OptionText,
                            DisplayOrder = GetDisplayOrder(o),
                            ScoreValue = o.ScoreValue
                        };
                        question.QuestionOptions.Add(option);
                    }

                    newQuestions.Add(question);
                }

                // Add all questions in bulk
                if (newQuestions.Count > 0)
                {
                    await _context.Questions.AddRangeAsync(newQuestions);
                    await _context.SaveChangesAsync();
                }

                return ResultResponseDto<string>.Success("", new[] { newQuestions.Count + " Questions imported successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in AddBulkQuestion", ex);
                return ResultResponseDto<string>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<GetPillarQuestionByProgramResponse>> GetQuestionsByProgramIDAsync(StaffProgramPillerRequestDto request, int userId)
        {
            try
            {
                var valid = _context.StaffProgramMappings.Any(x => x.StaffProgramMappingID == request.StaffProgramMappingID && x.UserID == userId && !x.IsDeleted);
                if (valid)
                {
                    var answeredPillarIds = new List<int>();
                    var assessment = await _context.Assessments
                        .Include(x => x.PillarAssessments).ThenInclude(x => x.Responses)
                        .Where(a => a.StaffProgramMappingID == request.StaffProgramMappingID && a.IsActive)
                        .FirstOrDefaultAsync();
                    if (assessment != null)
                    {
                      answeredPillarIds = assessment?.PillarAssessments
                     .Where(x => x.Responses != null && x.Responses.Any())
                     .OrderByDescending(r => r.Responses
                     .Select(resp => (DateTime?)resp.UpdatedAt).Max())
                     .Select(r => r.PillarID)
                     .ToList() ?? new List<int>();
                    }
                    int pillarCount = (await _commonService.GetPillars()).Count;
                    if (assessment != null && answeredPillarIds.Count == pillarCount && !request.PillarID.HasValue)
                    {
                        request.PillarID = assessment.PillarAssessments.First().PillarID;
                    }
                    
                    if (!request.PillarID.HasValue)
                    {
                        if (answeredPillarIds.Any())
                        {
                            request.PillarID = answeredPillarIds.First();
                        }
                        else
                        {
                            request.PillarID = (await _commonService.GetPillars()).FirstOrDefault()?.PillarID;
                        }
                    }
                    // Calculate the submitted pillar display order from the max pillar ID in answeredPillarIds
                    int submittedPillarDisplayOrder = 0;
                    if (answeredPillarIds.Any())
                    {
                        var maxAnsweredPillarId = answeredPillarIds.Max();
                        var cachedPillars = await _commonService.GetPillars();
                        submittedPillarDisplayOrder = cachedPillars
                            .FirstOrDefault(p => p.PillarID == maxAnsweredPillarId)?.DisplayOrder ?? 0;
                    }

                    // Get next unanswered pillar
                    var selectPillar = await _context.Pillars
                   .Where(x => x.IsActive && !x.IsDeleted && x.PillarID == request.PillarID)
                   .Include(p => p.Questions.Where(x => !x.IsDeleted))
                   .ThenInclude(q => q.QuestionOptions)
                   .FirstOrDefaultAsync();

                    var summitedPillar = (await _commonService.GetPillars())
                        .Where(p => !answeredPillarIds.Contains(p.PillarID))
                        .OrderBy(p => p.DisplayOrder)
                        .FirstOrDefault();

                    if (selectPillar == null || selectPillar?.Questions == null)
                    {
                        return ResultResponseDto<GetPillarQuestionByProgramResponse>.Failure(new[] { "You have submitted assessment for this program" });
                    }

                    var editAssessmentResponse = new Dictionary<int, AssessmentResponse>();
                    if (assessment != null)
                    {
                        editAssessmentResponse = assessment.PillarAssessments
                        .Where(a => a.PillarID == request.PillarID)
                        .SelectMany(x => x.Responses)
                        .ToDictionary(x => x.QuestionID);
                    }

                    // Project questions
                    var questions = selectPillar.Questions
                    .OrderBy(q => q.DisplayOrder)
                    .Select(q =>
                    {
                        var questoin = editAssessmentResponse.TryGetValue(q.QuestionID, out var submittedQuestion);
                        submittedQuestion = submittedQuestion ?? new();
                        return new AssessmentQuestionResponseDto
                        {
                            QuestionID = q.QuestionID,
                            QuestionText = q.QuestionText,
                            PillarID = q.PillarID,
                            ResponseID = submittedQuestion.ResponseID,
                            IsSelected = submittedQuestion.QuestionID == q.QuestionID,
                            QuestionOptions = q.QuestionOptions.Select(x => new QuestionOptionDto
                            {
                                DisplayOrder = x.DisplayOrder,
                                OptionID = x.OptionID,
                                QuestionID = x.QuestionID,
                                IsSelected = submittedQuestion.QuestionOptionID == x.OptionID,
                                OptionText = x.OptionText,
                                ScoreValue = x.ScoreValue,
                                Justification = submittedQuestion.Justification,
                                Source = submittedQuestion.Source
                            }).OrderBy(x=>x.DisplayOrder).ToList(),
                        };
                    }).ToList();

                    var result = new GetPillarQuestionByProgramResponse
                    {
                        AssessmentID = assessment?.AssessmentID ?? 0,
                        StaffProgramMappingID = request.StaffProgramMappingID,
                        PillarName = selectPillar.PillarName,
                        PillarID = selectPillar.PillarID,
                        Description = selectPillar.Description,
                        DisplayOrder = selectPillar.DisplayOrder,
                        SubmittedPillarDisplayOrder = submittedPillarDisplayOrder,
                        Questions = questions
                    };
                    return ResultResponseDto<GetPillarQuestionByProgramResponse>.Success(result, new[] { "get questions successfully" });
                }
                return null;

            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetQuestionsByProgramIDAsync", ex);
                return ResultResponseDto<GetPillarQuestionByProgramResponse>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<Tuple<string, byte[]>> ExportAssessment(int staffProgramMappingID, int userId, UserRole role)
        {
            try
            {

                var fileName = (from m in _context.StaffProgramMappings.Where(us => us.UserID == userId)
                                join p in _context.ClimatePrograms on m.ClimateProgramID equals p.ClimateProgramID
                                join u in _context.Users.Where(x => x.UserID == userId && !x.IsDeleted) on m.UserID equals u.UserID
                                join assignedByUser in _context.Users.Where(x => !x.IsDeleted) on m.AssignedByUserId equals assignedByUser.UserID into assignedUsers
                                from assignedByUser in assignedUsers.DefaultIfEmpty()
                                where m.StaffProgramMappingID == staffProgramMappingID
                                select new
                                {
                                    ProgramName = p.ProgramName,
                                    FullName = u.FullName,
                                    Year = p.Year,
                                    Role = u.Role,
                                    AssignedByUsername = assignedByUser != null ? assignedByUser.FullName : null,
                                    AssignedByRole = assignedByUser.Role
                                }).FirstOrDefault();

                var sheetName = fileName?.ProgramName + "_" + fileName?.FullName;
                var pillarAssessments = _context.Assessments
                    .Include(x => x.PillarAssessments)
                    .ThenInclude(x => x.Responses)
                    .Where(a => a.StaffProgramMappingID == staffProgramMappingID && a.IsActive)
                    .SelectMany(x => x.PillarAssessments).ToList();

                // Get next unanswered pillar
                var nextPillars = await _context.Pillars
                    .Include(p => p.Questions.Where(x => !x.IsDeleted))
                    .ThenInclude(q => q.QuestionOptions)
                    .Where(x => x.IsActive && !x.IsDeleted)
                    .OrderBy(p => p.DisplayOrder)
                    .ToListAsync();
               
                var byteArray = MakePillarSheetClientReadable_Updated(nextPillars, pillarAssessments, staffProgramMappingID, fileName);

                return new(sheetName, byteArray);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in ExportAssessment", ex);
                return new Tuple<string, byte[]>("", Array.Empty<byte>());
            }
        }
        private const int FIRST_Q_ROW = 9;
        private const int ROWS_PER_Q = 4;

        private static readonly XLColor ColHeaderBlue = XLColor.FromArgb(0, 109, 119);
        private static readonly XLColor ColAccentBlue = XLColor.FromArgb(76, 175, 80);
        private static readonly XLColor ColLightBlue = XLColor.FromArgb(126, 200, 207);
        private static readonly XLColor ColRowAlt = XLColor.FromArgb(245, 248, 247);
        private static readonly XLColor ColEditableYellow = XLColor.FromArgb(168, 224, 99);
        private static readonly XLColor ColSeparator = XLColor.FromArgb(228, 228, 228);
        private static readonly XLColor ColInputBorder = XLColor.FromArgb(228, 228, 228);
        private static readonly XLColor ColGrayText = XLColor.FromArgb(74, 95, 98);
        private static readonly XLColor ColDescBg = XLColor.FromArgb(245, 248, 247);
        private static readonly XLColor ColDescBorder = XLColor.FromArgb(0, 90, 98);
        private static readonly XLColor ColTotalBg = XLColor.FromArgb(126, 200, 207);

        private byte[] MakePillarSheetClientReadable_Updated(
             List<Pillar> pillars,
             List<PillarAssessment> pillarAssessments,
             int StaffProgramMappingID,
             dynamic? file)
        {
            using var workbook = new XLWorkbook();

            // Hidden sheet that stores all option texts for dropdown validation.
            // Excel data-validation lists must point to a worksheet range, so we
            // write each question's options into a contiguous block here.
            var optWs = workbook.Worksheets.Add("__OptionData");
            optWs.Visibility = XLWorksheetVisibility.VeryHidden;
            int optRow = 1; // global pointer into __OptionData col A

            foreach (var pillar in pillars)
            {
                if (pillar.Questions.Count == 0) continue;

                var ws = workbook.Worksheets.Add(GetValidSheetName(pillar.PillarName));

                // -- Column widths -------------------------------------
                ws.Column(1).Width = 6;   // #
                ws.Column(2).Width = 72;  // Question
                ws.Column(3).Width = 13;  // Field label
                ws.Column(4).Width = 52;  // Response (dropdown / text)

                // -- Row 1 : Title -------------------------------------
                var title = ws.Range("A1:D1").Merge();
                title.Value = "Veridian Climate Pulse - Program Assessment";
                title.Style.Font.Bold = true;
                title.Style.Font.FontSize = 13;
                title.Style.Font.FontColor = XLColor.White;
                title.Style.Fill.BackgroundColor = ColHeaderBlue;
                title.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                title.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                ws.Row(1).Height = 26;

                // -- Row 2 : Pillar name -------------------------------
                var pillarTitle = ws.Range("A2:D2").Merge();
                pillarTitle.Value = $"Pillar: {pillar.PillarName}";
                pillarTitle.Style.Font.Bold = true;
                pillarTitle.Style.Font.FontSize = 11;
                pillarTitle.Style.Font.FontColor = XLColor.White;
                pillarTitle.Style.Fill.BackgroundColor = ColAccentBlue;
                pillarTitle.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                ws.Row(2).Height = 20;

                // -- Rows 3-4 : Meta -----------------------------------
                ws.Cell(3, 1).Value = "Program:";
                ws.Cell(3, 2).Value = file?.ProgramName?.ToString() ?? "";
                ws.Cell(3, 3).Value = "Program Year:";
                ws.Cell(3, 4).Value = file?.Year;
                ws.Cell(3, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                ws.Cell(4, 1).Value = "AssignedBy:";
                ws.Cell(4, 2).Value = $"{file?.AssignedByUsername ?? ""} ({file?.AssignedByRole ?? ""})";

                foreach (int r in new[] { 3, 4 })
                {
                    var mr = ws.Range(r, 1, r, 4);
                    mr.Style.Fill.BackgroundColor = ColLightBlue;
                    mr.Style.Font.FontSize = 10;
                    ws.Cell(r, 1).Style.Font.Bold = true;
                    ws.Cell(r, 3).Style.Font.Bold = true;
                    ws.Row(r).Height = 18;
                }

                // -- Row 5 : thin gap ----------------------------------
                ws.Row(5).Height = 4;

                // -- Row 6 : Pillar description -------------------------
                var desc = ws.Range("A6:D6").Merge();
                desc.Value = CleanHtml(pillar.Description);
                desc.Style.Fill.BackgroundColor = ColDescBg;
                desc.Style.Font.FontSize = 10;
                desc.Style.Alignment.WrapText = true;
                desc.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                desc.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                desc.Style.Border.OutsideBorderColor = ColDescBorder;
                ws.Row(6).Height = 50;

                // -- Row 7 : thin gap ----------------------------------
                ws.Row(7).Height = 4;

                // -- Row 8 : Column headers ----------------------------
                ws.Cell(8, 1).Value = "#";
                ws.Cell(8, 2).Value = "Question";
                ws.Cell(8, 3).Value = "Field";
                ws.Cell(8, 4).Value = "Response  ?  (select from dropdown)";

                var hdr = ws.Range(8, 1, 8, 4);
                hdr.Style.Font.Bold = true;
                hdr.Style.Font.FontColor = XLColor.White;
                hdr.Style.Fill.BackgroundColor = ColHeaderBlue;
                hdr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                hdr.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                hdr.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                ws.Row(8).Height = 20;

                // Freeze rows 1-8 so header stays on screen while scrolling
                ws.SheetView.FreezeRows(8);

                // -- Questions -----------------------------------------
                int row = FIRST_Q_ROW;
                int sno = 1;
                var scoreRowNums = new List<int>(); // rows that carry a score (Col J formula)

                var submittedResponses = pillarAssessments
                    .Where(x => x.PillarID == pillar.PillarID)
                    .SelectMany(x => x.Responses)
                    .ToDictionary(x => x.QuestionID);

                foreach (var q in pillar.Questions.OrderBy(x => x.DisplayOrder))
                {
                    submittedResponses.TryGetValue(q.QuestionID, out var ans);
                    ans ??= new();

                    bool isEven = (sno % 2 == 0);
                    XLColor qBg = isEven ? ColRowAlt : XLColor.White;

                    // -- Build option texts (score desc first, then N/A / Unknown) --
                    var options = (q.QuestionOptions ?? new List<QuestionOption>())
                                  .OrderBy(x => x.DisplayOrder)
                                  .ToList();

                    var optionTexts = options.Select(opt =>
                    {
                        string prefix = !string.IsNullOrEmpty(opt.ScoreValue) ? $"{opt.ScoreValue} - " : "";
                        return (prefix + opt.OptionText.Trim()).Trim();
                    }).ToList();

                    // Write option texts to hidden sheet and record range
                    int qOptStart = optRow;
                    foreach (var txt in optionTexts)
                        optWs.Cell(optRow++, 1).Value = txt;
                    int qOptEnd = optRow - 1;   // inclusive end row in __OptionData

                    // -- Create a workbook-level Named Range for this question's options.
                    //    ClosedXML does NOT support cross-sheet sheet-references directly
                    //    in dv.Value ("'Sheet'!$A$1:$A$7" silently fails).
                    //    A Named Range IS recognised by Excel and works correctly.
                    string namedRangeKey = $"Opts_Q{q.QuestionID}";
                    // Remove any stale definition (e.g. re-export after code change)
                    if (workbook.NamedRanges.Contains(namedRangeKey))
                        workbook.NamedRanges.Delete(namedRangeKey);
                    workbook.NamedRanges.Add(namedRangeKey,
                        optWs.Range(qOptStart, 1, qOptEnd, 1));

                    // -- Current answer text (pre-fill if data exists) --
                    string currentAnswer = "";
                    if (ans.QuestionOptionID > 0)
                    {
                        var sel = options.FirstOrDefault(x => x.OptionID == ans.QuestionOptionID);
                        if (sel != null)
                        {
                            string prefix = !string.IsNullOrEmpty(sel.ScoreValue) ? $"{sel.ScoreValue} - " : "";
                            currentAnswer = (prefix + sel.OptionText.Trim()).Trim();
                        }
                    }

                    int ansRow = row;
                    scoreRowNums.Add(ansRow);

                    // -- Col A : S.No ----------------------------------
                    ws.Cell(ansRow, 1).Value = sno++;
                    ws.Cell(ansRow, 1).Style.Font.Bold = true;
                    ws.Cell(ansRow, 1).Style.Font.FontColor = XLColor.White;
                    ws.Cell(ansRow, 1).Style.Fill.BackgroundColor = ColAccentBlue;
                    ws.Cell(ansRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Cell(ansRow, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

                    // -- Col B : Question text -------------------------
                    ws.Cell(ansRow, 2).Value = q.QuestionText;
                    ws.Cell(ansRow, 2).Style.Font.Bold = true;
                    ws.Cell(ansRow, 2).Style.Font.FontSize = 10;
                    ws.Cell(ansRow, 2).Style.Fill.BackgroundColor = qBg;
                    ws.Cell(ansRow, 2).Style.Alignment.WrapText = true;
                    ws.Cell(ansRow, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

                    // -- Col C : Label ---------------------------------
                    ws.Cell(ansRow, 3).Value = "Answer";
                    ws.Cell(ansRow, 3).Style.Font.Bold = true;
                    ws.Cell(ansRow, 3).Style.Font.FontColor = ColAccentBlue;
                    ws.Cell(ansRow, 3).Style.Fill.BackgroundColor = qBg;
                    ws.Cell(ansRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    ws.Cell(ansRow, 3).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

                    // -- Col D : Dropdown answer cell ------------------
                    var ansCell = ws.Cell(ansRow, 4);
                    ws.Column(4).Width = 89;
                    ws.Row(ansRow).AdjustToContents();
                    ansCell.Value = currentAnswer;
                    ansCell.Style.Fill.BackgroundColor = ColEditableYellow;
                    ansCell.Style.Alignment.WrapText = true;
                    ansCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                    ansCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    ansCell.Style.Border.OutsideBorderColor = ColAccentBlue;

                    // DATA VALIDATION - list via Named Range (cross-sheet refs don't work in ClosedXML dv.Value)
                    if (optionTexts.Any())
                    {
                        var dv = ansCell.GetDataValidation();
                        dv.Clear();
                        dv.AllowedValues = XLAllowedValues.List;
                        // Reference the Named Range we created above - this IS supported by ClosedXML
                        // and produces a real clickable dropdown arrow in Excel / LibreOffice.
                        dv.Value = namedRangeKey;
                        dv.IgnoreBlanks = true;
                        dv.ShowInputMessage = true;
                        dv.InputTitle = "Select Answer";
                        dv.InputMessage = "Click the dropdown arrow ? to choose a score option.";
                        dv.ShowErrorMessage = true;
                        dv.ErrorTitle = "Invalid Entry";
                        dv.ErrorMessage = "Please select a value from the dropdown list provided.";
                        dv.ErrorStyle = XLErrorStyle.Warning; // warn but allow manual override
                    }

                    ws.Row(ansRow).Height = 45;

                    // -- Col J (hidden) : Numeric score formula ---------
                    // Extracts the leading digit from the dropdown text (e.g. "3 - Good...") ? 3
                    // Returns "" for N/A, Unknown, or blank


                    ws.Cell(ansRow, 10).FormulaA1 =
                        $"=IF(D{ansRow}=\"\",\"\",IFERROR(VALUE(LEFT(D{ansRow},FIND(\" -\",D{ansRow})-1)),\"\"))";
                    // -- Comment row (row+1) ---------------------------
                    int commentRow = ansRow + 1;

                    ws.Cell(commentRow, 1).Style.Fill.BackgroundColor = qBg;
                    ws.Cell(commentRow, 2).Style.Fill.BackgroundColor = qBg;
                    ws.Cell(commentRow, 2).Value = "Justification / supporting evidence:";
                    ws.Cell(commentRow, 2).Style.Font.Italic = true;
                    ws.Cell(commentRow, 2).Style.Font.FontColor = ColGrayText;
                    ws.Cell(commentRow, 2).Style.Font.FontSize = 9;
                    ws.Cell(commentRow, 2).Style.Alignment.WrapText = true;
                    ws.Cell(commentRow, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

                    ws.Cell(commentRow, 3).Value = "Comment";
                    ws.Cell(commentRow, 3).Style.Font.Bold = true;
                    ws.Cell(commentRow, 3).Style.Font.FontColor = ColGrayText;
                    ws.Cell(commentRow, 3).Style.Fill.BackgroundColor = qBg;
                    ws.Cell(commentRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    ws.Cell(commentRow, 3).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

                    ws.Cell(commentRow, 4).Value = ans.Justification ?? "";
                    ws.Cell(commentRow, 4).Style.Alignment.WrapText = true;
                    ws.Cell(commentRow, 4).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                    ws.Cell(commentRow, 4).Style.Fill.BackgroundColor = XLColor.FromArgb(252, 252, 252);
                    ws.Cell(commentRow, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    ws.Cell(commentRow, 4).Style.Border.OutsideBorderColor = ColInputBorder;
                    ws.Row(commentRow).Height = 40;

                    // -- Source row (row+2) - also carries hidden IDs --
                    int sourceRow = ansRow + 2;

                    ws.Cell(sourceRow, 1).Style.Fill.BackgroundColor = qBg;
                    ws.Cell(sourceRow, 2).Style.Fill.BackgroundColor = qBg;

                    ws.Cell(sourceRow, 3).Value = "Source";
                    ws.Cell(sourceRow, 3).Style.Font.Bold = true;
                    ws.Cell(sourceRow, 3).Style.Font.FontColor = ColGrayText;
                    ws.Cell(sourceRow, 3).Style.Fill.BackgroundColor = qBg;
                    ws.Cell(sourceRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    ws.Cell(sourceRow, 3).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

                    ws.Cell(sourceRow, 4).Value = ans.Source ?? "";
                    ws.Cell(sourceRow, 4).Style.Alignment.WrapText = true;
                    ws.Cell(sourceRow, 4).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                    ws.Cell(sourceRow, 4).Style.Fill.BackgroundColor = XLColor.FromArgb(252, 252, 252);
                    ws.Cell(sourceRow, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    ws.Cell(sourceRow, 4).Style.Border.OutsideBorderColor = ColInputBorder;
                    ws.Row(sourceRow).Height = 25;

                    // Hidden IDs (cols K-O = 11-15)
                    ws.Cell(sourceRow, 11).Value = StaffProgramMappingID;
                    ws.Cell(sourceRow, 12).Value = pillar.PillarID;
                    ws.Cell(sourceRow, 13).Value = q.QuestionID;
                    ws.Cell(sourceRow, 14).Value = ans.QuestionOptionID;
                    ws.Cell(sourceRow, 15).Value = ans.ResponseID;

                    // -- Separator row (row+3) -------------------------
                    int sepRow = ansRow + 3;
                    ws.Range(sepRow, 1, sepRow, 4).Style.Fill.BackgroundColor = ColSeparator;
                    ws.Row(sepRow).Height = 3;

                    row += ROWS_PER_Q;
                }

                // -- Totals section ------------------------------------
                row++; // one blank row gap

                // Helper to build comma-separated J-column references
                string JRefs() => string.Join(",", scoreRowNums.Select(r => $"J{r}"));

                // Total Score row
                ws.Cell(row, 3).Value = "Total Score";
                ws.Cell(row, 3).Style.Font.Bold = true;
                ws.Cell(row, 3).Style.Font.FontSize = 11;
                ws.Cell(row, 3).Style.Font.FontColor = ColHeaderBlue;
                ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                string totalF = scoreRowNums.Any() ? $"=SUM({JRefs()})" : "=0";
                ws.Cell(row, 4).FormulaA1 = totalF;
                ws.Cell(row, 4).Style.Font.Bold = true;
                ws.Cell(row, 4).Style.Font.FontSize = 13;
                ws.Cell(row, 4).Style.Fill.BackgroundColor = ColTotalBg;
                ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                ws.Cell(row, 4).Style.Border.OutsideBorderColor = ColAccentBlue;
                ws.Range(row, 1, row, 3).Style.Fill.BackgroundColor = ColTotalBg;
                ws.Row(row).Height = 24;
                row++;

                // Average Score row
                ws.Cell(row, 3).Value = "Average Score";
                ws.Cell(row, 3).Style.Font.Bold = true;
                ws.Cell(row, 3).Style.Font.FontColor = ColHeaderBlue;
                ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                // SUM/COUNT approach works reliably with non-contiguous cells
                string avgF = scoreRowNums.Any()
                    ? $"=IFERROR(SUM({JRefs()})/COUNT({JRefs()}),\"\")"
                    : "=\"\"";
                ws.Cell(row, 4).FormulaA1 = avgF;
                ws.Cell(row, 4).Style.Font.Bold = true;
                ws.Cell(row, 4).Style.NumberFormat.Format = "0.00";
                ws.Cell(row, 4).Style.Fill.BackgroundColor = ColTotalBg;
                ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                ws.Cell(row, 4).Style.Border.OutsideBorderColor = ColAccentBlue;
                ws.Range(row, 1, row, 3).Style.Fill.BackgroundColor = ColTotalBg;
                ws.Row(row).Height = 22;
                row++;

                // ValidAnswered / Total count row
                ws.Cell(row, 3).Value = "Valid Answers";
                ws.Cell(row, 3).Style.Font.Bold = true;
                ws.Cell(row, 3).Style.Font.FontColor = ColGrayText;
                ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                // DRefs: comma-joined D-column answer cells

                string answeredF = scoreRowNums.Any()
                    ? $"=COUNT({JRefs()}) & \" / {scoreRowNums.Count}\""
                    : "=\"0 / 0\"";

                ws.Cell(row, 4).FormulaA1 = answeredF;

                ws.Cell(row, 4).Style.Fill.BackgroundColor = ColTotalBg;
                ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range(row, 1, row, 3).Style.Fill.BackgroundColor = ColTotalBg;
                ws.Row(row).Height = 20;

                // -- Hide auxiliary columns ----------------------------
                ws.Column(10).Hide(); // Score formula (J)
                ws.Column(11).Hide(); // userCityMappingID
                ws.Column(12).Hide(); // pillarID
                ws.Column(13).Hide(); // questionID
                ws.Column(14).Hide(); // questionOptionID
                ws.Column(15).Hide(); // responseID
            }

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }
        string CleanHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;

            html = html.Replace("&nbsp;", " ")
                       .Replace("<br>", "\n")
                       .Replace("<br/>", "\n")
                       .Replace("<p>", "")
                       .Replace("</p>", "\n");

            // remove any remaining tags
            return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", "").Trim();
        }

        private string GetValidSheetName(string safeName)
        {
            // Trim to 31 characters
            if (safeName.Length > 31)
                safeName = safeName.Substring(0, 31);

            // Remove illegal characters
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(c.ToString(), "");
            }
            safeName = safeName.Replace("[", "").Replace("]", "").Replace(":", "")
                               .Replace("*", "").Replace("?", "").Replace("/", "").Replace("\\", "");

            return safeName;
        }
        public async Task<ResultResponseDto<List<QuestionsByUserPillarsResponsetDto>>> GetQuestionsHistoryByPillar(GetProgramPillarHistoryRequestDto requestDto, UserRole userRole)
        {
            try
            {
                // =========================
                // 1. PILLAR + QUESTIONS
                // =========================
                var pillar = await _context.Pillars
                    .Where(x => x.IsActive && !x.IsDeleted)
                    .Include(x => x.Questions)
                        .ThenInclude(x => x.QuestionOptions)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.PillarID == requestDto.PillarID.Value);

                if (pillar == null)
                {
                    return ResultResponseDto<List<QuestionsByUserPillarsResponsetDto>>
                        .Failure(new[] { "Pillar not found" });
                }

                // =========================
                // 2. USER MAPPINGS
                // =========================
                var userMappings = await _context.StaffProgramMappings
                    .Where(x => x.ClimateProgramID == requestDto.ClimateProgramID
                                && !x.IsDeleted
                                && (x.AssignedByUserId == requestDto.UserID
                                    || x.UserID == requestDto.UserID
                                    || userRole == UserRole.Admin))
                    .AsNoTracking()
                    .ToListAsync();

                var mappingIds = userMappings.Select(x => x.StaffProgramMappingID).ToList();

                // =========================
                // 3. ASSESSMENTS
                // =========================
                var assessments = await _context.Assessments
                    .Include(x => x.StaffProgramMapping)
                    .Include(a => a.PillarAssessments
                        .Where(pa => pa.PillarID == requestDto.PillarID))
                    .ThenInclude(pa => pa.Responses)
                    .Where(a => mappingIds.Contains(a.StaffProgramMappingID)
                                && a.IsActive)
                    .AsNoTracking()
                    .ToListAsync();

                var userIds = assessments.Select(x => x.StaffProgramMapping.UserID).Distinct().ToList();

                // =========================
                // 4. USERS DICTIONARY
                // =========================
                var users = await _context.Users
                    .Where(x => userIds.Contains(x.UserID))
                    .AsNoTracking()
                    .ToDictionaryAsync(x => x.UserID);

                // =========================
                // 5. USER RESPONSES
                // =========================
                var responsesByUser = assessments
                    .GroupBy(a => a.StaffProgramMapping.UserID)
                    .ToDictionary(
                        g => g.Key,
                        g => g.SelectMany(a => a.PillarAssessments)
                              .SelectMany(pa => pa.Responses)
                              .ToDictionary(r => r.QuestionID)
                    );

                // =========================
                // 6. AI DATA (NEW)
                // =========================
                var aiRaw = await _context.AIEstimatedQuestionScores
                    .Where(x => x.ClimateProgramID == requestDto.ClimateProgramID
                                && x.PillarID == requestDto.PillarID)
                    .ToListAsync();

                var aiDict = aiRaw
                    .GroupBy(x => x.QuestionID)
                    .ToDictionary(
                        g => g.Key,
                        g => new
                        {
                            Score = Math.Round(g.Average(x => x.AIScore ?? 0), 0),
                            Progress = g.Average(x => x.AIProgress ?? 0),
                            EvidenceSummary = g.FirstOrDefault()?.EvidenceSummary ?? ""
                        }
                    );

                // =========================
                // 7. FINAL RESULT
                // =========================
                var pillarResponses = pillar.Questions
                    .OrderBy(q => q.DisplayOrder)
                    .Select(q =>
                    {
                        var userInfos = userIds.Select(uid =>
                        {
                            users.TryGetValue(uid, out var u);
                            responsesByUser.TryGetValue(uid, out var userResponseDict);

                            (userResponseDict ?? new()).TryGetValue(q.QuestionID, out var response);

                            var optionID = response?.QuestionOptionID;
                            var option = q.QuestionOptions.FirstOrDefault(x => x.OptionID == optionID);

                            return new QuestionsByUserInfo
                            {
                                UserID = uid,
                                FullName = u?.FullName ?? string.Empty,
                                Score = response != null ? (int?)response.Score : null,
                                OptionText = option?.OptionText ?? "",
                                Justification = response?.Justification ?? string.Empty
                            };
                        }).ToList();

                        // ? ADD AI RESULT ROW
                        if (aiDict.TryGetValue(q.QuestionID, out var ai))
                        {
                            var option = q.QuestionOptions.FirstOrDefault(x => x.ScoreValue == ai.Score.ToString());


                            userInfos.Insert(0, new QuestionsByUserInfo
                            {
                                UserID = int.MaxValue,
                                FullName = "AI_Result",
                                Score = (int?)ai.Score,
                                OptionText = option?.OptionText ?? "OptionText",
                                Justification = ai.EvidenceSummary
                            });
                        }
                        else
                        {
                            // default AI row
                            userInfos.Insert(0, new QuestionsByUserInfo
                            {
                                UserID = int.MaxValue,
                                FullName = "AI_Result",
                                Score = null,
                                OptionText = "",
                                Justification = ""
                            });
                        }

                        return new QuestionsByUserPillarsResponsetDto
                        {
                            QuestionID = q.QuestionID,
                            PillarID = q.PillarID,
                            QuestionText = q.QuestionText,
                            DisplayOrder = q.DisplayOrder,
                            Users = userInfos
                        };
                    })
                    .ToList();

                return ResultResponseDto<List<QuestionsByUserPillarsResponsetDto>>
                    .Success(pillarResponses, Array.Empty<string>());
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetQuestionsHistoryByPillar", ex);

                return ResultResponseDto<List<QuestionsByUserPillarsResponsetDto>>
                    .Failure(new[] { "There was an error, please try again later" });
            }
        }

        public async Task<ResultResponseDto<GetPillarQuestionByProgramResponse>> GetQuestionsByProgramMappingIdForAnalyst(
            StaffProgramPillerRequestDto request, int userId)
        {
            try
            {
                var staffProgramMappings = await _context.StaffProgramMappings
                    .FirstOrDefaultAsync(x => x.StaffProgramMappingID == request.StaffProgramMappingID
                                           && x.UserID == userId
                                           && !x.IsDeleted);

                if (staffProgramMappings == null)
                    return ResultResponseDto<GetPillarQuestionByProgramResponse>.Failure(
                        new[] { "Invalid request" });
                
                Expression<Func<Assessment, bool>> predicate = a =>
                                    a.StaffProgramMappingID == request.StaffProgramMappingID &&
                                    a.IsActive;

                // Load assessment with related data
                var assessment = await _context.Assessments
                   .Include(x => x.PillarAssessments)
                   .ThenInclude(x => x.Responses)
                   .FirstOrDefaultAsync(predicate);

                var answeredPillarIds = assessment?.PillarAssessments
                     .Where(x => x.Responses != null && x.Responses.Any())
                     .OrderByDescending(r => r.Responses
                     .Select(resp => (DateTime?)resp.UpdatedAt).Max())
                     .Select(r => r.PillarID)
                     .ToList() ?? new List<int>();


                int pillarCount = (await _commonService.GetPillars()).Count;
                
                if (assessment != null && answeredPillarIds.Count == pillarCount && !request.PillarID.HasValue)
                    request.PillarID = assessment.PillarAssessments.First().PillarID;

                if (!request.PillarID.HasValue)
                {
                    if (answeredPillarIds.Any())
                    {
                        request.PillarID = answeredPillarIds.First();
                    }
                    else
                    {
                        request.PillarID = (await _commonService.GetPillars()).FirstOrDefault()?.PillarID;
                    }
                }

                // Calculate the submitted pillar display order from the max pillar ID in answeredPillarIds
                int submittedPillarDisplayOrder = 0;
                if (answeredPillarIds.Any())
                {
                    var maxAnsweredPillarId = answeredPillarIds.Max();
                    var cachedPillars = await _commonService.GetPillars();
                    submittedPillarDisplayOrder = cachedPillars
                        .FirstOrDefault(p => p.PillarID == maxAnsweredPillarId)?.DisplayOrder ?? 0;
                }

                // Get the target pillar (next unanswered or specific)
                var selectPillar = await _context.Pillars
                   .Where(x => x.IsActive && !x.IsDeleted && x.PillarID == request.PillarID)
                   .Include(p => p.Questions.Where(x => !x.IsDeleted))
                   .ThenInclude(q => q.QuestionOptions)
                   .FirstOrDefaultAsync();

                if (selectPillar == null || !selectPillar.Questions.Any())
                    return ResultResponseDto<GetPillarQuestionByProgramResponse>.Failure(
                        new[] { "You have submitted assessment for this program" });

                // Build lookup for existing responses for the selected pillar
                var editAssessmentResponse = assessment?.PillarAssessments
                    .Where(a => a.PillarID == request.PillarID)
                    .SelectMany(x => x.Responses)
                    .GroupBy(x => x.QuestionID)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First())
                    ?? new Dictionary<int, AssessmentResponse>();

                // Project questions with pre-filled answers
                var questions = selectPillar.Questions
                    .OrderBy(q => q.DisplayOrder)
                    .Select(q =>
                    {
                        editAssessmentResponse.TryGetValue(q.QuestionID, out var submitted);
                        submitted ??= new AssessmentResponse();

                        return new AssessmentQuestionResponseDto
                        {
                            QuestionID = q.QuestionID,
                            QuestionText = q.QuestionText,
                            PillarID = q.PillarID,
                            ResponseID = submitted.ResponseID,
                            IsSelected = submitted.QuestionID == q.QuestionID,
                            History = new List<HistoryQuestionAnswerRawDto>(),
                            QuestionOptions = q.QuestionOptions.Select(x => new QuestionOptionDto
                            {
                                DisplayOrder = x.DisplayOrder,
                                OptionID = x.OptionID,
                                QuestionID = x.QuestionID,
                                IsSelected = submitted.QuestionOptionID == x.OptionID,
                                OptionText = x.OptionText,
                                ScoreValue = x.ScoreValue,
                                Justification = submitted.QuestionOptionID == x.OptionID ? submitted.Justification : string.Empty,
                                Source = submitted.QuestionOptionID == x.OptionID ? submitted.Source : string.Empty
                            }).OrderBy(x=>x.DisplayOrder).ToList()
                        };
                    }).ToList();

                // Load program-level user mappings assigned by this analyst
                var staffProgramMappingsList = await _context.StaffProgramMappings
                    .Where(x => x.ClimateProgramID == staffProgramMappings.ClimateProgramID
                             && x.AssignedByUserId == staffProgramMappings.UserID)
                    .AsNoTracking()
                    .ToListAsync();

                var mappingIds = staffProgramMappingsList.Select(x => x.StaffProgramMappingID).ToList();
                var userIds = staffProgramMappingsList.Select(x => x.UserID).ToList();

                var users = await _context.Users
                    .Where(x => userIds.Contains(x.UserID))
                    .AsNoTracking()
                    .ToDictionaryAsync(x => x.UserID);

                // Load analyst responses for this pillar
                var analystResponses = await _context.Assessments
                    .Where(a => mappingIds.Contains(a.StaffProgramMappingID)
                             && a.IsActive)
                    .SelectMany(a => a.PillarAssessments
                        .Where(pa => pa.PillarID == selectPillar.PillarID)
                        .SelectMany(pa => pa.Responses
                            .Where(r => r != null)
                            .Select(r => new HistoryQuestionAnswerRawDto
                            {
                                UserID = a.StaffProgramMapping.UserID,
                                QuestionID = r.QuestionID,
                                OptionID = r.QuestionOptionID,
                                ScoreValue = (int?)r.Score,
                                Progress = null,
                                Justification = r.Justification ?? "",
                                Source = r.Source ?? ""
                            })))
                    .AsNoTracking()
                    .ToListAsync();

                // Enrich analyst responses with full names
                foreach (var r in analystResponses)
                    r.FullName = users.TryGetValue(r.UserID, out var u) ? u?.FullName ?? "" : "";

                // Load AI estimated scores for this pillar
                var aiRawData = await _context.AIEstimatedQuestionScores
                    .Where(x => x.ClimateProgramID == staffProgramMappings.ClimateProgramID
                             && x.PillarID == selectPillar.PillarID)
                    .AsNoTracking()
                    .ToListAsync();

                var aiResponses = aiRawData.Select(x => new HistoryQuestionAnswerRawDto
                {
                    UserID = 0,
                    QuestionID = x.QuestionID,
                    OptionID = null,
                    ScoreValue = (int?)x.AIScore,
                    Progress = x.AIProgress ?? 0,
                    Justification = x.EvidenceSummary ?? "",
                    Source = $"{x.SourceDataExtract ?? ""} {x.SourceURL ?? ""}".Trim(),
                    FullName = "AI"
                }).ToList();

                // Combine analyst + AI responses and map onto each question's history
                var combined = analystResponses.Concat(aiResponses).ToList();

                foreach (var question in questions)
                {
                    var relatedEntries = combined.Where(x => x.QuestionID == question.QuestionID);

                    foreach (var entry in relatedEntries)
                    {
                        var option = question.QuestionOptions.FirstOrDefault(x => x.OptionID == entry.OptionID || x.ScoreValue == entry.ScoreValue.ToString());
                        question.History.Add(new HistoryQuestionAnswerRawDto
                        {
                            UserID = entry.UserID,
                            FullName = entry.FullName,
                            ScoreValue = entry.ScoreValue,
                            Justification = entry.Justification,
                            OptionID = option?.OptionID,
                            OptionText = option?.OptionText ?? "",
                            Source = entry.Source
                        });
                    }
                }

                var result = new GetPillarQuestionByProgramResponse
                {
                    AssessmentID = assessment?.AssessmentID ?? 0,
                    StaffProgramMappingID = request.StaffProgramMappingID,
                    PillarName = selectPillar.PillarName,
                    PillarID = selectPillar.PillarID,
                    Description = selectPillar.Description,
                    DisplayOrder = selectPillar.DisplayOrder,
                    SubmittedPillarDisplayOrder = submittedPillarDisplayOrder,
                    Questions = questions
                };

                return ResultResponseDto<GetPillarQuestionByProgramResponse>.Success(
                    result, new[] { "get questions successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetQuestionsByProgramMappingIdForAnalyst", ex);
                return ResultResponseDto<GetPillarQuestionByProgramResponse>.Failure(
                    new[] { "There is an error please try later" });
            }
        }
    }
}