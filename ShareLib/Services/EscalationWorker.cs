using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using SharedLib.Services;
using System;
using System.Data.SQLite;
using System.Threading;
using System.Threading.Tasks;

namespace AndonDashBroad.Services
{
    public class EscalationWorker : BackgroundService
    {
        private readonly ILogger<EscalationWorker> _logger;
        private readonly string _connStr;
        private readonly EmailSender _email;
        private readonly string[] _emailTo;
        private readonly ZaloSender _zalo;
        private readonly string _zaloUserId;
        private readonly int _thresholdMinutes;

        public EscalationWorker(ILogger<EscalationWorker> logger, IConfiguration cfg, EmailSender email, ZaloSender zalo)
        {
            _logger = logger;
            _email = email;
            _zalo = zalo;
            _connStr = cfg.GetConnectionString("DefaultConnection");
            _emailTo = cfg.GetSection("Escalation:EmailTo").Get<string[]>() ?? Array.Empty<string>();
            _zaloUserId = cfg.GetValue<string>("Zalo:DefaultUserId") ?? "";
            _thresholdMinutes = cfg.GetValue<int>("Escalation:ThresholdMinutes", 15);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await ScanAsync(); }
                catch (Exception ex) { _logger.LogError(ex, "Escalation error"); }
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task ScanAsync()
        {
            using var conn = new SQLiteConnection(_connStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, TicketId, LineNumber, StationName, ReportedAt
                FROM Tickets
                WHERE (Status = 1 OR Status = 2)
                  AND EscalationSentAt IS NULL
                  AND julianday('now') - julianday(ReportedAt) >= @mins/1440.0";
            cmd.Parameters.AddWithValue("@mins", _thresholdMinutes);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                long id = r.GetInt64(0);
                string ticketId = r["TicketId"]?.ToString() ?? "";
                string line = r["LineNumber"]?.ToString() ?? "";
                string stn = r["StationName"]?.ToString() ?? "";
                string reported = r["ReportedAt"]?.ToString() ?? "";

                string subject = $"ESCALATION: {line} - {stn}";
                string body = $"Ticket {ticketId}<br/>Line: {line}<br/>Station: {stn}<br/>Reported: {reported}<br/>Chưa xử lý sau {_thresholdMinutes} phút.";
                _email.Send(_emailTo, subject, body);

                if (!string.IsNullOrEmpty(_zaloUserId))
                    await _zalo.SendTextAsync(_zaloUserId, $"[ESC] {line}/{stn} chưa xử lý {_thresholdMinutes}p (Ticket {ticketId})");

                using var upd = conn.CreateCommand();
                upd.CommandText = "UPDATE Tickets SET EscalationSentAt=datetime('now'), EscalationLevel=@lvl WHERE Id=@id";
                upd.Parameters.AddWithValue("@lvl", _thresholdMinutes);
                upd.Parameters.AddWithValue("@id", id);
                upd.ExecuteNonQuery();
            }
        }
    }
}