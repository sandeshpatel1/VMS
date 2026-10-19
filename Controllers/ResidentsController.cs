using DwaarixVMS.Data;
using DwaarixVMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DwaarixVMS.Controllers;

[Authorize(Roles = Roles.Staff)]
public class ResidentsController : AppController
{
    public ResidentsController(AppDbContext db) : base(db) { }

    public async Task<IActionResult> Index(string? q)
    {
        var query = Db.Residents.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(r => r.FlatNo.Contains(q) || r.FullName.Contains(q) || r.Mobile.Contains(q));
        }
        ViewBag.Q = q;
        return View(await query.OrderBy(r => r.FlatNo).ThenBy(r => r.FullName).ToListAsync());
    }

    [Authorize(Roles = Roles.Admin), HttpGet]
    public IActionResult Create() => View("Form", new Resident());

    [Authorize(Roles = Roles.Admin), HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var r = await Db.Residents.FindAsync(id);
        return r == null ? NotFound() : View("Form", r);
    }

    [Authorize(Roles = Roles.Admin), HttpPost]
    public async Task<IActionResult> Save([Bind("Id,FlatNo,FullName,Mobile,Email,IsActive")] Resident model)
    {
        model.FlatNo = (model.FlatNo ?? "").Trim().ToUpper();
        if (!string.IsNullOrEmpty(model.FlatNo) &&
            await Db.Residents.AnyAsync(r => r.Id != model.Id && r.FlatNo == model.FlatNo && r.FullName == model.FullName))
            ModelState.AddModelError(nameof(model.FullName), "This resident is already listed for that flat");

        if (!ModelState.IsValid) return View("Form", model);

        if (model.Id == 0)
        {
            Db.Residents.Add(model);
            FlashOk("Resident added.");
        }
        else
        {
            var existing = await Db.Residents.FindAsync(model.Id);
            if (existing == null) return NotFound();
            existing.FlatNo = model.FlatNo;
            existing.FullName = model.FullName.Trim();
            existing.Mobile = model.Mobile;
            existing.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
            existing.IsActive = model.IsActive;
            FlashOk("Resident updated.");
        }
        await Db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
