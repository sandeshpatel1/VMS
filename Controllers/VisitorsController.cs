using DwaarixVMS.Data;
using DwaarixVMS.Helpers;
using DwaarixVMS.Models;
using DwaarixVMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace DwaarixVMS.Controllers;

public class VisitorsController : AppController
{
    private readonly NotificationService _notify;
    private readonly IWebHostEnvironment _env;

    public VisitorsController(AppDbContext db, NotificationService notify, IWebHostEnvironment env) : base(db)
    {
        _notify = notify;
        _env = env;
    }

    /// <summary>Residents only ever see visits to their own flat.</summary>
    private IQueryable<Visitor> Scoped()
    {
        IQueryable<Visitor> q = Db.Visitors.Include(v => v.Resident);
        if (IsResident)
        {
            var rid = User.ResidentId() ?? -1;
            q = q.Where(v => v.ResidentId == rid);
        }
        return q;
    }

    public async Task<IActionResult> Index(string? q, string? status, DateTime? date)
    {
        var query = Scoped();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(v => v.VisitorName.Contains(q) || v.Mobile.Contains(q) ||
                                     v.VisitorCode.Contains(q) || v.Resident.FlatNo.Contains(q));
        }
        if (!string.IsNullOrEmpty(status)) query = query.Where(v => v.Status == status);
        if (date.HasValue)
        {
            var d = date.Value.Date;
            var next = d.AddDays(1);
            query = query.Where(v => v.CreatedAt >= d && v.CreatedAt < next);
        }
        var items = await query.OrderByDescending(v => v.CreatedAt).Take(200).ToListAsync();
        return View(new VisitorListVM { Items = items, Q = q, Status = status, Date = date });
    }

    [Authorize(Roles = Roles.Staff), HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadResidentsAsync();
        var now = DateTime.Now;
        return View(new VisitorCreateVM { ExpectedTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0) });
    }

    [Authorize(Roles = Roles.Staff), HttpPost]
    public async Task<IActionResult> Create(VisitorCreateVM vm)
    {
        if (vm.ResidentId.HasValue && !await Db.Residents.AnyAsync(r => r.Id == vm.ResidentId.Value && r.IsActive))
            ModelState.AddModelError(nameof(vm.ResidentId), "This resident is not available");

        string? photo = null;
        if (!string.IsNullOrWhiteSpace(vm.PhotoData))
        {
            photo = await SavePhotoAsync(vm.PhotoData);
            if (photo == null) ModelState.AddModelError(nameof(vm.PhotoData), "The photo could not be read. Capture it again or skip it.");
        }

        if (!ModelState.IsValid)
        {
            await LoadResidentsAsync();
            return View(vm);
        }

        var v = new Visitor
        {
            VisitorCode = await CodeGen.NextVisitorCodeAsync(Db),
            VisitorName = vm.VisitorName.Trim(),
            Mobile = vm.Mobile,
            Purpose = vm.Purpose,
            ResidentId = vm.ResidentId!.Value,
            ExpectedTime = vm.ExpectedTime,
            Remarks = string.IsNullOrWhiteSpace(vm.Remarks) ? null : vm.Remarks.Trim(),
            PhotoPath = photo,
            Status = VisitStatus.Pending,
            ValidTill = vm.ExpectedTime.AddHours(8),
            CreatedById = User.UserId(),
            CreatedAt = DateTime.Now
        };
        Db.Visitors.Add(v);
        await Db.SaveChangesAsync();

        await _notify.ToResidentAsync(v.ResidentId, "Visitor at the gate",
            $"{v.VisitorName} ({v.Purpose}) is waiting. Approve or reject the entry.", "visitor-pending");

        FlashOk("Visitor registered. Waiting for the resident to approve.");
        return RedirectToAction(nameof(Details), new { id = v.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var v = await Scoped().Include(x => x.CreatedBy).FirstOrDefaultAsync(x => x.Id == id);
        return v == null ? NotFound() : View(v);
    }

    public async Task<IActionResult> Pass(int id)
    {
        var v = await Scoped().FirstOrDefaultAsync(x => x.Id == id);
        if (v == null) return NotFound();
        if (v.Status is VisitStatus.Pending or VisitStatus.Rejected or VisitStatus.Cancelled)
        {
            FlashErr("A pass is available once the resident approves the visit.");
            return RedirectToAction(nameof(Details), new { id });
        }
        return View(v);
    }

    public async Task<IActionResult> Qr(int id)
    {
        var v = await Scoped().FirstOrDefaultAsync(x => x.Id == id);
        if (v == null) return NotFound();
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(v.VisitorCode, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(8);
        return File(png, "image/png");
    }

    [HttpPost]
    public async Task<IActionResult> Decide(int id, bool approve, string? returnUrl)
    {
        var v = await Scoped().FirstOrDefaultAsync(x => x.Id == id);
        if (v == null) return NotFound();
        if (v.Status != VisitStatus.Pending)
        {
            FlashErr("This visit was already handled.");
            return BackTo(returnUrl, nameof(Details), new { id });
        }

        v.Status = approve ? VisitStatus.Approved : VisitStatus.Rejected;
        await Db.SaveChangesAsync();

        var who = IsResident ? $"Flat {v.Resident.FlatNo}" : $"{User.Identity!.Name} for flat {v.Resident.FlatNo}";
        await _notify.ToStaffAsync(approve ? "Visitor approved" : "Visitor rejected",
            $"{who} {(approve ? "approved" : "rejected")} {v.VisitorName}.", approve ? "visitor-approved" : "visitor-rejected");

        FlashOk(approve ? "Visit approved." : "Visit rejected.");
        return BackTo(returnUrl, nameof(Details), new { id });
    }

    [Authorize(Roles = Roles.Staff), HttpPost]
    public async Task<IActionResult> CheckIn(int id)
    {
        var v = await Scoped().FirstOrDefaultAsync(x => x.Id == id);
        if (v == null) return NotFound();
        if (v.Status != VisitStatus.Approved) { FlashErr("Only approved visitors can be checked in."); return RedirectToAction(nameof(Details), new { id }); }
        if (DateTime.Now > v.ValidTill) { FlashErr("This pass has expired. Register the visitor again."); return RedirectToAction(nameof(Details), new { id }); }

        v.Status = VisitStatus.CheckedIn;
        v.InTime = DateTime.Now;
        await Db.SaveChangesAsync();
        await _notify.ToResidentAsync(v.ResidentId, "Visitor checked in", $"{v.VisitorName} entered at {Ui.Tm(v.InTime)}.", "visitor-in");
        FlashOk("Checked in.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = Roles.Staff), HttpPost]
    public async Task<IActionResult> CheckOut(int id, string? returnUrl)
    {
        var v = await Scoped().FirstOrDefaultAsync(x => x.Id == id);
        if (v == null) return NotFound();
        if (v.Status != VisitStatus.CheckedIn) { FlashErr("This visitor is not inside."); return BackTo(returnUrl, nameof(Details), new { id }); }

        v.Status = VisitStatus.CheckedOut;
        v.OutTime = DateTime.Now;
        await Db.SaveChangesAsync();
        await _notify.ToResidentAsync(v.ResidentId, "Visitor checked out", $"{v.VisitorName} left at {Ui.Tm(v.OutTime)}.", "visitor-out");
        FlashOk("Checked out.");
        return BackTo(returnUrl, nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var v = await Scoped().FirstOrDefaultAsync(x => x.Id == id);
        if (v == null) return NotFound();
        if (v.Status is not (VisitStatus.Pending or VisitStatus.Approved))
        {
            FlashErr("Only visits that have not started can be cancelled.");
            return RedirectToAction(nameof(Details), new { id });
        }
        v.Status = VisitStatus.Cancelled;
        await Db.SaveChangesAsync();

        if (IsResident)
            await _notify.ToStaffAsync("Visit cancelled", $"Flat {v.Resident.FlatNo} cancelled the visit of {v.VisitorName}.", "visitor-cancelled");
        else
            await _notify.ToResidentAsync(v.ResidentId, "Visit cancelled", $"The entry for {v.VisitorName} was cancelled by security.", "visitor-cancelled");

        FlashOk("Entry cancelled.");
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<string?> SavePhotoAsync(string data)
    {
        var marker = data.IndexOf("base64,", StringComparison.Ordinal);
        if (marker < 0) return null;
        var header = data[..marker];
        var ext = header.StartsWith("data:image/jpeg", StringComparison.Ordinal) ? "jpg"
                : header.StartsWith("data:image/png", StringComparison.Ordinal) ? "png" : null;
        if (ext == null) return null;

        byte[] bytes;
        try { bytes = Convert.FromBase64String(data[(marker + 7)..]); }
        catch (FormatException) { return null; }
        if (bytes.Length < 4 || bytes.Length > 3 * 1024 * 1024) return null;

        var isJpeg = bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        var isPng = bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
        if ((ext == "jpg" && !isJpeg) || (ext == "png" && !isPng)) return null;

        var dir = Path.Combine(_env.WebRootPath, "uploads", "visitors");
        Directory.CreateDirectory(dir);
        var name = $"{Guid.NewGuid():N}.{ext}";
        await System.IO.File.WriteAllBytesAsync(Path.Combine(dir, name), bytes);
        return $"/uploads/visitors/{name}";
    }
}
