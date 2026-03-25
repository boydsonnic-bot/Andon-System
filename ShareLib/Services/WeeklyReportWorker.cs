using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using SharedLib.Services;
using System;
using System.Data.SQLite;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AndonDashBroad.Services
{
    public class WeeklyReportWorker : BackgroundService
    {
        private readonly ILogger<WeeklyReportWorker> _logger;
        private readonly string _connStr;
        private readonly EmailSender _email;
        private readonly string[] _to;

        public WeeklyReportWorker(ILogger<WeeklyReportWorker> logger, IConfiguration cfg, EmailSender email)
        {
            _logger = logger;
            _email = email;
            _connStr = cfg.GetConnectionString("DefaultConnection");
            _to = cfg.GetSection("WeeklyReportTo").Get<string[]>()
                  ?? cfg.GetSection("Escalation:WeeklyReportTo").Get<string[]>()
                  ?? Array.Empty<string>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                DateTime now = DateTime.Now;
                DateTime next = NextSunday2300(now);
                await Task.Delay(next - now, stoppingToken);
                try { SendReport(); } catch (Exception ex) { _logger.LogError(ex, "Weekly report"); }
            }
        }

        private DateTime NextSunday2300(DateTime now)
        {
            int days = ((DayOfWeek.Sunday - now.DayOfWeek + 7) % 7);
            var target = now.Date.AddDays(days).AddHours(23);
            if (target <= now) target = target.AddDays(7);
            return target;
        }

        private void SendReport()
        {
            if (_to.Length == 0) return;
            var from = DateTime.Today.AddDays(-7);

            int total = 0, red = 0, yellow = 0;
            using var conn = new SQLiteConnection(_connStr);
            conn.Open();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT COUNT(*) Total,
                           SUM(CASE WHEN Status=2 THEN 1 ELSE 0 END) Red,
                           SUM(CASE WHEN Status=1 THEN 1 ELSE 0 END) Yellow
                    FROM Tickets WHERE ReportedAt >= @from";
                cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd"));
                using var r = cmd.ExecuteReader(); r.Read();
                total = Convert.ToInt32(r["Total"]);
                red = Convert.ToInt32(r["Red"]);
                yellow = Convert.ToInt32(r["Yellow"]);
            }

            var topStations = new StringBuilder();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT StationName, COUNT(*) c
                    FROM Tickets WHERE ReportedAt >= @from
                    GROUP BY StationName
                    ORDER BY c DESC LIMIT 5";
                cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd"));
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    topStations.AppendLine($"<li>{r["StationName"]}: {r["c"]} lỗi</li>");
            }

            string body = $@"
                <h3>Báo cáo tuần Andon</h3>
                <p>Thời gian: {from:yyyy-MM-dd} → {DateTime.Today:yyyy-MM-dd}</p>
                <ul>
                    <li>Tổng sự cố: {total}</li>
                    <li>Đỏ: {red}</li>
                    <li>Vàng: {yellow}</li>
                </ul>
                <p>Top 5 trạm nhiều lỗi:</p>
                <ul>{topStations}</ul>";

            _email.Send(_to, "[Andon] Báo cáo tuần", body);
        }
    }
}