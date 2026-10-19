using DwaarixVMS.Data;
using DwaarixVMS.Helpers;
using DwaarixVMS.Models;
using DwaarixVMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DwaarixVMS.Controllers;

public class DeliveriesController : AppController
{
    private readonly NotificationService _notify;
    public DeliveriesController(AppDbContext db, NotificationService notify) : base(db) => _notify = notify;

    public async Task<IActionResult> Index(string? q, bool pendingOnly = false)
    {
        IQueryable<Delivery> query = Db.Deliveries.Include(d => d.Resident);
        if (IsResident)
        {
            var rid = User.ResidentId() ?? -1;
            query = query.Where(d => d.ResidentId == rid);
        }
        if (pendingOnly) query = query.Where(d => d.CollectedAt == null);
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(d => d.PersonName.Contains(q) || d.Company.Contains(q) || d.Resident.FlatNo.Contains(q));
        }
        ViewBag.Q = q;
        ViewBag.PendingOnly = pendingOnly;
        return View(await query.OrderByDescending(d => d.ArrivedAt).Take(200).ToListAsync());
    }

    [Authorize(Roles = Roles.Staff), HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadResidentsAsync();
        return View(new DeliveryVM());
    }

    [Authorize(Roles = Roles.Staff), HttpPost]
    public async Task<IActionResult> Create(DeliveryVM vm)
    {
        if (vm.ResidentId.HasValue && !await Db.Residents.AnyAsync(r => r.Id == vm.ResidentId.Value && r.IsActive))
            ModelState.AddModelError(nameof(vm.ResidentId), "This resident is not available");
        if (!ModelState.IsValid)
        {
            await LoadResidentsAsync();
            return View(vm);
        }

        var d = new Delivery
        {
            PersonName = vm.PersonName.Trim(),
            Mobile = vm.Mobile,
            Company = vm.Company,
            ResidentId = vm.ResidentId!.Value,
            Remarks = string.IsNullOrWhiteSpace(vm.Remarks) ? null : vm.Remarks.Trim(),
            ArrivedAt = DateTime.Now,
            CreatedById = User.UserId()
        };
        Db.Deliveries.Add(d);
        await Db.SaveChangesAsync();

        await _notify.ToResidentAsync(d.ResidentId, "Delivery arrived", $"{d.Company} delivery by {d.PersonName} is at the gate.", "delivery");
        FlashOk("Delivery saved and the resident has been told.");
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Staff), HttpPost]
    public async Task<IActionResult> Collect(int id)
    {
        var d = await Db.Deliveries.FindAsync(id);
        if (d == null) return NotFound();
        if (d.CollectedAt == null)
        {
            d.CollectedAt = DateTime.Now;
            await Db.SaveChangesAsync();
            FlashOk("Marked as collected.");
        }
        return RedirectToAction(nameof(Index));
    }
}
