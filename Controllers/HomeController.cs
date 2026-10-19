using System.Diagnostics;
using DwaarixVMS.Data;
using DwaarixVMS.Helpers;
using DwaarixVMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DwaarixVMS.Controllers;

public class HomeController : AppController
{
    public HomeController(AppDbContext db) : base(db) { }

    public async Task<IActionResult> Index()
    {
        var now = DateTime.Now;
        var today = now.Date;
        var tomorrow = today.AddDays(1);

        IQueryable<Visitor> visits = Db.Visitors;
        IQueryable<PreApproval> pre = Db.PreApprovals;
        IQueryable<Delivery> deliveries = Db.Deliveries;
        if (IsResident)
        {
            var rid = User.ResidentId() ?? -1;
            visits = visits.Where(v => v.ResidentId == rid);
            pre = pre.Where(p => p.ResidentId == rid);
            deliveries = deliveries.Where(d => d.ResidentId == rid);
        }

        var vm = new DashboardVM
        {
            Inside = await visits.CountAsync(v => v.Status == VisitStatus.CheckedIn),
            TodayCount = await visits.CountAsync(v => v.CreatedAt >= today && v.CreatedAt < tomorrow),
            Pending = await visits.CountAsync(v => v.Status == VisitStatus.Pending),
            ActivePre = await pre.CountAsync(p => p.Status == "Active" && p.ValidTill >= now),
            Uncollected = await deliveries.CountAsync(d => d.CollectedAt == null),
            OpenEmergencies = await Db.Emergencies.CountAsync(e => !e.IsResolved),
            PendingList = await visits.Include(v => v.Resident).Where(v => v.Status == VisitStatus.Pending)
                .OrderBy(v => v.CreatedAt).Take(5).ToListAsync(),
            InsideList = await visits.Include(v => v.Resident).Where(v => v.Status == VisitStatus.CheckedIn)
                .OrderByDescending(v => v.InTime).Take(5).ToListAsync()
        };
        return View(vm);
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(model: Activity.Current?.Id ?? HttpContext.TraceIdentifier);
}
