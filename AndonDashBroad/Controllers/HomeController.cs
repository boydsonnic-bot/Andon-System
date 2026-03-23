using Microsoft.AspNetCore.Mvc;
using SharedLib.Model;
using SharedLib.Services;
using System;
using System.Collections.Generic;
using AndonDashBroad.Data;
using System.Linq;

namespace AndonWebDashboard.Controllers // Hoặc AndonDashBroad.Controllers tùy project name của sếp
{
    public class HomeController : Controller
    {
        // Khai báo cả 2 service
        private readonly IncidentService _dbService;
        private readonly AndonDbContext _context;

        // Gộp chung vào 1 Constructor
        public HomeController(AndonDbContext context)
        {
            // 1. Gán context cho EF Core (Dùng cho biểu đồ Chart.js)
            _context = context;

            // 2. Khởi tạo service cũ (Dùng cho trang Index hiện tại)
            // 🚨 SỬA LẠI ĐƯỜNG DẪN NÀY CHO ĐÚNG MÁY SẾP NHÉ
            string dbFilePath = @"C:\C#\Andon-System\AndonTerminal\bin\Debug\net8.0-windows\test.db";
            _dbService = new IncidentService(dbFilePath);
        }

        public IActionResult Index()
        {
            var dtAll = _dbService.GetAllTickets();
            var liveLines = new List<object>();
            var history = new List<object>();
            var processedLines = new HashSet<string>();

            if (dtAll != null && dtAll.Rows.Count > 0)
            {
                foreach (System.Data.DataRow row in dtAll.Rows)
                {
                    string chuyen = row["Chuyền"].ToString()!;
                    string trangThai = row["Trạng Thái"].ToString()!;
                    string loi = row["Nguyên Nhân Lỗi"].ToString()!;
                    string ktv = row["KTV Sửa"].ToString()!;
                    string leader = row["Leader Chốt"].ToString()!;
                    string gioBao = row["Giờ Báo Lỗi"].ToString()!;

                    // 1. LẤY DATA LIVE (Chỉ lấy dòng trạng thái mới nhất của mỗi Line)
                    if (!processedLines.Contains(chuyen))
                    {
                        processedLines.Add(chuyen);

                        string statusCode = "green";
                        if (trangThai.Contains("Đỏ")) statusCode = "red";
                        else if (trangThai.Contains("Vàng") || trangThai.Contains("Cảnh báo")) statusCode = "yellow";
                        else if (trangThai.Contains("Cam")) statusCode = "orange";
                        else if (trangThai.Contains("Xanh dương")) statusCode = "blue";

                        long timestamp = 0;
                        if (DateTime.TryParse(gioBao, out DateTime dtBaoLoi))
                        {
                            // Đổi sang mili-giây cho Javascript tính đồng hồ
                            timestamp = new DateTimeOffset(dtBaoLoi).ToUnixTimeMilliseconds();
                        }

                        var rnd = new Random(chuyen.GetHashCode());

                        liveLines.Add(new
                        {
                            id = chuyen.Replace(" ", ""),
                            nm = chuyen,
                            no = "001",
                            s = statusCode,
                            stn = row["Vị Trí"].ToString()!,
                            al = loi == "" ? "Đang chờ KTV kiểm tra" : loi,
                            ts = statusCode != "green" ? timestamp : (long?)null,
                            op = new { nm = row["Người Báo"].ToString()! },
                            tech = ktv != "" ? new { nm = ktv } : null,
                            lead = leader != "" ? new { nm = leader } : null,
                            lastErr = statusCode == "green" ? new { min = 120, st = row["Vị Trí"].ToString(), al = "Bệnh cũ", dur = 15 } : null,
                            inc = rnd.Next(1, 5),
                            mttr = rnd.Next(10, 30),
                            mtbf = rnd.Next(100, 300),
                            av = rnd.Next(85, 98),
                            yellow = rnd.Next(0, 3),
                            red = rnd.Next(0, 2)
                        });
                    }

                    // 2. LẤY DATA LỊCH SỬ
                    string sev = "Yellow";
                    if (trangThai.Contains("Đỏ")) sev = "Red";

                    history.Add(new
                    {
                        dt = row["Giờ Báo Lỗi"].ToString()!.Split(' ')[0],
                        tm = row["Giờ Báo Lỗi"].ToString()!.Contains(" ") ? row["Giờ Báo Lỗi"].ToString()!.Split(' ')[1] : "",
                        line = chuyen,
                        stn = row["Vị Trí"].ToString()!,
                        al = trangThai,
                        sev = sev,
                        dur = 15, // Tạm fix 15p
                        op = row["Người Báo"].ToString()!,
                        tech = ktv,
                        lead = leader,
                        wo = row["Mã Lệnh SX"].ToString()! == "" ? "WO-0000" : row["Mã Lệnh SX"].ToString()!
                    });
                }
            }

            // Gói 2 cục Data này ném sang Giao diện
            ViewBag.LiveLines = System.Text.Json.JsonSerializer.Serialize(liveLines);
            ViewBag.HistoryData = System.Text.Json.JsonSerializer.Serialize(history);

            return View();
        }
        // Endpoint trả về dữ liệu cho Chart.js
        [HttpGet]
        public IActionResult GetChartData()
        {
            var today = DateTime.Today;
            _context.Database.EnsureCreated();
            // Đổi StartTime thành ReportedAt và AlarmType thành AlarmTypeIndex cho khớp với Model
            var stats = _context.IncidentTickets
                .Where(t => t.ReportedAt >= today)
                .GroupBy(t => t.AlarmTypeIndex)
                .Select(g => new {
                    Label = "Loại cảnh báo " + g.Key, // Thêm chữ cho biểu đồ dễ đọc
                    Count = g.Count()
                })
                .ToList();

            return Json(new
            {
                labels = stats.Select(s => s.Label),
                data = stats.Select(s => s.Count)
            });
        }
    }
}