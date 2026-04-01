using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;

namespace AndonDashboard.Services
{
    public class PlanModel
    {
        public string? Factory { get; set; }
        public string? Line { get; set; }
        public string? Model { get; set; }
        public string? WorkOrder { get; set; }
        public DateTime ProdDate { get; set; }
        public int Target { get; set; }
    }

    public class ProductionPlanReader
    {
        public List<PlanModel> ReadPlan()
        {
            var planList = new List<PlanModel>();

            // LƯU Ý Ổ Z: Nếu sếp build xong mà vẫn Demo Mode, sếp PHẢI đổi Z: thành IP gốc
            // Ví dụ: string filePath = @"\\10.102.4.xx\Share\12. KPI\Plan\production_plan.xlsx";
            string filePath = @"Z:\Share\12. KPI\Plan\production_plan.xlsx";

            if (!File.Exists(filePath)) return planList;

            try
            {
                // VŨ KHÍ MỚI: Thêm FileShare.ReadWrite để đọc được file kể cả khi đang có người mở file!
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var workbook = new XLWorkbook(fileStream))
                {
                    var worksheet = workbook.Worksheet("Production Plan");
                    var usedRange = worksheet.RangeUsed();
                    if (usedRange == null) return planList;

                    var rows = usedRange.RowsUsed();

                    foreach (var row in rows)
                    {
                        // 1. SỬA LỖI CHÍ MẠNG: Bỏ qua 5 dòng đầu (vì dòng 6 mới bắt đầu có data)
                        if (row.RowNumber() < 6) continue;

                        // 2. Bỏ qua dòng rác: Nếu không có mã Model thì lờ đi luôn
                        string model = row.Cell(3).GetString();
                        if (string.IsNullOrWhiteSpace(model)) continue;

                        planList.Add(new PlanModel
                        {
                            Factory = row.Cell(1).GetString(),
                            Line = row.Cell(2).GetString(),
                            Model = model,
                            WorkOrder = row.Cell(4).GetString(),

                            // 3. TryGetValue để lỡ file Excel gõ nhầm chữ/trống nó không bị crash
                            ProdDate = row.Cell(5).TryGetValue<DateTime>(out var d) ? d.Date : DateTime.Today,
                            Target = row.Cell(7).TryGetValue<int>(out var t) ? t : 0
                        });
                    }
                }
            }
            catch
            {
                // Bắt lỗi ngầm để tránh chết chương trình
            }

            return planList;
        }
    }
}