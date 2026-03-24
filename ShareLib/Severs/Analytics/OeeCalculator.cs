using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using SharedLib.Model;

namespace SharedLib.Services.Analytics
{
    /// <summary>
    /// Tính MTTR, MTBF, Availability từ ShiftSession + IncidentTicket
    /// Logic: config cứng (Break=) merge với nút bấm công nhân, lấy khung rộng hơn
    /// </summary>
    public class OeeCalculator
    {
        private readonly string _connString;

        public OeeCalculator(string dbPath)
        {
            _connString = $"Data Source={dbPath};Version=3;";
        }

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC — KẾT QUẢ OEE
        // ═══════════════════════════════════════════════════════════════
        public class OeeResult
        {
            public double PlannedMinutes { get; set; }  // Tổng giờ WORK
            public double DowntimeMinutes { get; set; }  // Tổng downtime đã gọt
            public double Availability { get; set; }  // %
            public double MttrMinutes { get; set; }  // Trung bình thời gian sửa
            public double MtbfMinutes { get; set; }  // Trung bình giữa 2 lần lỗi
            public int TotalIncidents { get; set; }
            public DateTime Date { get; set; }
            public string LineName { get; set; } = "";
        }

        /// <summary>
        /// Tính OEE cho 1 line trong 1 ngày
        /// </summary>
        public OeeResult Calculate(string factoryCode, string lineName, DateTime date, List<BreakSlot> configBreaks)
        {
            var result = new OeeResult
            {
                Date = date.Date,
                LineName = lineName
            };

            var dayStart = date.Date;
            var dayEnd = date.Date.AddDays(1);

            var workSessions = GetSessions(factoryCode, lineName, "WORK", dayStart, dayEnd);

            var manualBreaks = GetSessions(factoryCode, lineName, "BREAK", dayStart, dayEnd)
                               .Where(s => s.EndTime.HasValue)
                               .Select(s => new BreakSlot(s.StartTime, s.EndTime!.Value, "MANUAL"))
                               .ToList();

            var allBreaks = MergeBreaks(configBreaks, manualBreaks, dayStart, dayEnd);

            result.PlannedMinutes = workSessions
                .Where(s => s.EndTime.HasValue)
                .Sum(s => s.DurationMinutes ?? 0);

            if (result.PlannedMinutes <= 0)
                return result;

            var tickets = GetClosedTickets(lineName, dayStart, dayEnd);
            result.TotalIncidents = tickets.Count;

            // SỬA Ở ĐÂY: Lọc thêm ReportedAt.HasValue và dùng .Value
            result.DowntimeMinutes = tickets
                .Where(t => t.LeaderConfirmedAt.HasValue && t.ReportedAt.HasValue)
                .Sum(t => CalcNetDowntime(t.ReportedAt!.Value, t.LeaderConfirmedAt!.Value, allBreaks));

            result.Availability = result.PlannedMinutes > 0
                ? Math.Round((result.PlannedMinutes - result.DowntimeMinutes) / result.PlannedMinutes * 100, 1)
                : 100;

            // SỬA Ở ĐÂY: Lọc thêm ReportedAt.HasValue và dùng .Value
            var durations = tickets
                .Where(t => t.LeaderConfirmedAt.HasValue && t.ReportedAt.HasValue)
                .Select(t => CalcNetDowntime(t.ReportedAt!.Value, t.LeaderConfirmedAt!.Value, allBreaks))
                .Where(d => d > 0)
                .ToList();

            result.MttrMinutes = durations.Count > 0
                ? Math.Round(durations.Average(), 1)
                : 0;

            result.MtbfMinutes = CalcMtbf(tickets, result.PlannedMinutes);

            return result;
        }

        // ═══════════════════════════════════════════════════════════════
        // CORE — GỌT THỜI GIAN NGHỈ
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Tính thời gian downtime thực tế, loại bỏ phần trùng với giờ nghỉ
        /// </summary>
        public static double CalcNetDowntime(DateTime start, DateTime end,
                                             List<BreakSlot> breaks)
        {
            if (end <= start) return 0;

            double total = (end - start).TotalMinutes;

            // Trừ đi phần trùng với từng break
            foreach (var b in breaks)
            {
                var overlapStart = start < b.Start ? b.Start : start;
                var overlapEnd = end > b.End ? b.End : end;
                if (overlapEnd > overlapStart)
                    total -= (overlapEnd - overlapStart).TotalMinutes;
            }

            return Math.Max(0, Math.Round(total, 1));
        }

        /// <summary>
        /// Merge config breaks + manual breaks, lấy union (khung rộng hơn)
        /// </summary>
        public static List<BreakSlot> MergeBreaks(
            List<BreakSlot> configBreaks,
            List<BreakSlot> manualBreaks,
            DateTime dayStart, DateTime dayEnd)
        {
            // Gộp tất cả vào 1 list
            var all = new List<BreakSlot>();
            all.AddRange(configBreaks.Select(b => ClipToDay(b, dayStart, dayEnd)));
            all.AddRange(manualBreaks.Select(b => ClipToDay(b, dayStart, dayEnd)));
            all = all.Where(b => b.End > b.Start).ToList();

            if (all.Count == 0) return all;

            // Sort theo Start rồi merge overlap
            all.Sort((a, b) => a.Start.CompareTo(b.Start));
            var merged = new List<BreakSlot> { all[0] };

            for (int i = 1; i < all.Count; i++)
            {
                var last = merged[merged.Count - 1];
                if (all[i].Start <= last.End)
                {
                    // Mở rộng khung nếu overlap
                    if (all[i].End > last.End)
                        merged[merged.Count - 1] = new BreakSlot(last.Start, all[i].End, "MERGED");
                }
                else
                {
                    merged.Add(all[i]);
                }
            }

            return merged;
        }

        // ═══════════════════════════════════════════════════════════════
        // MTBF
        // ═══════════════════════════════════════════════════════════════
        private static double CalcMtbf(List<IncidentTicket> tickets, double plannedMinutes)
        {
            // Chỉ lấy những ticket có ReportedAt để tính toán an toàn
            var validTickets = tickets.Where(t => t.ReportedAt.HasValue).ToList();

            if (validTickets.Count == 0 || validTickets.Count == 1) return plannedMinutes;

            var sorted = validTickets.OrderBy(t => t.ReportedAt).ToList();
            var gaps = new List<double>();

            for (int i = 1; i < sorted.Count; i++)
            {
                // SỬA Ở ĐÂY: Thêm .Value vào ReportedAt
                double gap = (sorted[i].ReportedAt!.Value - sorted[i - 1].ReportedAt!.Value).TotalMinutes;
                if (gap > 0) gaps.Add(gap);
            }

            return gaps.Count > 0 ? Math.Round(gaps.Average(), 1) : plannedMinutes;
        }

        // ═══════════════════════════════════════════════════════════════
        // DB HELPERS
        // ═══════════════════════════════════════════════════════════════
        private List<ShiftSession> GetSessions(string factoryCode, string lineName,
                                               string type, DateTime from, DateTime to)
        {
            var list = new List<ShiftSession>();
            using var conn = new SQLiteConnection(_connString);
            conn.Open();
            using var cmd = new SQLiteCommand(@"
                SELECT * FROM ShiftSession
                WHERE FactoryCode=@fc AND LineName=@ln AND Type=@type
                  AND StartTime >= @from AND StartTime < @to", conn);
            cmd.Parameters.AddWithValue("@fc", factoryCode);
            cmd.Parameters.AddWithValue("@ln", lineName);
            cmd.Parameters.AddWithValue("@type", type);
            cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@to", to.ToString("yyyy-MM-dd HH:mm:ss"));

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new ShiftSession
                {
                    Id = r.GetInt32(r.GetOrdinal("Id")),
                    FactoryCode = r["FactoryCode"].ToString() ?? "",
                    LineName = r["LineName"].ToString() ?? "",
                    Type = r["Type"].ToString() ?? "",
                    Source = r["Source"].ToString() ?? "",
                    StartTime = DateTime.Parse(r["StartTime"].ToString()!),
                    EndTime = r["EndTime"] == DBNull.Value
                                  ? null
                                  : DateTime.Parse(r["EndTime"].ToString()!)
                });
            }
            return list;
        }

        private List<IncidentTicket> GetClosedTickets(string lineName, DateTime from, DateTime to)
        {
            var list = new List<IncidentTicket>();
            using var conn = new SQLiteConnection(_connString);
            conn.Open();
            using var cmd = new SQLiteCommand(@"
        SELECT * FROM Tickets
        WHERE LineNumber=@ln
          AND ReportedAt >= @from AND ReportedAt < @to
          AND Status='Closed'", conn);
            cmd.Parameters.AddWithValue("@ln", lineName);
            cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@to", to.ToString("yyyy-MM-dd HH:mm:ss"));

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new IncidentTicket
                {
                    TicketId = r["TicketId"].ToString() ?? "",
                    // SỬA Ở ĐÂY: Thay LineName bằng LineNumber
                    LineNumber = r["LineNumber"].ToString() ?? "",
                    StationName = r["StationName"].ToString() ?? "",
                    ReportedAt = DateTime.Parse(r["ReportedAt"].ToString()!),
                    LeaderConfirmedAt = r["LeaderConfirmedAt"] == DBNull.Value
                                       ? null
                                       : DateTime.Parse(r["LeaderConfirmedAt"].ToString()!)
                });
            }
            return list;
        }

        // ── Clip break vào đúng ngày ──────────────────────────────────
        private static BreakSlot ClipToDay(BreakSlot b, DateTime dayStart, DateTime dayEnd)
        {
            var s = b.Start < dayStart ? dayStart : b.Start;
            var e = b.End > dayEnd ? dayEnd : b.End;
            return new BreakSlot(s, e, b.Source);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // BreakSlot — đại diện 1 khoảng thời gian nghỉ
    // ═══════════════════════════════════════════════════════════════════
    public class BreakSlot
    {
        public DateTime Start { get; }
        public DateTime End { get; }
        public string Source { get; } // "CONFIG" | "MANUAL" | "MERGED"

        public BreakSlot(DateTime start, DateTime end, string source = "CONFIG")
        {
            Start = start;
            End = end;
            Source = source;
        }

        /// <summary>
        /// Tạo BreakSlot từ string "HH:mm-HH:mm" trong terminal.cfg
        /// Ví dụ: "10:00-10:10" → BreakSlot cho ngày hôm nay
        /// </summary>
        public static BreakSlot? FromConfig(string raw, DateTime date)
        {
            try
            {
                var parts = raw.Split('-');
                if (parts.Length != 2) return null;

                var start = DateTime.Parse($"{date:yyyy-MM-dd} {parts[0].Trim()}");
                var end = DateTime.Parse($"{date:yyyy-MM-dd} {parts[1].Trim()}");

                // Xử lý ca đêm (end < start → end là ngày hôm sau)
                if (end < start) end = end.AddDays(1);

                return new BreakSlot(start, end, "CONFIG");
            }
            catch { return null; }
        }
    }
}