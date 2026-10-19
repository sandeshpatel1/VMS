using DwaarixVMS.Data;
using DwaarixVMS.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DwaarixVMS.Controllers;

public class NotificationsController : AppController
{
    public NotificationsController(AppDbContext db) : base(db) { }

    public async Task<IActionResult> Index()
    {
        var uid = User.UserId();
        var items = await Db.Notifications.Where(n => n.UserId == uid)
            .OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync();
        return View(items);
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllRead()
    {
        var uid = User.UserId();
        var unread = await Db.Notifications.Where(n => n.UserId == uid && !n.IsRead).ToListAsync();
        foreach (var n in unread) n.IsRead = true;
        await Db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> MarkRead(int id)
    {
        var uid = User.UserId();
        var n = await Db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid);
        if (n != null) { n.IsRead = true; await Db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }
}
