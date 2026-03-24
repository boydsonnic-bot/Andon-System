using System;

namespace SharedLib.Model
{
    /// <summary>
    /// Lưu từng đoạn thời gian LÀM VIỆC hoặc NGHỈ của một ca
    /// Type = "WORK"  → máy đang chạy, tính vào Planned time
    /// Type = "BREAK" → nghỉ (config cứng hoặc công nhân bấm), không tính
    /// </summary>
    public class ShiftSession
    {
        public int Id { get; set; }
        public string FactoryCode { get; set; } = "";
        public string LineName { get; set; } = "";
        public string Type { get; set; } = "WORK";   // "WORK" | "BREAK"
        public string Source { get; set; } = "MANUAL"; // "MANUAL" | "CONFIG"
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }  // null = đang chạy

        /// <summary>Số phút thực tế của session (null nếu chưa kết thúc)</summary>
        public double? DurationMinutes =>
            EndTime.HasValue ? (EndTime.Value - StartTime).TotalMinutes : null;
    }
}