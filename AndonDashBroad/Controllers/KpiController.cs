using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;

namespace AndonDashBroad.Controllers
{
    public class KpiController : Controller
    {
        // -------------------------------------------------------------
        // TODO: SAU NÀY KHI CÓ DB SẢN XUẤT, SẾP KHAI BÁO VÀO ĐÂY:
        // private readonly ProdDbContext _prodDb;
        // public KpiController(ProdDbContext prodDb) { _prodDb = prodDb; }
        // -------------------------------------------------------------

        public IActionResult Index()
        {
            var prodList = new List<object>();
            var rnd = new Random();

            // Giả lập Query từ DB Sản Xuất của công ty
            for (int i = 1; i <= 11; i++)
            {
                string lineId = $"L{i:D2}";
                string lineName = $"Line {i:D2}";

                int target = 1500;
                int actual = rnd.Next(800, 1600);
                double mrb = Math.Round(rnd.NextDouble() * 5, 2);
                int stopMin = rnd.Next(0, 120);
                string status = stopMin > 60 ? "red" : (stopMin > 20 ? "yellow" : "green");

                prodList.Add(new
                {
                    id = lineId,
                    nm = lineName,
                    model = "MODEL-X-2026",
                    target = target,
                    actual = actual,
                    mrb = mrb,
                    status = status,
                    andonStop = status != "green" ? 1 : 0,
                    stopCount = status != "green" ? rnd.Next(1, 4) : 0,
                    stopMin = stopMin,
                    reason = status != "green" ? "Lỗi máy" : "",
                    ewma = status != "green" ? rnd.Next(10, 45) : 0,
                    zscore = (rnd.NextDouble() * 3).ToString("0.1"),
                    pattern = rnd.Next(0, 10) > 7 ? "high" : "low"
                });
            }

            ViewBag.ProductionData = System.Text.Json.JsonSerializer.Serialize(prodList);
            return View();
        }
    }
}