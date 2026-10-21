using System.Globalization;
using System.Text;
using DwaarixVMS.Data;
using DwaarixVMS.Helpers;
using DwaarixVMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DwaarixVMS.Controllers;

/// <summary>Everything the report screen can filter by. Binds straight from the query string.</summary>
public class ReportFilter
{
    public string? Preset { get; set; }          // today | 7d | 30d | 90d | month | lastmonth | custom
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Purpose { get; set; }
    public string? Status { get; set; }
    public string? Flat { get; set; }
    public int? LoggedBy { get; set; }
    public string? Q { get; set; }
    public bool Compare { get; set; } = true;
}

[Authorize(Roles = Roles.Admin)]
public class ReportsController : AppController
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public ReportsController(AppDbContext db) : base(db) { }

    // ---------------------------------------------------------------- page shell
    public async Task<IActionResult> Index()
    {
        ViewBag.Purposes = Lists.Purposes;
        ViewBag.Statuses = VisitStatus.All;
        ViewBag.Flats = await Db.Residents.Select(r => r.FlatNo).Distinct().OrderBy(x => x).ToListAsync();
        ViewBag.Staff = (await Db.Users.Where(u => u.Role == Roles.Watchman || u.Role == Roles.Admin)
            .OrderBy(u => u.FullName).ToListAsync())
            .Select(u => new SelectListItem(u.FullName, u.Id.ToString())).ToList();
        return View();
    }

    // ---------------------------------------------------------------- JSON for the dashboard
    [HttpGet]
    public async Task<IActionResult> Data(ReportFilter f)
    {
        var (start, last) = Range(f);
        var end = last.AddDays(1);
        var span = (int)(last - start).TotalDays + 1;
        var prevStart = start.AddDays(-span);

        var rows = await LoadAsync(f, start, end);
        var prev = f.Compare ? await LoadAsync(f, prevStart, start) : new List<Visitor>();
        var hasPrev = f.Compare && prev.Count > 0;

        // ---- time series (hourly for one day, daily up to ~3 months, then weekly)
        var gran = span == 1 ? "hour" : span <= 92 ? "day" : "week";
        var n = gran == "hour" ? 24 : gran == "day" ? span : (int)Math.Ceiling(span / 7.0);
        int Idx(DateTime d, DateTime s) => gran == "hour" ? d.Hour
            : gran == "day" ? (int)(d.Date - s.Date).TotalDays
            : (int)(d.Date - s.Date).TotalDays / 7;
        var cur = new int[n]; var prv = new int[n];
        foreach (var v in rows) { var i = Idx(v.CreatedAt, start); if (i >= 0 && i < n) cur[i]++; }
        foreach (var v in prev) { var i = Idx(v.CreatedAt, prevStart); if (i >= 0 && i < n) prv[i]++; }
        var series = Enumerable.Range(0, n).Select(i => new
        {
            label = gran == "hour" ? DateTime.Today.AddHours(i).ToString("h tt", Inv)
                  : start.AddDays(gran == "day" ? i : i * 7).ToString("dd MMM", Inv),
            key = gran == "hour" ? i.ToString() : start.AddDays(gran == "day" ? i : i * 7).ToString("yyyy-MM-dd"),
            count = cur[i],
            prev = prv[i]
        }).ToList();

        // ---- headline numbers
        var c = Summarise(rows);
        var p = Summarise(prev);
        var inside = await Db.Visitors.CountAsync(v => v.Status == VisitStatus.CheckedIn);
        double? P(double x) => hasPrev ? x : null;
        var kpis = new[]
        {
            Kpi("total", "Visitors", c.Total, P(p.Total), "n", "registered at the gate"),
            Kpi("entered", "Entered", c.Entered, P(p.Entered), "n", "checked in"),
            Kpi("unique", "Unique visitors", c.Unique, P(p.Unique), "n", "different mobile numbers"),
            Kpi("repeat", "Repeat visitors", c.Repeat, P(p.Repeat), "n", "came more than once"),
            Kpi("rej", "Rejection rate", c.Total == 0 ? 0 : 100.0 * c.Rejected / c.Total, P(p.Total == 0 ? 0 : 100.0 * p.Rejected / p.Total), "pct", $"{c.Rejected} rejected"),
            Kpi("stay", "Average stay", c.AvgStay, P(p.AvgStay), "min", "from check-in to check-out"),
            Kpi("pre", "Pre-approved share", c.PreShare, P(p.PreShare), "pct", "entered with a pre-approval"),
            Kpi("inside", "Inside right now", inside, null, "n", "all flats, live")
        };

        // ---- when do people arrive
        var heat = Enumerable.Range(0, 7).Select(_ => new int[24]).ToArray();
        var byHour = new int[24]; var byDow = new int[7];
        foreach (var v in rows)
        {
            var d = ((int)v.CreatedAt.DayOfWeek + 6) % 7;
            heat[d][v.CreatedAt.Hour]++; byHour[v.CreatedAt.Hour]++; byDow[d]++;
        }
        var peakHour = rows.Count == 0 ? -1 : Array.IndexOf(byHour, byHour.Max());
        var peakDay = rows.Count == 0 ? -1 : Array.IndexOf(byDow, byDow.Max());

        // ---- breakdowns
        var purposes = rows.GroupBy(v => v.Purpose)
            .Select(g => new { name = g.Key, count = g.Count(), prev = prev.Count(x => x.Purpose == g.Key) })
            .OrderByDescending(x => x.count).ToList();
        var statuses = rows.GroupBy(v => v.Status)
            .Select(g => new { name = g.Key, count = g.Count() }).OrderByDescending(x => x.count).ToList();
        var approved = rows.Count(v => v.Status is VisitStatus.Approved or VisitStatus.CheckedIn or VisitStatus.CheckedOut);
        var funnel = new[]
        {
            new { label = "Registered", count = rows.Count },
            new { label = "Approved", count = approved },
            new { label = "Entered", count = c.Entered },
            new { label = "Checked out", count = rows.Count(v => v.OutTime.HasValue) }
        };
        var flats = rows.GroupBy(v => v.Resident.FlatNo)
            .Select(g => new { name = g.Key, count = g.Count() }).OrderByDescending(x => x.count).Take(10).ToList();
        var stayByPurpose = rows.Where(v => v.InTime.HasValue && v.OutTime.HasValue)
            .GroupBy(v => v.Purpose)
            .Select(g => new { name = g.Key, count = Math.Round(g.Average(v => (v.OutTime!.Value - v.InTime!.Value).TotalMinutes)) })
            .OrderByDescending(x => x.count).ToList();
        var repeat = rows.GroupBy(v => v.Mobile).Where(g => g.Count() > 1)
            .Select(g => new
            {
                name = g.OrderByDescending(v => v.CreatedAt).First().VisitorName,
                mobile = g.Key,
                count = g.Count(),
                last = g.Max(v => v.CreatedAt).ToString("dd MMM", Inv)
            }).OrderByDescending(x => x.count).Take(8).ToList();
        var staff = rows.GroupBy(v => v.CreatedBy.FullName)
            .Select(g => new { name = g.Key, count = g.Count() }).OrderByDescending(x => x.count).ToList();

        // ---- deliveries and alerts in the same window
        var dq = Db.Deliveries.AsNoTracking().Include(d => d.Resident)
            .Where(d => d.ArrivedAt >= start && d.ArrivedAt < end);
        if (!string.IsNullOrEmpty(f.Flat)) { var flat = f.Flat; dq = dq.Where(d => d.Resident.FlatNo == flat); }
        var dl = await dq.ToListAsync();
        var picked = dl.Where(d => d.CollectedAt.HasValue).Select(d => (d.CollectedAt!.Value - d.ArrivedAt).TotalMinutes).ToList();
        var other = new
        {
            deliveries = dl.Count,
            waiting = dl.Count(d => d.CollectedAt == null),
            avgPickup = picked.Count == 0 ? 0 : Math.Round(picked.Average()),
            emergencies = await Db.Emergencies.CountAsync(e => e.RaisedAt >= start && e.RaisedAt < end),
            byCompany = dl.GroupBy(d => d.Company).Select(g => new { name = g.Key, count = g.Count() }).OrderByDescending(x => x.count).ToList()
        };

        var table = rows.Take(1000).Select(v => new
        {
            id = v.Id,
            code = v.VisitorCode,
            name = v.VisitorName,
            mobile = v.Mobile,
            purpose = v.Purpose,
            flat = v.Resident.FlatNo,
            resident = v.Resident.FullName,
            status = v.Status,
            created = v.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            inTime = v.InTime?.ToString("yyyy-MM-ddTHH:mm:ss"),
            outTime = v.OutTime?.ToString("yyyy-MM-ddTHH:mm:ss"),
            stay = v.InTime.HasValue && v.OutTime.HasValue ? Math.Round((v.OutTime.Value - v.InTime.Value).TotalMinutes) : (double?)null,
            loggedBy = v.CreatedBy.FullName,
            pre = v.PreApprovalId.HasValue
        }).ToList();

        return Json(new
        {
            range = new
            {
                from = start.ToString("yyyy-MM-dd"),
                to = last.ToString("yyyy-MM-dd"),
                span,
                gran,
                label = span == 1 ? start.ToString("dd MMM yyyy", Inv) : $"{start.ToString("dd MMM yyyy", Inv)} to {last.ToString("dd MMM yyyy", Inv)}",
                prevLabel = hasPrev ? (span == 1 ? prevStart.ToString("dd MMM yyyy", Inv) : $"{prevStart.ToString("dd MMM", Inv)} to {start.AddDays(-1).ToString("dd MMM yyyy", Inv)}") : null
            },
            kpis,
            series,
            heat,
            peakHour,
            peakDay,
            purposes,
            statuses,
            funnel,
            flats,
            stayByPurpose,
            repeat,
            staff,
            other,
            rows = table,
            total = rows.Count
        });
    }

    // ---------------------------------------------------------------- CSV of exactly what is on screen
    public async Task<IActionResult> Export(ReportFilter f)
    {
        var (start, last) = Range(f);
        var rows = await LoadAsync(f, start, last.AddDays(1));
        var sb = new StringBuilder();
        sb.AppendLine("Visitor ID,Name,Mobile,Purpose,Flat,Resident,Status,Registered,In time,Out time,Stay (min),Pre-approved,Logged by,Remarks");
        foreach (var v in rows)
        {
            var stay = v.InTime.HasValue && v.OutTime.HasValue ? Math.Round((v.OutTime.Value - v.InTime.Value).TotalMinutes).ToString(Inv) : "";
            sb.AppendLine(string.Join(",", new[]
            {
                Ui.CsvCell(v.VisitorCode), Ui.CsvCell(v.VisitorName), Ui.CsvCell(v.Mobile), Ui.CsvCell(v.Purpose),
                Ui.CsvCell(v.Resident.FlatNo), Ui.CsvCell(v.Resident.FullName), Ui.CsvCell(v.Status),
                Ui.CsvCell(v.CreatedAt.ToString("yyyy-MM-dd HH:mm")),
                Ui.CsvCell(v.InTime?.ToString("yyyy-MM-dd HH:mm")), Ui.CsvCell(v.OutTime?.ToString("yyyy-MM-dd HH:mm")),
                Ui.CsvCell(stay), Ui.CsvCell(v.PreApprovalId.HasValue ? "Yes" : "No"), Ui.CsvCell(v.CreatedBy.FullName), Ui.CsvCell(v.Remarks)
            }));
        }
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"visitors_{start:yyyyMMdd}_{last:yyyyMMdd}.csv");
    }

    // ---------------------------------------------------------------- helpers
    private static (DateTime start, DateTime last) Range(ReportFilter f)
    {
        var today = DateTime.Today;
        switch ((f.Preset ?? "7d").ToLowerInvariant())
        {
            case "today": return (today, today);
            case "30d": return (today.AddDays(-29), today);
            case "90d": return (today.AddDays(-89), today);
            case "month": return (new DateTime(today.Year, today.Month, 1), today);
            case "lastmonth":
                {
                    var first = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
                    return (first, first.AddMonths(1).AddDays(-1));
                }
            case "custom":
                {
                    var a = (f.From ?? today.AddDays(-6)).Date;
                    var b = (f.To ?? today).Date;
                    if (b < a) (a, b) = (b, a);
                    if ((b - a).TotalDays > 366) a = b.AddDays(-366);
                    return (a, b);
                }
            default: return (today.AddDays(-6), today);
        }
    }

    private async Task<List<Visitor>> LoadAsync(ReportFilter f, DateTime start, DateTime endExclusive)
    {
        IQueryable<Visitor> q = Db.Visitors.AsNoTracking()
            .Include(v => v.Resident).Include(v => v.CreatedBy)
            .Where(v => v.CreatedAt >= start && v.CreatedAt < endExclusive);

        var purpose = f.Purpose; var status = f.Status; var flat = f.Flat; var by = f.LoggedBy; var text = f.Q?.Trim();
        if (!string.IsNullOrEmpty(purpose)) q = q.Where(v => v.Purpose == purpose);
        if (!string.IsNullOrEmpty(status)) q = q.Where(v => v.Status == status);
        if (!string.IsNullOrEmpty(flat)) q = q.Where(v => v.Resident.FlatNo == flat);
        if (by.HasValue) q = q.Where(v => v.CreatedById == by.Value);
        if (!string.IsNullOrEmpty(text))
            q = q.Where(v => v.VisitorName.Contains(text) || v.Mobile.Contains(text) || v.VisitorCode.Contains(text));

        return await q.OrderByDescending(v => v.CreatedAt).Take(20000).ToListAsync();
    }

    private sealed record Calc(int Total, int Entered, int Rejected, int Cancelled, int Pending,
                               double AvgStay, int Unique, int Repeat, double PreShare);

    private static Calc Summarise(List<Visitor> l)
    {
        var stays = l.Where(v => v.InTime.HasValue && v.OutTime.HasValue)
            .Select(v => (v.OutTime!.Value - v.InTime!.Value).TotalMinutes).ToList();
        return new Calc(
            l.Count,
            l.Count(v => v.InTime.HasValue),
            l.Count(v => v.Status == VisitStatus.Rejected),
            l.Count(v => v.Status == VisitStatus.Cancelled),
            l.Count(v => v.Status == VisitStatus.Pending),
            stays.Count == 0 ? 0 : stays.Average(),
            l.Select(v => v.Mobile).Distinct().Count(),
            l.GroupBy(v => v.Mobile).Count(g => g.Count() > 1),
            l.Count == 0 ? 0 : 100.0 * l.Count(v => v.PreApprovalId.HasValue) / l.Count);
    }

    /// <summary>Change against the previous period: percent for counts, percentage points for rates.</summary>
    private static object Kpi(string id, string label, double value, double? prev, string fmt, string hint)
    {
        double? delta = null;
        if (prev.HasValue)
        {
            if (fmt == "pct") delta = Math.Round(value - prev.Value, 1);
            else if (prev.Value > 0) delta = Math.Round((value - prev.Value) / prev.Value * 100, 1);
        }
        return new { id, label, value = Math.Round(value, 1), prev, delta, unit = fmt == "pct" ? "pts" : "%", fmt, hint };
    }
}