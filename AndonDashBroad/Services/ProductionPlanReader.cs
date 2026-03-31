using ClosedXML.Excel;

using System;
using System.Collections.Generic;
using System.IO;

namespace AndonDashboard.Services // Đổi tên namespace cho khớp project của sếp
{
    public class PlanModel
    {
        public string ? Factory { get; set; }
        public string? Line { get; set; }
        public string? Model { get; set; }
        public int Target { get; set; }
    }

    public class ProductionPlanReader
    {
        public List<PlanModel> ReadPlan()
        {
            var planList = new List<PlanModel>();
            // Trỏ đường dẫn tới file Excel. Có thể đổi thành đường dẫn Ổ chung mạng LAN sau
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "production_plan.xlsx");

            if (!File.Exists(filePath)) return planList;

            using (var workbook = new XLWorkbook(filePath))
            {
                var worksheet = workbook.Worksheet("Production Plan"); // Đọc đúng Sheet này
               
                var usedRange = worksheet.RangeUsed();

                // Nếu sheet Excel trống trơn không có data, thì trả về danh sách rỗng luôn cho an toàn
                if (usedRange == null) return planList;

                var rows = usedRange.RowsUsed();

                bool isFirstRow = true;
                foreach (var row in rows)
                {
                    if (isFirstRow) { isFirstRow = false; continue; } // Bỏ qua dòng tiêu đề

                    planList.Add(new PlanModel
                    {
                        Factory = row.Cell(1).GetString(),
                        Line = row.Cell(2).GetString(),
                        Model = row.Cell(3).GetString(),
                        Target = row.Cell(4).GetValue<int>()
                    });
                }
            }
            return planList;
        }
    }
}