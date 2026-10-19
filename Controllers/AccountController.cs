using System.Security.Claims;
using DwaarixVMS.Data;
using DwaarixVMS.Helpers;
using DwaarixVMS.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DwaarixVMS.Controllers;

public class AccountController : AppController
{
    public AccountController(AppDbContext db) : base(db) { }

    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginVM());
    }

    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(LoginVM vm, string? returnUrl)
    {
        ViewBag.ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return View(vm);

        var username = vm.Username.Trim();
        var user = await Db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null || !user.IsActive || !Passwords.Verify(vm.Password, user.PasswordHash))
        {
            ModelState.AddModelError("", "The username or password is wrong, or the account is switched off.");
            return View(vm);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role)
        };
        if (user.ResidentId.HasValue) claims.Add(new Claim("ResidentId", user.ResidentId.Value.ToString()));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = vm.RememberMe, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(vm.RememberMe ? 72 : 8) });

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet] public IActionResult ChangePassword() => View(new ChangePasswordVM());

    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordVM vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var user = await Db.Users.FindAsync(User.UserId());
        if (user == null || !Passwords.Verify(vm.CurrentPassword, user.PasswordHash))
        {
            ModelState.AddModelError(nameof(vm.CurrentPassword), "Your current password is not correct");
            return View(vm);
        }
        user.PasswordHash = Passwords.Hash(vm.NewPassword);
        await Db.SaveChangesAsync();
        FlashOk("Password changed.");
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous] public IActionResult AccessDenied() => View();
}
