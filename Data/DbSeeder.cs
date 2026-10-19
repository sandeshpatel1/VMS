using DwaarixVMS.Helpers;
using DwaarixVMS.Models;

namespace DwaarixVMS.Data;

/// <summary>Adds demo data on first run when the Users table is empty.</summary>
public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (db.Users.Any()) return;

        var residents = new[]
        {
            new Resident { FlatNo = "A-501", FullName = "Amit Desai",   Mobile = "9876543210" },
            new Resident { FlatNo = "A-502", FullName = "Priya Desai",  Mobile = "9876543211" },
            new Resident { FlatNo = "B-101", FullName = "Sanjay Patel", Mobile = "9876500001" },
            new Resident { FlatNo = "B-102", FullName = "Neha Patel",   Mobile = "9876500002" },
            new Resident { FlatNo = "B-204", FullName = "Kiran Shah",   Mobile = "9876500003" },
            new Resident { FlatNo = "B-302", FullName = "Rohit Mehta",  Mobile = "9876500004" },
            new Resident { FlatNo = "C-101", FullName = "Anita Joshi",  Mobile = "9876500005" }
        };
        if (!db.Residents.Any())
        {
            db.Residents.AddRange(residents);
            db.SaveChanges();
        }
        var amit = db.Residents.First(r => r.FlatNo == "A-501");

        var watchman = new AppUser { FullName = "Ramesh Gate", Username = "watchman", PasswordHash = Passwords.Hash("Watch@123"), Role = Roles.Watchman, Mobile = "9800000001" };
        db.Users.AddRange(
            new AppUser { FullName = "Society Admin", Username = "admin", PasswordHash = Passwords.Hash("Admin@123"), Role = Roles.Admin, Mobile = "9800000000" },
            watchman,
            new AppUser { FullName = "Amit Desai", Username = "amit", PasswordHash = Passwords.Hash("Resident@123"), Role = Roles.Resident, ResidentId = amit.Id, Mobile = "9876543210" });
        db.SaveChanges();

        var now = DateTime.Now;
        db.Visitors.AddRange(
            new Visitor { VisitorCode = "V" + now.ToString("yyMMdd") + "001", VisitorName = "Rajesh Kumar", Mobile = "9876543210", Purpose = "Service", ResidentId = amit.Id, ExpectedTime = now.AddHours(-2), Status = VisitStatus.CheckedIn, InTime = now.AddHours(-2), ValidTill = now.AddHours(6), CreatedById = watchman.Id, CreatedAt = now.AddHours(-2) },
            new Visitor { VisitorCode = "V" + now.ToString("yyMMdd") + "002", VisitorName = "Suresh Patel", Mobile = "9812345678", Purpose = "Guest", ResidentId = amit.Id, ExpectedTime = now.AddHours(-5), Status = VisitStatus.CheckedOut, InTime = now.AddHours(-5), OutTime = now.AddHours(-3), ValidTill = now.AddHours(3), CreatedById = watchman.Id, CreatedAt = now.AddHours(-5) });
        db.PreApprovals.Add(new PreApproval { ResidentId = amit.Id, VisitorName = "Ramesh Yadav", Mobile = "9898989898", Category = "Electrician", ValidFrom = now.Date, ValidTill = now.Date.AddDays(7), AccessCode = "482913", CreatedById = watchman.Id });
        db.Deliveries.Add(new Delivery { PersonName = "Vikram Singh", Mobile = "9765432100", Company = "Amazon", ResidentId = amit.Id, ArrivedAt = now.AddMinutes(-40), CreatedById = watchman.Id });
        db.SaveChanges();
    }
}
