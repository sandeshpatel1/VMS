using DwaarixVMS.Data;
using DwaarixVMS.Helpers;
using DwaarixVMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DwaarixVMS.Controllers;

[Authorize(Roles = Roles.Admin)]
public class UsersController : AppController
{
    public UsersController(AppDbContext db) : base(db) { }

    public async Task<IActionResult> Index() =>
        View(await Db.Users.Include(u => u.Resident).OrderBy(u => u.Role).ThenBy(u => u.FullName).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadResidentsAsync();
        return View("Form", new UserVM());
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var u = await Db.Users.FindAsync(id);
        if (u == null) return NotFound();
        await LoadResidentsAsync();
        return View("Form", new UserVM { Id = u.Id, FullName = u.FullName, Username = u.Username, Role = u.Role, Mobile = u.Mobile, ResidentId = u.ResidentId, IsActive = u.IsActive });
    }

    [HttpPost]
    public async Task<IActionResult> Save(UserVM vm)
    {
        var username = vm.Username.Trim();
        if (await Db.Users.AnyAsync(u => u.Username == username && u.Id != vm.Id))
            ModelState.AddModelError(nameof(vm.Username), "This username is taken");
        if (vm.Id == User.UserId() && (!vm.IsActive || vm.Role != Roles.Admin))
            ModelState.AddModelError(nameof(vm.Role), "You cannot switch off or demote your own account");
        if (vm.ResidentId.HasValue && !await Db.Residents.AnyAsync(r => r.Id == vm.ResidentId.Value))
            ModelState.AddModelError(nameof(vm.ResidentId), "Select a valid resident");

        if (!ModelState.IsValid)
        {
            await LoadResidentsAsync();
            return View("Form", vm);
        }

        AppUser? u;
        if (vm.Id == 0)
        {
            u = new AppUser { PasswordHash = Passwords.Hash(vm.Password!) };
            Db.Users.Add(u);
        }
        else
        {
            u = await Db.Users.FindAsync(vm.Id);
            if (u == null) return NotFound();
            if (!string.IsNullOrWhiteSpace(vm.Password)) u.PasswordHash = Passwords.Hash(vm.Password);
        }

        u.FullName = vm.FullName.Trim();
        u.Username = username;
        u.Role = vm.Role;
        u.Mobile = string.IsNullOrWhiteSpace(vm.Mobile) ? null : vm.Mobile;
        u.ResidentId = vm.Role == Roles.Resident ? vm.ResidentId : null;
        u.IsActive = vm.IsActive;
        await Db.SaveChangesAsync();

        FlashOk(vm.Id == 0 ? "User created." : "User updated.");
        return RedirectToAction(nameof(Index));
    }
}
