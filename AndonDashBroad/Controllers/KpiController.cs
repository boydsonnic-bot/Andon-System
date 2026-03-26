using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;

namespace AndonDashBroad.Controllers
{
    [Route("kpi")]
    public class KpiController : Controller
    {
        // 1. TRANG GIAO DIỆN CHÍNH
        [HttpGet("")]
        public IActionResult Index()
        {
            return View();
        }

        // 2. API LẤY SUMMARY (Cập nhật mỗi 30s)
        [HttpGet("summary")]
        public IActionResult GetSummary([FromQuery] string preset = "today")
        {
            var (from, to) = GetDateRange(preset);
            var lines = new List<object>();
            var rnd = new Random();

            // Giả lập 11 Line (Chờ có bảng thật trong DB thì viết câu SQL SELECT vứt vào đây)
            for (int i = 1; i <= 11; i++)
            {
                string lineName = $"Line {i:D2}";

                int target = 1500;
                int actual = rnd.Next(800, 1600);
                double mrb = Math.Round(rnd.NextDouble() * 5, 2);
                double downMins = rnd.Next(0, 120); // Phút downtime

                // Trạng thái (Green, Yellow, Red)
                string status = downMins > 60 ? "red" : (downMins > 20 ? "yellow" : "green");

                // Cờ cảnh báo AI
                bool hasPattern = rnd.Next(0, 10) > 8;
                bool hasZScore = rnd.Next(0, 10) > 8;
                double ewma = hasPattern ? rnd.Next(15, 45) : 0;

                lines.Add(new
                {
                    line = lineName,
                    status = status,
                    target = target,
                    actual = actual,
                    mrb = mrb,
                    downtime = downMins,
                    ewma = ewma,
                    patternFlag = hasPattern,
                    zscoreFlag = hasZScore
                });
            }

            return Json(new { range = $"{from:dd/MM HH:mm} - {to:dd/MM HH:mm}", data = lines });
        }

        // 3. API LẤY HISTORY ĐỂ VẼ BIỂU ĐỒ TRONG MODAL ZOOM
        [HttpGet("history")]
        public IActionResult GetHistory([FromQuery] string line, [FromQuery] string range = "7d")
        {
            var labels = new[] { "T2", "T3", "T4", "T5", "T6", "T7", "CN" };
            var targets = new[] { 1500, 1500, 1500, 1500, 1500, 1000, 0 };
            var actuals = new[] { 1450, 1320, 1510, 1200, 1490, 950, 0 };
            var downtimes = new[] { 15, 45, 0, 120, 10, 5, 0 };

            return Json(new { labels, targets, actuals, downtimes });
        }

        // HÀM LỌC THỜI GIAN THEO MỐC 08:00 SÁNG
        private (DateTime from, DateTime to) GetDateRange(string preset)
        {
            DateTime now = DateTime.Now;
            DateTime today8Am = now.Date.AddHours(8);
            DateTime from = now < today8Am ? today8Am.AddDays(-1) : today8Am;
            DateTime to = from.AddDays(1).AddTicks(-1);

            switch (preset.ToLower())
            {
                case "week":
                    int diff = (7 + (from.DayOfWeek - DayOfWeek.Monday)) % 7;
                    from = from.AddDays(-1 * diff).Date.AddHours(8);
                    break;
                case "7d": from = from.AddDays(-7); break;
                case "30d": from = from.AddDays(-30); break;
                case "ytd": from = new DateTime(now.Year, 1, 1, 8, 0, 0); break;
                case "lytd":
                    from = new DateTime(now.Year - 1, 1, 1, 8, 0, 0);
                    to = new DateTime(now.Year - 1, now.Month, now.Day, 8, 0, 0);
                    break;
            }
            return (from, to);
        }
    }
}