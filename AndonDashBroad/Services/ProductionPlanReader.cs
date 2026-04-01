using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;

namespace AndonDashboard.Services
{
    // Bổ sung thêm trường Shift (Ca Sản Xuất)
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

            // Đường dẫn tới file Excel của sếp
            string filePath = @"Z:\Share\12. KPI\Plan\production_plan.xlsx";

            if (!File.Exists(filePath)) return planList;

            try
            {
                // FileShare.ReadWrite: Đọc file an toàn kể cả khi có người đang mở Excel
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var workbook = new XLWorkbook(fileStream))
                {
                    var worksheet = workbook.Worksheet("Production Plan");
                    var usedRange = worksheet.RangeUsed();
                    if (usedRange == null) return planList;

                    var rows = usedRange.RowsUsed();

                    foreach (var row in rows)
                    {
                        // Bỏ qua 5 dòng tiêu đề đầu tiên
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

                            // Lấy thông tin Ca Sáng / Ca Tối ở Cột 6 (Cột F)
                            Shift = row.Cell(6).GetString(),

                            Target = row.Cell(7).TryGetValue<int>(out var t) ? t : 0
                        });
                    }
                }
            }
            catch
            {
                // Bỏ qua lỗi ngầm
            }

            return planList;
        }
    }
}