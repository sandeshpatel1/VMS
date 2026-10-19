using DwaarixVMS.Data;
using DwaarixVMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DwaarixVMS.Controllers;

[Authorize]
public abstract class AppController : Controller
{
    protected readonly AppDbContext Db;
    protected AppController(AppDbContext db) => Db = db;

    protected bool IsResident => User.IsInRole(Roles.Resident);

    protected async Task LoadResidentsAsync()
    {
        var list = await Db.Residents.Where(r => r.IsActive).OrderBy(r => r.FlatNo).ThenBy(r => r.FullName).ToListAsync();
        ViewBag.Residents = list.Select(r => new SelectListItem(r.Label, r.Id.ToString())).ToList();
    }

    protected void FlashOk(string message) => TempData["Success"] = message;
    protected void FlashErr(string message) => TempData["Error"] = message;

    protected IActionResult BackTo(string? returnUrl, string action, object? routeValues = null) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? (IActionResult)LocalRedirect(returnUrl)
            : RedirectToAction(action, routeValues);
}
