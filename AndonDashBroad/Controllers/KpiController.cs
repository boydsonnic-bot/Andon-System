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
        [HttpGet]
        public IActionResult GetProductionData()
        {
            try
            {
                bool isError = false;
                var finalData = new List<object>();
                var planData = new List<AndonDashboard.Services.PlanModel>(); // Sửa theo đúng namespace của sếp
                var sqlData = new List<dynamic>();

                // 1. TRY EXCEL
                try
                {
                    planData = _excelReader.ReadPlan();
                    if (planData == null || planData.Count == 0) isError = true;
                }
                catch { isError = true; }

                // 2. TRY SQL
                string connString = _config.GetConnectionString("MesDatabase") ?? "";
                if (string.IsNullOrEmpty(connString)) isError = true;
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
                            FROM LCS_BoxID_SN_XRef WHERE Date_Time >= DATEADD(day, -30, GETDATE()) GROUP BY CAST(Date_Time AS DATE), Assy_PN
                        ),
                        MRB AS (
                            SELECT CAST(Date_Time AS DATE) AS ProdDate, Assy_PN AS Model, COUNT(*) AS MrbQty 
                            FROM Aavid_MRB_Rework_Log_Station WHERE Date_Time >= DATEADD(day, -30, GETDATE()) GROUP BY CAST(Date_Time AS DATE), Assy_PN
                        )
                        SELECT ISNULL(p.ProdDate, m.ProdDate) AS ProdDate, ISNULL(p.Model, m.Model) AS Model, ISNULL(p.ActualQty, 0) AS ActualQty, ISNULL(m.MrbQty, 0) AS MrbQty
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
                    catch { isError = true; }
                }

                Random rnd = new Random();

                // 3. MERGE DATA OR GENERATE MEGA FAKE DATA FOR DEMO
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
                    // MEGA DEMO MODE: 25 Numeric Models, 15 Lines
                    var fakeModelsToday = new List<string>();
                    for (int i = 0; i < 25; i++) fakeModelsToday.Add(rnd.Next(100000, 999999).ToString());

                    var lines = new List<string>();
                    for (int i = 1; i <= 15; i++) lines.Add("Line " + i.ToString("D2"));

                    foreach (var model in fakeModelsToday)
                    {
                        // Each model runs on 1 to 3 random lines
                        int linesCount = rnd.Next(1, 4);
                        var shuffledLines = lines.OrderBy(x => rnd.Next()).Take(linesCount).ToList();

                        foreach (var line in shuffledLines)
                        {
                            int fTarget = rnd.Next(2000, 6000);
                            finalData.Add(new
                            {
                                date = DateTime.Now.ToString("yyyy-MM-dd"),
                                model = model,
                                line = line,
                                workOrder = "WO-" + rnd.Next(10000, 99999),
                                target = fTarget,
                                actual = (int)(fTarget * (0.4 + rnd.NextDouble() * 0.5)), // 40-90% done
                                mrb = rnd.Next(0, 30)
                            });
                        }
                    }
                }

                // 4. FAKE YTD DATA
                DateTime startDate = new DateTime(2026, 1, 1);
                DateTime fakeEndDate = new DateTime(2026, 3, 22);
                var uniqueModels = planData?.Select(p => p.Model).Distinct().ToList() ?? new List<string>();
                if (uniqueModels.Count == 0) uniqueModels = new List<string> { "849201", "392011" };

                for (DateTime dt = startDate; dt <= fakeEndDate; dt = dt.AddDays(1))
                {
                    foreach (var model in uniqueModels)
                    {
                        int fTarget = rnd.Next(1500, 4000);
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

                return Json(new { isError = isError, data = finalData });
            }
            catch (Exception) { return Json(new { isError = true, data = new List<object>() }); }
        }
    }
}