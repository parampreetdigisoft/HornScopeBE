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

        [HttpGet("getAllCountries")]
        public async Task<IActionResult> GetAllCountries()
        {
            var response = await _publicService.GetAllCountries();
            return Ok(response);
        }

        [HttpGet]
        [Route("GetAllPillarAsync")]
        public async Task<IActionResult> GetAllPillarAsync() => Ok(await _publicService.GetAllPillarAsync());

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
        [HttpGet("countries-Countries")]
        public async Task<IActionResult> GetCountriesCountries()
        {
            var data = await _publicService.GetCountriesAndCountries_WithStaleSupport();
            return Ok(data);
        }

        [HttpGet("promoted-Countries")]
        public async Task<IActionResult> GetPromotedCountries()
        {
            var data = await _publicService.GetPromotedCountries();
            return Ok(data);
        }


        [HttpGet("emergingTrendsAndIssues")]
        public async Task<IActionResult> GetEmergingTrendsAndIssues([FromQuery] int countryCount = 8)
        {
            return Ok(await _publicService.GetEmergingTrendsAndIssues(countryCount));
        }

        [HttpGet("pillarLiveSignals")]
        public async Task<IActionResult> GetPillarLiveSignals()
        {
            return Ok(await _publicService.GetPillarLiveSignals());
        }


        [HttpGet("overAllHornscopeScore")]
        public async Task<IActionResult> GetOverAllHornscopeScore()
        {
            return Ok(await _publicService.GetOverAllHornscopeScore());
        }

        [HttpGet("pillarOverview")]
        public async Task<IActionResult> GetPillarOverview()
        {
            return Ok(await _publicService.GetPillarOverview());
        }
    }
}
