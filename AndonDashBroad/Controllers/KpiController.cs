using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using AndonDashboard.Services; // Thư mục chứa file ProductionPlanReader.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace AndonDashboard.Controllers // Tên project của sếp
{
    public class KpiController : Controller
    {
        private readonly ProductionPlanReader _excelReader;
        private readonly IConfiguration _config;

        public KpiController(ProductionPlanReader excelReader, IConfiguration config)
        {
            _excelReader = excelReader;
            _config = config;
        }

        // 1. TRẢ VỀ GIAO DIỆN WEB (File Index.cshtml)
        public IActionResult Index()
        {
            // Trả về giao diện trống, data sẽ được Javascript tự tải sau
            return View();
        }

        // 2. API ĐỂ JAVASCRIPT GỌI MỖI 5 PHÚT (Lấy cả Kế hoạch + Thực tế)
        [HttpGet]
        public IActionResult GetProductionData()
        {
            try
            {
                // A. Lấy Kế Hoạch từ Excel
                var planData = _excelReader.ReadPlan();

                // B. Lấy Actual & MRB từ SQL Server
                // Thêm đoạn ?? "" nghĩa là: Nếu không tìm thấy cấu hình DB thì gán bằng chuỗi rỗng
                string connString = _config.GetConnectionString("MesDatabase") ?? "";

                // (Tùy chọn) Sếp có thể chặn luôn ở đây cho chắc cú:
                if (string.IsNullOrEmpty(connString))
                {
                    throw new Exception("Chưa cấu hình MesDatabase trong appsettings.json!");
                }
                var sqlData = new List<dynamic>();

                using (SqlConnection conn = new SqlConnection(connString))
                {
                    conn.Open();
                    string sql = @"
                        WITH Packing AS (
                            SELECT Assy_PN AS Model, COUNT(*) AS ActualQty FROM LCS_BoxID_SN_XRef 
                            WHERE CAST(Date_Time AS DATE) = CAST(GETDATE() AS DATE) GROUP BY Assy_PN
                        ),
                        MRB AS (
                            SELECT Assy_PN AS Model, COUNT(*) AS MrbQty FROM Aavid_MRB_Rework_Log_Station
                            WHERE CAST(Date_Time AS DATE) = CAST(GETDATE() AS DATE) GROUP BY Assy_PN
                        )
                        SELECT p.Model, p.ActualQty, ISNULL(m.MrbQty, 0) AS MrbQty
                        FROM Packing p LEFT JOIN MRB m ON p.Model = m.Model";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            sqlData.Add(new
                            {
                                Model = reader["Model"].ToString(),
                                Actual = Convert.ToInt32(reader["ActualQty"]),
                                Mrb = Convert.ToInt32(reader["MrbQty"])
                            });
                        }
                    }
                }

                // C. Mai Mối: Ghép Excel và SQL vào nhau
                var finalData = new List<object>();
                foreach (var plan in planData)
                {
                    var actualMatch = sqlData.FirstOrDefault(x => x.Model == plan.Model);
                    finalData.Add(new
                    {
                        id = plan.Line + "_" + plan.Model,
                        factory = plan.Factory,
                        line = plan.Line,
                        model = plan.Model,
                        target = plan.Target,
                        actual = actualMatch != null ? actualMatch.Actual : 0,
                        mrb = actualMatch != null ? actualMatch.Mrb : 0,
                        stopMin = 0
                    });
                }

                // Trả về dữ liệu dạng JSON
                return Json(finalData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}