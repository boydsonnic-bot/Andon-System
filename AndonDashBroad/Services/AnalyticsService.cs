using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;

namespace AndonDashBroad.Services
{
    public class AnalyticsService
    {
        private readonly string _connStr;

        public AnalyticsService(IConfiguration cfg)
        {
            _connStr = cfg.GetConnectionString("DefaultConnection") ?? "";
        }

        // 1. THUẬT TOÁN EWMA (Dự đoán MTTR trung bình trượt)
        public object GetEwmaMttr(string lineName, double alpha)
        {
            var repairTimes = new List<double>();

            using (var conn = new SQLiteConnection(_connStr))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT (julianday(TechFixedAt) - julianday(TechCheckinAt)) * 1440 AS RepairMins
                    FROM Tickets 
                    WHERE LineNumber = @line AND Status = 5 AND TechFixedAt IS NOT NULL AND TechCheckinAt IS NOT NULL
                    ORDER BY TechFixedAt ASC";
                cmd.Parameters.AddWithValue("@line", lineName);

                using var r = cmd.ExecuteReader();
                while (r.Read()) repairTimes.Add(Convert.ToDouble(r["RepairMins"]));
            }

            if (repairTimes.Count == 0) return new { Line = lineName, EwmaMttr = 0, DataPoints = 0 };

            double ewma = repairTimes[0]; // Khởi tạo bằng giá trị đầu tiên
            for (int i = 1; i < repairTimes.Count; i++)
            {
                ewma = (alpha * repairTimes[i]) + ((1 - alpha) * ewma);
            }

            return new { Line = lineName, EwmaMttr = Math.Round(ewma, 2), DataPoints = repairTimes.Count };
        }

        // 2. THUẬT TOÁN Z-SCORE (Phát hiện thời gian chết bất thường)
        public object GetZScoreOutliers(string lineName, double threshold)
        {
            var downtimes = new List<TicketDowntime>();

            using (var conn = new SQLiteConnection(_connStr))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT TicketId, StationName, ErrorReason, ReportedAt,
                           (julianday(LeaderConfirmedAt) - julianday(ReportedAt)) * 1440 AS DownMins
                    FROM Tickets 
                    WHERE LineNumber = @line AND Status = 5 AND LeaderConfirmedAt IS NOT NULL
                    ORDER BY ReportedAt DESC";
                cmd.Parameters.AddWithValue("@line", lineName);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    downtimes.Add(new TicketDowntime
                    {
                        TicketId = r["TicketId"].ToString(),
                        StationName = r["StationName"].ToString(),
                        ErrorReason = r["ErrorReason"].ToString(),
                        ReportedAt = Convert.ToDateTime(r["ReportedAt"]),
                        DowntimeMins = Convert.ToDouble(r["DownMins"])
                    });
                }
            }

            if (downtimes.Count < 2) return new { Mean = 0, Outliers = new List<object>() };

            double mean = downtimes.Average(x => x.DowntimeMins);
            double sumSquares = downtimes.Sum(x => Math.Pow(x.DowntimeMins - mean, 2));
            double stdDev = Math.Sqrt(sumSquares / (downtimes.Count - 1));

            if (stdDev == 0) return new { Mean = Math.Round(mean, 1), Outliers = new List<object>() };

            var outliers = downtimes.Select(x => new
            {
                x.TicketId,
                x.StationName,
                x.ErrorReason,
                x.ReportedAt,
                Downtime = Math.Round(x.DowntimeMins, 1),
                ZScore = Math.Round((x.DowntimeMins - mean) / stdDev, 2)
            }).Where(x => Math.Abs(x.ZScore) > threshold).ToList();

            return new { Mean = Math.Round(mean, 1), StdDev = Math.Round(stdDev, 2), Outliers = outliers };
        }

        // 3. THUẬT TOÁN N-GRAM PATTERN (Tìm quy luật lỗi liên tiếp)
        public object GetErrorPatterns(string stationName, int nGram)
        {
            var errors = new List<string>();

            using (var conn = new SQLiteConnection(_connStr))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT ErrorReason FROM Tickets 
                    WHERE StationName = @stn AND ErrorReason IS NOT NULL AND ErrorReason != ''
                    ORDER BY ReportedAt ASC";
                cmd.Parameters.AddWithValue("@stn", stationName);

                using var r = cmd.ExecuteReader();
                while (r.Read()) errors.Add(r["ErrorReason"].ToString()!);
            }

            var patternCounts = new Dictionary<string, int>();

            for (int i = 0; i <= errors.Count - nGram; i++)
            {
                var sequence = string.Join(" ➔ ", errors.Skip(i).Take(nGram));
                if (patternCounts.ContainsKey(sequence))
                    patternCounts[sequence]++;
                else
                    patternCounts[sequence] = 1;
            }

            var topPatterns = patternCounts
                .Where(p => p.Value > 1) // Chỉ lấy các quy luật lặp lại >= 2 lần
                .OrderByDescending(p => p.Value)
                .Take(5)
                .Select(p => new { Pattern = p.Key, Frequency = p.Value })
                .ToList();

            return new { Station = stationName, NGram = nGram, Patterns = topPatterns };
        }

        private class TicketDowntime
        {
            public string? TicketId { get; set; }
            public string? StationName { get; set; }
            public string? ErrorReason { get; set; }
            public DateTime ReportedAt { get; set; }
            public double DowntimeMins { get; set; }
        }
    }
}