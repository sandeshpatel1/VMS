using System.Security.Cryptography;
using DwaarixVMS.Data;
using DwaarixVMS.Helpers;
using DwaarixVMS.Models;
using DwaarixVMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DwaarixVMS.Controllers;

public class PreApprovalsController : AppController
{
    private readonly NotificationService _notify;
    public PreApprovalsController(AppDbContext db, NotificationService notify) : base(db) => _notify = notify;

    private IQueryable<PreApproval> Scoped()
    {
        IQueryable<PreApproval> q = Db.PreApprovals.Include(p => p.Resident);
        if (IsResident)
        {
            var rid = User.ResidentId() ?? -1;
            q = q.Where(p => p.ResidentId == rid);
        }
        return q;
    }

    public async Task<IActionResult> Index(string? q, bool all = false)
    {
        var now = DateTime.Now;
        var query = Scoped();
        if (!all) query = query.Where(p => p.Status == "Active" && p.ValidTill >= now);
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(p => p.VisitorName.Contains(q) || p.Mobile.Contains(q) || p.Resident.FlatNo.Contains(q) || p.AccessCode == q);
        }
        ViewBag.Q = q;
        ViewBag.All = all;
        return View(await query.OrderByDescending(p => p.CreatedAt).Take(200).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadResidentsAsync();
        var now = DateTime.Now;
        var start = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
        return View(new PreApprovalVM { ValidFrom = start, ValidTill = start.AddDays(1) });
    }

    [HttpPost]
    public async Task<IActionResult> Create(PreApprovalVM vm)
    {
        int residentId;
        if (IsResident)
        {
            residentId = User.ResidentId() ?? 0;
            ModelState.Remove(nameof(vm.ResidentId));
        }
        else
        {
            if (vm.ResidentId == null) ModelState.AddModelError(nameof(vm.ResidentId), "Select the flat or resident");
            residentId = vm.ResidentId ?? 0;
        }
        var resident = await Db.Residents.FirstOrDefaultAsync(r => r.Id == residentId && r.IsActive);
        if (resident == null && ModelState.IsValid) ModelState.AddModelError(nameof(vm.ResidentId), "This resident is not available");

        if (!ModelState.IsValid)
        {
            await LoadResidentsAsync();
            return View(vm);
        }

        var p = new PreApproval
        {
            ResidentId = resident!.Id,
            VisitorName = vm.VisitorName.Trim(),
            Mobile = vm.Mobile,
            Category = vm.Category,
            ValidFrom = vm.ValidFrom,
            ValidTill = vm.ValidTill,
            IsRecurring = vm.IsRecurring,
            Status = "Active",
            AccessCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(),
            CreatedById = User.UserId()
        };
        Db.PreApprovals.Add(p);
        await Db.SaveChangesAsync();

        await _notify.ToStaffAsync("Pre-approved visitor",
            $"{p.Category} - {p.VisitorName} for {resident.FlatNo}, valid till {Ui.Dt(p.ValidTill)}.", "pre-approved");

        FlashOk($"Pre-approval saved. Access code: {p.AccessCode}");
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Staff), HttpPost]
    public async Task<IActionResult> CheckIn(int id)
    {
        var p = await Scoped().FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        var now = DateTime.Now;
        if (p.Status != "Active" || now < p.ValidFrom || now > p.ValidTill)
        {
            FlashErr("This pre-approval is not valid right now.");
            return RedirectToAction(nameof(Index));
        }

        var v = new Visitor
        {
            VisitorCode = await CodeGen.NextVisitorCodeAsync(Db),
            VisitorName = p.VisitorName,
            Mobile = p.Mobile,
            Purpose = p.Category,
            ResidentId = p.ResidentId,
            ExpectedTime = now,
            Status = VisitStatus.CheckedIn,
            InTime = now,
            ValidTill = p.ValidTill,
            PreApprovalId = p.Id,
            CreatedById = User.UserId(),
            CreatedAt = now
        };
        Db.Visitors.Add(v);
        if (!p.IsRecurring) p.Status = "Used";
        await Db.SaveChangesAsync();

        await _notify.ToResidentAsync(p.ResidentId, "Pre-approved visitor arrived", $"{p.VisitorName} ({p.Category}) entered at {Ui.Tm(now)}.", "visitor-in");
        FlashOk("Pre-approved visitor checked in.");
        return RedirectToAction("Details", "Visitors", new { id = v.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var p = await Scoped().FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        if (p.Status == "Active")
        {
            p.Status = "Cancelled";
            await Db.SaveChangesAsync();
            FlashOk("Pre-approval cancelled.");
        }
        return RedirectToAction(nameof(Index));
    }
}
