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
                bool isError = false; // CỜ BÁO LỖI
                var finalData = new List<object>();
                var planData = new List<PlanModel>();
                var sqlData = new List<dynamic>();

                // 1. ĐỌC EXCEL (Thử đọc xem có bị lỗi hoặc trống không)
                try
                {
                    planData = _excelReader.ReadPlan();
                    if (planData == null || planData.Count == 0) isError = true;
                }
                catch
                {
                    isError = true;
                }

                // 2. MÓC DATA SQL (Thử kết nối DB)
                string connString = _config.GetConnectionString("MesDatabase") ?? "";
                if (string.IsNullOrEmpty(connString))
                {
                    isError = true;
                }
                else
                {
                    try
                    {
                        using (SqlConnection conn = new SqlConnection(connString))
                        {
                            conn.Open();
                            string sql = @"
                        WITH Packing AS (
                            SELECT CAST(Date_Time AS DATE) AS ProdDate, Assy_PN AS Model, COUNT(*) AS ActualQty 
                            FROM LCS_BoxID_SN_XRef 
                            WHERE Date_Time >= DATEADD(day, -30, GETDATE())
                            GROUP BY CAST(Date_Time AS DATE), Assy_PN
                        ),
                        MRB AS (
                            SELECT CAST(Date_Time AS DATE) AS ProdDate, Assy_PN AS Model, COUNT(*) AS MrbQty 
                            FROM Aavid_MRB_Rework_Log_Station
                            WHERE Date_Time >= DATEADD(day, -30, GETDATE())
                            GROUP BY CAST(Date_Time AS DATE), Assy_PN
                        )
                        SELECT 
                            ISNULL(p.ProdDate, m.ProdDate) AS ProdDate,
                            ISNULL(p.Model, m.Model) AS Model, 
                            ISNULL(p.ActualQty, 0) AS ActualQty, 
                            ISNULL(m.MrbQty, 0) AS MrbQty
                        FROM Packing p FULL OUTER JOIN MRB m ON p.ProdDate = m.ProdDate AND p.Model = m.Model";

                            using (SqlCommand cmd = new SqlCommand(sql, conn))
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    sqlData.Add(new
                                    {
                                        ProdDate = Convert.ToDateTime(reader["ProdDate"]),
                                        Model = reader["Model"].ToString(),
                                        Actual = Convert.ToInt32(reader["ActualQty"]),
                                        Mrb = Convert.ToInt32(reader["MrbQty"])
                                    });
                                }
                            }
                        }
                    }
                    catch
                    {
                        isError = true; // Lỗi SQL -> Bật cờ đỏ
                    }
                }

                Random rnd = new Random();

                // 3. GHÉP DATA (Nếu MỌI THỨ NGON LÀNH)
                if (!isError && planData != null)
                {
                    foreach (var plan in planData)
                    {
                        var actualMatch = sqlData.FirstOrDefault(x => x.Model == plan.Model && x.ProdDate == plan.ProdDate);
                        finalData.Add(new
                        {
                            date = plan.ProdDate.ToString("yyyy-MM-dd"),
                            model = plan.Model,
                            line = plan.Line,
                            workOrder = plan.WorkOrder,
                            target = plan.Target,
                            actual = actualMatch != null ? actualMatch.Actual : 0,
                            mrb = actualMatch != null ? actualMatch.Mrb : 0
                        });
                    }
                }
                else
                {
                    // NẾU CÓ LỖI HOẶC KHÔNG CÓ DATA EXCEL -> BƠM FAKE DATA CHO "HÔM NAY" ĐỂ LÊN HÌNH TƯNG BỪNG
                    var fakeModelsToday = new List<string> { "123489", "123491", "679918" };
                    foreach (var model in fakeModelsToday)
                    {
                        int fTarget = rnd.Next(1500, 3500);
                        finalData.Add(new
                        {
                            date = DateTime.Now.ToString("yyyy-MM-dd"), // Ép ngày hôm nay
                            model = model,
                            line = "Line " + rnd.Next(1, 6),
                            workOrder = "WO-TEST-" + rnd.Next(100, 999),
                            target = fTarget,
                            actual = (int)(fTarget * (0.6 + rnd.NextDouble() * 0.4)),
                            mrb = rnd.Next(0, 15)
                        });
                    }
                }

                // 4. BƠM FAKE DATA LỊCH SỬ CHO BIỂU ĐỒ YTD (Từ 1/1 đến 22/3)
                DateTime startDate = new DateTime(2026, 1, 1);
                DateTime fakeEndDate = new DateTime(2026, 3, 22);
                var uniqueModels = planData?.Select(p => p.Model).Distinct().ToList() ?? new List<string?>();
                if (uniqueModels.Count == 0) uniqueModels = new List<string?> { "123489", "123491", "679918" };

                for (DateTime dt = startDate; dt <= fakeEndDate; dt = dt.AddDays(1))
                {
                    foreach (var model in uniqueModels)
                    {
                        int fTarget = rnd.Next(1000, 3000);
                        finalData.Add(new
                        {
                            date = dt.ToString("yyyy-MM-dd"),
                            model = model,
                            line = "Fake Line",
                            workOrder = "WO-FAKE",
                            target = fTarget,
                            actual = (int)(fTarget * (0.8 + rnd.NextDouble() * 0.2)),
                            mrb = rnd.Next(0, 50)
                        });
                    }
                }

                // TRẢ VỀ JSON BAO GỒM DATA + TRẠNG THÁI LỖI
                return Json(new
                {
                    isError = isError,
                    data = finalData
                });
            }
            catch (Exception)
            {
                return Json(new { isError = true, data = new List<object>() });
            }
        }
    }
}