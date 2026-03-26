using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

public class KpiController : Controller
{
    private readonly ProdDbContext _prod;   // DB sản xuất (plan/actual/defect/revenue)
    private readonly AndonDbContext _andon; // DB andon (downtime, alarm)
    public KpiController(ProdDbContext prod, AndonDbContext andon)
    { _prod = prod; _andon = andon; }

    // Trang chính
    public IActionResult Index() => View();

    // API: dữ liệu tổng quan (cards + filters)
    [HttpGet]
    public async Task<IActionResult> Overview(DateTime? from, DateTime? to,
        string plant = null, string category = null, string product = null, string supervisor = null)
    {
        var f = from ?? DateTime.Today.AddDays(-30);
        var t = to ?? DateTime.Today.AddDays(1);

        // Quantity produced / Defects / Cost / Revenue / Efficiency
        var qty = await _prod.Productions
            .Where(x => x.Timestamp >= f && x.Timestamp < t)
            .SumAsync(x => (int?)x.GoodQty) ?? 0;

        var defects = await _prod.Productions
            .Where(x => x.Timestamp >= f && x.Timestamp < t)
            .SumAsync(x => (int?)x.DefectQty) ?? 0;

        var prodCost = await _prod.Costs
            .Where(x => x.Timestamp >= f && x.Timestamp < t)
            .SumAsync(x => (decimal?)x.ProductionCost) ?? 0m;

        var revenue = await _prod.Sales
            .Where(x => x.Timestamp >= f && x.Timestamp < t)
            .SumAsync(x => (decimal?)x.Revenue) ?? 0m;

        var efficiency = qty == 0 ? 0 : Math.Round((qty - defects) * 100.0 / qty, 2);

        // Revenue vs Production Cost by product
        var revCostByProduct = await _prod.Productions
            .Where(x => x.Timestamp >= f && x.Timestamp < t)
            .GroupBy(x => x.ProductName)
            .Select(g => new { label = g.Key, revenue = g.Sum(x => x.Revenue), cost = g.Sum(x => x.ProductionCost) })
            .ToListAsync();

        // Quantity produced by plant (pie)
        var qtyByPlant = await _prod.Productions
            .Where(x => x.Timestamp >= f && x.Timestamp < t)
            .GroupBy(x => x.Plant)
            .Select(g => new { label = g.Key, value = g.Sum(x => x.GoodQty) })
            .ToListAsync();

        // Defects by category (pie)
        var defectsByCat = await _prod.Productions
            .Where(x => x.Timestamp >= f && x.Timestamp < t)
            .GroupBy(x => x.Category)
            .Select(g => new { label = g.Key, value = g.Sum(x => x.DefectQty) })
            .ToListAsync();

        // Sales by category & month (heatmap)
        var salesCatMonth = await _prod.Sales
            .Where(x => x.Timestamp >= f && x.Timestamp < t)
            .GroupBy(x => new { x.Category, M = x.Timestamp.Month })
            .Select(g => new { g.Key.Category, g.Key.M, value = g.Sum(x => x.Revenue) })
            .ToListAsync();

        // Downtime (Andon) real-time
        var downByLine = await _andon.Tickets
            .Where(x => x.ReportedAt >= f && x.ReportedAt < t && x.Status != 5)
            .GroupBy(x => x.LineNumber)
            .Select(g => new {
                line = g.Key,
                downMinutes = g.Sum(x => EF.Functions.DateDiffMinute(x.ReportedAt, x.TechFixedAt ?? DateTime.UtcNow))
            }).ToListAsync();

        return Json(new
        {
            cards = new
            {
                quantity = qty,
                defects = defects,
                productionCost = prodCost,
                revenue = revenue,
                efficiency = efficiency
            },
            revCostByProduct,
            qtyByPlant,
            defectsByCat,
            salesCatMonth,
            downtime = downByLine
        });
    }
}