using AndonDashBroad.Services;
using Microsoft.AspNetCore.Mvc;

namespace AndonDashBroad.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AnalyticsController : ControllerBase
    {
        private readonly AnalyticsService _analyticsService;

        public AnalyticsController(AnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        // GET: /api/analytics/ewma?line=Line 01&alpha=0.3
        [HttpGet("ewma")]
        public IActionResult GetEwma([FromQuery] string line, [FromQuery] double alpha = 0.3)
        {
            if (string.IsNullOrEmpty(line)) return BadRequest("Missing line parameter");
            return Ok(_analyticsService.GetEwmaMttr(line, alpha));
        }

        // GET: /api/analytics/zscore?line=Line 01&threshold=2.5
        [HttpGet("zscore")]
        public IActionResult GetZScore([FromQuery] string line, [FromQuery] double threshold = 2.5)
        {
            if (string.IsNullOrEmpty(line)) return BadRequest("Missing line parameter");
            return Ok(_analyticsService.GetZScoreOutliers(line, threshold));
        }

        // GET: /api/analytics/pattern?station=Máy dập 01&n=2
        [HttpGet("pattern")]
        public IActionResult GetPattern([FromQuery] string station, [FromQuery] int n = 2)
        {
            if (string.IsNullOrEmpty(station)) return BadRequest("Missing station parameter");
            if (n < 2 || n > 3) return BadRequest("n-gram must be 2 or 3");
            return Ok(_analyticsService.GetErrorPatterns(station, n));
        }
    }
}