using SharedLib.Model; // Đảm bảo dùng đúng Model (không s)
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;


namespace SharedLib.Services
{
    public class IncidentService
    {
        private readonly string _connectionString;

        public IncidentService(string dbFilePath)
        {
            _connectionString = $"Data Source={dbFilePath};Version=3;";

            // TỰ ĐỘNG KHỞI TẠO CẢ 2 BẢNG KHI BẬT PHẦN MỀM
            CreateTableIfNotExists();
            InitializeStationTable();
        }

        private void CreateTableIfNotExists()
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();

                // 1. Tạo bảng mới (Đã bổ sung sẵn 2 cột Escalation cho các máy cài mới)
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Tickets (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            TicketId TEXT UNIQUE,
                            LineNumber TEXT,
                            StationName TEXT,
                            AlarmTypeIndex INTEGER,
                            Status INTEGER,
                            ReportedAt TEXT,
                            TechCheckinAt TEXT,
                            TechFixedAt TEXT,
                            LeaderConfirmedAt TEXT,
                            OperatorName TEXT,
                            TechnicianName TEXT,
                            LeaderName TEXT,
                            WorkOrder TEXT,
                            Product TEXT,
                            ErrorReason TEXT,
                            FixNote TEXT,
                            EscalationSentAt TEXT,
                            EscalationLevel INTEGER
                        )";
                    cmd.ExecuteNonQuery();
                }

                // 2. TỰ ĐỘNG NÂNG CẤP DB CŨ (Dành cho các máy đã cài từ trước)
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "ALTER TABLE Tickets ADD COLUMN EscalationSentAt TEXT;";
                        cmd.ExecuteNonQuery();
                    }
                }
                catch { /* Cột đã tồn tại, đi tiếp */ }

                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "ALTER TABLE Tickets ADD COLUMN EscalationLevel INTEGER;";
                        cmd.ExecuteNonQuery();
                    }
                }
                catch { /* Cột đã tồn tại, đi tiếp */ }
            }
        }

        // =================================================================
        // QUẢN LÝ TRẠM BẰNG DATABASE (THAY THẾ FILE TEXT)
        // =================================================================

        public void InitializeStationTable()
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    // Đã gỡ bỏ lệnh DROP TABLE để bảo toàn dữ liệu của sếp

                    // 1. Tạo bảng Stations với cột FactoryCode (Mã nhà máy 1, 2, 3, 4)
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Stations (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            FactoryCode TEXT NOT NULL DEFAULT 'F1', 
                            LineName TEXT NOT NULL,
                            StationName TEXT NOT NULL,
                            IsActive INTEGER DEFAULT 1,
                            InstalledAt DATETIME DEFAULT CURRENT_TIMESTAMP
                        );";
                    cmd.ExecuteNonQuery();

                    // 2. Nếu bảng trống, đẻ dữ liệu mẫu theo chuẩn Xưởng 1, Xưởng 2
                    cmd.CommandText = "SELECT COUNT(*) FROM Stations";
                    long count = (long)cmd.ExecuteScalar();
                    if (count == 0)
                    {
                        cmd.CommandText = @"
                            INSERT INTO Stations (FactoryCode, LineName, StationName, IsActive) VALUES 
                            ('F1', 'Line 01', 'Máy dập 01', 1),
                            ('F1', 'Line 01', 'Máy CNC 02', 1),
                            ('F1', 'Line 01', 'Băng chuyền 03', 1),
                            ('F1', 'Line 01', 'Máy cũ đã bỏ', 0), 
                            ('F2', 'Line 01', 'Máy dập Xưởng 2', 1); 
                        ";
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        // THÊM THAM SỐ factoryCode VÀO ĐỂ LỌC ĐÚNG NHÀ MÁY
        public List<string> GetActiveStations(string factoryCode, string lineName)
        {
            var stations = new List<string>();
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    // Lọc 3 điều kiện: Mã Xưởng + Tên Line + Đang hoạt động
                    cmd.CommandText = @"
                        SELECT StationName 
                        FROM Stations 
                        WHERE FactoryCode = @factoryCode 
                          AND LineName = @lineName 
                          AND IsActive = 1 
                        ORDER BY Id ASC";

                    cmd.Parameters.AddWithValue("@factoryCode", factoryCode);
                    cmd.Parameters.AddWithValue("@lineName", lineName);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            stations.Add(reader["StationName"].ToString()!);
                        }
                    }
                }
            }
            return stations;
        }

        // 1. HÀM MỞ PHIẾU MỚI (Dùng khi Operator ấn nút Đỏ)
        public void OpenIncident(IncidentTicket ticket)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO Tickets (
                            TicketId, LineNumber, StationName, AlarmTypeIndex, Status, 
                            ReportedAt, TechCheckinAt, TechFixedAt, LeaderConfirmedAt,
                            OperatorName, TechnicianName, LeaderName, 
                            WorkOrder, Product, ErrorReason, FixNote
                        )
                        VALUES (
                            @TicketId, @LineNumber, @StationName, @AlarmTypeIndex, @Status, 
                            @ReportedAt, @TechCheckinAt, @TechFixedAt, @LeaderConfirmedAt,
                            @OperatorName, @TechnicianName, @LeaderName, 
                            @WorkOrder, @Product, @ErrorReason, @FixNote
                        )";

                    cmd.Parameters.AddWithValue("@TicketId", ticket.TicketId);
                    cmd.Parameters.AddWithValue("@LineNumber", ticket.LineNumber ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@StationName", ticket.StationName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@AlarmTypeIndex", ticket.AlarmTypeIndex);
                    cmd.Parameters.AddWithValue("@Status", (int)ticket.Status);
                    cmd.Parameters.AddWithValue("@WorkOrder", ticket.WorkOrder ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Product", ticket.Product ?? (object)DBNull.Value);

                    cmd.Parameters.AddWithValue("@ReportedAt", ticket.ReportedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@TechCheckinAt", ticket.TechCheckinAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@TechFixedAt", ticket.TechFixedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@LeaderConfirmedAt", ticket.LeaderConfirmedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);

                    cmd.Parameters.AddWithValue("@OperatorName", ticket.OperatorName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@TechnicianName", ticket.TechnicianName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@LeaderName", ticket.LeaderName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ErrorReason", ticket.ErrorReason ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@FixNote", ticket.FixNote ?? (object)DBNull.Value);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        // 2. HÀM CẬP NHẬT PHIẾU (Dùng cho nút Cam, Xanh Dương, Xanh Lá)
        public void UpdateTicket(string ticketId, TicketStatus newStatus,
            string? techName = null,
            string? leaderName = null,
            string? errorReason = null,
            string? fixNote = null,
            DateTime? techCheckinAt = null,
            DateTime? techFixedAt = null,
            DateTime? leaderConfirmedAt = null)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        UPDATE Tickets 
                        SET Status = @Status,
                            TechnicianName = COALESCE(@TechName, TechnicianName),
                            LeaderName = COALESCE(@LeaderName, LeaderName),
                            ErrorReason = COALESCE(@ErrorReason, ErrorReason),
                            FixNote = COALESCE(@FixNote, FixNote),
                            TechCheckinAt = COALESCE(@TechCheckinAt, TechCheckinAt),
                            TechFixedAt = COALESCE(@TechFixedAt, TechFixedAt),
                            LeaderConfirmedAt = COALESCE(@LeaderConfirmedAt, LeaderConfirmedAt)
                        WHERE TicketId = @TicketId";

                    cmd.Parameters.AddWithValue("@Status", (int)newStatus);
                    cmd.Parameters.AddWithValue("@TechName", techName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@LeaderName", leaderName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ErrorReason", errorReason ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@FixNote", fixNote ?? (object)DBNull.Value);

                    cmd.Parameters.AddWithValue("@TechCheckinAt", techCheckinAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@TechFixedAt", techFixedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@LeaderConfirmedAt", leaderConfirmedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);

                    cmd.Parameters.AddWithValue("@TicketId", ticketId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // 3. HÀM LẤY DỮ LIỆU ĐỂ HIỂN THỊ LÊN LƯỚI
        public System.Data.DataTable GetAllTickets()
        {
            var dt = new System.Data.DataTable();
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT 
                            TicketId AS 'Mã Sự Cố',
                            LineNumber AS 'Chuyền',
                            StationName AS 'Vị Trí',
                            CASE Status 
                                WHEN 0 THEN 'Bình thường'
                                WHEN 1 THEN 'Cảnh báo'
                                WHEN 2 THEN 'Dừng máy (Đỏ)'
                                WHEN 3 THEN 'Đang sửa (Cam)'
                                WHEN 4 THEN 'Chờ xác nhận (Xanh dương)'
                                WHEN 5 THEN 'Đã đóng (Xanh Lá)'
                                ELSE Status 
                            END AS 'Trạng Thái',
                            ReportedAt AS 'Giờ Báo Lỗi',
                            TechCheckinAt AS 'Giờ KTV Đến',
                            TechFixedAt AS 'Giờ Sửa Xong',
                            LeaderConfirmedAt AS 'Giờ Chốt',
                            OperatorName AS 'Người Báo',
                            TechnicianName AS 'KTV Sửa',
                            LeaderName AS 'Leader Chốt',
                            ErrorReason AS 'Nguyên Nhân Lỗi',
                            FixNote AS 'Cách Khắc Phục',
                            WorkOrder AS 'Mã Lệnh SX',
                            Product AS 'Mã Sản Phẩm'
                        FROM Tickets 
                        ORDER BY Id DESC LIMIT 50";

                    using (var reader = cmd.ExecuteReader())
                    {
                        dt.Load(reader);
                    }
                }
            }
            return dt;
        }

        // 4. THUẬT TOÁN LEARN THEO TICKET (TÌM GỢI Ý THÔNG MINH)
        public string GetSmartHintFromHistory(string stationName)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT FixNote, COUNT(*) as Frequency
                        FROM Tickets 
                        WHERE StationName = $stationName 
                          AND Status = 5 
                          AND FixNote IS NOT NULL 
                          AND FixNote != ''
                        GROUP BY FixNote 
                        ORDER BY Frequency DESC 
                        LIMIT 1";

                    cmd.Parameters.AddWithValue("$stationName", stationName);

                    var result = cmd.ExecuteScalar();

                    if (result != null && result != DBNull.Value)
                    {
                        return $"💡 AI-Hint: Lỗi máy này thường được xử lý bằng cách:\n👉 '{result.ToString()}'";
                    }
                }
            }
            return "💡 Hệ thống đang thu thập dữ liệu.\nChưa có gợi ý lịch sử cho máy này.";
        }

        // --- THUẬT TOÁN PHÂN TÍCH LỊCH SỬ THÔNG MINH ---
        public string GetSmartAnalyticsHint(string stationName)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();

                string hint = "";

                // 1. TÌM LỖI HAY GẶP NHẤT CỦA MÁY NÀY
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT ErrorReason, COUNT(*) as Freq 
                        FROM Tickets 
                        WHERE StationName = @stationName AND Status = 5 AND ErrorReason IS NOT NULL AND ErrorReason != ''
                        GROUP BY ErrorReason 
                        ORDER BY Freq DESC 
                        LIMIT 1";
                    cmd.Parameters.AddWithValue("@stationName", stationName);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string topError = reader["ErrorReason"].ToString()!;
                            int freq = Convert.ToInt32(reader["Freq"]);
                            if (freq >= 2)
                            {
                                hint += $"⚠️ Chú ý: Lỗi '{topError}' đã lặp lại {freq} lần. Cần kiểm tra bảo dưỡng/thay mới.\n";
                            }
                        }
                    }
                }

                // 2. TÌM KTV SỬA NHANH NHẤT
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT TechnicianName, 
                               AVG((julianday(TechFixedAt) - julianday(TechCheckinAt)) * 1440) as AvgMinutes
                        FROM Tickets 
                        WHERE StationName = @stationName AND Status = 5 
                              AND TechFixedAt IS NOT NULL AND TechCheckinAt IS NOT NULL
                        GROUP BY TechnicianName 
                        ORDER BY AvgMinutes ASC 
                        LIMIT 1";
                    cmd.Parameters.AddWithValue("@stationName", stationName);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string bestTech = reader["TechnicianName"].ToString()!;
                            double avgTime = Convert.ToDouble(reader["AvgMinutes"]);
                            hint += $"🏆 KTV bắt bệnh nhanh nhất: {bestTech} ({Math.Round(avgTime, 1)} phút).";
                        }
                    }
                }

                if (string.IsNullOrEmpty(hint))
                {
                    hint = "💡 Máy đang hoạt động ổn định. Chưa có lịch sử hỏng hóc đáng chú ý.";
                }

                return hint;
            }
        }
        // =================================================================
        // VŨ KHÍ CHO ADMIN DASHBOARD (THÊM / SỬA / XÓA TRẠM)
        // =================================================================

        public List<string> GetLinesForFactory(string factoryCode)
        {
            var lines = new List<string>();
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT DISTINCT LineName FROM Stations
                        WHERE FactoryCode = @f AND IsActive = 1
                        ORDER BY LineName";
                    cmd.Parameters.AddWithValue("@f", factoryCode);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read()) lines.Add(reader[0].ToString()!);
                    }
                }
            }
            return lines;
        }

        public void AddStation(string factoryCode, string lineName, string stationName)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO Stations (FactoryCode, LineName, StationName, IsActive)
                        VALUES (@f, @l, @s, 1)";
                    cmd.Parameters.AddWithValue("@f", factoryCode);
                    cmd.Parameters.AddWithValue("@l", lineName);
                    cmd.Parameters.AddWithValue("@s", stationName);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void RenameStation(int stationId, string newName)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE Stations SET StationName = @n WHERE Id = @id";
                    cmd.Parameters.AddWithValue("@n", newName);
                    cmd.Parameters.AddWithValue("@id", stationId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // Tắt trạm (IsActive = 0) thay vì xóa, để không làm mất lịch sử Ticket cũ
        public void SetStationActive(int stationId, bool isActive)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE Stations SET IsActive = @a WHERE Id = @id";
                    cmd.Parameters.AddWithValue("@a", isActive ? 1 : 0);
                    cmd.Parameters.AddWithValue("@id", stationId);
                    cmd.ExecuteNonQuery();
                }
            }
        }
        public void UpsertStations(string factoryCode, string lineName, IEnumerable<string> stations)
        {
            if (string.IsNullOrWhiteSpace(factoryCode) || string.IsNullOrWhiteSpace(lineName))
                return;

            using var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            using var tran = conn.BeginTransaction();

            try
            {
                // Xóa trạm cũ
                using (var del = conn.CreateCommand())
                {
                    del.CommandText = "DELETE FROM Stations WHERE FactoryCode=@f AND LineName=@l";
                    del.Parameters.AddWithValue("@f", factoryCode);
                    del.Parameters.AddWithValue("@l", lineName);
                    del.ExecuteNonQuery();
                }

                // Chèn trạm mới
                foreach (var s in stations.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    using var ins = conn.CreateCommand();
                    ins.CommandText = @"INSERT INTO Stations (FactoryCode, LineName, StationName, IsActive)
                                VALUES (@f, @l, @s, 1)";
                    ins.Parameters.AddWithValue("@f", factoryCode);
                    ins.Parameters.AddWithValue("@l", lineName);
                    ins.Parameters.AddWithValue("@s", s.Trim());
                    ins.ExecuteNonQuery();
                }

                tran.Commit();
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        // =========================================================================
        // THUẬT TOÁN TÍNH OEE (DÙNG SQL ĐỌC TRỰC TIẾP TỪ DATABASE)
        // =========================================================================
        public OeeMetrics GetStationOee(string stationName)
        {
            double totalDowntimeMins = 0;

            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    // Chỉ lôi ra những phiếu của trạm này
                    cmd.CommandText = "SELECT ReportedAt, LeaderConfirmedAt FROM Tickets WHERE StationName = @s";
                    cmd.Parameters.AddWithValue("@s", stationName);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string reportedStr = reader["ReportedAt"].ToString()!;
                            string confStr = reader["LeaderConfirmedAt"].ToString()!;

                            if (DateTime.TryParse(reportedStr, out DateTime reportedAt))
                            {
                                // Chỉ tính các phiếu trong ngày hôm nay
                                if (reportedAt.Date == DateTime.Today)
                                {
                                    // Nếu chưa chốt phiếu thì lấy giờ hiện tại
                                    DateTime endTime = DateTime.Now;
                                    if (!string.IsNullOrEmpty(confStr) && DateTime.TryParse(confStr, out DateTime confTime))
                                    {
                                        endTime = confTime;
                                    }

                                    double mins = (endTime - reportedAt).TotalMinutes;
                                    if (mins > 0) totalDowntimeMins += mins;
                                }
                            }
                        }
                    }
                }
            }

            double plannedTimeMins = 480; // Ca 8 tiếng = 480 phút
            int a_Value = 100;
            if (totalDowntimeMins < plannedTimeMins)
            {
                a_Value = (int)Math.Round(((plannedTimeMins - totalDowntimeMins) / plannedTimeMins) * 100);
            }
            else
            {
                a_Value = 0;
            }

            // Giả lập P và Q 
            int p_Value = new Random(stationName.GetHashCode()).Next(85, 96);
            int q_Value = new Random(stationName.GetHashCode() + 1).Next(90, 99);

            return new OeeMetrics
            {
                StationName = stationName,
                Availability = a_Value,
                Performance = p_Value,
                Quality = q_Value,
                OeeValue = (a_Value * p_Value * q_Value) / 10000
            };
        }

        // =========================================================================
        // THUẬT TOÁN ĐẾM LỖI VẼ BIỂU ĐỒ (DÙNG SQL ĐỌC TRỰC TIẾP)
        // =========================================================================
        public Dictionary<string, int> GetErrorCountByReason()
        {
            int loiMay = 0;
            int thieuVatTu = 0;

            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT AlarmTypeIndex, StationName, ReportedAt FROM Tickets";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string reportedStr = reader["ReportedAt"].ToString()!;
                            if (DateTime.TryParse(reportedStr, out DateTime reportedAt))
                            {
                                // Chỉ đếm lỗi trong ngày hôm nay
                                if (reportedAt.Date == DateTime.Today)
                                {
                                    int alarmType = Convert.ToInt32(reader["AlarmTypeIndex"]);
                                    string stName = reader["StationName"].ToString()!;

                                    if (alarmType == 1) loiMay++;
                                    if (alarmType == 2 || stName == "TOÀN CHUYỀN") thieuVatTu++;
                                }
                            }
                        }
                    }
                }
            }

            return new Dictionary<string, int>
            {
                { "Run", 100 - (loiMay + thieuVatTu) }, // Giả lập số lần chạy tốt
                { "Stop (Lỗi Máy)", loiMay },
                { "Material (Thiếu Hàng)", thieuVatTu }
            };
        }

        // ═══════════════════════════════════════════════════════════════════
        // THÊM VÀO IncidentService.cs — PHẦN SHIFT SESSION
        // Copy paste đoạn này vào trong class IncidentService, sau phần
        // InitializeStationTable() hiện có
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Tạo bảng ShiftSession nếu chưa có — gọi 1 lần khi khởi động
        /// </summary>
        public void InitializeShiftSessionTable()
        {
            using var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            using var cmd = new SQLiteCommand(@"
        CREATE TABLE IF NOT EXISTS ShiftSession (
            Id          INTEGER PRIMARY KEY AUTOINCREMENT,
            FactoryCode TEXT NOT NULL,
            LineName    TEXT NOT NULL,
            Type        TEXT NOT NULL DEFAULT 'WORK',
            Source      TEXT NOT NULL DEFAULT 'MANUAL',
            StartTime   DATETIME NOT NULL,
            EndTime     DATETIME
        );
        CREATE INDEX IF NOT EXISTS idx_session_line_date
            ON ShiftSession(LineName, StartTime);
    ", conn);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Gọi khi terminal BẬT — bắt đầu đếm giờ làm
        /// </summary>
        public string StartWorkSession(string factoryCode, string lineName)
        {
            using var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            using var cmd = new SQLiteCommand(@"
        INSERT INTO ShiftSession (FactoryCode, LineName, Type, Source, StartTime)
        VALUES (@fc, @ln, 'WORK', 'MANUAL', @st);
        SELECT last_insert_rowid();", conn);
            cmd.Parameters.AddWithValue("@fc", factoryCode);
            cmd.Parameters.AddWithValue("@ln", lineName);
            cmd.Parameters.AddWithValue("@st", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            return cmd.ExecuteScalar()?.ToString() ?? "0";
        }

        /// <summary>
        /// Gọi khi terminal TẮT hoặc kết thúc ca — đóng session WORK hiện tại
        /// </summary>
        public void EndCurrentWorkSession(string factoryCode, string lineName)
        {
            using var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            using var cmd = new SQLiteCommand(@"
        UPDATE ShiftSession
        SET EndTime = @et
        WHERE FactoryCode = @fc
          AND LineName    = @ln
          AND Type        = 'WORK'
          AND EndTime IS NULL", conn);
            cmd.Parameters.AddWithValue("@et", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@fc", factoryCode);
            cmd.Parameters.AddWithValue("@ln", lineName);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Gọi khi công nhân bấm NGHỈ — bắt đầu đoạn nghỉ
        /// Đồng thời tạm đóng WORK session hiện tại
        /// </summary>
        public void StartBreak(string factoryCode, string lineName, string source = "MANUAL")
        {
            using var conn = new SQLiteConnection(_connectionString);
            conn.Open();

            // Đóng WORK session hiện tại
            using (var cmd = new SQLiteCommand(@"
        UPDATE ShiftSession SET EndTime=@et
        WHERE FactoryCode=@fc AND LineName=@ln AND Type='WORK' AND EndTime IS NULL", conn))
            {
                cmd.Parameters.AddWithValue("@et", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@fc", factoryCode);
                cmd.Parameters.AddWithValue("@ln", lineName);
                cmd.ExecuteNonQuery();
            }

            // Mở BREAK session mới
            using (var cmd = new SQLiteCommand(@"
        INSERT INTO ShiftSession (FactoryCode, LineName, Type, Source, StartTime)
        VALUES (@fc, @ln, 'BREAK', @src, @st)", conn))
            {
                cmd.Parameters.AddWithValue("@fc", factoryCode);
                cmd.Parameters.AddWithValue("@ln", lineName);
                cmd.Parameters.AddWithValue("@src", source);
                cmd.Parameters.AddWithValue("@st", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Gọi khi công nhân bấm TIẾP TỤC CA — kết thúc nghỉ, mở WORK mới
        /// </summary>
        public void EndBreak(string factoryCode, string lineName)
        {
            using var conn = new SQLiteConnection(_connectionString);
            conn.Open();

            // Đóng BREAK session hiện tại
            using (var cmd = new SQLiteCommand(@"
        UPDATE ShiftSession SET EndTime=@et
        WHERE FactoryCode=@fc AND LineName=@ln AND Type='BREAK' AND EndTime IS NULL", conn))
            {
                cmd.Parameters.AddWithValue("@et", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@fc", factoryCode);
                cmd.Parameters.AddWithValue("@ln", lineName);
                cmd.ExecuteNonQuery();
            }

            // Mở WORK session mới
            using (var cmd = new SQLiteCommand(@"
        INSERT INTO ShiftSession (FactoryCode, LineName, Type, Source, StartTime)
        VALUES (@fc, @ln, 'WORK', 'MANUAL', @st)", conn))
            {
                cmd.Parameters.AddWithValue("@fc", factoryCode);
                cmd.Parameters.AddWithValue("@ln", lineName);
                cmd.Parameters.AddWithValue("@st", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Kiểm tra terminal hiện đang ở trạng thái WORK hay BREAK
        /// </summary>
        public bool IsCurrentlyOnBreak(string factoryCode, string lineName)
        {
            using var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            using var cmd = new SQLiteCommand(@"
        SELECT Type FROM ShiftSession
        WHERE FactoryCode=@fc AND LineName=@ln AND EndTime IS NULL
        ORDER BY StartTime DESC LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@fc", factoryCode);
            cmd.Parameters.AddWithValue("@ln", lineName);
            var result = cmd.ExecuteScalar()?.ToString();
            return result == "BREAK";
        }

       

    }
}