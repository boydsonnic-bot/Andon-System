using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShareLib.Model
{// Định nghĩa 5 trạng thái cốt lõi của preAndon
    public enum TicketStatus
    {
        Green = 0,      // Bình thường
        Yellow = 1,     // Cảnh báo (vẫn chạy)
        Red = 2,        // Dừng máy
        Repairing = 3,  // KTV đang sửa
        WaitLeader = 4, // Chờ xác nhận
        Closed = 5      // Đã đóng
    }

    public class IncidentTicket
    {
        // Thông tin cơ bản
        public string? TicketId { get; set; }
        public string? LineNumber { get; set; }
        public string? StationName { get; set; }

        // Trạng thái hiện tại
        public TicketStatus Status { get; set; } = TicketStatus.Green;

        // Dữ liệu thời gian (Rất quan trọng để tính KPI MTTR/MTBF)
        public DateTime? ReportedAt { get; set; }
        public DateTime? TechCheckinAt { get; set; }
        public DateTime? LeaderConfirmedAt { get; set; }

        // Thông tin con người (Để bạn quản lý hiệu suất)
        public string? OperatorName { get; set; }
        public string? TechnicianName { get; set; }

        // Mã lệnh sản xuất đang chạy/ mã sản phẩm 
        public string? WorkOrder { get; set; }
        public string? Product { get;set; }
    }
}
