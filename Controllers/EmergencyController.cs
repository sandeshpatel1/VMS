using DwaarixVMS.Data;
using DwaarixVMS.Helpers;
using DwaarixVMS.Models;
using DwaarixVMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DwaarixVMS.Controllers;

public class EmergencyController : AppController
{
    private readonly NotificationService _notify;
    public EmergencyController(AppDbContext db, NotificationService notify) : base(db) => _notify = notify;

    public async Task<IActionResult> Index()
    {
        ViewBag.Alerts = await Db.Emergencies.Include(e => e.RaisedBy)
            .OrderBy(e => e.IsResolved).ThenByDescending(e => e.RaisedAt).Take(30).ToListAsync();
        return View(new EmergencyVM());
    }

    [HttpPost]
    public async Task<IActionResult> Raise(EmergencyVM vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Alerts = await Db.Emergencies.Include(e => e.RaisedBy)
                .OrderBy(e => e.IsResolved).ThenByDescending(e => e.RaisedAt).Take(30).ToListAsync();
            return View("Index", vm);
        }

        var e = new Emergency { Type = vm.Type, Message = vm.Message.Trim(), RaisedById = User.UserId() };
        Db.Emergencies.Add(e);
        await Db.SaveChangesAsync();

        var who = IsResident ? "A resident" : "Security";
        await _notify.ToEveryoneAsync($"Emergency: {e.Type}", $"{who} raised an alert. {e.Message}", "emergency");
        FlashOk("Emergency alert sent to everyone.");
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Staff), HttpPost]
    public async Task<IActionResult> Resolve(int id)
    {
        var e = await Db.Emergencies.FindAsync(id);
        if (e == null) return NotFound();
        if (!e.IsResolved)
        {
            e.IsResolved = true;
            e.ResolvedAt = DateTime.Now;
            await Db.SaveChangesAsync();
            await _notify.ToEveryoneAsync("Emergency resolved", $"The {e.Type} alert has been resolved.", "info");
            FlashOk("Alert marked as resolved.");
        }
        return RedirectToAction(nameof(Index));
    }
}
