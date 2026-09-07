using HornScope.Dtos.PublicDto;
using HornScope.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HornScope.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class PublicController : ControllerBase
    {
        public readonly IPublicService _publicService;
        public PublicController(IPublicService publicService)
        {
            _publicService = publicService;
        }

        [HttpGet("getAllPrograms")]
        public async Task<IActionResult> getAllPrograms()
        {
            var response = await _publicService.GetAllPrograms();
            return Ok(response);
        }

        [HttpGet("GetPartnerProgramsFilterRecord")]
        public async Task<IActionResult> GetPartnerProgramsFilterRecord() => Ok(await _publicService.GetPartnerProgramsFilterRecord());

        [HttpGet]
        [Route("GetAllPillarAsync")]
        public async Task<IActionResult> GetAllPillarAsync() => Ok(await _publicService.GetAllPillarAsync());

        [HttpGet("GetPartnerPrograms")]
        public async Task<IActionResult> GetPartnerPrograms([FromQuery] PartnerProgramRequestDto r)
        {
            var response = await _publicService.GetPartnerPrograms(r);
            return Ok(response);
        }
        [HttpGet("DownloadExecutiveSummeryPdf")]
        public IActionResult DownloadExecutiveSummeryPdf()
        {
            try
            {
                var fileName = "Executive-Summary.pdf";
                // Assuming PDFs are in wwwroot/pdf folder
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "pdf", fileName);

                if (!System.IO.File.Exists(filePath))
                    return NotFound("File not found");

                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                return File(fileBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error");
            }
        }
        [HttpGet("DownloadSummeryReportPdf")]
        public IActionResult DownloadSummeryReportPdf()
        {
            try
            {
                var fileName = "download-summary-report.pdf";
                // Assuming PDFs are in wwwroot/pdf folder
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "pdf", fileName);

                if (!System.IO.File.Exists(filePath))
                    return NotFound("File not found");

                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                return File(fileBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error");
            }
        }
        [HttpGet("programs-WithStaleSupport")]
        public async Task<IActionResult> GetProgramsAndPrograms_WithStaleSupport()
        {
            var data = await _publicService.GetProgramsAndPrograms_WithStaleSupport();
            return Ok(data);
        }

        [HttpGet("promoted-Programs")]
        public async Task<IActionResult> GetPromotedPrograms()
        {
            var data = await _publicService.GetPromotedPrograms();
            return Ok(data);
        }

        [HttpGet("emergingTrendsAndIssues")]
        public async Task<IActionResult> GetEmergingTrendsAndIssues()
        {
            return Ok(await _publicService.GetEmergingTrendsAndIssues());
        }

        [HttpGet("pillarLiveSignals")]
        public async Task<IActionResult> GetPillarLiveSignals()
        {
            return Ok(await _publicService.GetPillarLiveSignals());
        }

        [HttpGet("getResilienceScorecard")]
        public async Task<IActionResult> GetResilienceScorecard()
        {
            var result = await _publicService.GetResilienceScorecard();
            return Ok(result);
        }
    }
}
