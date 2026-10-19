using System.Text;
using DwaarixVMS.Data;
using DwaarixVMS.Helpers;
using DwaarixVMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DwaarixVMS.Controllers;

[Authorize(Roles = Roles.Admin)]
public class ReportsController : AppController
{
    public ReportsController(AppDbContext db) : base(db) { }

    private async Task<(DateTime f, DateTime t, List<Visitor> rows)> LoadAsync(DateTime? from, DateTime? to)
    {
        var f = (from ?? DateTime.Today.AddDays(-6)).Date;
        var t = (to ?? DateTime.Today).Date;
        if (t < f) (f, t) = (t, f);
        var end = t.AddDays(1);
        var rows = await Db.Visitors.Include(v => v.Resident)
            .Where(v => v.CreatedAt >= f && v.CreatedAt < end)
            .OrderByDescending(v => v.CreatedAt).ToListAsync();
        return (f, t, rows);
    }

    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        var (f, t, rows) = await LoadAsync(from, to);
        return View(new ReportVM
        {
            From = f,
            To = t,
            Rows = rows,
            ByStatus = rows.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count()),
            ByPurpose = rows.GroupBy(r => r.Purpose).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()),
            ByDay = rows.GroupBy(r => r.CreatedAt.Date).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count())
        });
    }

    public async Task<IActionResult> Export(DateTime? from, DateTime? to)
    {
        var (f, t, rows) = await LoadAsync(from, to);
        var sb = new StringBuilder();
        sb.AppendLine("Visitor ID,Name,Mobile,Purpose,Flat,Resident,Status,Registered,In time,Out time,Remarks");
        foreach (var v in rows)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                Ui.CsvCell(v.VisitorCode), Ui.CsvCell(v.VisitorName), Ui.CsvCell(v.Mobile), Ui.CsvCell(v.Purpose),
                Ui.CsvCell(v.Resident.FlatNo), Ui.CsvCell(v.Resident.FullName), Ui.CsvCell(v.Status),
                Ui.CsvCell(v.CreatedAt.ToString("yyyy-MM-dd HH:mm")),
                Ui.CsvCell(v.InTime?.ToString("yyyy-MM-dd HH:mm")), Ui.CsvCell(v.OutTime?.ToString("yyyy-MM-dd HH:mm")),
                Ui.CsvCell(v.Remarks)
            }));
        }
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"visitors_{f:yyyyMMdd}_{t:yyyyMMdd}.csv");
    }
}
