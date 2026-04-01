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
        public string? Shift { get; set; }
        public int Target { get; set; }
    }

    public class ProductionPlanReader
    {
        public List<PlanModel> ReadPlan()
        {
            var planList = new List<PlanModel>();

            // PATCH: Dùng đường dẫn mạng UNC (Tuyệt đối không dùng Z:\)
            string filePath = @"Z:\Share\12. KPI\Plan\production_plan.xlsx";

            if (!File.Exists(filePath))
            {
                // PATCH: Không nuốt lỗi nữa, ném thẳng lỗi ra cho hệ thống biết
                throw new Exception($"KHÔNG ĐỌC ĐƯỢC EXCEL: Không tìm thấy file tại đường dẫn '{filePath}'. Vui lòng kiểm tra quyền truy cập mạng (Network Share) của IIS/AppPool.");
            }

            // Vẫn dùng FileShare.ReadWrite để chống lỗi khi có người đang mở file
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var workbook = new XLWorkbook(fileStream))
            {
                var worksheet = workbook.Worksheet("Production Plan");
                var usedRange = worksheet.RangeUsed();
                if (usedRange == null) throw new Exception("File Excel không có dữ liệu trong sheet 'Production Plan'.");

                var rows = usedRange.RowsUsed();
                foreach (var row in rows)
                {
                    if (row.RowNumber() < 6) continue;

                    string model = row.Cell(3).GetString();
                    if (string.IsNullOrWhiteSpace(model)) continue;

                    planList.Add(new PlanModel
                    {
                        Factory = row.Cell(1).GetString(),
                        Line = row.Cell(2).GetString(),
                        Model = model,
                        WorkOrder = row.Cell(4).GetString(),
                        ProdDate = row.Cell(5).TryGetValue<DateTime>(out var d) ? d.Date : DateTime.Today,
                        Shift = row.Cell(6).GetString(),
                        Target = row.Cell(7).TryGetValue<int>(out var t) ? t : 0
                    });
                }
            }

            return planList;
        }
    }
}