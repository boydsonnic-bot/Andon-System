using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using AndonDashboard.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AndonDashboard.Controllers
{
    // ── Models ────────────────────────────────────────────────────

    public class QcRow
    {
        public DateTime ProdDate { get; set; }
        public int ProdHour { get; set; }  // BUG 5: giờ để vẽ chart thực
        public string? Model { get; set; }
        public int Actual { get; set; }
        public int QcFail { get; set; }  // fail tại QC (có FailCode)
    }

    public class MrbRow
    {
        public DateTime ProdDate { get; set; }
        public string? Model { get; set; }
        public int MrbCount { get; set; }  // BUG 6: đếm từ MRB table
        public int Scrapped { get; set; }  // đơn vị bị scrapped
        public int Reworked { get; set; }  // đơn vị rework thành công
    }

    public class KpiController : Controller
    {
        private readonly ProductionPlanReader _excelReader;
        private readonly IConfiguration _config;

        public KpiController(ProductionPlanReader excelReader, IConfiguration config)
        {
            _excelReader = excelReader;
            _config = config;
        }

        public IActionResult Index() => View();

        // ════════════════════════════════════════════════════════
        // MAIN DATA ENDPOINT
        // ════════════════════════════════════════════════════════
        [HttpGet]
        public IActionResult GetProductionData()
        {
            try
            {
                var planData = _excelReader.ReadPlan();
                if (planData == null || planData.Count == 0)
                    return Json(new { isError = false, data = new List<object>() });

                var qcRows = new List<QcRow>();
                var mrbRows = new List<MrbRow>();

                string connString = _config.GetConnectionString("MesDatabase") ?? "";
                bool hasDb = !string.IsNullOrEmpty(connString);

                if (hasDb)
                {
                    using var conn = new SqlConnection(connString);
                    conn.Open();
                    qcRows = FetchQcData(conn);
                    mrbRows = FetchMrbData(conn);   // BUG 6 FIX: kéo bảng MRB
                }

                var finalData = BuildFinalData(planData, qcRows, mrbRows);
                return Json(new { isError = false, data = finalData });
            }
            catch (Exception ex)
            {
                // BUG 4 FIX: log lỗi thật thay vì nuốt ngầm
                return Json(new { isError = true, message = ex.Message, data = new List<object>() });
            }
        }

        // ════════════════════════════════════════════════════════
        // HOURLY TREND ENDPOINT  (BUG 5 FIX — charts dùng data thật)
        // GET /Kpi/GetHourlyTrend?model=679917&line=S2&date=2026-04-01
        // ════════════════════════════════════════════════════════
        [HttpGet]
        [HttpGet]
        public IActionResult GetHourlyTrend(string model, string line, string date)
        {
            try
            {
                if (!DateTime.TryParse(date, out var prodDate))
                    return Json(new { isError = true, message = "Invalid date" });

                string connString = _config.GetConnectionString("MesDatabase") ?? "";
                if (string.IsNullOrEmpty(connString))
                    return Json(new { isError = false, data = new List<object>() });

                using var conn = new SqlConnection(connString);
                conn.Open();

                var sql = @"
            SELECT
                CASE WHEN DATEPART(HOUR, Date_Time) < 8
                     THEN DATEPART(HOUR, Date_Time) + 24
                     ELSE DATEPART(HOUR, Date_Time)
                END AS ProdHour,
                CASE WHEN Remark = 'To packing' THEN 1 ELSE 0 END AS IsPass,
                CASE WHEN FailCode IS NOT NULL 
                      AND LTRIM(RTRIM(FailCode)) <> '' THEN 1 ELSE 0 END AS IsFail
            FROM [Production_SZ].[dbo].[QC_Inspection_Log]
            WHERE LTRIM(RTRIM(Assy_PN)) = @Model
              AND (Remark IS NULL OR Remark <> 'Production Test')
              AND (
                    (Date_Time >= DATEADD(HOUR,  8, CAST(@ProdDate AS DATETIME))
                 AND Date_Time <  DATEADD(HOUR, 20, CAST(@ProdDate AS DATETIME)))
                 OR
                    (Date_Time >= DATEADD(HOUR, 20, CAST(@ProdDate AS DATETIME))
                 AND Date_Time <  DATEADD(HOUR, 32, CAST(@ProdDate AS DATETIME)))
                  )";

                var hourlyRaw = new Dictionary<int, (int pass, int fail)>();

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Model", model);
                    cmd.Parameters.AddWithValue("@ProdDate", prodDate.Date);
                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        int hr = Convert.ToInt32(rdr["ProdHour"]);
                        int pass = Convert.ToInt32(rdr["IsPass"]);
                        int fail = Convert.ToInt32(rdr["IsFail"]);

                        if (!hourlyRaw.ContainsKey(hr))
                            hourlyRaw[hr] = (pass: 0, fail: 0);

                        var current = hourlyRaw[hr];
                        hourlyRaw[hr] = (pass: current.pass + pass, fail: current.fail + fail);
                    }
                }

                // Build 24 slots (08:00 → 07:00 hôm sau)
                var slots = new List<object>();
                for (int h = 8; h < 32; h++)
                {
                    int key = h;
                    int label = h >= 24 ? h - 24 : h;

                    var v = hourlyRaw.ContainsKey(key) ? hourlyRaw[key] : (pass: 0, fail: 0);

                    slots.Add(new
                    {
                        hour = $"{label:D2}:00",
                        actual = v.pass,
                        fail = v.fail
                    });
                }

                return Json(new { isError = false, data = slots });
            }
            catch (Exception ex)
            {
                return Json(new { isError = true, message = ex.Message });
            }
        }

        // ════════════════════════════════════════════════════════
        // PRIVATE: Fetch QC data
        // ════════════════════════════════════════════════════════
        private static List<QcRow> FetchQcData(SqlConnection conn)
        {
            var list = new List<QcRow>();

            // BUG 2 FIX: logic ngày sản xuất phải khớp với Excel
            // Ca sáng 08:00-20:00 → ProdDate = ngày đó
            // Ca tối  20:00-07:59 → ProdDate = ngày bắt đầu ca (20:00)
            // => Bất kỳ record nào có giờ < 08:00 → thuộc ngày hôm trước
            const string sql = @"
                WITH Norm AS (
                    SELECT
                        CASE
                            WHEN CAST(Date_Time AS TIME) < '08:00:00'
                            THEN CAST(DATEADD(day, -1, Date_Time) AS DATE)
                            ELSE CAST(Date_Time AS DATE)
                        END AS ProdDate,

                        -- BUG 5: giữ lại giờ để endpoint hourly dùng
                        DATEPART(HOUR, Date_Time) AS ProdHour,

                        LTRIM(RTRIM(Assy_PN)) AS Model,
                        CASE WHEN Remark = 'To packing'
                             THEN 1 ELSE 0 END AS IsPass,
                        CASE WHEN FailCode IS NOT NULL
                              AND LTRIM(RTRIM(FailCode)) <> ''
                             THEN 1 ELSE 0 END AS IsFail

                    FROM [Production_SZ].[dbo].[QC_Inspection_Log]
                    WHERE Date_Time >= DATEADD(day, -40, GETDATE())
                      AND (Remark IS NULL OR Remark <> 'Production Test')
                )
                SELECT
                    ProdDate, ProdHour, Model,
                    SUM(IsPass) AS Actual,
                    SUM(IsFail) AS QcFail
                FROM Norm
                GROUP BY ProdDate, ProdHour, Model";

            using var cmd = new SqlCommand(sql, conn);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                list.Add(new QcRow
                {
                    ProdDate = Convert.ToDateTime(rdr["ProdDate"]),
                    ProdHour = Convert.ToInt32(rdr["ProdHour"]),
                    Model = rdr["Model"].ToString(),
                    Actual = Convert.ToInt32(rdr["Actual"]),
                    QcFail = Convert.ToInt32(rdr["QcFail"]),
                });
            }
            return list;
        }

        // ════════════════════════════════════════════════════════
        // PRIVATE: Fetch MRB + Rework data  (BUG 6 FIX)
        // ════════════════════════════════════════════════════════
        private static List<MrbRow> FetchMrbData(SqlConnection conn)
        {
            var list = new List<MrbRow>();

            // BUG 6: kéo từ bảng Aavid_MRB_Rework_Log_Station
            const string sql = @"
        WITH NormMrb AS (
            SELECT
                CASE
                    WHEN CAST(Date_Time AS TIME) < '08:00:00'
                    THEN CAST(DATEADD(day, -1, Date_Time) AS DATE)
                    ELSE CAST(Date_Time AS DATE)
                END AS ProdDate,
                LTRIM(RTRIM(Assy_PN)) AS Model,
                1 AS IsMrb,
                
                -- ĐÃ FIX Ở ĐÂY: Xử lý an toàn mọi kiểu dữ liệu (chuỗi, số, rỗng, True/False)
                CASE WHEN Scrapped IS NOT NULL 
                      AND LTRIM(RTRIM(Scrapped)) NOT IN ('', '0', 'False', 'false') 
                     THEN 1 ELSE 0 END AS IsScrapped,
                     
                CASE WHEN RW_Date_Time IS NOT NULL
                     THEN 1 ELSE 0 END AS IsReworked
            FROM [Production_SZ].[dbo].[Aavid_MRB_Rework_Log_Station]
            WHERE Date_Time >= DATEADD(day, -40, GETDATE())
        )
        SELECT
            ProdDate, Model,
            SUM(IsMrb)     AS MrbCount,
            SUM(IsScrapped) AS Scrapped,
            SUM(IsReworked) AS Reworked
        FROM NormMrb
        GROUP BY ProdDate, Model";

            using var cmd = new SqlCommand(sql, conn);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                list.Add(new MrbRow
                {
                    ProdDate = Convert.ToDateTime(rdr["ProdDate"]),
                    Model = rdr["Model"].ToString(),
                    MrbCount = Convert.ToInt32(rdr["MrbCount"]),
                    Scrapped = Convert.ToInt32(rdr["Scrapped"]),
                    Reworked = Convert.ToInt32(rdr["Reworked"]),
                });
            }
            return list;
        }

        // ════════════════════════════════════════════════════════
        // PRIVATE: Build final JSON for dashboard
        // ════════════════════════════════════════════════════════
        private static List<object> BuildFinalData(
            List<PlanModel> planData,
            List<QcRow> qcRows,
            List<MrbRow> mrbRows)
        {
            var finalData = new List<object>();

            // BUG 1 FIX: Giữ WorkOrder trong key
            // Nếu cùng Line + Model + WO, chạy 2 ca → vẫn group để cộng target
            // Nếu cùng Line + Model nhưng WO khác → hiển thị riêng 2 thẻ
            var dailyPlans = planData
                .GroupBy(p => new {
                    p.ProdDate,
                    p.Model,
                    p.Line,
                    p.WorkOrder   // BUG 1: giữ WO để phân biệt
                })
                .Select(g => new
                {
                    ProdDate = g.Key.ProdDate,
                    Model = g.Key.Model,
                    Line = g.Key.Line,
                    WorkOrder = g.Key.WorkOrder,
                    // Cộng target tất cả ca có cùng Line+Model+WO trong ngày
                    TotalTarget = g.Sum(x => x.Target),
                    // Ghi nhận các ca để hiển thị tooltip
                    Shifts = string.Join(" + ", g.Select(x => x.Shift)
                                                      .Where(s => !string.IsNullOrEmpty(s))
                                                      .Distinct()),
                    ShiftCount = g.Count()
                })
                .ToList();

            // Group by Date+Model để chia actual cho các Line
            var byDateModel = dailyPlans
                .GroupBy(p => new { p.ProdDate, p.Model })
                .ToList();

            foreach (var group in byDateModel)
            {
                // Lấy QC data cho ngày+model này (cộng tất cả giờ)
                var qcForDay = qcRows
                    .Where(q => q.Model == group.Key.Model
                             && q.ProdDate == group.Key.ProdDate)
                    .ToList();

                int totalActual = qcForDay.Sum(q => q.Actual);
                int totalQcFail = qcForDay.Sum(q => q.QcFail);

                // BUG 6: MRB data
                var mrbForDay = mrbRows
                    .FirstOrDefault(m => m.Model == group.Key.Model
                                      && m.ProdDate == group.Key.ProdDate);

                int totalMrb = mrbForDay?.MrbCount ?? 0;
                int totalScrapped = mrbForDay?.Scrapped ?? 0;
                int totalReworked = mrbForDay?.Reworked ?? 0;

                // BUG 3 FIX: FPY = đơn vị qua QC lần đầu không có lỗi
                // FPY thực = (Actual pass - QC fail units) / (Actual + QC fail) * 100
                // Đơn giản và chính xác hơn:
                // FPY% = Actual / (Actual + QcFail) * 100  (% lần đầu đạt)
                int totalInspected = totalActual + totalQcFail;
                double fpy = totalInspected > 0
                    ? Math.Round(totalActual / (double)totalInspected * 100, 1)
                    : 0;

                // True MRB% (scrapped/total shipped)
                int totalShipped = totalActual + totalScrapped;
                double mrbPct = totalShipped > 0
                    ? Math.Round(totalScrapped / (double)totalShipped * 100, 1)
                    : 0;

                // Chia đều actual cho các line chạy cùng model trong ngày
                int lineCount = group.Count();
                int actPerLine = lineCount > 0 ? totalActual / lineCount : 0;
                int mrbPerLine = lineCount > 0 ? totalMrb / lineCount : 0;
                int scrapPerLine = lineCount > 0 ? totalScrapped / lineCount : 0;
                int remainder = lineCount > 0 ? totalActual % lineCount : 0;

                int idx = 0;
                foreach (var plan in group.OrderBy(p => p.Line))
                {
                    int lineActual = actPerLine + (idx == 0 ? remainder : 0);

                    // BUG 3 FIX: OEE per line
                    // Performance = Actual / Target
                    // Quality     = FPY (từ QC data thật)
                    // Availability= (ShiftMinutes - Downtime) / ShiftMinutes
                    //               → downtime chưa có → dùng 100% tạm
                    //               → khi Andon có dữ liệu downtime thì gán vào đây
                    double perfPct = plan.TotalTarget > 0
                        ? Math.Round(lineActual / (double)plan.TotalTarget * 100, 1)
                        : 0;

                    double availPct = 100.0; // TODO: thay bằng (480 - downtimeMin) / 480 * 100 từ Andon
                    double oeePct = Math.Round(availPct / 100 * perfPct / 100 * fpy / 100 * 100, 1);

                    finalData.Add(new
                    {
                        // Thông tin Plan (Excel)
                        date = plan.ProdDate.ToString("yyyy-MM-dd"),
                        model = plan.Model,
                        line = plan.Line,
                        workOrder = plan.WorkOrder,
                        shifts = plan.Shifts,       // BUG 1: truyền xuống để JS hiển thị

                        // Target từ Excel (đã gộp theo WO)
                        target = plan.TotalTarget,

                        // Actual từ SQL (đã chia đều theo line)
                        actual = lineActual,

                        // BUG 6: MRB breakdown từ 2 bảng
                        mrb = mrbPerLine,
                        scrapped = scrapPerLine,
                        reworked = lineCount > 0 ? (mrbForDay?.Reworked ?? 0) / lineCount : 0,
                        qcFailCount = lineCount > 0 ? totalQcFail / lineCount : 0,

                        // BUG 3: FPY và OEE tính từ data thật
                        fpy = fpy,
                        oee = oeePct,
                        perfPct = perfPct,
                        availPct = availPct,
                        mrbPct = mrbPct,
                    });
                    idx++;
                }
            }

            // BUG 2 FIX: Không để lọt data rỗng — chỉ trả về dòng có target > 0
            return finalData.Cast<dynamic>()
                            .Where(d => d.target > 0)
                            .Cast<object>()
                            .ToList();
        }
    }
}