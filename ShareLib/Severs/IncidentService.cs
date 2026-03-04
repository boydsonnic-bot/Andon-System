
using ShareLib.Model;
using System;
using System.Data.SQLite;

namespace SharedLib.Services
{
    public class IncidentService
    {
        private readonly string _connectionString;

        // Nhận đường dẫn file database (.db) khi khởi tạo
        public IncidentService(string dbFilePath)
        {
            _connectionString = $"Data Source={dbFilePath};Version=3;";
            CreateTableIfNotExists(); // Tự động tạo bảng nếu chưa có
        }

        // 1. Lệnh tạo bảng SQLite
        private void CreateTableIfNotExists()
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Tickets (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            TicketId TEXT UNIQUE,
                            LineNumber TEXT,
                            AlarmTypeIndex INTEGER,
                            Status INTEGER,
                            ReportedAt TEXT,
                            OperatorName TEXT,
                            WorkOrder TEXT,
                            Product TEXT

                        )";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // 2. Lệnh Mở Ticket mới (Lưu xuống DB)
        public void OpenIncident(IncidentTicket ticket)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    // LUÔN dùng @parameter để truyền biến vào SQL
                    cmd.CommandText = @"
                        INSERT INTO Tickets (TicketId, LineNumber, AlarmTypeIndex, Status, ReportedAt, OperatorName, WorkOrder, Product )
                        VALUES (@TicketId, @LineNumber, @AlarmTypeIndex, @Status, @ReportedAt, @OperatorName, @WorkOrder, @Product)";

                    cmd.Parameters.AddWithValue("@TicketId", ticket.TicketId);
                    cmd.Parameters.AddWithValue("@LineNumber", ticket.LineNumber);
                    cmd.Parameters.AddWithValue("@AlarmTypeIndex", ticket.AlarmTypeIndex);
                    cmd.Parameters.AddWithValue("@Status", (int)ticket.Status);
                    cmd.Parameters.AddWithValue("@ReportedAt", ticket.ReportedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@OperatorName", ticket.OperatorName);
                    cmd.Parameters.AddWithValue("@Product", ticket.Product);
                    cmd.Parameters.AddWithValue("@WorkOrder", ticket.WorkOrder);

                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}