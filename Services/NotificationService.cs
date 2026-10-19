using DwaarixVMS.Data;
using DwaarixVMS.Models;
using Microsoft.EntityFrameworkCore;

namespace DwaarixVMS.Services;

public class NotificationService
{
    private readonly AppDbContext _db;
    public NotificationService(AppDbContext db) => _db = db;

    public async Task ToResidentAsync(int residentId, string title, string message, string type)
    {
        var ids = await _db.Users.Where(u => u.ResidentId == residentId && u.IsActive).Select(u => u.Id).ToListAsync();
        await AddAsync(ids, title, message, type);
    }

    public async Task ToStaffAsync(string title, string message, string type)
    {
        var ids = await _db.Users.Where(u => u.IsActive && (u.Role == Roles.Watchman || u.Role == Roles.Admin))
            .Select(u => u.Id).ToListAsync();
        await AddAsync(ids, title, message, type);
    }

    public async Task ToEveryoneAsync(string title, string message, string type)
    {
        var ids = await _db.Users.Where(u => u.IsActive).Select(u => u.Id).ToListAsync();
        await AddAsync(ids, title, message, type);
    }

    private async Task AddAsync(IEnumerable<int> userIds, string title, string message, string type)
    {
        foreach (var id in userIds)
            _db.Notifications.Add(new Notification { UserId = id, Title = title, Message = message, Type = type });
        await _db.SaveChangesAsync();
    }
}
